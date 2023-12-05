﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.AI.TextGeneration;

namespace Microsoft.SemanticKernel.AI;

/// <summary>
/// Execution settings for an AI request.
/// Implementors of <see cref="ITextGenerationService"/> or <see cref="IChatCompletionService"/> can extend this
/// if the service they are calling supports additional properties. For an example please reference
/// the Microsoft.SemanticKernel.Connectors.AI.OpenAI.OpenAIPromptExecutionSettings implementation.
/// </summary>
public class PromptExecutionSettings
{
    private Dictionary<string, object>? _extensionData;

    /// <summary>
    /// Service identifier.
    /// This identifies a service and is set when the AI service is registered.
    /// </summary>
    [JsonPropertyName("service_id")]
    public string? ServiceId { get; set; } = null;

    /// <summary>
    /// Model identifier.
    /// This identifies the AI model these settings are configured for e.g., gpt-4, gpt-3.5-turbo
    /// </summary>
    [JsonPropertyName("model_id")]
    public string? ModelId { get; set; } = null;

    /// <summary>
    /// Extra properties
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object> ExtensionData
    {
        get => this._extensionData ??= new();
        set => this._extensionData = value;
    }
}
