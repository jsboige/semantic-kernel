﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.SemanticKernel.AI.TextCompletion;

namespace Microsoft.SemanticKernel.Connectors.AI.MultiConnector;

/// <summary>
/// Represents a job to be executed by the MultiConnector's completion
/// </summary>
[DebuggerDisplay("{DebuggerDisplay}")]
public readonly struct CompletionJob : System.IEquatable<CompletionJob>
{
    public CompletionJob(string prompt, CompleteRequestSettings settings)
    {
        this.Prompt = prompt;
        this.RequestSettings = settings;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => $"{this.Prompt.Substring(0, Math.Min(this.Prompt.Length, 10))}(...)";

    public string Prompt { get; }

    public CompleteRequestSettings RequestSettings { get; }

    public override bool Equals(object obj)
    {
        return obj is CompletionJob job && this.Equals(job);
    }

    public override int GetHashCode()
    {
        return System.HashCode.Combine(this.Prompt, this.RequestSettings);
    }

    public static bool operator ==(CompletionJob left, CompletionJob right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(CompletionJob left, CompletionJob right)
    {
        return !(left == right);
    }

    public bool Equals(CompletionJob other)
    {
        return this.Prompt == other.Prompt &&
               EqualityComparer<CompleteRequestSettings>.Default.Equals(this.RequestSettings, other.RequestSettings);
    }
}