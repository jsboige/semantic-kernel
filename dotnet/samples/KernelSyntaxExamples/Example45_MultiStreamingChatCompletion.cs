﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.ChatCompletion;

/**
 * The following example shows how to use Semantic Kernel with Multiple Results Text Completion as streaming
 */
// ReSharper disable once InconsistentNaming
public static class Example45_MultiStreamingChatCompletion
{
    public static async Task RunAsync()
    {
        await AzureOpenAIMultiStreamingChatCompletionAsync();
        await OpenAIMultiStreamingChatCompletionAsync();
    }

    private static async Task AzureOpenAIMultiStreamingChatCompletionAsync()
    {
        Console.WriteLine("======== Azure OpenAI - Multiple Chat Completion - Raw Streaming ========");

        AzureOpenAIChatCompletionService chatCompletionService = new(
            TestConfiguration.AzureOpenAI.ChatDeploymentName,
            TestConfiguration.AzureOpenAI.ChatModelId,
            TestConfiguration.AzureOpenAI.Endpoint,
            TestConfiguration.AzureOpenAI.ApiKey);

        await StreamingChatCompletionAsync(chatCompletionService);
    }

    private static async Task OpenAIMultiStreamingChatCompletionAsync()
    {
        Console.WriteLine("======== Open AI - Multiple Text Completion - Raw Streaming ========");

        OpenAIChatCompletionService chatCompletionService = new(
            modelId: TestConfiguration.OpenAI.ChatModelId,
            apiKey: TestConfiguration.OpenAI.ApiKey);

        await StreamingChatCompletionAsync(chatCompletionService);
    }

    private static async Task StreamingChatCompletionAsync(IChatCompletionService chatCompletionService)
    {
        var executionSettings = new OpenAIPromptExecutionSettings()
        {
            MaxTokens = 200,
            FrequencyPenalty = 0,
            PresencePenalty = 0,
            Temperature = 1,
            TopP = 0.5,
            ResultsPerPrompt = 3
        };

        var consoleLinesPerResult = 10;

        PrepareDisplay();
        var prompt = "Hi, I'm looking for 5 random title names for sci-fi books";
        await ProcessStreamAsyncEnumerableAsync(chatCompletionService, prompt, executionSettings, consoleLinesPerResult);

        Console.WriteLine();

        Console.SetCursorPosition(0, executionSettings.ResultsPerPrompt * consoleLinesPerResult);
        Console.WriteLine();
    }

    private static async Task ProcessStreamAsyncEnumerableAsync(IChatCompletionService chatCompletionService, string prompt, OpenAIPromptExecutionSettings executionSettings, int consoleLinesPerResult)
    {
        var roleDisplayed = new List<int>();
        var messagePerChoice = new Dictionary<int, string>();
        var chatHistory = new ChatHistory(prompt);
        await foreach (var chatUpdate in chatCompletionService.GetStreamingChatMessageContentsAsync(chatHistory, executionSettings))
        {
            string newContent = string.Empty;
            Console.SetCursorPosition(0, chatUpdate.ChoiceIndex * consoleLinesPerResult);
            if (!roleDisplayed.Contains(chatUpdate.ChoiceIndex) && chatUpdate.Role.HasValue)
            {
                newContent = $"Role: {chatUpdate.Role.Value}\n";
                roleDisplayed.Add(chatUpdate.ChoiceIndex);
            }

            if (chatUpdate.Content is { Length: > 0 })
            {
                newContent += chatUpdate.Content;
            }

            if (!messagePerChoice.ContainsKey(chatUpdate.ChoiceIndex))
            {
                messagePerChoice.Add(chatUpdate.ChoiceIndex, string.Empty);
            }
            messagePerChoice[chatUpdate.ChoiceIndex] += newContent;

            Console.Write(messagePerChoice[chatUpdate.ChoiceIndex]);
        }
    }

    /// <summary>
    /// Break enough lines as the current console window size to display the results
    /// </summary>
    private static void PrepareDisplay()
    {
        for (int i = 0; i < Console.WindowHeight - 2; i++)
        {
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Outputs the last message of the chat history
    /// </summary>
    private static Task MessageOutputAsync(ChatHistory chatHistory)
    {
        var message = chatHistory.Last();

        Console.WriteLine($"{message.Role}: {message.Content}");
        Console.WriteLine("------------------------");

        return Task.CompletedTask;
    }
}
