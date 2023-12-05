﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.Connectors.AI.OpenAI.ChatCompletionWithData;

[Experimental("SKEXP0010")]
internal sealed class ChatWithDataRequest
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0;

    [JsonPropertyName("top_p")]
    public double TopP { get; set; } = 0;

    [JsonPropertyName("stream")]
    public bool IsStreamEnabled { get; set; }

    [JsonPropertyName("stop")]
    public IList<string>? StopSequences { get; set; } = Array.Empty<string>();

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("presence_penalty")]
    public double PresencePenalty { get; set; } = 0;

    [JsonPropertyName("frequency_penalty")]
    public double FrequencyPenalty { get; set; } = 0;

    [JsonPropertyName("logit_bias")]
    public IDictionary<int, int> TokenSelectionBiases { get; set; } = new Dictionary<int, int>();

    [JsonPropertyName("dataSources")]
    public IList<ChatWithDataSource> DataSources { get; set; } = Array.Empty<ChatWithDataSource>();

    [JsonPropertyName("messages")]
    public IList<ChatWithDataMessage> Messages { get; set; } = Array.Empty<ChatWithDataMessage>();
}

[Experimental("SKEXP0010")]
internal sealed class ChatWithDataSource
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = ChatWithDataSourceType.AzureCognitiveSearch.ToString();

    [JsonPropertyName("parameters")]
    public ChatWithDataSourceParameters Parameters { get; set; } = new ChatWithDataSourceParameters();
}

[Experimental("SKEXP0010")]
internal sealed class ChatWithDataSourceParameters
{
    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string ApiKey { get; set; } = string.Empty;

    [JsonPropertyName("indexName")]
    public string IndexName { get; set; } = string.Empty;
}

[Experimental("SKEXP0010")]
internal enum ChatWithDataSourceType
{
    AzureCognitiveSearch
}
