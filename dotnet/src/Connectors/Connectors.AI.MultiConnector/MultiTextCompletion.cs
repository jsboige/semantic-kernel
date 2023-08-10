﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.AI.TextCompletion;

namespace Microsoft.SemanticKernel.Connectors.AI.MultiConnector;

/// <summary>
/// Represents a text completion comprising several child completion connectors and capable of routing completion calls to specific connectors.
/// Offers analysis capabilities where a primary completion connector is tasked with vetting secondary connectors.
/// </summary>
public class MultiTextCompletion : ITextCompletion
{
    private readonly ILogger? _logger;
    private readonly IReadOnlyList<NamedTextCompletion> _textCompletions;
    private readonly MultiTextCompletionSettings _settings;
    private readonly Channel<ConnectorTest> _connectorTestChannel;

    /// <summary>
    /// Initializes a new instance of the MultiTextCompletion class.
    /// </summary>
    /// <param name="settings">An instance of the <see cref="MultiTextCompletionSettings"/> to configure the multi Text completion.</param>
    /// <param name="mainTextCompletion">The primary text completion to used by default for completion calls and vetting other completion providers.</param>
    /// <param name="analysisTaskCancellationToken">The cancellation token to use for the completion manager.</param>
    /// <param name="logger">An optional logger for instrumentation.</param>
    /// <param name="otherCompletions">The secondary text completions that need vetting to be used for completion calls.</param>
    public MultiTextCompletion(MultiTextCompletionSettings settings,
        NamedTextCompletion mainTextCompletion,
        CancellationToken? analysisTaskCancellationToken,
        ILogger? logger = null,
        params NamedTextCompletion[]? otherCompletions)
    {
        this._settings = settings;
        this._logger = logger;
        this._textCompletions = new[] { mainTextCompletion }.Concat(otherCompletions ?? Array.Empty<NamedTextCompletion>()).ToArray();
        this._connectorTestChannel = Channel.CreateUnbounded<ConnectorTest>();
        this.StartManagementTask(analysisTaskCancellationToken ?? CancellationToken.None);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ITextResult>> GetCompletionsAsync(string text, CompleteRequestSettings requestSettings, CancellationToken cancellationToken = default)
    {
        var session = this.GetPromptAndConnectorSettings(text, requestSettings);
        var completions = await session.NamedTextCompletion.TextCompletion.GetCompletionsAsync(text, requestSettings, cancellationToken).ConfigureAwait(false);

        var resultLazy = new AsyncLazy<string>(() =>
        {
            var toReturn = completions[0].GetCompletionAsync(cancellationToken);
            session.StopWatch.Stop();
            return toReturn;
        }, cancellationToken);

        session.ResultProducer = resultLazy;

        await this.ProcessTextCompletionResultsAsync(session, cancellationToken).ConfigureAwait(false);

        return completions;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ITextStreamingResult> GetStreamingCompletionsAsync(string text, CompleteRequestSettings requestSettings, CancellationToken cancellationToken = default)
    {
        var session = this.GetPromptAndConnectorSettings(text, requestSettings);

        var result = session.NamedTextCompletion.TextCompletion.GetStreamingCompletionsAsync(text, requestSettings, cancellationToken);

        var resultLazy = new AsyncLazy<string>(async () =>
        {
            var sb = new StringBuilder();
            await foreach (var completionResult in result.WithCancellation(cancellationToken))
            {
                await foreach (var word in completionResult.GetCompletionStreamingAsync(cancellationToken).ConfigureAwait(false))
                {
                    sb.Append(word);
                }

                break;
            }

            session.StopWatch.Stop();
            return sb.ToString();
        }, cancellationToken);

        session.ResultProducer = resultLazy;

        this.ProcessTextCompletionResultsAsync(session, cancellationToken).ConfigureAwait(false);

        return result;
    }

    /// <summary>
    /// This method is responsible for loading the appropriate settings in order to initiate the session state
    /// </summary>
    private MultiCompletionSession GetPromptAndConnectorSettings(string text, CompleteRequestSettings requestSettings)
    {
        var promptSettings = this._settings.GetPromptSettings(text, requestSettings, out var isNewPrompt);
        var textCompletionAndSettings = promptSettings.SelectAppropriateTextCompletion(this._textCompletions, this._settings.ConnectorComparer);
        var adjustedPrompt = textCompletionAndSettings.namedTextCompletion.AdjustPromptAndRequestSettings(text, requestSettings, promptSettings, this._settings, this._logger);
        var stopWatch = Stopwatch.StartNew();

        return new MultiCompletionSession
        {
            PromptSettings = promptSettings,
            InputText = text,
            InputRequestSettings = requestSettings,
            CallText = adjustedPrompt.text,
            CallsRequestSettings = adjustedPrompt.requestSettings,
            IsNewPrompt = isNewPrompt,
            NamedTextCompletion = textCompletionAndSettings.namedTextCompletion,
            PromptConnectorSettings = textCompletionAndSettings.promptConnectorSettings,
            StopWatch = stopWatch
        };
    }

    /// <summary>
    /// This method ends the multi-completion session and collects the results for analysis if needed
    /// </summary>
    private async Task ProcessTextCompletionResultsAsync(MultiCompletionSession session, CancellationToken cancellationToken)
    {
        var costDebited = await this.ApplyCreditorCostsAsync(session.CallText, session.ResultProducer, session.NamedTextCompletion).ConfigureAwait(false);

        if (session.NamedTextCompletion == this._textCompletions[0] && this._settings.AnalysisSettings.EnableAnalysis)
        {
            if (session.PromptSettings.IsTestingNeeded(session.InputText, this._textCompletions, session.IsNewPrompt))
            {
                session.PromptSettings.AddSessionPrompt(session.InputText);
                await this.CollectResultForTestAsync(session, costDebited, cancellationToken).ConfigureAwait(false);
            }
        }

        if (this._settings.LogResult)
        {
            this._logger?.LogInformation("MultiTextCompletion.GetCompletionsAsync: {0}: duration: \nPROMPT:\n{1}\nRESULT:\n{2}", session.NamedTextCompletion.Name, session.CallText, await session.ResultProducer.Value.ConfigureAwait(false));
        }
    }

    /// <summary>
    /// This method applies the cost of the text completion (input + result) to the creditor if one is configured
    /// </summary>
    private async Task<decimal> ApplyCreditorCostsAsync(string text, AsyncLazy<string> resultLazy, NamedTextCompletion textCompletion)
    {
        decimal cost = 0;
        if (this._settings.Creditor != null)
        {
            var result = await resultLazy.Value.ConfigureAwait(false);
            cost = textCompletion.GetCost(text, result);
            this._settings.Creditor.Credit(cost);
        }

        return cost;
    }

    /// <summary>
    /// Asynchronously collects results from a prompt call to evaluate connectors against the same prompt.
    /// </summary>
    private async Task CollectResultForTestAsync(MultiCompletionSession session, decimal textCompletionCost, CancellationToken cancellationToken)
    {
        var result = await session.ResultProducer.Value.ConfigureAwait(false);

        var duration = session.StopWatch.Elapsed;

        // For the management task
        ConnectorTest connectorTest = ConnectorTest.Create(session.InputText, session.InputRequestSettings, session.NamedTextCompletion, result, duration, textCompletionCost);
        this.AppendConnectorTest(connectorTest);
    }

    /// <summary>
    /// Starts a management task charged with collecting and analyzing prompt connector usage.
    /// </summary>
    private void StartManagementTask(CancellationToken cancellationToken)
    {
        Task.Factory.StartNew(
            async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await this.OptimizeCompletionsAsync(cancellationToken).ConfigureAwait(false);
                }
            },
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    /// <summary>
    /// Asynchronously receives new ConnectorTest from completion calls, evaluate available connectors against tests and perform analysis to vet connectors.
    /// </summary>
    private async Task OptimizeCompletionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await this._connectorTestChannel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var testSeries = new List<ConnectorTest>();

                while (this._connectorTestChannel.Reader.TryRead(out var connectorTest))
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        this._logger?.LogTrace(message: "OptimizeCompletionsAsync received a new ConnectorTest", connectorTest);
                        testSeries.Add(connectorTest);
                        await Task.Delay(this._settings.AnalysisSettings.AnalysisDelay, cancellationToken).ConfigureAwait(false);
                        this._logger?.LogTrace(message: "OptimizeCompletionsAsync waited analysis delay for new ConnectorTest", connectorTest);
                    }
                }

                this._logger?.LogTrace(message: "OptimizeCompletionsAsync collected a new ConnectorTest series to analyze", testSeries);
                // Evaluate the test

                var analysisResult = await this._settings.AnalysisSettings.EvaluatePromptConnectorsAsync(testSeries, this._textCompletions, this._settings, this._logger, cancellationToken).ConfigureAwait(false);

                // Raise the event after optimization is done
                this._settings.OnOptimizationCompleted(analysisResult, this._logger);
            }
        }
        catch (OperationCanceledException exception)
        {
            this._logger?.LogTrace("OptimizeCompletionsAsync Optimize task was cancelled with exception {0}", exception, exception.ToString());
        }
        catch (Exception exception)
        {
            this._logger?.LogError("OptimizeCompletionsAsync Optimize task failed with exception {0}", exception, exception.ToString());
            throw;
        }
    }

    // Define the event

    /// <summary>
    /// Appends a connector test to the test channel listened to in the Optimization long running task.
    /// </summary>
    private void AppendConnectorTest(ConnectorTest connectorTest)
    {
        this._logger?.LogTrace("Collecting new test with duration {0},\nORIGINAL_PROMPT:\n{1}\nORIGINAL_RESULT:\n{2}", connectorTest.Duration, connectorTest.Prompt, connectorTest.Result);
        this._connectorTestChannel.Writer.TryWrite(connectorTest);
    }
}