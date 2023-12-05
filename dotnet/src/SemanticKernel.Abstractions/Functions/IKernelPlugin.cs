﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

#pragma warning disable CA1716 // Identifiers should not match keywords

namespace Microsoft.SemanticKernel;

/// <summary>Represents a plugin that may be registered with a <see cref="Kernel"/>.</summary>
/// <remarks>
/// A plugin is named collection of functions. There is a many-to-many relationship between
/// plugins and functions: a plugin may contain any number of functions, and a function may
/// exist in any number of plugins.
/// </remarks>
public interface IKernelPlugin : IEnumerable<KernelFunction>
{
    /// <summary>Gets the name of the plugin.</summary>
    string Name { get; }

    /// <summary>Gets a description of the plugin.</summary>
    string Description { get; }

    /// <summary>Gets the function in the plugin with the specified name.</summary>
    /// <param name="functionName">The name of the function.</param>
    /// <returns>The function.</returns>
    /// <exception cref="KeyNotFoundException">The plugin does not contain a function with the specified name.</exception>
    KernelFunction this[string functionName] { get; }

    /// <summary>Finds a function in the plugin by name.</summary>
    /// <param name="name">The name of the function to find.</param>
    /// <param name="function">If the plugin contains the requested function, the found function instance; otherwise, null.</param>
    /// <returns>true if the function was found in the plugin; otherwise, false.</returns>
    bool TryGetFunction(string name, [NotNullWhen(true)] out KernelFunction? function);
}
