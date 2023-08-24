﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel.AI.TextCompletion;

namespace Microsoft.SemanticKernel.Connectors.AI.Oobabooga.Completion;

/// <summary>
/// HTTP schema to perform oobabooga completion request. Contains many parameters, some of which are specific to certain kinds of models.
/// See <see href="https://github.com/oobabooga/text-generation-webui/blob/main/docs/Generation-parameters.md"/> and subsequent links for additional information.
/// </summary>
[Serializable]
public class CompletionRequest : CompletionOobaboogaSettings
{
    /// <summary>
    /// The prompt text to complete.
    /// </summary>
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Creates a new CompletionRequest with the given prompt, oobabooga settings and semantic-kernel settings.
    /// </summary>
    public static CompletionRequest Create(string prompt, CompletionOobaboogaSettings settings, CompleteRequestSettings requestSettings)
    {
        var toReturn = new CompletionRequest()
        {
            Prompt = prompt
        };
        toReturn.Apply(settings);
        if (!settings.OverrideSKSettings)
        {
            toReturn.MaxNewTokens = requestSettings.MaxTokens;
            toReturn.Temperature = requestSettings.Temperature;
            toReturn.TopP = requestSettings.TopP;
            toReturn.RepetitionPenalty = GetRepetitionPenalty(requestSettings.PresencePenalty);
            toReturn.StoppingStrings = requestSettings.StopSequences.ToList();
        }

        return toReturn;
    }
}