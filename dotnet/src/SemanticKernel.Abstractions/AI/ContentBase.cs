﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.AI;

/// <summary>
/// Base class for all AI non-streaming results
/// </summary>
public abstract class ContentBase
{
    /// <summary>
    /// The inner content representation. Use this to bypass the current abstraction.
    /// </summary>
    /// <remarks>
    /// The usage of this property is considered "unsafe". Use it only if strictly necessary.
    /// </remarks>
    [JsonIgnore]
    public object? InnerContent { get; }

    /// <summary>
    /// The metadata associated with the content.
    /// </summary>
    public IDictionary<string, object?>? Metadata { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentBase"/> class.
    /// </summary>
    /// <param name="innerContent">Raw content object reference</param>
    /// <param name="metadata">Metadata associated with the content</param>
    protected ContentBase(object? innerContent, IDictionary<string, object?>? metadata = null)
    {
        this.InnerContent = innerContent;
        if (metadata is not null)
        {
            this.Metadata = new Dictionary<string, object?>(metadata);
        }
    }

    /// <summary>
    /// Implicit conversion to string
    /// </summary>
    /// <param name="modelContent">model Content</param>
    public static implicit operator string(ContentBase modelContent)
    {
        return modelContent.ToString();
    }
}
