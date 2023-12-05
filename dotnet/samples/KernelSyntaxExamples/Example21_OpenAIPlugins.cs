﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.OpenApi.Model;
using Microsoft.SemanticKernel.Plugins.OpenApi.OpenAI;
using RepoUtils;

// ReSharper disable once InconsistentNaming
public static class Example21_OpenAIPlugins
{
    public static async Task RunAsync()
    {
        await RunOpenAIPluginAsync();
    }

    private static async Task RunOpenAIPluginAsync()
    {
        var kernel = new KernelBuilder().WithLoggerFactory(ConsoleLogger.LoggerFactory).Build();

        //This HTTP client is optional. SK will fallback to a default internal one if omitted.
        using HttpClient httpClient = new();

        //Import an Open AI plugin via URI
        var plugin = await kernel.ImportPluginFromOpenAIAsync("<plugin name>", new Uri("<OpenAI-plugin>"), new OpenAIFunctionExecutionParameters(httpClient));

        //Add arguments for required parameters, arguments for optional ones can be skipped.
        var arguments = new KernelArguments { ["<parameter-name>"] = "<parameter-value>" };

        //Run
        var functionResult = await kernel.InvokeAsync(plugin["<function-name>"], arguments);

        var result = functionResult.GetValue<RestApiOperationResponse>();

        Console.WriteLine("Function execution result: {0}", result?.Content?.ToString());

        //--------------- Example of using Klarna OpenAI plugin ------------------------

        //var kernel = new KernelBuilder().WithLoggerFactory(ConsoleLogger.LoggerFactory).Build();

        //var plugin = await kernel.ImportPluginFromOpenAIAsync("Klarna", new Uri("https://www.klarna.com/.well-known/ai-plugin.json"));

        //var arguments = new KernelArguments();
        //arguments["q"] = "Laptop";      // A precise query that matches one very small category or product that needs to be searched for to find the products the user is looking for. If the user explicitly stated what they want, use that as a query. The query is as specific as possible to the product name or category mentioned by the user in its singular form, and don't contain any clarifiers like latest, newest, cheapest, budget, premium, expensive or similar. The query is always taken from the latest topic, if there is a new topic a new query is started.
        //arguments["size"] = "3";        // number of products returned
        //arguments["budget"] = "200";    // maximum price of the matching product in local currency, filters results
        //arguments["countryCode"] = "US";// ISO 3166 country code with 2 characters based on the user location. Currently, only US, GB, DE, SE and DK are supported.

        //var functionResult = await kernel.InvokeAsync(plugin["productsUsingGET"], arguments);

        //var result = functionResult.GetValue<RestApiOperationResponse>();

        //Console.WriteLine("Function execution result: {0}", result?.Content?.ToString());
    }
}
