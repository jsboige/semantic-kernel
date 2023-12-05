﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.AI;

namespace Microsoft.SemanticKernel;

/// <summary>Provides extension methods for working with <see cref="KernelPlugin"/>s.</summary>
public static class KernelPluginExtensions
{
    #region AddFunctionFromMethod
    /// <summary>
    /// Creates a <see cref="KernelFunction"/> instance for a method, specified via a delegate, and adds it to the <see cref="KernelPlugin"/>.
    /// </summary>
    /// <param name="plugin">The plugin to which the function should be added.</param>
    /// <param name="method">The method to be represented via the created <see cref="KernelFunction"/>.</param>
    /// <param name="functionName">Optional function name. If null, it will default to one derived from the method represented by <paramref name="method"/>.</param>
    /// <param name="description">Optional description of the method. If null, it will default to one derived from the method represented by <paramref name="method"/>, if possible (e.g. via a <see cref="DescriptionAttribute"/> on the method).</param>
    /// <param name="parameters">Optional parameter descriptions. If null, it will default to one derived from the method represented by <paramref name="method"/>.</param>
    /// <param name="returnParameter">Optional return parameter description. If null, it will default to one derived from the method represented by <paramref name="method"/>.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    /// <returns>The created <see cref="KernelFunction"/> wrapper for <paramref name="method"/>.</returns>
    public static KernelFunction AddFunctionFromMethod(
        this KernelPlugin plugin,
        Delegate method,
        string? functionName = null,
        string? description = null,
        IEnumerable<KernelParameterMetadata>? parameters = null,
        KernelReturnParameterMetadata? returnParameter = null,
        ILoggerFactory? loggerFactory = null)
    {
        Verify.NotNull(plugin);

        KernelFunction function = KernelFunctionFactory.CreateFromMethod(method.Method, method.Target, functionName, description, parameters, returnParameter, loggerFactory);
        plugin.AddFunction(function);
        return function;
    }

    /// <summary>
    /// Creates a <see cref="KernelFunction"/> instance for a method, specified via an <see cref="MethodInfo"/> instance
    /// and an optional target object if the method is an instance method, and adds it to the <see cref="KernelPlugin"/>.
    /// </summary>
    /// <param name="plugin">The plugin to which the function should be added.</param>
    /// <param name="method">The method to be represented via the created <see cref="KernelFunction"/>.</param>
    /// <param name="target">The target object for the <paramref name="method"/> if it represents an instance method. This should be null if and only if <paramref name="method"/> is a static method.</param>
    /// <param name="functionName">Optional function name. If null, it will default to one derived from the method represented by <paramref name="method"/>.</param>
    /// <param name="description">Optional description of the method. If null, it will default to one derived from the method represented by <paramref name="method"/>, if possible (e.g. via a <see cref="DescriptionAttribute"/> on the method).</param>
    /// <param name="parameters">Optional parameter descriptions. If null, it will default to one derived from the method represented by <paramref name="method"/>.</param>
    /// <param name="returnParameter">Optional return parameter description. If null, it will default to one derived from the method represented by <paramref name="method"/>.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    /// <returns>The created <see cref="KernelFunction"/> wrapper for <paramref name="method"/>.</returns>
    public static KernelFunction AddFunctionFromMethod(
        this KernelPlugin plugin,
        MethodInfo method,
        object? target = null,
        string? functionName = null,
        string? description = null,
        IEnumerable<KernelParameterMetadata>? parameters = null,
        KernelReturnParameterMetadata? returnParameter = null,
        ILoggerFactory? loggerFactory = null)
    {
        Verify.NotNull(plugin);

        KernelFunction function = KernelFunctionFactory.CreateFromMethod(method, target, functionName, description, parameters, returnParameter, loggerFactory);
        plugin.AddFunction(function);
        return function;
    }
    #endregion

    #region AddFunctionFromPrompt
    // TODO: Revise these CreateFunctionFromPrompt method XML comments

    /// <summary>
    /// Creates a string-to-string prompt function, with no direct support for input context, and adds it to the <see cref="KernelPlugin"/>.
    /// The function can be referenced in templates and will receive the context, but when invoked programmatically you
    /// can only pass in a string in input and receive a string in output.
    /// </summary>
    /// <param name="plugin">The plugin to which the function should be added.</param>
    /// <param name="promptTemplate">Plain language definition of the prompt function, using SK template language</param>
    /// <param name="executionSettings">Optional LLM execution settings</param>
    /// <param name="functionName">A name for the given function. The name can be referenced in templates and used by the pipeline planner.</param>
    /// <param name="description">Optional description, useful for the planner</param>
    /// <param name="promptTemplateFactory">Optional: Prompt template factory</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    /// <returns>A function ready to use</returns>
    public static KernelFunction AddFunctionFromPrompt(
        this KernelPlugin plugin,
        string promptTemplate,
        PromptExecutionSettings? executionSettings = null,
        string? functionName = null,
        string? description = null,
        IPromptTemplateFactory? promptTemplateFactory = null,
        ILoggerFactory? loggerFactory = null)
    {
        Verify.NotNull(plugin);

        KernelFunction function = KernelFunctionFactory.CreateFromPrompt(promptTemplate, executionSettings, functionName, description, promptTemplateFactory, loggerFactory);
        plugin.AddFunction(function);
        return function;
    }

    /// <summary>
    /// Creates a prompt function passing in the definition in natural language, i.e. the prompt template, and adds it to the <see cref="KernelPlugin"/>.
    /// </summary>
    /// <param name="plugin">The plugin to which the function should be added.</param>
    /// <param name="promptConfig">Prompt template configuration.</param>
    /// <param name="promptTemplateFactory">Prompt template factory</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    public static KernelFunction AddFunctionFromPrompt(
        this KernelPlugin plugin,
        PromptTemplateConfig promptConfig,
        IPromptTemplateFactory? promptTemplateFactory = null,
        ILoggerFactory? loggerFactory = null)
    {
        Verify.NotNull(plugin);

        KernelFunction function = KernelFunctionFactory.CreateFromPrompt(promptConfig, promptTemplateFactory, loggerFactory);
        plugin.AddFunction(function);
        return function;
    }

    /// <summary>
    /// Allow to define a prompt function passing in the definition in natural language, i.e. the prompt template.
    /// </summary>
    /// <param name="plugin">The plugin to which the function should be added.</param>
    /// <param name="promptTemplate">Prompt template</param>
    /// <param name="promptConfig">Prompt template configuration.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    /// <returns>A function ready to use</returns>
    public static KernelFunction AddFunctionFromPrompt(
        this KernelPlugin plugin,
        IPromptTemplate promptTemplate,
        PromptTemplateConfig promptConfig,
        ILoggerFactory? loggerFactory = null)
    {
        Verify.NotNull(plugin);

        KernelFunction function = KernelFunctionFactory.CreateFromPrompt(promptTemplate, promptConfig, loggerFactory);
        plugin.AddFunction(function);
        return function;
    }
    #endregion
}
