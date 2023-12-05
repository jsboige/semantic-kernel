﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.AI.TextGeneration;
using Microsoft.SemanticKernel.Text;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Provides factory methods for creating <see cref="KernelFunction"/> instances backed by a .NET method.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class KernelFunctionFromMethod : KernelFunction
{
    /// <summary>
    /// Creates a <see cref="KernelFunction"/> instance for a method, specified via an <see cref="MethodInfo"/> instance
    /// and an optional target object if the method is an instance method.
    /// </summary>
    /// <param name="method">The method to be represented via the created <see cref="KernelFunction"/>.</param>
    /// <param name="target">The target object for the <paramref name="method"/> if it represents an instance method. This should be null if and only if <paramref name="method"/> is a static method.</param>
    /// <param name="functionName">Optional function name. If null, it will default to one derived from the method represented by <paramref name="method"/>.</param>
    /// <param name="description">Optional description of the method. If null, it will default to one derived from the method represented by <paramref name="method"/>, if possible (e.g. via a <see cref="DescriptionAttribute"/> on the method).</param>
    /// <param name="parameters">Optional parameter descriptions. If null, it will default to one derived from the method represented by <paramref name="method"/>.</param>
    /// <param name="returnParameter">Optional return parameter description. If null, it will default to one derived from the method represented by <paramref name="method"/>.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    /// <returns>The created <see cref="KernelFunction"/> wrapper for <paramref name="method"/>.</returns>
    public static KernelFunction Create(
        MethodInfo method,
        object? target = null,
        string? functionName = null,
        string? description = null,
        IEnumerable<KernelParameterMetadata>? parameters = null,
        KernelReturnParameterMetadata? returnParameter = null,
        ILoggerFactory? loggerFactory = null)
    {
        Verify.NotNull(method);
        if (!method.IsStatic && target is null)
        {
            throw new ArgumentNullException(nameof(target), "Target must not be null for an instance method.");
        }

        ILogger logger = loggerFactory?.CreateLogger(method.DeclaringType ?? typeof(KernelFunctionFromPrompt)) ?? NullLogger.Instance;

        MethodDetails methodDetails = GetMethodDetails(functionName, method, target, logger);
        var result = new KernelFunctionFromMethod(
            methodDetails.Function,
            methodDetails.Name,
            description ?? methodDetails.Description,
            parameters?.ToList() ?? methodDetails.Parameters,
            returnParameter ?? methodDetails.ReturnParameter,
            logger);

        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace("Created ISKFunction '{Name}' for '{MethodName}'", result.Name, method.Name);
        }

        return result;
    }

    /// <inheritdoc/>
    protected override ValueTask<FunctionResult> InvokeCoreAsync(
        Kernel kernel,
        KernelArguments arguments,
        CancellationToken cancellationToken)
    {
        return this._function(null, kernel, this, arguments, cancellationToken);
    }

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<T> InvokeCoreStreamingAsync<T>(
        Kernel kernel,
        KernelArguments arguments,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var functionResult = await this.InvokeCoreAsync(kernel, arguments, cancellationToken).ConfigureAwait(false);
        if (functionResult.Value is T)
        {
            yield return (T)functionResult.Value;
            yield break;
        }

        // Supports the following provided T types for Method streaming
        if (typeof(T) == typeof(StreamingContentBase) ||
            typeof(T) == typeof(StreamingMethodContent))
        {
            if (functionResult.Value is not null)
            {
                yield return (T)(object)new StreamingMethodContent(functionResult.Value);
            }
            yield break;
        }

        throw new NotSupportedException($"Streaming function {this.Name} does not support type {typeof(T)}");

        // We don't invoke the hook here as the InvokeCoreAsync will do that for us
    }

    /// <summary>
    /// JSON serialized string representation of the function.
    /// </summary>
    public override string ToString() => JsonSerializer.Serialize(this, JsonOptionsCache.WriteIndented);

    #region private

    /// <summary>Delegate used to invoke the underlying delegate.</summary>
    private delegate ValueTask<FunctionResult> ImplementationFunc(
        ITextGenerationService? textGeneration,
        Kernel kernel,
        KernelFunction function,
        KernelArguments arguments,
        CancellationToken cancellationToken);

    private static readonly object[] s_cancellationTokenNoneArray = new object[] { CancellationToken.None };
    private readonly ImplementationFunc _function;
    private readonly ILogger _logger;

    private record struct MethodDetails(string Name, string Description, ImplementationFunc Function, List<KernelParameterMetadata> Parameters, KernelReturnParameterMetadata ReturnParameter);

    private KernelFunctionFromMethod(
        ImplementationFunc implementationFunc,
        string functionName,
        string description,
        IReadOnlyList<KernelParameterMetadata> parameters,
        KernelReturnParameterMetadata returnParameter,
        ILogger logger) : base(functionName, description, parameters, returnParameter)
    {
        Verify.ValidFunctionName(functionName);

        this._logger = logger;

        this._function = implementationFunc;
    }

    private static MethodDetails GetMethodDetails(string? functionName, MethodInfo method, object? target, ILogger logger)
    {
        ThrowForInvalidSignatureIf(method.IsGenericMethodDefinition, method, "Generic methods are not supported");

        if (functionName is null)
        {
            // Get the name to use for the function.  If the function has a KernelFunction attribute and it contains a name, we use that.
            // Otherwise, we use the name of the method, but strip off any "Async" suffix if it's {Value}Task-returning.
            // We don't apply any heuristics to the value supplied by SKName so that it can always be used
            // as a definitive override.
            functionName = method.GetCustomAttribute<KernelFunctionAttribute>(inherit: true)?.Name?.Trim();
            if (string.IsNullOrEmpty(functionName))
            {
                functionName = SanitizeMetadataName(method.Name!);

                if (IsAsyncMethod(method) &&
                    functionName.EndsWith("Async", StringComparison.Ordinal) &&
                    functionName.Length > "Async".Length)
                {
                    functionName = functionName.Substring(0, functionName.Length - "Async".Length);
                }
            }
        }

        Verify.ValidFunctionName(functionName);

        List<KernelParameterMetadata> stringParameterViews = new();
        var parameters = method.GetParameters();

        // Get marshaling funcs for parameters and build up the parameter metadata.
        var parameterFuncs = new Func<Kernel, KernelArguments, CancellationToken, object?>[parameters.Length];
        bool sawFirstParameter = false, hasKernelParam = false, hasFunctionArgumentsParam = false, hasCancellationTokenParam = false, hasLoggerParam = false, hasCultureParam = false;
        for (int i = 0; i < parameters.Length; i++)
        {
            (parameterFuncs[i], KernelParameterMetadata? parameterView) = GetParameterMarshalerDelegate(
                method, parameters[i],
                ref sawFirstParameter, ref hasKernelParam, ref hasFunctionArgumentsParam, ref hasCancellationTokenParam, ref hasLoggerParam, ref hasCultureParam);
            if (parameterView is not null)
            {
                stringParameterViews.Add(parameterView);
            }
        }

        // Check for param names conflict
        Verify.ParametersUniqueness(stringParameterViews);

        // Get marshaling func for the return value.
        Func<Kernel, KernelFunction, object?, ValueTask<FunctionResult>> returnFunc = GetReturnValueMarshalerDelegate(method);

        // Create the func
        ValueTask<FunctionResult> Function(ITextGenerationService? text, Kernel kernel, KernelFunction function, KernelArguments arguments, CancellationToken cancellationToken)
        {
            // Create the arguments.
            object?[] args = parameterFuncs.Length != 0 ? new object?[parameterFuncs.Length] : Array.Empty<object?>();
            for (int i = 0; i < args.Length; i++)
            {
                args[i] = parameterFuncs[i](kernel, arguments, cancellationToken);
            }

            // Invoke the method.
            object? result = Invoke(method, target, args);

            // Extract and return the result.
            return returnFunc(kernel, function, result);
        }

        // And return the details.
        return new MethodDetails
        {
            Function = Function,
            Name = functionName!,
            Description = method.GetCustomAttribute<DescriptionAttribute>(inherit: true)?.Description ?? "",
            Parameters = stringParameterViews,
            ReturnParameter = new KernelReturnParameterMetadata()
            {
                Description = method.ReturnParameter.GetCustomAttribute<DescriptionAttribute>(inherit: true)?.Description,
                ParameterType = method.ReturnType,
            }
        };
    }

    /// <summary>Gets whether a method has a known async return type.</summary>
    private static bool IsAsyncMethod(MethodInfo method)
    {
        Type t = method.ReturnType;

        if (t == typeof(Task) || t == typeof(ValueTask))
        {
            return true;
        }

        if (t.IsGenericType)
        {
            t = t.GetGenericTypeDefinition();
            if (t == typeof(Task<>) || t == typeof(ValueTask<>) || t == typeof(IAsyncEnumerable<>))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets a delegate for handling the marshaling of a parameter.
    /// </summary>
    private static (Func<Kernel, KernelArguments, CancellationToken, object?>, KernelParameterMetadata?) GetParameterMarshalerDelegate(
        MethodInfo method, ParameterInfo parameter,
        ref bool sawFirstParameter, ref bool hasKernelParam, ref bool hasFunctionArgumentsParam, ref bool hasCancellationTokenParam, ref bool hasLoggerParam, ref bool hasCultureParam)
    {
        Type type = parameter.ParameterType;

        // Handle special types. These can each show up at most once in the method signature.

        if (type == typeof(Kernel))
        {
            TrackUniqueParameterType(ref hasKernelParam, method, $"At most one {nameof(Kernel)} parameter is permitted.");
            return (static (Kernel kernel, KernelArguments _, CancellationToken _) => kernel, null);
        }

        if (type == typeof(KernelArguments))
        {
            TrackUniqueParameterType(ref hasFunctionArgumentsParam, method, $"At most one {nameof(KernelArguments)} parameter is permitted.");
            return (static (Kernel _, KernelArguments arguments, CancellationToken _) => arguments, null);
        }

        if (type == typeof(ILogger) || type == typeof(ILoggerFactory))
        {
            TrackUniqueParameterType(ref hasLoggerParam, method, $"At most one {nameof(ILogger)}/{nameof(ILoggerFactory)} parameter is permitted.");
            return type == typeof(ILogger) ?
                ((Kernel kernel, KernelArguments _, CancellationToken _) => kernel.LoggerFactory.CreateLogger(method?.DeclaringType ?? typeof(KernelFunctionFromPrompt)), null) :
                ((Kernel kernel, KernelArguments _, CancellationToken _) => kernel.LoggerFactory, null);
        }

        if (type == typeof(CultureInfo) || type == typeof(IFormatProvider))
        {
            TrackUniqueParameterType(ref hasCultureParam, method, $"At most one {nameof(CultureInfo)}/{nameof(IFormatProvider)} parameter is permitted.");
            return (static (Kernel kernel, KernelArguments _, CancellationToken _) => kernel.Culture, null);
        }

        if (type == typeof(CancellationToken))
        {
            TrackUniqueParameterType(ref hasCancellationTokenParam, method, $"At most one {nameof(CancellationToken)} parameter is permitted.");
            return (static (Kernel _, KernelArguments _, CancellationToken cancellationToken) => cancellationToken, null);
        }

        // Handle the other types. These can each show up multiple times in the method signature.

        string name = SanitizeMetadataName(parameter.Name ?? "");
        bool nameIsInput = name.Equals(KernelArguments.InputParameterName, StringComparison.OrdinalIgnoreCase);
        ThrowForInvalidSignatureIf(string.IsNullOrWhiteSpace(name), method, $"Parameter {parameter.Name}'s attribute defines an invalid name.");
        ThrowForInvalidSignatureIf(sawFirstParameter && nameIsInput, method, "Only the first parameter may be named 'input'");

        bool fallBackToInput = !sawFirstParameter && !nameIsInput;

        var parser = GetParser(type);

        object? parameterFunc(Kernel kernel, KernelArguments arguments, CancellationToken _)
        {
            // 1. Use the value of the variable if it exists.
            if (arguments.TryGetValue(name, out object? value))
            {
                return Process(value);
            }

            // 2. Otherwise, use the default value if there is one, sourced either from an attribute or the parameter's default.
            if (parameter.HasDefaultValue)
            {
                return parameter.DefaultValue;
            }

            // 3. Otherwise, use "input" if this is the first (or only) parameter.
            if (fallBackToInput)
            {
                arguments.TryGetValue(KernelArguments.InputParameterName, out object? input);
                return Process(input);
            }

            // 4. Otherwise, fail.
            throw new KernelException($"Missing value for parameter '{name}'",
                new ArgumentException("Missing value function parameter", name));

            object? Process(object? value)
            {
                //Converting string argument to target parameter type
                if (value is string stringValue && value.GetType() != type && parser is Func<string?, CultureInfo, object>)
                {
                    try
                    {
                        return parser(stringValue, kernel.Culture);
                    }
                    catch (Exception e) when (!e.IsCriticalException())
                    {
                        throw new ArgumentOutOfRangeException(name, value, e.Message);
                    }
                }

                return value;
            }
        }

        sawFirstParameter = true;

        var parameterView = new KernelParameterMetadata(name)
        {
            Description = parameter.GetCustomAttribute<DescriptionAttribute>(inherit: true)?.Description,
            DefaultValue = parameter.DefaultValue?.ToString(),
            IsRequired = !parameter.IsOptional,
            ParameterType = type
        };

        return (parameterFunc, parameterView);
    }

    /// <summary>
    /// Gets a delegate for handling the result value of a method, converting it into the <see cref="Task{SKContext}"/> to return from the invocation.
    /// </summary>
    private static Func<Kernel, KernelFunction, object?, ValueTask<FunctionResult>> GetReturnValueMarshalerDelegate(MethodInfo method)
    {
        // Handle each known return type for the method
        Type returnType = method.ReturnType;

        // No return value, either synchronous (void) or asynchronous (Task / ValueTask).

        if (returnType == typeof(void))
        {
            return static (_, function, _) =>
                new ValueTask<FunctionResult>(new FunctionResult(function));
        }

        if (returnType == typeof(Task))
        {
            return async static (_, function, result) =>
            {
                await ((Task)ThrowIfNullResult(result)).ConfigureAwait(false);
                return new FunctionResult(function);
            };
        }

        if (returnType == typeof(ValueTask))
        {
            return async static (_, function, result) =>
            {
                await ((ValueTask)ThrowIfNullResult(result)).ConfigureAwait(false);
                return new FunctionResult(function);
            };
        }

        // string (which is special as no marshaling is required), either synchronous (string) or asynchronous (Task<string> / ValueTask<string>)

        if (returnType == typeof(string))
        {
            return static (kernel, function, result) =>
            {
                var resultString = (string?)result;
                return new ValueTask<FunctionResult>(new FunctionResult(function, resultString, kernel.Culture));
            };
        }

        if (returnType == typeof(Task<string>))
        {
            return async static (kernel, function, result) =>
            {
                var resultString = await ((Task<string>)ThrowIfNullResult(result)).ConfigureAwait(false);
                return new FunctionResult(function, resultString, kernel.Culture);
            };
        }

        if (returnType == typeof(ValueTask<string>))
        {
            return async static (kernel, function, result) =>
            {
                var resultString = await ((ValueTask<string>)ThrowIfNullResult(result)).ConfigureAwait(false);
                return new FunctionResult(function, resultString, kernel.Culture);
            };
        }

        if (returnType == typeof(FunctionResult))
        {
            return static (_, function, result) =>
            {
                var functionResult = (FunctionResult?)result;
                return new ValueTask<FunctionResult>(functionResult ?? new FunctionResult(function));
            };
        }

        if (returnType == typeof(Task<FunctionResult>))
        {
            return async static (_, _, result) =>
            {
                var functionResult = await ((Task<FunctionResult>)ThrowIfNullResult(result)).ConfigureAwait(false);
                return functionResult;
            };
        }

        if (returnType == typeof(ValueTask<FunctionResult>))
        {
            return async static (_, _, result) =>
            {
                var functionResult = await ((ValueTask<FunctionResult>)ThrowIfNullResult(result)).ConfigureAwait(false);
                return functionResult;
            };
        }

        // All other synchronous return types T.

        if (!returnType.IsGenericType || returnType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return (kernel, function, result) =>
            {
                return new ValueTask<FunctionResult>(new FunctionResult(function, result, kernel.Culture));
            };
        }

        // All other asynchronous return types

        // Task<T>
        if (returnType.GetGenericTypeDefinition() is Type genericTask &&
            genericTask == typeof(Task<>) &&
            returnType.GetProperty("Result", BindingFlags.Public | BindingFlags.Instance)?.GetGetMethod() is MethodInfo taskResultGetter)
        {
            return async (kernel, function, result) =>
            {
                await ((Task)ThrowIfNullResult(result)).ConfigureAwait(false);

                var taskResult = Invoke(taskResultGetter, result, Array.Empty<object>());
                return new FunctionResult(function, taskResult, kernel.Culture);
            };
        }

        // ValueTask<T>
        if (returnType.GetGenericTypeDefinition() is Type genericValueTask &&
            genericValueTask == typeof(ValueTask<>) &&
            returnType.GetMethod("AsTask", BindingFlags.Public | BindingFlags.Instance) is MethodInfo valueTaskAsTask &&
            valueTaskAsTask.ReturnType.GetProperty("Result", BindingFlags.Public | BindingFlags.Instance)?.GetGetMethod() is MethodInfo asTaskResultGetter)
        {
            return async (kernel, function, result) =>
            {
                Task task = (Task)Invoke(valueTaskAsTask, ThrowIfNullResult(result), Array.Empty<object>())!;
                await task.ConfigureAwait(false);

                var taskResult = Invoke(asTaskResultGetter, task, Array.Empty<object>());
                return new FunctionResult(function, taskResult, kernel.Culture);
            };
        }

        // IAsyncEnumerable<T>
        if (returnType.GetGenericTypeDefinition() is Type genericAsyncEnumerable && genericAsyncEnumerable == typeof(IAsyncEnumerable<>))
        {
            Type elementType = returnType.GetGenericArguments()[0];

            MethodInfo? getAsyncEnumeratorMethod = typeof(IAsyncEnumerable<>)
                .MakeGenericType(elementType)
                .GetMethod("GetAsyncEnumerator");

            if (getAsyncEnumeratorMethod is not null)
            {
                return (kernel, function, result) =>
                {
                    var asyncEnumerator = Invoke(getAsyncEnumeratorMethod, result, s_cancellationTokenNoneArray);

                    if (asyncEnumerator is not null)
                    {
                        return new ValueTask<FunctionResult>(new FunctionResult(function, asyncEnumerator, kernel.Culture));
                    }

                    return new ValueTask<FunctionResult>(new FunctionResult(function));
                };
            }
        }

        // Unrecognized return type.
        throw GetExceptionForInvalidSignature(method, $"Unknown return type {returnType}");

        // Throws an exception if a result is found to be null unexpectedly
        static object ThrowIfNullResult(object? result) =>
            result ??
            throw new KernelException("Function returned null unexpectedly.");
    }

    /// <summary>Invokes the MethodInfo with the specified target object and arguments.</summary>
    private static object? Invoke(MethodInfo method, object? target, object?[]? arguments)
    {
        object? result = null;
        try
        {
            const BindingFlags BindingFlagsDoNotWrapExceptions = (BindingFlags)0x02000000; // BindingFlags.DoNotWrapExceptions on .NET Core 2.1+, ignored before then
            result = method.Invoke(target, BindingFlagsDoNotWrapExceptions, binder: null, arguments, culture: null);
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            // If we're targeting .NET Framework, such that BindingFlags.DoNotWrapExceptions
            // is ignored, the original exception will be wrapped in a TargetInvocationException.
            // Unwrap it and throw that original exception, maintaining its stack information.
            ExceptionDispatchInfo.Capture(e.InnerException).Throw();
        }

        return result;
    }

    /// <summary>Gets an exception that can be thrown indicating an invalid signature.</summary>
    [DoesNotReturn]
    private static Exception GetExceptionForInvalidSignature(MethodInfo method, string reason) =>
        throw new KernelException($"Function '{method.Name}' is not supported by the kernel. {reason}");

    /// <summary>Throws an exception indicating an invalid KernelFunctionFactory signature if the specified condition is not met.</summary>
    private static void ThrowForInvalidSignatureIf([DoesNotReturnIf(true)] bool condition, MethodInfo method, string reason)
    {
        if (condition)
        {
            throw GetExceptionForInvalidSignature(method, reason);
        }
    }

    /// <summary>Tracks whether a particular kind of parameter has been seen, throwing an exception if it has, and marking it as seen if it hasn't</summary>
    private static void TrackUniqueParameterType(ref bool hasParameterType, MethodInfo method, string failureMessage)
    {
        ThrowForInvalidSignatureIf(hasParameterType, method, failureMessage);
        hasParameterType = true;
    }

    /// <summary>
    /// Gets a TypeConverter-based parser for parsing a string as the target type.
    /// </summary>
    /// <param name="targetType">Specifies the target type into which a string should be parsed.</param>
    /// <returns>The parsing function if the target type is supported; otherwise, null.</returns>
    /// <remarks>
    /// The parsing function uses whatever TypeConverter is registered for the target type.
    /// Parsing is first attempted using the current culture, and if that fails, it tries again
    /// with the invariant culture. If both fail, an exception is thrown.
    /// </remarks>
    private static Func<string?, CultureInfo, object?>? GetParser(Type targetType) =>
        s_parsers.GetOrAdd(targetType, static targetType =>
        {
            // Strings just parse to themselves.
            if (targetType == typeof(string))
            {
                return (input, cultureInfo) => input;
            }

            // For nullables, parse as the inner type.  We then just need to be careful to treat null as null,
            // as the underlying parser might not be expecting null.
            bool wasNullable = false;
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                wasNullable = true;
                targetType = Nullable.GetUnderlyingType(targetType)!;
            }

            // For enums, delegate to Enum.Parse, special-casing null if it was actually Nullable<EnumType>.
            if (targetType.IsEnum)
            {
                return (input, cultureInfo) =>
                {
                    if (wasNullable && input is null)
                    {
                        return null!;
                    }

                    return Enum.Parse(targetType, input, ignoreCase: true);
                };
            }

            // Finally, look up and use a type converter. Again, special-case null if it was actually Nullable<T>.
            if (TypeConverterFactory.GetTypeConverter(targetType) is TypeConverter converter && converter.CanConvertFrom(typeof(string)))
            {
                return (input, cultureInfo) =>
                {
                    if (wasNullable && input is null)
                    {
                        return null!;
                    }

                    // First try to parse using the supplied culture (or current if none was supplied).
                    // If that fails, try with the invariant culture and allow any exception to propagate.
                    try
                    {
                        return converter.ConvertFromString(context: null, cultureInfo, input);
                    }
                    catch (Exception e) when (!e.IsCriticalException() && cultureInfo != CultureInfo.InvariantCulture)
                    {
                        return converter.ConvertFromInvariantString(input);
                    }
                };
            }

            // Unsupported type.
            return null;
        });

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => string.IsNullOrWhiteSpace(this.Description) ? this.Name : $"{this.Name} ({this.Description})";

    /// <summary>
    /// Remove characters from method name that are valid in metadata but invalid for SK.
    /// </summary>
    private static string SanitizeMetadataName(string methodName) =>
        s_invalidNameCharsRegex.Replace(methodName, "_");

    /// <summary>Regex that flags any character other than ASCII digits or letters or the underscore.</summary>
    private static readonly Regex s_invalidNameCharsRegex = new("[^0-9A-Za-z_]");

    /// <summary>Parser functions for converting strings to parameter types.</summary>
    private static readonly ConcurrentDictionary<Type, Func<string?, CultureInfo, object?>?> s_parsers = new();

    #endregion
}
