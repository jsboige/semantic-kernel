# Copyright (c) Microsoft. All rights reserved.
import os

import pytest
from openai import AsyncOpenAI
from test_utils import retry

import semantic_kernel.connectors.ai.open_ai as sk_oai
from semantic_kernel.connectors.ai.function_call_behavior import FunctionCallBehavior
from semantic_kernel.connectors.ai.open_ai.settings.open_ai_settings import OpenAISettings
from semantic_kernel.connectors.ai.prompt_execution_settings import PromptExecutionSettings
from semantic_kernel.contents.chat_history import ChatHistory
from semantic_kernel.core_plugins.math_plugin import MathPlugin
from semantic_kernel.prompt_template.prompt_template_config import PromptTemplateConfig


@pytest.mark.asyncio
async def test_oai_chat_service_with_plugins(setup_tldr_function_for_oai_models):
    kernel, prompt, text_to_summarize = setup_tldr_function_for_oai_models

    kernel.add_service(
        sk_oai.OpenAIChatCompletion(service_id="chat-gpt", ai_model_id="gpt-3.5-turbo"),
    )

    exec_settings = PromptExecutionSettings(
        service_id="chat-gpt", extension_data={"max_tokens": 200, "temperature": 0, "top_p": 0.5}
    )

    prompt_template_config = PromptTemplateConfig(
        template=prompt, description="Write a short story.", execution_settings=exec_settings
    )

    # Create the semantic function
    tldr_function = kernel.add_function(
        function_name="story", plugin_name="plugin", prompt_template_config=prompt_template_config
    )

    summary = await retry(lambda: kernel.invoke(tldr_function, input=text_to_summarize))
    output = str(summary).strip()
    print(f"TLDR using input string: '{output}'")
    assert "First Law" not in output and ("human" in output or "Human" in output or "preserve" in output)
    assert len(output) < 100


@pytest.mark.asyncio
async def test_oai_chat_service_with_tool_call(setup_tldr_function_for_oai_models):
    kernel, _, _ = setup_tldr_function_for_oai_models

    kernel.add_service(
        sk_oai.OpenAIChatCompletion(
            service_id="chat-gpt",
            ai_model_id="gpt-3.5-turbo-1106",
        ),
    )

    kernel.add_plugin(MathPlugin(), plugin_name="math")

    execution_settings = sk_oai.OpenAIChatPromptExecutionSettings(
        service_id="chat-gpt",
        max_tokens=2000,
        temperature=0.7,
        top_p=0.8,
        function_call_behavior=FunctionCallBehavior.EnableFunctions(
            auto_invoke=True, filters={"excluded_plugins": ["ChatBot"]}
        ),
    )

    prompt_template_config = PromptTemplateConfig(
        template="{{$input}}", description="Do math.", execution_settings=execution_settings
    )

    # Create the prompt function
    tldr_function = kernel.add_function(
        function_name="math_fun", plugin_name="math_int_test", prompt_template_config=prompt_template_config
    )

    summary = await retry(lambda: kernel.invoke(tldr_function, input="what is 1+1?"))
    output = str(summary).strip()
    print(f"Math output: '{output}'")
    assert "2" in output
    assert 0 < len(output) < 100


@pytest.mark.asyncio
async def test_oai_chat_service_with_tool_call_streaming(setup_tldr_function_for_oai_models):
    kernel, _, _ = setup_tldr_function_for_oai_models

    kernel.add_service(
        sk_oai.OpenAIChatCompletion(
            service_id="chat-gpt",
            ai_model_id="gpt-3.5-turbo-1106",
        ),
    )

    kernel.add_plugin(MathPlugin(), plugin_name="math")

    execution_settings = sk_oai.OpenAIChatPromptExecutionSettings(
        service_id="chat-gpt",
        max_tokens=2000,
        temperature=0.7,
        top_p=0.8,
        function_call_behavior=FunctionCallBehavior.EnableFunctions(
            auto_invoke=True, filters={"excluded_plugins": ["ChatBot"]}
        ),
    )

    prompt_template_config = PromptTemplateConfig(
        template="{{$input}}", description="Do math.", execution_settings=execution_settings
    )

    # Create the prompt function
    tldr_function = kernel.add_function(
        function_name="math_fun", plugin_name="math_int_test", prompt_template_config=prompt_template_config
    )

    result = None
    async for message in kernel.invoke_stream(tldr_function, input="what is 101+102?"):
        result = message[0] if not result else result + message[0]
    output = str(result)

    print(f"Math output: '{output}'")
    assert "2" in output
    assert 0 < len(output) < 100


@pytest.mark.asyncio
async def test_oai_chat_service_with_plugins_with_provided_client(setup_tldr_function_for_oai_models):
    kernel, prompt, text_to_summarize = setup_tldr_function_for_oai_models

    openai_settings = OpenAISettings.create()
    api_key = openai_settings.api_key.get_secret_value()
    org_id = openai_settings.org_id

    client = AsyncOpenAI(
        api_key=api_key,
        organization=org_id,
    )

    kernel.add_service(
        sk_oai.OpenAIChatCompletion(
            service_id="chat-gpt",
            ai_model_id="gpt-3.5-turbo",
            async_client=client,
        ),
        overwrite=True,  # Overwrite the service if it already exists since add service says it does
    )

    exec_settings = PromptExecutionSettings(
        service_id="chat-gpt", extension_data={"max_tokens": 200, "temperature": 0, "top_p": 0.5}
    )

    prompt_template_config = PromptTemplateConfig(
        template=prompt, description="Write a short story.", execution_settings=exec_settings
    )

    # Create the semantic function
    tldr_function = kernel.add_function(
        function_name="story",
        plugin_name="story_plugin",
        prompt_template_config=prompt_template_config,
    )

    summary = await retry(lambda: kernel.invoke(tldr_function, input=text_to_summarize))
    output = str(summary).strip()
    print(f"TLDR using input string: '{output}'")
    assert "First Law" not in output and ("human" in output or "Human" in output or "preserve" in output)
    assert len(output) < 100


@pytest.mark.asyncio
async def test_azure_oai_chat_stream_service_with_plugins(setup_tldr_function_for_oai_models):
    kernel, prompt, text_to_summarize = setup_tldr_function_for_oai_models

    # Configure LLM service
    kernel.add_service(
        sk_oai.AzureChatCompletion(
            service_id="chat_completion",
        ),
        overwrite=True,
    )

    exec_settings = PromptExecutionSettings(
        service_id="chat_completion", extension_data={"max_tokens": 200, "temperature": 0, "top_p": 0.5}
    )

    prompt_template_config = PromptTemplateConfig(
        template=prompt, description="Write a short story.", execution_settings=exec_settings
    )

    # Create the prompt function
    tldr_function = kernel.add_function(
        function_name="story",
        plugin_name="story_plugin",
        prompt_template_config=prompt_template_config,
    )

    result = None
    async for message in kernel.invoke_stream(tldr_function, input=text_to_summarize):
        result = message[0] if not result else result + message[0]
    output = str(result)

    print(f"TLDR using input string: '{output}'")
    # assert "First Law" not in output and ("human" in output or "Human" in output or "preserve" in output)
    assert 0 < len(output) < 100


@pytest.mark.asyncio
async def test_oai_chat_service_with_yaml_jinja2(setup_tldr_function_for_oai_models):
    kernel, _, _ = setup_tldr_function_for_oai_models

    openai_settings = OpenAISettings.create()
    api_key = openai_settings.api_key.get_secret_value()
    org_id = openai_settings.org_id

    client = AsyncOpenAI(
        api_key=api_key,
        organization=org_id,
    )

    kernel.add_service(
        sk_oai.OpenAIChatCompletion(
            service_id="chat-gpt",
            ai_model_id="gpt-3.5-turbo",
            async_client=client,
        ),
        overwrite=True,  # Overwrite the service if it already exists since add service says it does
    )

    plugins_directory = os.path.join(os.path.dirname(__file__), "../../assets/test_plugins")

    plugin = kernel.add_plugin(parent_directory=plugins_directory, plugin_name="TestFunctionYamlJinja2")
    assert plugin is not None
    assert plugin["TestFunctionJinja2"] is not None

    chat_history = ChatHistory()
    chat_history.add_system_message("Assistant is a large language model")
    chat_history.add_user_message("I love parrots.")

    result = await kernel.invoke(plugin["TestFunctionJinja2"], chat_history=chat_history)
    assert result is not None
    assert len(str(result.value)) > 0


@pytest.mark.asyncio
async def test_oai_chat_service_with_yaml_handlebars(setup_tldr_function_for_oai_models):
    kernel, _, _ = setup_tldr_function_for_oai_models

    openai_settings = OpenAISettings.create()
    api_key = openai_settings.api_key.get_secret_value()
    org_id = openai_settings.org_id

    client = AsyncOpenAI(
        api_key=api_key,
        organization=org_id,
    )

    kernel.add_service(
        sk_oai.OpenAIChatCompletion(
            service_id="chat-gpt",
            ai_model_id="gpt-3.5-turbo",
            async_client=client,
        ),
        overwrite=True,  # Overwrite the service if it already exists since add service says it does
    )

    plugins_directory = os.path.join(os.path.dirname(__file__), "../../assets/test_plugins")

    plugin = kernel.add_plugin(parent_directory=plugins_directory, plugin_name="TestFunctionYamlHandlebars")
    assert plugin is not None
    assert plugin["TestFunctionHandlebars"] is not None

    chat_history = ChatHistory()
    chat_history.add_system_message("Assistant is a large language model")
    chat_history.add_user_message("I love parrots.")

    result = await kernel.invoke(plugin["TestFunctionHandlebars"], chat_history=chat_history)
    assert result is not None
    assert len(str(result.value)) > 0
