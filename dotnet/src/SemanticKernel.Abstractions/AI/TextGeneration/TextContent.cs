﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.AI.TextGeneration;

/// <summary>
/// Represents text content return from a <see cref="ITextGenerationService" /> service.
/// </summary>
public sealed class TextContent : ContentBase
{
    /// <summary>
    /// The text content.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// The encoding of the text content.
    /// </summary>
    [JsonIgnore]
    public Encoding Encoding { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TextContent"/> class.
    /// </summary>
    /// <param name="text">Text content</param>
    /// <param name="innerContent">Inner content</param>
    /// <param name="encoding">Encoding of the text</param>
    /// <param name="metadata">Additional metadata</param>
    public TextContent(string? text, object? innerContent = null, Encoding? encoding = null, IDictionary<string, object?>? metadata = null) : base(innerContent, metadata)
    {
        this.Text = text;
        this.Encoding = encoding ?? Encoding.UTF8;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return this.Text ?? string.Empty;
    }
}
