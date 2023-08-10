﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.AI.TextCompletion;

namespace Microsoft.SemanticKernel.Connectors.AI.MultiConnector;

/// <summary>
/// Represents a text completion provider instance with the corresponding given name.
/// </summary>
[DebuggerDisplay("{Name}")]
public class NamedTextCompletion
{
    /// <summary>
    /// Gets or sets the name of the text completion provider.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// text completion provider instance, to be used for prompt answering and testing.
    /// </summary>
    public ITextCompletion TextCompletion { get; set; }

    /// <summary>
    /// The maximum number of tokens to generate in the completion.
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Optionally transform the input prompt specifically for the model
    /// </summary>
    public PromptTransform? PromptTransform { get; set; }

    /// <summary>
    /// The model might support a different range of temperature than SK (is 0 legal?) This optional function can help keep the temperature in the model's range.
    /// </summary>
    [JsonIgnore]
    public Func<double, double>? TemperatureTransform { get; set; }

    /// <summary>
    /// The model might support a different range of settings than SK. This optional function can help keep the settings in the model's range.
    /// </summary>
    [JsonIgnore]
    public Func<CompleteRequestSettings, CompleteRequestSettings>? RequestSettingsTransform { get; set; }

    /// <summary>
    /// The strategy to ensure request settings max token don't exceed the model's total max token.
    /// </summary>
    public MaxTokensAdjustment MaxTokensAdjustment { get; set; } = MaxTokensAdjustment.Percentage;

    /// <summary>
    /// When <see cref="MaxTokensAdjustment"/> is set to <see cref="MaxTokensAdjustment.Percentage"/>, this is the percentage of the model's max tokens available for completion settings.
    /// </summary>
    public int MaxTokensReservePercentage { get; set; } = 80;

    /// <summary>
    /// Cost per completion request.
    /// </summary>
    public decimal CostPerRequest { get; set; }

    /// <summary>
    /// Cost for 1000 completion token from request + result text.
    /// </summary>
    public decimal? CostPer1000Token { get; set; }

    [JsonIgnore]
    public Func<string, int>? TokenCountFunc { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedTextCompletion"/> class.
    /// </summary>
    /// <param name="name">The name of the text completion provider.</param>
    /// <param name="textCompletion">The text completion provider.</param>
    public NamedTextCompletion(string name, ITextCompletion textCompletion)
    {
        this.Name = name;
        this.TextCompletion = textCompletion;
    }

    public decimal GetCost(string text, string result)
    {
        return this.CostPerRequest + (this.CostPer1000Token ?? 0) * (this.TokenCountFunc ?? (s => 0))(text + result) / 1000;
    }

    /// <summary>
    /// Adjusts the request max tokens and temperature settings based on the completion max token supported.
    /// </summary>
    public (string text, CompleteRequestSettings requestSettings) AdjustPromptAndRequestSettings(string text,
        CompleteRequestSettings requestSettings,
        PromptMultiConnectorSettings promptMultiConnectorSettings,
        MultiTextCompletionSettings multiTextCompletionSettings,
        ILogger? logger)
    {
        // Adjusting settings

        var adjustedSettings = requestSettings;

        var adjustedSettingsModifier = new SettingsUpdater<CompleteRequestSettings>(adjustedSettings, MultiTextCompletionSettings.CloneRequestSettings);

        bool valueChanged = false;
        if (this.MaxTokens != null && requestSettings.MaxTokens != null)
        {
            int? ComputeMaxTokens(int? initialValue)
            {
                var newMaxTokens = initialValue;
                if (newMaxTokens != null)
                {
                    switch (this.MaxTokensAdjustment)
                    {
                        case MaxTokensAdjustment.Percentage:
                            newMaxTokens = Math.Min(newMaxTokens.Value, this.MaxTokens.Value * this.MaxTokensReservePercentage / 100);
                            break;
                        case MaxTokensAdjustment.CountInputTokens:
                            if (this.TokenCountFunc != null)
                            {
                                newMaxTokens = Math.Min(newMaxTokens.Value, this.MaxTokens.Value - this.TokenCountFunc(text));
                            }
                            else
                            {
                                logger?.LogWarning("Inconsistency found with named Completion {0}: Max Token adjustment is configured to account for input token number but no Token count function was defined. MaxToken settings will be left untouched", this.Name);
                            }

                            break;
                    }
                }

                return newMaxTokens;
            }

            adjustedSettings = adjustedSettingsModifier.ModifyIfChanged(r => r.MaxTokens, ComputeMaxTokens, (setting, value) => setting.MaxTokens = value, out valueChanged);

            if (valueChanged)
            {
                logger?.LogDebug("Changed request max token from {0} to {1}", requestSettings.MaxTokens.Value, adjustedSettings.MaxTokens);
            }
        }

        if (this.TemperatureTransform != null)
        {
            adjustedSettings = adjustedSettingsModifier.ModifyIfChanged(r => r.Temperature, this.TemperatureTransform, (setting, value) => setting.Temperature = value, out valueChanged);

            if (valueChanged)
            {
                logger?.LogDebug("Changed temperature from {0} to {1}", requestSettings.Temperature, adjustedSettings.Temperature);
            }
        }

        if (this.RequestSettingsTransform != null)
        {
            adjustedSettings = this.RequestSettingsTransform(requestSettings);
            logger?.LogTrace("Applied request settings transform");
        }

        //Adjusting prompt

        var adjustedPrompt = text;

        if (multiTextCompletionSettings.GlobalPromptTransform != null)
        {
            adjustedPrompt = multiTextCompletionSettings.GlobalPromptTransform.Transform(adjustedPrompt);
            logger?.LogTrace("Applied global settings prompt transform");
        }

        if (promptMultiConnectorSettings.PromptTypeTransform != null)
        {
            adjustedPrompt = promptMultiConnectorSettings.PromptTypeTransform.Transform(adjustedPrompt);
            logger?.LogTrace("Applied prompt type settings prompt transform");
        }

        if (promptMultiConnectorSettings.ApplyModelTransform && this.PromptTransform != null)
        {
            adjustedPrompt = this.PromptTransform.Transform(adjustedPrompt);
            logger?.LogTrace("Applied named connector settings transform");
        }

        return (adjustedPrompt, adjustedSettings);
    }
}