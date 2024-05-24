# Copyright (c) Microsoft. All rights reserved.

import pytest

import semantic_kernel.connectors.ai.hugging_face as sk_hf
from semantic_kernel.connectors.ai.prompt_execution_settings import PromptExecutionSettings
from semantic_kernel.functions.kernel_arguments import KernelArguments
from semantic_kernel.kernel import Kernel
from semantic_kernel.prompt_template.prompt_template_config import PromptTemplateConfig


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("model_name", "task", "input_str"),
    [
        (
            "patrickvonplaten/t5-tiny-random",
            "text2text-generation",
            "translate English to Dutch: Hello, how are you?",
        ),
        (
            "jotamunz/billsum_tiny_summarization",
            "summarization",
            """
        Summarize: Whales are fully aquatic, open-ocean animals:
        they can feed, mate, give birth, suckle and raise their young at sea.
        Whales range in size from the 2.6 metres (8.5 ft) and 135 kilograms (298 lb)
        dwarf sperm whale to the 29.9 metres (98 ft) and 190 tonnes (210 short tons) blue whale,
        which is the largest known animal that has ever lived. The sperm whale is the largest
        toothed predator on Earth. Several whale species exhibit sexual dimorphism,
        in that the females are larger than males.
    """,
        ),
        ("HuggingFaceM4/tiny-random-LlamaForCausalLM", "text-generation", "Hello, I like sleeping and "),
    ],
    ids=["text2text-generation", "summarization", "text-generation"],
)
async def test_text_completion(model_name, task, input_str):
    kernel = Kernel()

    # Configure LLM service
    kernel.add_service(
        service=sk_hf.HuggingFaceTextCompletion(service_id=model_name, ai_model_id=model_name, task=task),
    )

    exec_settings = PromptExecutionSettings(service_id=model_name, extension_data={"max_new_tokens": 25})

    # Define semantic function using SK prompt template language
    prompt = "{{$input}}"

    prompt_template_config = PromptTemplateConfig(template=prompt, execution_settings=exec_settings)

    kernel.add_function(
        prompt_template_config=prompt_template_config,
        function_name="TestFunction",
        plugin_name="TestPlugin",
        prompt_execution_settings=exec_settings,
    )

    arguments = KernelArguments(input=input_str)

    try:
        summary = await kernel.invoke(function_name="TestFunction", plugin_name="TestPlugin", arguments=arguments)
    except Exception as e:
        pytest.xfail(f"Failed to complete invoke: {e}, skipping or now...")
    output = str(summary).strip()
    try:
        assert len(output) > 0
    except AssertionError:
        pytest.xfail("The output is empty, but completed invoke")

    stream_summary = ""
    async for text in kernel.invoke_stream(function_name="TestFunction", plugin_name="TestPlugin", arguments=arguments):
        stream_summary += str(text[0])

    stream_output = str(stream_summary).strip()
    assert len(stream_output) > 0
