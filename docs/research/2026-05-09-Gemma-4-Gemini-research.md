# **Comprehensive Analysis of Gemma-4 Deployment Frameworks for ASR and Local Reasoning Integration**

The release of the Gemma-4 family (E2B, E4B, 26B-A4B, and 31B) in April 2026 has introduced a trimodal native architecture that fundamentally changes how local Automatic Speech Recognition (ASR) is implemented1. For developers of applications like **Parlotype**, the key shift is the move away from separate adapter modules (like Whisper) toward a native audio conformer encoder integrated directly into the LLM4. This allows for a unified "thinking" and "transcribing" pipeline where the model can reason over the audio context as it generates text5.

## **Architectural Foundations for ASR**

The native audio capability is specific to the **E2B** and **E4B** variants3. These models utilize a 40ms frame duration, which is 50% smaller than previous generations, allowing for significantly lower latency during live speech recognition5. Integrating these into a local workflow requires specific audio preprocessing:

* **Sample Rate:** 16kHz8.
* **Format:** Mono-channel, 32-bit float waveforms9.
* **Segment Limit:** Max duration of \~30 seconds due to the audio encoder's context window8.

## **API Analysis: Integration for Parlotype**

Most modern local inference servers provide an **OpenAI-compatible API**, which simplifies the migration from cloud services to local hosting10. However, for high-performance ASR, specialized endpoints are often required.

### **1\. OpenAI-Compatible APIs (REST)**

Applications like **vLLM**, **Docker Model Runner (DMR)**, **Ollama**, and **LM Studio** expose standard endpoints on localhost2.

* **How to use it:** You typically send a POST request to /v1/chat/completions. For Gemma-4 multimodal models, the messages array includes an input\_audio content block4.
    * **Endpoint:** http://localhost:8000/v1/chat/completions (port varies by app).
    * **Payload Example:** The audio file is base64-encoded and passed in the input\_audio object alongside the transcription prompt4.
* **Limitations in llama.cpp:** As of May 2026, llama-server (the REST component of llama.cpp) has been reported to return HTTP 500 errors for input\_audio blocks due to a gap in its dispatch logic4. While the underlying library (libmtmd) is functional, the REST API for ASR is currently unstable in this specific engine4.

### **2\. Custom and Real-time APIs (WebSocket)**

For real-time transcription in Parlotype, REST APIs can introduce "chunking" latency. Several applications provide WebSocket interfaces for streaming.

* **Lemonade Server:** Provides a specialized WS /realtime endpoint17. It supports OpenAI-compatible streaming audio-to-text with built-in Voice Activity Detection (VAD), making it highly suitable for live interfaces18.
* **SGLang:** Recently proposed a WS /v1/audio/transcriptions/stream endpoint specifically for real-time meeting transcription19. It buffers raw PCM16 frames and emits transcript deltas as they are processed19.
* **Ollama Native API:** Uses its own /api/chat endpoint which can accept audio data in an images or audio field within the JSON payload8.

## **CLI Support for ASR**

For batch processing or headless automation in Parlotype workflows, CLI tools offer a direct path to the inference engine.

* **llama-mtmd-cli:** This is the most reliable way to use llama.cpp for Gemma-4 ASR currently, as it bypasses the unstable server dispatch logic4. It requires the \--audio flag followed by the path to a 16kHz WAV file4.
* **Ollama CLI:** Supports direct audio transcription via ollama run gemma4:e4b "transcribe ./audio.wav"8.
* **Lemonade CLI:** Uses the lemonade run \[model\] \--audio \[path\] command21. It is optimized for AMD Ryzen AI NPUs, allowing ASR to run with very low CPU overhead23.
* **Docker Model Runner:** Allows running models via docker model run ai/gemma4:E4B, though it is primarily designed to serve the API rather than process local files through a persistent CLI session12.

## **Hardware Backend Analysis**

| Backend | Best Application Support | Performance Context |
| :---- | :---- | :---- |
| **CUDA** | vLLM, DMR, Ollama | Standard for NVIDIA; highest throughput for 31B/26B models13. |
| **Vulkan** | llama.cpp, Lemonade | Excellent for AMD RDNA3; often outperforms ROCm in token generation27. |
| **Metal** | MLX LM, LM Studio | Native to Apple Silicon; maximizes unified memory for large context29. |
| **ROCm** | SGLang, vLLM | Target for AMD Instinct/Radeon; current llama.cpp version is a CUDA port23. |
| **XDNA 2 (NPU)** | Lemonade | Specialized for AMD Ryzen AI; ideal for offloading ASR from the GPU23. |

## **Integration Summary for Parlotype**

For your application **Parlotype**, the recommended integration paths depend on the target hardware:

1. **For AMD Ryzen AI (PC):** Use **Lemonade**. Its WebSocket API and native NPU support provide the most efficient real-time transcription with the lowest system impact18.
2. **For High-Throughput Servers:** Use **vLLM** or **SGLang**. These provide robust OpenAI-compatible APIs and speculative decoding to reduce latency during "thinking" steps13.
3. **For Cross-Platform Desktop:** **Ollama** is the most accessible, providing a stable REST API that handles the complex chat templates for "Thinking Mode" automatically2.
4. **For Manual Batch Processing:** **llama-mtmd-cli** provides the most control over model parameters and hardware offloading without the overhead of a server process4.

### **Summary Table of ASR Applications**

| Application | Integration Method | API Type | Audio Requirement | Gemma-4 Status |
| :---- | :---- | :---- | :---- | :---- |
| **vLLM** | API/Docker | OpenAI REST | Native (E2B/E4B) | Production Ready13 |
| **Ollama** | CLI/API | OpenAI \+ Native | 16kHz WAV, \<30s | Day-Zero Support2 |
| **Lemonade** | CLI/API/WS | OpenAI \+ WebSocket | PCM16/NPU-Optimized | Best for AMD11 |
| **SGLang** | API/WS | OpenAI \+ WebSocket | Streaming PCM16 | Pro/High-Throughput19 |
| **llama.cpp** | CLI (only) | Subprocess (CLI) | GGUF \+ mmproj | REST API Unstable4 |
| **LM Studio** | GUI/API | OpenAI REST | Via Local Server | GUI-Friendly36 |
| **DMR** | CLI/API | OpenAI REST | Engine Dependent | Containerized/DevOps10 |
| **Unsloth Studio** | Web/API | Anthropic/OpenAI | 16-bit GGUF | Best for Fine-Tuning38 |

#### **Works cited**

1. The 2.3B AI Model that "Thinks" like a 70B (Gemma 4), [https://www.youtube.com/watch?v=ZxQ2DuejRhU](https://www.youtube.com/watch?v=ZxQ2DuejRhU)
2. Google Gemma 4: Best Open AI Model in 2026?, [https://www.buildfastwithai.com/blogs/google-gemma-4-open-model](https://www.buildfastwithai.com/blogs/google-gemma-4-open-model)
3. Gemma 4: Our most capable open models to date \- Google Blog, [https://blog.google/innovation-and-ai/technology/developers-tools/gemma-4/](https://blog.google/innovation-and-ai/technology/developers-tools/gemma-4/)
4. server: add input\_audio content type routing for Gemma 4 audio inference · Issue \#21868 · ggml-org/llama.cpp \- GitHub, [https://github.com/ggml-org/llama.cpp/issues/21868](https://github.com/ggml-org/llama.cpp/issues/21868)
5. What Is Gemma 4's Audio Encoder? How the E2B and E4B Models Handle Speech Recognition | MindStudio, [https://www.mindstudio.ai/blog/gemma-4-audio-encoder-e2b-e4b-speech-recognition](https://www.mindstudio.ai/blog/gemma-4-audio-encoder-e2b-e4b-speech-recognition)
6. What Is Gemma 4? Google's Apache 2.0 Open-Weight Model With Native Audio and Vision, [https://www.mindstudio.ai/blog/what-is-gemma-4-google-apache-open-weight-model](https://www.mindstudio.ai/blog/what-is-gemma-4-google-apache-open-weight-model)
7. Gemma 4 \- LM Studio, [https://lmstudio.ai/models/gemma-4](https://lmstudio.ai/models/gemma-4)
8. Any documentation for Audio? · Issue \#15427 · ollama/ollama \- GitHub, [https://github.com/ollama/ollama/issues/15427](https://github.com/ollama/ollama/issues/15427)
9. Audio understanding | Gemma | Google AI for Developers, [https://ai.google.dev/gemma/docs/capabilities/audio](https://ai.google.dev/gemma/docs/capabilities/audio)
10. Docker Model Runner Guide: Run LLMs with Docker 2026 | Local AI Master, [https://localaimaster.com/blog/docker-model-runner-guide](https://localaimaster.com/blog/docker-model-runner-guide)
11. Lemonade: Local AI for Text, Images, and Speech, [https://lemonade-server.ai/](https://lemonade-server.ai/)
12. Running an AI Agent Locally: ADK, Gemma 4, and Docker Model Runner \- Medium, [https://medium.com/google-cloud/running-an-ai-agent-locally-adk-gemma-4-and-docker-model-runner-95ca9e6f506d](https://medium.com/google-cloud/running-an-ai-agent-locally-adk-gemma-4-and-docker-model-runner-95ca9e6f506d)
13. Gemma 4 Usage Guide \- vLLM Recipes, [https://docs.vllm.ai/projects/recipes/en/latest/Google/Gemma4.html](https://docs.vllm.ai/projects/recipes/en/latest/Google/Gemma4.html)
14. Run Gemma 4 on Intel® Xeon® Out-Of-the-Box \- Hugging Face, [https://huggingface.co/blog/MatrixYao/xeon](https://huggingface.co/blog/MatrixYao/xeon)
15. How to input audio to Gemma 4 E4B? · ggml-org llama.cpp · Discussion \#21334 \- GitHub, [https://github.com/ggml-org/llama.cpp/discussions/21334](https://github.com/ggml-org/llama.cpp/discussions/21334)
16. (Planning) support Voxtral Mini 4B realtime ASR · Issue \#20914 · ggml-org/llama.cpp, [https://github.com/ggml-org/llama.cpp/issues/20914](https://github.com/ggml-org/llama.cpp/issues/20914)
17. OpenAI-Compatible API \- Lemonade Server Documentation, [https://lemonade-server.ai/docs/api/openai/](https://lemonade-server.ai/docs/api/openai/)
18. Add streaming transcription via Lemonade WebSocket realtime API · Issue \#372 · amd/gaia, [https://github.com/amd/gaia/issues/372](https://github.com/amd/gaia/issues/372)
19. \[RFC\]: Real-Time Streaming Audio Input for ASR Models · Issue \#22474 · sgl-project/sglang, [https://github.com/sgl-project/sglang/issues/22474](https://github.com/sgl-project/sglang/issues/22474)
20. Gemma 4 Tutorial: Build a Local AI Coding Agent with Gradio and Ollama \- DataCamp, [https://www.datacamp.com/de/tutorial/gemma-4-tutorial](https://www.datacamp.com/de/tutorial/gemma-4-tutorial)
21. lemonade-sdk/lemonade: Lemonade helps users discover and run local AI apps by serving optimized LLMs right from their own GPUs and NPUs. Join our discord: https://discord.gg/5xXzkMu8Zk · GitHub \- GitHub, [https://github.com/lemonade-sdk/lemonade](https://github.com/lemonade-sdk/lemonade)
22. Lemonade CLI Guide, [https://lemonade-server.ai/docs/lemonade-cli/](https://lemonade-server.ai/docs/lemonade-cli/)
23. Day 0 Support for Gemma 4 on AMD Processors and GPUs, [https://www.amd.com/en/developer/resources/technical-articles/2026/day-0-support-for-gemma-4-on-amd-processors-and-gpus.html](https://www.amd.com/en/developer/resources/technical-articles/2026/day-0-support-for-gemma-4-on-amd-processors-and-gpus.html)
24. Lemonade by AMD: A Unified API for Local AI Developers, [https://www.amd.com/en/developer/resources/technical-articles/2026/lemonade-for-local-ai.html](https://www.amd.com/en/developer/resources/technical-articles/2026/lemonade-for-local-ai.html)
25. Docker Model Runner: A beginner's guide to running open models on your own machine \[Part 1\] \- Geshan Manandhar, [https://geshan.com.np/blog/2026/01/docker-model-runner/](https://geshan.com.np/blog/2026/01/docker-model-runner/)
26. Run Gemma 4 with Red Hat AI on Day 0: A step-by-step guide, [https://developers.redhat.com/articles/2026/04/02/run-gemma-4-red-hat-ai-day-0-step-step-guide](https://developers.redhat.com/articles/2026/04/02/run-gemma-4-red-hat-ai-day-0-step-step-guide)
27. Vulkan backend outperforms ROCm on Strix Halo (gfx1151) — llama.cpp benchmark : r/LocalLLaMA \- Reddit, [https://www.reddit.com/r/LocalLLaMA/comments/1t4fkri/vulkan\_backend\_outperforms\_rocm\_on\_strix\_halo/](https://www.reddit.com/r/LocalLLaMA/comments/1t4fkri/vulkan_backend_outperforms_rocm_on_strix_halo/)
28. \~21 tok/s Gemma 4 on a Ryzen mini PC: llama.cpp, Vulkan, and the messy truth about local chat \- DEV Community, [https://dev.to/hrodrig/21-toks-gemma-4-on-a-ryzen-mini-pc-llamacpp-vulkan-and-the-messy-truth-about-local-chat-m82](https://dev.to/hrodrig/21-toks-gemma-4-on-a-ryzen-mini-pc-llamacpp-vulkan-and-the-messy-truth-about-local-chat-m82)
29. Gemma 4 \- How to Run Locally | Unsloth Documentation, [https://unsloth.ai/docs/models/gemma-4](https://unsloth.ai/docs/models/gemma-4)
30. Run Gemma with LM Studio | Google AI for Developers, [https://ai.google.dev/gemma/docs/integrations/lmstudio](https://ai.google.dev/gemma/docs/integrations/lmstudio)
31. Ollama/Gemma4 is completely useless for OpenClaw. Here's why, [https://www.reddit.com/r/openclaw/comments/1sb3ezf/ollamagemma4\_is\_completely\_useless\_for\_openclaw/](https://www.reddit.com/r/openclaw/comments/1sb3ezf/ollamagemma4_is_completely_useless_for_openclaw/)
32. lemonade/AGENTS.md at main \- GitHub, [https://github.com/lemonade-sdk/lemonade/blob/main/AGENTS.md](https://github.com/lemonade-sdk/lemonade/blob/main/AGENTS.md)
33. google/gemma-4-31b \- LM Studio, [https://lmstudio.ai/models/google/gemma-4-31b](https://lmstudio.ai/models/google/gemma-4-31b)
34. Gemma 4 \- SGLang Documentation, [https://lmsysorg.mintlify.app/cookbook/autoregressive/Google/Gemma4](https://lmsysorg.mintlify.app/cookbook/autoregressive/Google/Gemma4)
35. gemma4 \- Ollama, [https://ollama.com/library/gemma4](https://ollama.com/library/gemma4)
36. gemma4:e4b-it-q4\_K\_M \- Ollama, [https://ollama.com/library/gemma4:e4b-it-q4\_K\_M](https://ollama.com/library/gemma4:e4b-it-q4_K_M)
37. How to run a local coding agent with Gemma 4 and Pi \- Patrick Loeber, [https://patloeber.com/gemma-4-pi-agent/](https://patloeber.com/gemma-4-pi-agent/)
38. How to use Unsloth as an API endpoint, [https://unsloth.ai/docs/basics/api](https://unsloth.ai/docs/basics/api)
39. Introducing Unsloth Studio, [https://unsloth.ai/docs/new/studio](https://unsloth.ai/docs/new/studio)