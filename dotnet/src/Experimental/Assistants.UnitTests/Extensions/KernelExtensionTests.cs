﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Experimental.Assistants;
using Xunit;

namespace SemanticKernel.Experimental.Assistants.UnitTests;

[Trait("Category", "Unit Tests")]
[Trait("Feature", "Assistant")]
public sealed class KernelExtensionTests
{
    private const string TwoPartToolName = "Fake-Bogus";

    [Fact]
    public static void InvokeTwoPartTool()
    {
        //Arrange
        var function = KernelFunctionFactory.CreateFromMethod(() => { }, functionName: "Bogus");

        var kernel = new Kernel();
        kernel.Plugins.Add(new KernelPlugin("Fake", new[] { function }));

        //Act
        var tool = kernel.GetAssistantTool(TwoPartToolName);

        //Assert
        Assert.NotNull(tool);
        Assert.Equal("Bogus", tool.Name);
    }

    [Theory]
    [InlineData("Bogus")]
    [InlineData("i-am-not-valid")]
    public static void InvokeInvalidSinglePartTool(string toolName)
    {
        //Arrange
        var kernel = new Kernel();

        //Act & Assert
        Assert.Throws<KernelException>(() => kernel.GetAssistantTool(toolName));
    }
}
