﻿// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.Plugins.OpenApi.Authentication;

/// <summary>
/// Represents the authentication section for an OpenAI plugin.
/// </summary>
public class OAuthTokenResponse
{
    /// <summary>
    /// The type of access token.
    /// </summary>
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "";

    /// <summary>
    /// The authorization scope.
    /// </summary>
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";
}
