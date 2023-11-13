﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Services;
using Microsoft.SemanticKernel.TemplateEngine;
using Xunit;

namespace SemanticKernel.UnitTests.Functions;
public class OrderedIAIServiceConfigurationProviderTests
{
    [Fact]
    public void ItThrowsAnSKExceptionForNoServices()
    {
        // Arrange
        var kernel = new KernelBuilder().Build();
        var context = kernel.CreateNewContext();
        var skfunction = kernel.CreateSemanticFunction("Hello AI");
        var serviceSelector = new OrderedIAIServiceSelector();

        // Act
        // Assert
        Assert.Throws<SKException>(() => serviceSelector.SelectAIService<ITextCompletion>(context, skfunction));
    }

    [Fact]
    public void ItGetsAIServiceConfigurationForSingleAIService()
    {
        // Arrange
        var kernel = new KernelBuilder().WithAIService<IAIService>("service1", new AIService()).Build();
        var context = kernel.CreateNewContext();
        var skfunction = kernel.CreateSemanticFunction("Hello AI");
        var serviceSelector = new OrderedIAIServiceSelector();

        // Act
        (var aiService, var defaultRequestSettings) = serviceSelector.SelectAIService<IAIService>(context, skfunction);

        // Assert
        Assert.NotNull(aiService);
        Assert.Null(defaultRequestSettings);
    }

    [Fact]
    public void ItGetsAIServiceConfigurationForSingleTextCompletion()
    {
        // Arrange
        var kernel = new KernelBuilder().WithAIService<ITextCompletion>("service1", new TextCompletion()).Build();
        var context = kernel.CreateNewContext();
        var skfunction = kernel.CreateSemanticFunction("Hello AI");
        var serviceSelector = new OrderedIAIServiceSelector();

        // Act
        (var aiService, var defaultRequestSettings) = serviceSelector.SelectAIService<ITextCompletion>(context, skfunction);

        // Assert
        Assert.NotNull(aiService);
        Assert.Null(defaultRequestSettings);
    }

    [Fact]
    public void ItGetsAIServiceConfigurationForTextCompletionByServiceId()
    {
        // Arrange
        var kernel = new KernelBuilder()
            .WithAIService<ITextCompletion>("service1", new TextCompletion())
            .WithAIService<ITextCompletion>("service2", new TextCompletion())
            .Build();
        var context = kernel.CreateNewContext();
        var requestSettings = new AIRequestSettings() { ServiceId = "service2" };
        var skfunction = kernel.CreateSemanticFunction("Hello AI", requestSettings: requestSettings);
        var serviceSelector = new OrderedIAIServiceSelector();

        // Act
        (var aiService, var defaultRequestSettings) = serviceSelector.SelectAIService<ITextCompletion>(context, skfunction);

        // Assert
        Assert.Equal(context.ServiceProvider.GetService<ITextCompletion>("service2"), aiService);
        Assert.Equal(requestSettings, defaultRequestSettings);
    }

    [Fact]
    public void ItThrowsAnSKExceptionForNotFoundService()
    {
        // Arrange
        var kernel = new KernelBuilder()
            .WithAIService<ITextCompletion>("service1", new TextCompletion())
            .WithAIService<ITextCompletion>("service2", new TextCompletion())
            .Build();
        var context = kernel.CreateNewContext();
        var requestSettings = new AIRequestSettings() { ServiceId = "service3" };
        var skfunction = kernel.CreateSemanticFunction("Hello AI", requestSettings: requestSettings);
        var serviceSelector = new OrderedIAIServiceSelector();

        // Act
        // Assert
        Assert.Throws<SKException>(() => serviceSelector.SelectAIService<ITextCompletion>(context, skfunction));
    }

    [Fact]
    public void ItUsesDefaultServiceForEmptyModelSettings()
    {
        // Arrange
        var kernel = new KernelBuilder()
            .WithAIService<ITextCompletion>("service1", new TextCompletion())
            .WithAIService<ITextCompletion>("service2", new TextCompletion(), true)
            .Build();
        var context = kernel.CreateNewContext();
        var skfunction = kernel.CreateSemanticFunction("Hello AI");
        var serviceSelector = new OrderedIAIServiceSelector();

        // Act
        (var aiService, var defaultRequestSettings) = serviceSelector.SelectAIService<ITextCompletion>(context, skfunction);

        // Assert
        Assert.Equal(context.ServiceProvider.GetService<ITextCompletion>("service2"), aiService);
        Assert.Null(defaultRequestSettings);
    }

    [Fact]
    public void ItUsesDefaultServiceAndSettings()
    {
        // Arrange
        // Arrange
        var kernel = new KernelBuilder()
            .WithAIService<ITextCompletion>("service1", new TextCompletion())
            .WithAIService<ITextCompletion>("service2", new TextCompletion(), true)
            .Build();
        var context = kernel.CreateNewContext();
        var requestSettings = new AIRequestSettings();
        var skfunction = kernel.CreateSemanticFunction("Hello AI", requestSettings: requestSettings);
        var serviceSelector = new OrderedIAIServiceSelector();

        // Act
        (var aiService, var defaultRequestSettings) = serviceSelector.SelectAIService<ITextCompletion>(context, skfunction);

        // Assert
        Assert.Equal(context.ServiceProvider.GetService<ITextCompletion>("service2"), aiService);
        Assert.Equal(requestSettings, defaultRequestSettings);
    }

    [Fact]
    public void ItUsesDefaultServiceAndSettingsEmptyServiceId()
    {
        // Arrange
        var kernel = new KernelBuilder()
            .WithAIService<ITextCompletion>("service1", new TextCompletion())
            .WithAIService<ITextCompletion>("service2", new TextCompletion(), true)
            .Build();
        var context = kernel.CreateNewContext();
        var requestSettings = new AIRequestSettings() { ServiceId = "" };
        var skfunction = kernel.CreateSemanticFunction("Hello AI", requestSettings: requestSettings);
        var serviceSelector = new OrderedIAIServiceSelector();

        // Act
        (var aiService, var defaultRequestSettings) = serviceSelector.SelectAIService<ITextCompletion>(context, skfunction);

        // Assert
        Assert.Equal(context.ServiceProvider.GetService<ITextCompletion>("service2"), aiService);
        Assert.Equal(requestSettings, defaultRequestSettings);
    }

    [Theory]
    [InlineData(new string[] { "service1" }, "service1")]
    [InlineData(new string[] { "service2" }, "service2")]
    [InlineData(new string[] { "service3" }, "service3")]
    [InlineData(new string[] { "service4", "service1" }, "service1")]
    public void ItGetsAIServiceConfigurationByOrder(string[] serviceIds, string expectedServiceId)
    {
        // Arrange
        var kernel = new KernelBuilder()
            .WithAIService<ITextCompletion>("service1", new TextCompletion())
            .WithAIService<ITextCompletion>("service2", new TextCompletion())
            .WithAIService<ITextCompletion>("service3", new TextCompletion())
            .Build();
        var context = kernel.CreateNewContext();
        var modelSettings = new List<AIRequestSettings>();
        foreach (var serviceId in serviceIds)
        {
            modelSettings.Add(new AIRequestSettings() { ServiceId = serviceId });
        }
        var skfunction = kernel.CreateSemanticFunction("Hello AI", promptTemplateConfig: new PromptTemplateConfig() { ModelSettings = modelSettings });
        var serviceSelector = new OrderedIAIServiceSelector();

        // Act
        (var aiService, var defaultRequestSettings) = serviceSelector.SelectAIService<ITextCompletion>(context, skfunction);

        // Assert
        Assert.Equal(context.ServiceProvider.GetService<ITextCompletion>(expectedServiceId), aiService);
        Assert.Equal(expectedServiceId, defaultRequestSettings!.ServiceId);
    }

    #region private
    private sealed class AIService : IAIService
    {
        public IReadOnlyDictionary<string, string> Attributes => new Dictionary<string, string>();

        public string? ModelId { get; }
    }

    private sealed class TextCompletion : ITextCompletion
    {
        public IReadOnlyDictionary<string, string> Attributes => new Dictionary<string, string>();

        public string? ModelId { get; }

        public Task<IReadOnlyList<ITextResult>> GetCompletionsAsync(string text, AIRequestSettings? requestSettings = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<ITextStreamingResult> GetStreamingCompletionsAsync(string text, AIRequestSettings? requestSettings = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
    #endregion
}
