Act as a Senior .NET/C# Architect. I need to design a C# console benchmark application to evaluate the accuracy and performance of a local Speech-to-Text pipeline. This benchmark will be used to optimize my main cross-platform application (Parlotype).

Core STT Engine: Whisper.net (wrapper for whisper.cpp).

Develop a detailed implementation plan and foundational architecture with the following requirements:

1. PIPELINE PARAMETERS (Configuration):
    - Model selection: Whisper model sizes (tiny, base, small, etc.).
    - Hardware Runtime: CPU, CUDA, Vulkan, CoreML.
    - VAD Settings: Sliding window size, silence threshold.
    - Whisper Parameters: Beam size, Temperature fallback.
    - Audio Pre-processing: Toggle for noise reduction/normalization.

2. INPUT DATA (Dataset):
    - A directory containing test .wav files (16kHz, 16-bit, mono).
    - Ground truth text files (.txt or .json) for each audio sample.

3. METRICS TO COLLECT:
    - Word Error Rate (WER) and Character Error Rate (CER).
    - Model initialization time.
    - Real-Time Factor (RTF).
    - Peak RAM and VRAM consumption during inference.

4. OUTPUT & REPORTING:
    - Export benchmark results into a structured format (CSV or JSON) to easily compare different pipeline configurations.

Please provide:
1. The software architecture (key interfaces and classes, e.g., IAudioProcessor, IMetricsCalculator).
2. Nuget package recommendations or algorithms for calculating WER/CER in .NET.
3. A sample JSON schema for the run configuration and the final output metrics.
