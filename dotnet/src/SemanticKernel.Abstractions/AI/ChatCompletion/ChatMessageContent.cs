﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.AI.ChatCompletion;

/// <summary>
/// Represents chat message content return from a <see cref="IChatCompletionService" /> service.
/// </summary>
public class ChatMessageContent : ContentBase
{
    /// <summary>
    /// Role of the author of the message
    /// </summary>
    public AuthorRole Role { get; set; }

    /// <summary>
    /// Content of the message
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// The encoding of the text content.
    /// </summary>
    [JsonIgnore]
    public Encoding Encoding { get; set; }

    /// <summary>
    /// Creates a new instance of the <see cref="ChatMessageContent"/> class
    /// </summary>
    /// <param name="role">Role of the author of the message</param>
    /// <param name="content">Content of the message</param>
    /// <param name="innerContent">Inner content object reference</param>
    /// <param name="encoding">Encoding of the text</param>
    /// <param name="metadata">Dictionary for any additional metadata</param>
    [JsonConstructor]
    public ChatMessageContent(AuthorRole role, string content, object? innerContent = null, Encoding? encoding = null, IDictionary<string, object?>? metadata = null) : base(innerContent, metadata)
    {
        this.Role = role;
        this.Content = content;
        this.Encoding = encoding ?? Encoding.UTF8;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return this.Content ?? string.Empty;
    }
}
