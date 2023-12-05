﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Provides an <see cref="IKernelPlugin"/> implementation around a collection of functions.
/// </summary>
[DebuggerDisplay("Name = {Name}, Functions = {FunctionCount}")]
[DebuggerTypeProxy(typeof(KernelPlugin.TypeProxy))]
public sealed class KernelPlugin : IKernelPlugin
{
    /// <summary>The collection of functions associated with this plugin.</summary>
    private readonly Dictionary<string, KernelFunction> _functions;

    /// <summary>Initializes the new plugin from the provided name.</summary>
    /// <param name="name">The name for the plugin.</param>
    public KernelPlugin(string name) : this(name, description: null, functions: null)
    {
    }

    /// <summary>Initializes the new plugin from the provided name and function collection.</summary>
    /// <param name="name">The name for the plugin.</param>
    /// <param name="functions">The initial functions to be available as part of the plugin.</param>
    public KernelPlugin(string name, IEnumerable<KernelFunction>? functions) : this(name, description: null, functions)
    {
    }

    /// <summary>Initializes the new plugin from the provided name, description, and function collection.</summary>
    /// <param name="name">The name for the plugin.</param>
    /// <param name="description">A description of the plugin.</param>
    /// <param name="functions">The initial functions to be available as part of the plugin.</param>
    /// <exception cref="ArgumentNullException"><paramref name="functions"/> contains a null function.</exception>
    /// <exception cref="ArgumentException"><paramref name="functions"/> contains two functions with the same name.</exception>
    public KernelPlugin(string name, string? description, IEnumerable<KernelFunction>? functions = null)
    {
        Verify.ValidPluginName(name);

        this.Name = name;
        this.Description = !string.IsNullOrWhiteSpace(description) ? description! : "";

        this._functions = new Dictionary<string, KernelFunction>(StringComparer.OrdinalIgnoreCase);
        if (functions is not null)
        {
            foreach (KernelFunction f in functions)
            {
                Verify.NotNull(f, nameof(functions));
                this._functions.Add(f.Name, f);
            }
        }
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public string Description { get; }

    /// <summary>Gets the number of functions in this plugin.</summary>
    public int FunctionCount => this._functions.Count;

    /// <inheritdoc/>
    public KernelFunction this[string functionName] => this._functions[functionName];

    /// <inheritdoc/>
    public bool TryGetFunction(string name, [NotNullWhen(true)] out KernelFunction? function) =>
        this._functions.TryGetValue(name, out function);

    /// <summary>Adds a function to the plugin.</summary>
    /// <param name="function">The function to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="function"/> is null.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="function"/>'s <see cref="KernelFunction.Name"/> is null.</exception>
    /// <exception cref="ArgumentException">A function with the same <see cref="KernelFunction.Name"/> already exists in this plugin.</exception>
    public void AddFunction(KernelFunction function)
    {
        Verify.NotNull(function);
        this._functions.Add(function.Name, function);
    }

    /// <summary>Adds all of the functions in the specified <paramref name="functions"/> collection to this plugin.</summary>
    /// <param name="functions">The functions to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="functions"/> is null.</exception>
    /// <exception cref="ArgumentNullException">A function in <paramref name="functions"/>'s has a null <see cref="KernelFunction.Name"/>.</exception>
    /// <exception cref="ArgumentException">A function with the same <see cref="KernelFunction.Name"/> already exists in this plugin.</exception>
    public void AddFunctions(IEnumerable<KernelFunction> functions)
    {
        Verify.NotNull(functions);

        foreach (KernelFunction function in functions)
        {
            this.AddFunction(function);
        }
    }

    /// <inheritdoc/>
    public IEnumerator<KernelFunction> GetEnumerator() => this._functions.Values.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    /// <summary>Debugger type proxy for the plugin.</summary>
    private sealed class TypeProxy
    {
        private readonly KernelPlugin _plugin;

        public TypeProxy(KernelPlugin plugin) => this._plugin = plugin;

        public string Name => this._plugin.Name;

        public string Description => this._plugin.Description;

        public KernelFunction[] Functions => this._plugin._functions.Values.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
