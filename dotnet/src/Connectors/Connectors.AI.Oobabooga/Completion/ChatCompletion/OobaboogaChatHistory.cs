﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.Connectors.AI.Oobabooga.Completion.ChatCompletion;

/// <summary>
/// Represents the chat history.
/// </summary>
public class OobaboogaChatHistory
{
    /// <summary>
    /// The internal chat history.
    /// </summary>
    [JsonPropertyName("internal")]
    public List<List<string>> Internal { get; set; } = new List<List<string>>();

    /// <summary>
    /// The visible chat history.
    /// </summary>
    [JsonPropertyName("visible")]
    public List<List<string>> Visible { get; set; } = new List<List<string>>();
}