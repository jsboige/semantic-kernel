﻿// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.Connectors.AI.HuggingFace.TextGeneration;

/// <summary>
/// HTTP schema to perform completion request.
/// </summary>
public sealed class TextGenerationRequest
{
    /// <summary>
    /// Prompt to complete.
    /// </summary>
    [JsonPropertyName("inputs")]
    public string Input { get; set; } = string.Empty;

    /// <summary>
    /// Enable streaming
    /// </summary>
    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;
}
