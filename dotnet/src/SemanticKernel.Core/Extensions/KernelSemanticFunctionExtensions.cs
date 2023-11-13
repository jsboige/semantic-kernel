﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.TemplateEngine;
using Microsoft.SemanticKernel.Text;

#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace - Using the namespace of IKernel
namespace Microsoft.SemanticKernel;
#pragma warning restore IDE0130

/// <summary>
/// Class for extensions methods to define semantic functions.
/// </summary>
public static class KernelSemanticFunctionExtensions
{
    /// <summary>
    /// Build and register a function in the internal function collection, in a global generic plugin.
    /// </summary>
    /// <param name="kernel">Semantic Kernel instance</param>
    /// <param name="functionName">Name of the semantic function. The name can contain only alphanumeric chars + underscore.</param>
    /// <param name="promptTemplateConfig">Prompt template configuration.</param>
    /// <param name="promptTemplate">Prompt template.</param>
    /// <returns>A C# function wrapping AI logic, usually defined with natural language</returns>
    public static ISKFunction RegisterSemanticFunction(
        this IKernel kernel,
        string functionName,
        PromptTemplateConfig promptTemplateConfig,
        IPromptTemplate promptTemplate)
    {
        return kernel.RegisterSemanticFunction(FunctionCollection.GlobalFunctionsPluginName, functionName, promptTemplateConfig, promptTemplate);
    }

    /// <summary>
    /// Build and register a function in the internal function collection.
    /// </summary>
    /// <param name="kernel">Semantic Kernel instance</param>
    /// <param name="pluginName">Name of the plugin containing the function. The name can contain only alphanumeric chars + underscore.</param>
    /// <param name="functionName">Name of the semantic function. The name can contain only alphanumeric chars + underscore.</param>
    /// <param name="promptTemplateConfig">Prompt template configuration.</param>
    /// <param name="promptTemplate">Prompt template.</param>
    /// <returns>A C# function wrapping AI logic, usually defined with natural language</returns>
    public static ISKFunction RegisterSemanticFunction(
        this IKernel kernel,
        string pluginName,
        string functionName,
        PromptTemplateConfig promptTemplateConfig,
        IPromptTemplate promptTemplate)
    {
        // Future-proofing the name not to contain special chars
        Verify.ValidFunctionName(functionName);

        ISKFunction function = kernel.CreateSemanticFunction(pluginName, functionName, promptTemplateConfig, promptTemplate);
        return kernel.RegisterCustomFunction(function);
    }

    /// <summary>
    /// Define a string-to-string semantic function, with no direct support for input context.
    /// The function can be referenced in templates and will receive the context, but when invoked programmatically you
    /// can only pass in a string in input and receive a string in output.
    /// </summary>
    /// <param name="kernel">Semantic Kernel instance</param>
    /// <param name="promptTemplate">Plain language definition of the semantic function, using SK template language</param>
    /// <param name="functionName">A name for the given function. The name can be referenced in templates and used by the pipeline planner.</param>
    /// <param name="pluginName">Optional plugin name, for namespacing and avoid collisions</param>
    /// <param name="description">Optional description, useful for the planner</param>
    /// <param name="requestSettings">Optional LLM request settings</param>
    /// <returns>A function ready to use</returns>
    public static ISKFunction CreateSemanticFunction(
        this IKernel kernel,
        string promptTemplate,
        string? functionName = null,
        string? pluginName = null,
        string? description = null,
        AIRequestSettings? requestSettings = null)
    {
        functionName ??= RandomFunctionName();

        var promptTemplateConfig = new PromptTemplateConfig
        {
            Description = description ?? "Generic function, unknown purpose",
        };

        if (requestSettings is not null)
        {
            promptTemplateConfig.ModelSettings.Add(requestSettings);
        }

        return kernel.CreateSemanticFunction(
            promptTemplate: promptTemplate,
            promptTemplateConfig: promptTemplateConfig,
            functionName: functionName,
            pluginName: pluginName);
    }

    /// <summary>
    /// Allow to define a semantic function passing in the definition in natural language, i.e. the prompt template.
    /// </summary>
    /// <param name="kernel">Semantic Kernel instance</param>
    /// <param name="promptTemplate">Plain language definition of the semantic function, using SK template language</param>
    /// <param name="promptTemplateConfig">Prompt template configuration.</param>
    /// <param name="functionName">A name for the given function. The name can be referenced in templates and used by the pipeline planner.</param>
    /// <param name="pluginName">An optional plugin name, e.g. to namespace functions with the same name. When empty,
    /// the function is added to the global namespace, overwriting functions with the same name</param>
    /// <param name="promptTemplateFactory">Prompt template factory</param>
    /// <returns>A function ready to use</returns>
    public static ISKFunction CreateSemanticFunction(
        this IKernel kernel,
        string promptTemplate,
        PromptTemplateConfig promptTemplateConfig,
        string? functionName = null,
        string? pluginName = null,
        IPromptTemplateFactory? promptTemplateFactory = null)
    {
        functionName ??= RandomFunctionName();
        Verify.ValidFunctionName(functionName);
        if (!string.IsNullOrEmpty(pluginName)) { Verify.ValidPluginName(pluginName); }

        var factory = promptTemplateFactory ?? CreateDefaultPromptTemplateFactory(kernel);
        IPromptTemplate promptTemplateInstance = factory.Create(promptTemplate, promptTemplateConfig);

        // TODO: manage overwrites, potentially error out
        return string.IsNullOrEmpty(pluginName)
            ? kernel.RegisterSemanticFunction(functionName, promptTemplateConfig, promptTemplateInstance)
            : kernel.RegisterSemanticFunction(pluginName!, functionName, promptTemplateConfig, promptTemplateInstance);
    }

    /// <summary>
    /// Invoke a semantic function using the provided prompt template.
    /// </summary>
    /// <param name="kernel">Semantic Kernel instance</param>
    /// <param name="template">Plain language definition of the semantic function, using SK template language</param>
    /// <param name="functionName">A name for the given function. The name can be referenced in templates and used by the pipeline planner.</param>
    /// <param name="pluginName">Optional plugin name, for namespacing and avoid collisions</param>
    /// <param name="description">Optional description, useful for the planner</param>
    /// <param name="requestSettings">Optional LLM request settings</param>
    /// <returns>Kernel execution result</returns>
    public static Task<KernelResult> InvokeSemanticFunctionAsync(
        this IKernel kernel,
        string template,
        string? functionName = null,
        string? pluginName = null,
        string? description = null,
        AIRequestSettings? requestSettings = null)
    {
        var skFunction = kernel.CreateSemanticFunction(
            template,
            functionName,
            pluginName,
            description,
            requestSettings);

        return kernel.RunAsync(skFunction);
    }

    [Obsolete("Methods and classes which includes Skill in the name have been renamed to use Plugin. Use Kernel.ImportSemanticFunctionsFromDirectory instead. This will be removed in a future release.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
#pragma warning disable CS1591
    public static IDictionary<string, ISKFunction> ImportSemanticSkillFromDirectory(
        this IKernel kernel, string parentDirectory, params string[] pluginDirectoryNames)
    {
        return kernel.ImportSemanticFunctionsFromDirectory(parentDirectory, pluginDirectoryNames);
    }
#pragma warning restore CS1591

    /// <summary>
    /// Imports semantic functions, defined by prompt templates stored in the filesystem.
    /// </summary>
    /// <param name="kernel"></param>
    /// <param name="parentDirectory"></param>
    /// <param name="pluginDirectoryNames"></param>
    /// <returns></returns>
    public static IDictionary<string, ISKFunction> ImportSemanticFunctionsFromDirectory(
        this IKernel kernel,
        string parentDirectory,
        params string[] pluginDirectoryNames)
    {
        return kernel.ImportSemanticFunctionsFromDirectory(parentDirectory, null, pluginDirectoryNames);
    }

    /// <summary>
    /// Imports semantic functions, defined by prompt templates stored in the filesystem.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A plugin directory contains a set of subdirectories, one for each semantic function.
    /// </para>
    /// <para>
    /// This method accepts the path of the parent directory (e.g. "d:\plugins") and the name of the plugin directory
    /// (e.g. "OfficePlugin"), which is used also as the "plugin name" in the internal function collection (note that
    /// plugin and function names can contain only alphanumeric chars and underscore).
    /// </para>
    /// <code>
    /// Example:
    /// D:\plugins\                            # parentDirectory = "D:\plugins"
    ///
    ///     |__ OfficePlugin\                  # pluginDirectoryName = "SummarizeEmailThread"
    ///
    ///         |__ ScheduleMeeting           # semantic function
    ///             |__ skprompt.txt          # prompt template
    ///             |__ config.json           # settings (optional file)
    ///
    ///         |__ SummarizeEmailThread      # semantic function
    ///             |__ skprompt.txt          # prompt template
    ///             |__ config.json           # settings (optional file)
    ///
    ///         |__ MergeWordAndExcelDocs     # semantic function
    ///             |__ skprompt.txt          # prompt template
    ///             |__ config.json           # settings (optional file)
    ///
    ///     |__ XboxPlugin\                    # another plugin, etc.
    ///
    ///         |__ MessageFriend
    ///             |__ skprompt.txt
    ///             |__ config.json
    ///         |__ LaunchGame
    ///             |__ skprompt.txt
    ///             |__ config.json
    /// </code>
    /// <para>
    /// See https://github.com/microsoft/semantic-kernel/tree/main/samples/plugins for examples in the Semantic Kernel repository.
    /// </para>
    /// </remarks>
    /// <param name="kernel">Semantic Kernel instance</param>
    /// <param name="parentDirectory">Directory containing the plugin directory, e.g. "d:\myAppPlugins"</param>
    /// <param name="promptTemplateFactory">Prompt template factory</param>
    /// <param name="pluginDirectoryNames">Name of the directories containing the selected plugins, e.g. "StrategyPlugin"</param>
    /// <returns>A list of all the semantic functions found in the directory, indexed by plugin name.</returns>
    public static IDictionary<string, ISKFunction> ImportSemanticFunctionsFromDirectory(
        this IKernel kernel,
        string parentDirectory,
        IPromptTemplateFactory? promptTemplateFactory = null,
        params string[] pluginDirectoryNames
        )
    {
        const string ConfigFile = "config.json";
        const string PromptFile = "skprompt.txt";

        var factory = promptTemplateFactory ?? CreateDefaultPromptTemplateFactory(kernel);
        var functions = new Dictionary<string, ISKFunction>();

        ILogger? logger = null;
        foreach (string pluginDirectoryName in pluginDirectoryNames)
        {
            Verify.ValidPluginName(pluginDirectoryName);
            var pluginDirectory = Path.Combine(parentDirectory, pluginDirectoryName);
            Verify.DirectoryExists(pluginDirectory);

            string[] directories = Directory.GetDirectories(pluginDirectory);
            foreach (string dir in directories)
            {
                var functionName = Path.GetFileName(dir);

                // Continue only if prompt template exists
                var promptPath = Path.Combine(dir, PromptFile);
                if (!File.Exists(promptPath)) { continue; }

                // Load prompt configuration. Note: the configuration is optional.
                var configPath = Path.Combine(dir, ConfigFile);
                var promptTemplateConfig = File.Exists(configPath) ?
                    PromptTemplateConfig.FromJson(File.ReadAllText(configPath)) :
                    new PromptTemplateConfig();

                logger ??= kernel.LoggerFactory.CreateLogger(typeof(IKernel));
                if (logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogTrace("Config {0}: {1}", functionName, Json.Serialize(promptTemplateConfig));
                }

                // Load prompt template
                var promptTemplate = File.ReadAllText(promptPath);
                IPromptTemplate? promptTemplateInstance = factory.Create(promptTemplate, promptTemplateConfig);

                if (logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogTrace("Registering function {0}.{1} loaded from {2}", pluginDirectoryName, functionName, dir);
                }

                functions[functionName] = kernel.RegisterSemanticFunction(pluginDirectoryName, functionName, promptTemplateConfig, promptTemplateInstance);
            }
        }

        return functions;
    }

    #region private
    private static string RandomFunctionName() => "func" + Guid.NewGuid().ToString("N");

    private static ISKFunction CreateSemanticFunction(
        this IKernel kernel,
        string pluginName,
        string functionName,
        PromptTemplateConfig promptTemplateConfig,
        IPromptTemplate promptTemplate)
    {
        return SemanticFunction.FromSemanticConfig(
            pluginName,
            functionName,
            promptTemplateConfig,
            promptTemplate,
            kernel.LoggerFactory
        );
    }

    private const string BasicTemplateFactoryAssemblyName = "Microsoft.SemanticKernel.TemplateEngine.Basic";
    private const string BasicTemplateFactoryTypeName = "BasicPromptTemplateFactory";
    private static bool s_promptTemplateFactoryInitialized = false;
    private static Type? s_promptTemplateFactoryType = null;

    /// <summary>
    /// Create a default prompt template factory.
    ///
    /// This is a temporary solution to avoid breaking existing clients.
    /// There will be a separate task to add support for registering instances of IPromptTemplateEngine and obsoleting the current approach.
    ///
    /// </summary>
    /// <param name="kernel">Kernel instance</param>
    /// <returns>Instance of <see cref="IPromptTemplateEngine"/>.</returns>
    private static IPromptTemplateFactory CreateDefaultPromptTemplateFactory(IKernel kernel)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        if (kernel.PromptTemplateEngine is not null)
        {
            return new PromptTemplateFactory(kernel.PromptTemplateEngine);
        }
#pragma warning restore CS0618 // Type or member is obsolete

        if (!s_promptTemplateFactoryInitialized)
        {
            s_promptTemplateFactoryType = GetPromptTemplateFactoryType();
            s_promptTemplateFactoryInitialized = true;
        }

        if (s_promptTemplateFactoryType is not null)
        {
            var constructor = s_promptTemplateFactoryType.GetConstructor(new Type[] { typeof(ILoggerFactory) });
            if (constructor is not null)
            {
#pragma warning disable CS8601 // Null logger factory is OK
                var factory = (IPromptTemplateFactory)constructor.Invoke(new object[] { kernel.LoggerFactory });
                if (factory is not null)
                {
                    return factory;
                }
#pragma warning restore CS8601
            }
        }

        return new NullPromptTemplateFactory();
    }

    /// <summary>
    /// Get the prompt template engine type if available
    /// </summary>
    /// <returns>The type for the prompt template engine if available</returns>
    private static Type? GetPromptTemplateFactoryType()
    {
        try
        {
            var assembly = Assembly.Load(BasicTemplateFactoryAssemblyName);

            return assembly.ExportedTypes.Single(type =>
                type.Name.Equals(BasicTemplateFactoryTypeName, StringComparison.Ordinal) &&
                type.GetInterface(nameof(IPromptTemplateFactory)) is not null);
        }
        catch (Exception ex) when (!ex.IsCriticalException())
        {
            return null;
        }
    }

    private sealed class NullPromptTemplateFactory : IPromptTemplateFactory
    {
        public IPromptTemplate Create(string templateString, PromptTemplateConfig promptTemplateConfig)
        {
            return new NullPromptTemplate(templateString);
        }
    }

    private sealed class NullPromptTemplate : IPromptTemplate
    {
        private readonly string _templateText;

        public NullPromptTemplate(string templateText)
        {
            this._templateText = templateText;
        }

        public IReadOnlyList<ParameterView> Parameters => Array.Empty<ParameterView>();

        public Task<string> RenderAsync(SKContext executionContext, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this._templateText);
        }
    }

    #endregion

    #region Obsolete
    [Obsolete("IPromptTemplateEngine is being replaced with IPromptTemplateFactory. This will be removed in a future release.")]
    internal sealed class PromptTemplateFactory : IPromptTemplateFactory
    {
        private readonly IPromptTemplateEngine _promptTemplateEngine;

        public PromptTemplateFactory(IPromptTemplateEngine promptTemplateEngine)
        {
            this._promptTemplateEngine = promptTemplateEngine;
        }

        public IPromptTemplate Create(string templateString, PromptTemplateConfig promptTemplateConfig)
        {
            return new PromptTemplate(templateString, promptTemplateConfig, this._promptTemplateEngine);
        }
    }
    #endregion
}
