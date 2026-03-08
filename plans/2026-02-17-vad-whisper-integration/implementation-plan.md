# VAD-Whisper Audio Pipeline Implementation Plan

## Problem
Implement the audio processing pipeline: Microphone → NAudio → SileroVAD → Whisper.Net → Transcription. This includes implementing the existing stub services (`WasapiAudioCaptureService`, `WhisperSpeechRecognizer`), adding a new VAD service, creating a pipeline orchestrator, and writing integration tests.

## Approach
- Add `IVadService` interface to Core and `SileroVadService` to Platform
- Add `IAudioPipeline` interface to Core and `AudioPipelineService` to Platform (orchestrates Mic→VAD→Whisper)
- Update `ISpeechRecognizer.TranscribeAsync` to accept `ReadOnlyMemory<byte>` (PCM data)
- Implement `WasapiAudioCaptureService` using NAudio WASAPI + WdlResampler for 16kHz mono output
- Implement `WhisperSpeechRecognizer` using Whisper.net with auto-download of models to AppData cache
- Implement `SileroVadService` using SileroVad NuGet package
- Pipeline supports both batch mode (send speech on silence) and streaming mode (fixed windows)
- Write unit tests using `one-small-step.wav` and Whisper tiny model

## Workplan

### Phase 1: Core Interface Updates
- [ ] Update `ISpeechRecognizer.TranscribeAsync` to accept `ReadOnlyMemory<byte>` instead of `ReadOnlyMemory<float>`
- [ ] Create `IVadService` interface in `Parlotype.Core/Audio/` — DetectSpeech, accepts float[], returns speech segments
- [ ] Create `VadSpeechSegment` record in `Parlotype.Core/Audio/` — StartSample, EndSample
- [ ] Create `IAudioPipeline` interface in `Parlotype.Core/Audio/` — StartAsync, StopAsync, TranscriptionAvailable event
- [ ] Create `TranscriptionEventArgs` in `Parlotype.Core/Speech/`
- [ ] Create `PipelineMode` enum in `Parlotype.Core/Audio/` — Batch, Streaming
- [ ] Git commit

### Phase 2: WasapiAudioCaptureService Implementation
- [ ] Implement `WasapiAudioCaptureService` using `WasapiCapture` from NAudio
- [ ] Add WdlResamplingSampleProvider for resampling to 16kHz mono 16-bit PCM
- [ ] Fire `DataAvailable` event with resampled PCM chunks
- [ ] Handle device enumeration (use default capture device)
- [ ] Implement `DisposeAsync` properly (dispose capture + resampler)
- [ ] Git commit

### Phase 3: SileroVadService Implementation
- [ ] Create `SileroVadService` implementing `IVadService` in `Parlotype.Platform/Audio/`
- [ ] Wrap `SileroVad.Vad` — accept float[] samples, call GetSpeechTimestamps
- [ ] Return `List<VadSpeechSegment>` with detected speech regions
- [ ] Implement IDisposable/IAsyncDisposable to dispose underlying Vad
- [ ] Git commit

### Phase 4: WhisperSpeechRecognizer Implementation
- [ ] Implement `InitializeAsync` — auto-download model via `WhisperGgmlDownloader` to AppData/parlotype/models cache
- [ ] Use `GgmlType.Base` by default, configurable
- [ ] Create `WhisperFactory` and `WhisperProcessor` with sensible defaults (language auto, greedy sampling)
- [ ] Implement `TranscribeAsync(ReadOnlyMemory<byte>)` — convert PCM bytes to float, feed to processor
- [ ] Aggregate SegmentData results into TranscriptionResult
- [ ] Implement `DisposeAsync` properly (dispose processor + factory)
- [ ] Git commit

### Phase 5: AudioPipelineService Implementation
- [ ] Create `AudioPipelineService` implementing `IAudioPipeline` in `Parlotype.Platform/Audio/`
- [ ] Wire: IAudioCaptureService.DataAvailable → accumulate samples → IVadService → ISpeechRecognizer → fire TranscriptionAvailable
- [ ] Implement batch mode: accumulate audio, on silence detected send speech segment to Whisper
- [ ] Implement streaming mode: fixed-size windows sent continuously to Whisper
- [ ] Support PipelineMode selection
- [ ] Register all new services in `PlatformServiceExtensions.cs`
- [ ] Git commit

### Phase 6: Unit Tests
- [ ] Add test resource handling — ensure `one-small-step.wav` is copied to output
- [ ] Write WhisperSpeechRecognizer test — load tiny model, transcribe wav, assert text contains expected words
- [ ] Write SileroVadService test — feed wav samples, verify speech segments detected
- [ ] Write AudioPipeline integration test — feed wav through VAD+Whisper, verify transcription output
- [ ] All tests use Whisper tiny model size
- [ ] Git commit

## Notes
- SileroVad expects 16kHz mono float32 samples
- Whisper.net ProcessAsync accepts both float[] and ReadOnlyMemory<float> directly (no WAV stream needed)
- Auto-download cache: `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)/parlotype/models/`
- SileroVad NuGet v1.3.0 already added to Platform project
- NAudio WdlResamplingSampleProvider handles resampling without Windows Media Foundation dependency
- Test WAV file: `src/Parlotype.Tests/resources/one-small-step.wav` (Neil Armstrong quote expected)
- Whisper tiny model for tests (fast), Base model for production default
