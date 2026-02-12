  
**Local Voice-To-Text Application**

Research & Analysis Report

February 2026

*Implementation Options • Competitive Landscape • Strategic Recommendations*

# **1\. Executive Summary**

The offline voice-to-text market is experiencing rapid growth, driven by privacy concerns and the maturation of on-device AI models. The landscape is dominated by **Apple-only products** (Superwhisper, VoiceInk, MacWhisper, Wispr Flow) with virtually **no serious cross-platform, privacy-first competitor** that covers both Windows and macOS. This represents a significant market gap.

The recommended core engine is **OpenAI Whisper via whisper.cpp**, which delivers state-of-the-art accuracy across 100+ languages, runs fully offline, and has excellent .NET bindings through the Whisper.net NuGet package. For the application framework, **.NET MAUI with Blazor Hybrid** is a strong fit given your C\# expertise and the need for Windows \+ macOS support, though Tauri is a compelling alternative if Linux support or minimal app size matters.

The planned LLM post-processing feature (via local Ollama integration) would be a powerful differentiator, as most competitors either skip this entirely or require cloud APIs.

# **2\. Speech-To-Text Engine Comparison**

These are the viable engines for local, offline speech recognition in a desktop application:

| Engine | License | Languages | Model Size | C\#/.NET | Best For |
| :---- | :---- | :---- | :---- | :---- | :---- |
| Whisper.cpp via Whisper.net | MIT | 100+ | 75MB–1.5GB | **Native NuGet** | Best accuracy, GPU acceleration |
| Vosk | Apache 2.0 | 20+ | 50MB– 1.8GB | C\# bindings | Lightweight, streaming, embedded |
| Picovoice (Cheetah/Leopard) | Commercial | 8+ | \<40MB | C\# SDK | Ultra-low latency, edge devices |
| Apple Speech (on-device) | Platform | 60+ | Built-in | Swift only | macOS/iOS native, zero setup |
| Windows Speech | Platform | 20+ | Built-in | **System.Speech** | Windows native, zero setup |

## **Recommendation: Whisper.cpp via Whisper.net**

**Whisper.net** (github.com/sandrohanea/whisper.net) is the clear winner for your stack. It provides native .NET bindings around whisper.cpp with NuGet packages for every hardware backend:

* **Whisper.net.Runtime.Cuda** – NVIDIA GPU acceleration (CUDA 12/13)

* **Whisper.net.Runtime.CoreML** – Apple Silicon acceleration

* **Whisper.net.Runtime.Vulkan** – Windows GPU via Vulkan

* **Whisper.net.Runtime** – CPU fallback for all platforms

The runtime is automatically selected based on the platform and available hardware. Models range from tiny (\~75MB, fast, lower accuracy) to large (\~1.5GB, slower, highest accuracy). The base model (\~140MB) provides an excellent speed/accuracy tradeoff for real-time dictation.

### **How Whisper Compares to Vosk**

Vosk is lighter (50MB models) and better for embedded/IoT devices, but **Whisper significantly outperforms Vosk in accuracy**, especially for natural speech with varied accents. Vosk is a good fallback for extremely resource-constrained environments, but for a desktop app, Whisper is the standard.

# **3\. Competitive Landscape**

The local voice-to-text market has exploded in 2024–2025, but a clear pattern emerges:

| Product | Platforms | Offline | AI Post-process | Open Source | Pricing | Weakness |
| :---- | :---- | :---- | :---- | :---- | :---- | :---- |
| Superwhisper | macOS, iOS | Yes | Yes (cloud+local) | No | $8.49/mo or $250 lifetime | Apple only, complex settings |
| Wispr Flow | macOS | No (cloud) | Yes (cloud) | No | $15/mo | Apple only, privacy concerns |
| VoiceInk | macOS | Yes | Yes (BYO API key) | Yes | Free / OSS | Apple only, less polished UI |
| MacWhisper | macOS | Yes | Basic | No | $29–$80 one-time | Apple only, dictation is basic |
| Handy | Win, Mac, Linux | Yes | No | Yes (Tauri) | Free / OSS | Minimal features, new project |
| Dragon Professional | Windows | Yes | No (legacy) | No | $500+ one-time | Windows only, expensive, aging |
| SpeechPulse | Win, Mac | Yes | Yes | No | Subscription | Small user base |
| Aqua Voice | Win, Mac | No (cloud) | Yes (cloud) | No | Subscription | Cloud-dependent, privacy concerns |
| Voice Type | macOS | Yes | BYO key | No | $20 one-time | Apple only |
| Built-in OS dictation | Win, Mac | Partial | No | N/A | Free | Limited accuracy, no AI processing |

## **Key Market Insights**

* **The market is overwhelmingly macOS-only.** Superwhisper, VoiceInk, MacWhisper, Wispr Flow, Voice Type, and Willow are all Apple-exclusive. Windows users have almost no quality options beyond Dragon (expensive, aging) and built-in Windows dictation.

* **Cross-platform is underserved.** Only Handy (Tauri-based, open source) and SpeechPulse attempt true cross-platform. Handy is minimal; SpeechPulse has a small user base.

* **AI post-processing is the new battleground.** Apps like Superwhisper and Wispr Flow differentiate through AI modes that clean up, format, and adapt transcribed text. Most rely on cloud LLMs (OpenAI, Anthropic). Fully local AI processing is rare.

* **Pricing converges on three models:** free/OSS (VoiceInk, Handy), one-time purchase ($20–$80), or subscription ($8–$15/mo). Lifetime licenses ($250) exist but are controversial.

* **Privacy is the \#1 marketing angle.** Every successful product leads with the privacy pitch. Users are increasingly aware that voice data is permanent biometric data.

## **Your Competitive Advantage**

A cross-platform (Windows \+ macOS), fully offline, free app with local LLM post-processing would occupy a unique position in the market. No current product combines all four of these attributes.

# **4\. Application Framework Comparison**

| Criteria | .NET MAUI \+ Blazor | Tauri (Rust \+ Web) | Native per Platform | Electron |
| :---- | :---- | :---- | :---- | :---- |
| **Platforms** | Win, Mac, iOS, Android | Win, Mac, Linux | Win \+ Mac (separate codebases) | Win, Mac, Linux |
| **App Size** | \~50–100MB | \~2–10MB | Minimal | \~150–300MB |
| **Performance** | Good (native rendering) | Excellent (Rust \+ system WebView) | Best (fully native) | Heavy (bundled Chromium) |
| **Whisper.net Integration** | **Excellent** – native NuGet | Good – via Rust FFI to whisper.cpp | Varies per platform | Good – via node bindings |
| **Learning Curve** | Moderate (C\# \+ XAML/Blazor) | Steep (Rust \+ JS) | High (2 codebases) | Low (JS ecosystem) |
| **Linux Support** | No (roadmap unclear) | **Yes** | Would need 3rd codebase | **Yes** |
| **Mobile Support** | **Yes** (iOS \+ Android) | Experimental | Separate projects | No |
| **Maturity** | Medium (MS-backed, some gaps) | Growing fast (120k GH stars) | Proven | Mature (100k+ GH stars) |

## **Option A: .NET MAUI \+ Blazor Hybrid (Recommended)**

This aligns with your C\# preference and provides the richest integration path:

* **Whisper.net** integrates natively via NuGet – no FFI or interop wrappers needed

* Blazor Hybrid gives you a web-style UI (HTML/CSS/JS) inside a native shell

* Audio capture via NAudio (Windows) and platform-specific APIs (macOS)

* Future mobile expansion (iOS/Android) uses the same codebase

* **Ollama integration** for local LLM is straightforward via HTTP API from C\#

*Trade-off:* No Linux support. Larger app size (\~50–100MB). MAUI has some rough edges and limited documentation compared to more mature frameworks.

## **Option B: Tauri (Rust \+ Web Frontend)**

If you value tiny app size, Linux support, or want to learn Rust:

* Handy (existing OSS project) proves Tauri \+ whisper.cpp works well for this exact use case

* App size as low as 2–10MB (excluding models)

* Direct integration with whisper.cpp via Rust FFI – no wrapper layers

* Linux, Windows, macOS from a single codebase

* Web frontend means you can reuse UI components for a marketing site

*Trade-off:* Requires learning Rust. No mobile support yet. Smaller ecosystem than .NET.

## **Option C: Native per Platform**

Maximum polish but maximum effort:

* **Windows:** WinUI 3 \+ Whisper.net. Best Windows experience, direct WinRT API access.

* **macOS:** SwiftUI \+ whisper.cpp (Swift bindings exist). Can also use Apple’s built-in Speech framework as fallback.

*Trade-off:* Two completely separate codebases. Double the maintenance. Makes sense only if you want platform-perfect UX and have the bandwidth.

# **5\. Local LLM Post-Processing Architecture**

Your planned LLM integration for text cleanup/formatting is one of the strongest differentiators you can build. Here is the recommended architecture:

## **Ollama as the LLM Backend**

**Ollama** (ollama.com) is the de facto standard for running local LLMs. It provides an OpenAI-compatible REST API on localhost:11434, supports all major models (Llama 3, Mistral, Phi, Gemma, Qwen), and handles GPU/CPU optimization automatically.

Integration approach:

* **Bundle or recommend Ollama** – your app can either bundle Ollama silently or prompt the user to install it. Pieces (a developer tool) recently switched to bundling Ollama for their local LLM features.

* **HTTP API from C\# or JS** – simple POST to localhost:11434/api/generate with the transcribed text and a system prompt for the desired transformation.

* **Preset modes** (inspired by Superwhisper): “Clean up”, “Formal email”, “Casual message”, “Translate to English”, “Summarize”, etc.

* **Custom modes** – let users write their own system prompts for domain-specific formatting.

## **Model Recommendations**

For text cleanup and reformatting, you don’t need large models. Recommended tiers:

1. **Fast (3B params):** Phi-3 Mini or Gemma 2B. Runs on any modern laptop. 2–3GB RAM.

2. **Balanced (7B params):** Llama 3.1 7B or Mistral 7B. Great quality. 8GB RAM recommended.

3. **High quality (13B+ params):** Llama 3.1 13B. Requires 16GB RAM and ideally a GPU.

# **6\. Topics You May Be Missing**

## **Audio Pipeline Challenges**

The hardest part of a voice-to-text app is not the STT engine—it’s the audio pipeline:

* **Voice Activity Detection (VAD):** Knowing when the user starts and stops speaking. Whisper.cpp has built-in VAD, but tuning silence thresholds for different environments (quiet room vs. coffee shop) is critical UX work.

* **Noise reduction and gain normalization:** Pre-processing audio before sending it to Whisper dramatically improves accuracy. Voice Type specifically highlights this as a key differentiator.

* **Audio format requirements:** Whisper requires 16-bit, 16kHz mono WAV. Your app needs to handle real-time resampling from whatever the microphone provides.

* **Streaming vs. batch:** Real-time dictation requires processing audio in sliding windows (\~30 second chunks) while the user speaks, then stitching results together. This is significantly harder than batch transcription of a finished recording.

* **Platform audio APIs differ significantly:** NAudio works on Windows; macOS needs AVFoundation or Core Audio. This is where cross-platform frameworks struggle the most.

## **System-Wide Input Method**

The most successful dictation apps work everywhere, not just inside their own window:

* **Global hotkey:** Press a keyboard shortcut (e.g., Fn key, Ctrl+Shift+Space) from any app to start dictation.

* **Text insertion:** After transcription, the text needs to be pasted/typed into the active application. This typically requires clipboard manipulation or simulating keystrokes via OS accessibility APIs.

* **Context awareness:** Superwhisper reads the current screen/selected text via accessibility APIs to give the LLM context about what you’re writing. This dramatically improves AI post-processing quality.

## **Installer, Updates, and Model Management**

* **First-run model download:** Whisper models are 75MB–1.5GB. You need a smooth first-run experience that downloads the chosen model (this requires internet once, then works offline forever).

* **Auto-update mechanism:** Tauri has built-in updaters. MAUI apps distributed via Microsoft Store get it for free. Otherwise, consider Squirrel, MSIX, or Sparkle (macOS).

* **Code signing:** macOS requires notarization; Windows requires code signing certificates. Without these, users get scary security warnings. Budget \~$100–300/year.

## **Distribution and Marketing**

* **App stores:** Mac App Store and Microsoft Store provide distribution and trust. Mac App Store takes 30% but handles notarization. Consider whether to publish there or distribute directly.

* **Static marketing site:** Azure CDN or Cloudflare Pages works well. Look at how superwhisper.com, whispernotes.app, and carelesswhisper.app present their products—these are good templates.

* **Open source strategy:** VoiceInk and Handy are open source and gained traction through GitHub visibility. Consider open-sourcing the core while keeping premium features proprietary.

* **SEO and content marketing:** The search space is active. Terms like “offline speech to text”, “private dictation app”, “local voice to text” drive traffic. A blog with comparison posts (like the ones in my research) is effective.

* **Product Hunt launch:** Almost every competitor in this space launched on Product Hunt. It’s essentially mandatory for indie developer tools.

## **Legal and Licensing**

* **Whisper model license:** OpenAI Whisper models are MIT-licensed. whisper.cpp is MIT. Whisper.net is MIT. You’re clear to use them commercially.

* **Ollama and LLM models:** Ollama is MIT. Most popular models (Llama 3, Mistral, Gemma) have permissive licenses but read them carefully—some have usage thresholds.

* **Privacy policy:** Since privacy is your main selling point, you need a clear, simple privacy policy that states no data leaves the device. This is a trust signal.

## **Accessibility**

Voice-to-text tools are critical accessibility technology. Consider RSI sufferers, people with motor disabilities, and visually impaired users. Building with accessibility in mind from day one can open partnerships with accessibility organizations and adds genuine social value to your product.

## **Monetization Ideas (If You Choose to Charge)**

* Free: basic transcription with the Whisper tiny/base model

* Paid: larger models, AI post-processing modes, custom modes, file transcription

* One-time purchase: most popular model in this space ($20–$30 sweet spot)

* Tip jar / sponsor model: if fully open source

* Bring-your-own API key: let users connect their own OpenAI/Anthropic keys for cloud AI features (no cost to you)

# **7\. Recommended Development Roadmap**

## **Phase 1: MVP (4–6 weeks)**

* System tray app with global hotkey to start/stop recording

* Whisper.net integration with the base model

* Transcribed text inserted at cursor position in any app

* Simple model selection (tiny, base, small)

* Windows \+ macOS builds

## **Phase 2: AI Post-Processing (2–4 weeks)**

* Ollama integration with preset modes (clean up, formal, casual, translate)

* Custom mode editor

* Settings UI for model management and preferences

## **Phase 3: Polish and Launch (2–3 weeks)**

* Marketing site (Azure CDN static site)

* Code signing and notarization

* Product Hunt launch

* GitHub repository (if open-sourcing)

* Blog post: building a privacy-first voice-to-text app

## **Phase 4: Advanced Features (Ongoing)**

* Multi-language translation

* File transcription (audio/video files)

* Speaker diarization

* Streaming real-time display (text appears as you speak)

* Context awareness via accessibility APIs

*— End of Report —*