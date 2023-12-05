﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.AI.TextGeneration;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.TextGeneration;

/**
 * The following example shows how to use Semantic Kernel with streaming Text Completion.
 *
 * Note that all text generation models are deprecated by OpenAI and will be removed in a future release.
 *
 * Refer to example 33 for streaming chat completion.
 */
// ReSharper disable once InconsistentNaming
public static class Example32_StreamingCompletion
{
    public static async Task RunAsync()
    {
        await AzureOpenAITextGenerationStreamAsync();
        await OpenAITextGenerationStreamAsync();
    }

    private static async Task AzureOpenAITextGenerationStreamAsync()
    {
        Console.WriteLine("======== Azure OpenAI - Text Completion - Raw Streaming ========");

        var textGeneration = new AzureOpenAITextGenerationService(
            TestConfiguration.AzureOpenAI.DeploymentName,
            TestConfiguration.AzureOpenAI.ModelId,
            TestConfiguration.AzureOpenAI.Endpoint,
            TestConfiguration.AzureOpenAI.ApiKey);

        await TextGenerationStreamAsync(textGeneration);
    }

    private static async Task OpenAITextGenerationStreamAsync()
    {
        Console.WriteLine("======== Open AI - Text Completion - Raw Streaming ========");

        var textGeneration = new OpenAITextGenerationService("text-davinci-003", TestConfiguration.OpenAI.ApiKey);

        await TextGenerationStreamAsync(textGeneration);
    }

    private static async Task TextGenerationStreamAsync(ITextGenerationService textGeneration)
    {
        var executionSettings = new OpenAIPromptExecutionSettings()
        {
            MaxTokens = 100,
            FrequencyPenalty = 0,
            PresencePenalty = 0,
            Temperature = 1,
            TopP = 0.5
        };

        var prompt = "Write one paragraph why AI is awesome";

        Console.WriteLine("Prompt: " + prompt);
        await foreach (var content in textGeneration.GetStreamingTextContentsAsync(prompt, executionSettings))
        {
            Console.Write(content);
        }

        Console.WriteLine();
    }
}
