﻿// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Events;

/// <summary>
/// KernelEventArgs extensions
/// </summary>
public static class KernelEventArgsExtensions
{
    /// <summary>
    /// Metadata key for storing the rendered prompt when available.
    /// </summary>
    public const string RenderedPromptMetadataKey = "RenderedPrompt";

    /// <summary>
    /// Only tries to get the rendered prompt from the event args metadata if it exists.
    /// </summary>
    /// <param name="eventArgs">Target event args to extend</param>
    /// <param name="renderedPrompt">Outputs the renderedPrompt</param>
    /// <returns>True if the prompt was present</returns>
    public static bool TryGetRenderedPrompt(this KernelEventArgs eventArgs, out string? renderedPrompt)
    {
        object? renderedPromptObject = null;
        var found = eventArgs.Metadata?.TryGetValue(KernelEventArgsExtensions.RenderedPromptMetadataKey, out renderedPromptObject) is true;
        renderedPrompt = renderedPromptObject?.ToString();

        return found;
    }

    /// <summary>
    /// Only tries to update the prompt in the event args metadata if it exists.
    /// </summary>
    /// <param name="eventArgs">Target event args to extend</param>
    /// <param name="newPrompt">Prompt to override</param>
    /// <returns>True if the prompt exist and was updated</returns>
    public static bool TryUpdateRenderedPrompt(this KernelEventArgs eventArgs, string newPrompt)
    {
        if (eventArgs.Metadata?.ContainsKey(KernelEventArgsExtensions.RenderedPromptMetadataKey) is true)
        {
            eventArgs.Metadata[KernelEventArgsExtensions.RenderedPromptMetadataKey] = newPrompt;
            return true;
        }

        return false;
    }
}
