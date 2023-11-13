﻿// Copyright (c) Microsoft. All rights reserved.

using System.Linq;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Orchestration;

namespace Microsoft.SemanticKernel.Services;

/// <summary>
/// Implementation of <see cref="IAIServiceSelector"/> that selects the AI service based on the order of the model settings.
/// Uses the service id to select the preferred service provider and then returns the service and associated model settings.
/// </summary>
internal class OrderedIAIServiceSelector : IAIServiceSelector
{
    /// <inheritdoc/>
    public (T?, AIRequestSettings?) SelectAIService<T>(SKContext context, ISKFunction skfunction) where T : IAIService
    {
        var serviceProvider = context.ServiceProvider;
        var modelSettings = skfunction.ModelSettings;
        if (modelSettings is null || !modelSettings.Any())
        {
            var service = serviceProvider.GetService<T>(null);
            if (service is not null)
            {
                return (service, null);
            }
        }
        else
        {
            AIRequestSettings? defaultRequestSettings = null;
            foreach (var model in modelSettings)
            {
                if (!string.IsNullOrEmpty(model.ServiceId))
                {
                    var service = serviceProvider.GetService<T>(model.ServiceId);
                    if (service is not null)
                    {
                        return (service, model);
                    }
                }
                else if (!string.IsNullOrEmpty(model.ModelId))
                {
                    var service = this.GetServiceByModelId<T>(serviceProvider, model.ModelId!);
                    if (service is not null)
                    {
                        return (service, model);
                    }
                }
                else
                {
                    // First request settings with empty or null service id is the default
                    defaultRequestSettings ??= model;
                }
            }

            if (defaultRequestSettings is not null)
            {
                var service = serviceProvider.GetService<T>(null);
                if (service is not null)
                {
                    return (service, defaultRequestSettings);
                }
            }
        }

        var names = string.Join("|", modelSettings.Select(model => model.ServiceId).ToArray());
        throw new SKException($"Service of type {typeof(T)} and name {names ?? "<NONE>"} not registered.");
    }

    private T? GetServiceByModelId<T>(IAIServiceProvider serviceProvider, string modelId) where T : IAIService
    {
        var services = serviceProvider.GetServices<T>();
        foreach (var service in services)
        {
            string? serviceModelId = service.GetModelId();
            if (!string.IsNullOrEmpty(serviceModelId) && serviceModelId == modelId)
            {
                return service;
            }
        }

        return default;
    }
}
