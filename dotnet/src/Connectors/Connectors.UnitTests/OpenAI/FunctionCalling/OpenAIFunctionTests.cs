﻿// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using Azure.AI.OpenAI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Xunit;

namespace SemanticKernel.Connectors.UnitTests.OpenAI.FunctionCalling;

public sealed class OpenAIFunctionTests
{
    [Fact]
    public void ItCanConvertToFunctionDefinitionWithNoPluginName()
    {
        // Arrange
        var sut = new OpenAIFunction
        {
            FunctionName = "myfunc",
            PluginName = string.Empty,
            Description = "This is a description of the function.",
        };

        // Act
        FunctionDefinition result = sut.ToFunctionDefinition();

        // Assert
        Assert.Equal(sut.FunctionName, result.Name);
        Assert.Equal(sut.Description, result.Description);
    }

    [Fact]
    public void ItCanConvertToFunctionDefinitionWithPluginName()
    {
        // Arrange
        var sut = new OpenAIFunction
        {
            FunctionName = "myfunc",
            PluginName = "myplugin",
            Description = "This is a description of the function.",
        };

        // Act
        FunctionDefinition result = sut.ToFunctionDefinition();

        // Assert
        Assert.Equal("myplugin_myfunc", result.Name);
        Assert.Equal(sut.Description, result.Description);
    }

    [Fact]
    public void ItCanConvertToFunctionDefinitionsWithParameterTypesAndReturnParameterType()
    {
        string expectedParameterSchema = "{   \"type\": \"object\",   \"required\": [\"param1\", \"param2\"],   \"properties\": {     \"param1\": { \"type\": \"string\", \"description\": \"String param 1\" },     \"param2\": { \"type\": \"integer\", \"description\": \"Int param 2\" }   } } ";

        OpenAIFunctionParameter param1 = new()
        {
            Name = "param1",
            Description = "String param 1",
            IsRequired = true,
            ParameterType = typeof(string)
        };

        OpenAIFunctionParameter param2 = new()
        {
            Name = "param2",
            Description = "Int param 2",
            IsRequired = true,
            ParameterType = typeof(int)
        };

        OpenAIFunctionReturnParameter returnParameter = new()
        {
            Description = "My test Result",
            ParameterType = typeof(string)
        };

        OpenAIFunction sut = new()
        {
            PluginName = "Tests",
            FunctionName = "TestFunction",
            Description = "My test function",
            Parameters = new[] { param1, param2 },
            ReturnParameter = returnParameter
        };

        FunctionDefinition functionDefinition = sut.ToFunctionDefinition();

        var exp = JsonSerializer.Serialize(KernelJsonSchema.Parse(expectedParameterSchema));
        var act = JsonSerializer.Serialize(KernelJsonSchema.Parse(functionDefinition.Parameters));

        Assert.NotNull(functionDefinition);
        Assert.Equal("Tests_TestFunction", functionDefinition.Name);
        Assert.Equal("My test function", functionDefinition.Description);
        Assert.Equal(JsonSerializer.Serialize(KernelJsonSchema.Parse(expectedParameterSchema)), JsonSerializer.Serialize(KernelJsonSchema.Parse(functionDefinition.Parameters)));
    }

    [Fact]
    public void ItCanConvertToFunctionDefinitionsWithParameterTypesAndNoReturnParameterType()
    {
        string expectedParameterSchema = "{   \"type\": \"object\",   \"required\": [\"param1\", \"param2\"],   \"properties\": {     \"param1\": { \"type\": \"string\", \"description\": \"String param 1\" },     \"param2\": { \"type\": \"integer\", \"description\": \"Int param 2\" }   } } ";

        OpenAIFunctionParameter param1 = new()
        {
            Name = "param1",
            Description = "String param 1",
            IsRequired = true,
            ParameterType = typeof(string)
        };

        OpenAIFunctionParameter param2 = new()
        {
            Name = "param2",
            Description = "Int param 2",
            IsRequired = true,
            ParameterType = typeof(int)
        };

        OpenAIFunction sut = new()
        {
            PluginName = "Tests",
            FunctionName = "TestFunction",
            Description = "My test function",
            Parameters = new[] { param1, param2 }
        };

        FunctionDefinition functionDefinition = sut.ToFunctionDefinition();

        Assert.NotNull(functionDefinition);
        Assert.Equal("Tests_TestFunction", functionDefinition.Name);
        Assert.Equal("My test function", functionDefinition.Description);
        Assert.Equal(JsonSerializer.Serialize(KernelJsonSchema.Parse(expectedParameterSchema)), JsonSerializer.Serialize(KernelJsonSchema.Parse(functionDefinition.Parameters)));
    }
}
