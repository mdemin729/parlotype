"""Gemma 4 speech recognition sidecar server for Parlotype benchmarks."""

import argparse
import logging
import sys
import time
from pathlib import Path

import numpy as np
import soundfile as sf
import torch
import uvicorn
from fastapi import FastAPI, Header, HTTPException
from pydantic import BaseModel
from transformers import AutoProcessor, Gemma4ForConditionalGeneration

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger("gemma4-sidecar")

app = FastAPI(title="Gemma 4 Sidecar")

# Globals set during startup
_model = None
_processor = None
_device: str = "cpu"
_torch_dtype = torch.bfloat16
_auth_token: str | None = None
_max_new_tokens: int = 200
_allowed_dir: str | None = None


class TranscribeRequest(BaseModel):
    audio_path: str


class TranscribeResponse(BaseModel):
    text: str
    latency_ms: float | None = None


class HealthResponse(BaseModel):
    status: str
    model_id: str | None = None
    quantization: str | None = None
    device: str | None = None
    cuda_device: str | None = None
    vram_allocated_mb: float | None = None


_health_state = {
    "status": "starting",
    "model_id": None,
    "quantization": None,
    "device": None,
    "cuda_device": None,
    "vram_allocated_mb": None,
}


def _check_auth(authorization: str | None):
    if _auth_token and authorization != f"Bearer {_auth_token}":
        raise HTTPException(status_code=401, detail="Unauthorized")


@app.get("/health")
async def health() -> HealthResponse:
    state = dict(_health_state)
    if torch.cuda.is_available():
        state["vram_allocated_mb"] = round(torch.cuda.memory_allocated() / 1e6, 1)
    return HealthResponse(**state)


@app.post("/transcribe")
async def transcribe(
    request: TranscribeRequest,
    authorization: str | None = Header(default=None),
) -> TranscribeResponse:
    _check_auth(authorization)

    if _health_state["status"] != "ready":
        raise HTTPException(status_code=503, detail="Model not ready")

    # Validate path is within allowed directory
    audio_path = Path(request.audio_path).resolve()
    if _allowed_dir:
        allowed = Path(_allowed_dir).resolve()
        try:
            audio_path.relative_to(allowed)
        except ValueError:
            raise HTTPException(status_code=400, detail="Audio path outside allowed directory")

    if not audio_path.exists():
        raise HTTPException(status_code=400, detail=f"Audio file not found: {audio_path}")

    try:
        audio_array, sample_rate = sf.read(str(audio_path), dtype="float32")

        # Resample to 16kHz if needed
        if sample_rate != 16000:
            import torchaudio

            waveform = torch.from_numpy(audio_array).unsqueeze(0)
            resampler = torchaudio.transforms.Resample(orig_freq=sample_rate, new_freq=16000)
            waveform = resampler(waveform)
            audio_array = waveform.squeeze(0).numpy()

        messages = [
            {
                "role": "user",
                "content": [
                    {"type": "audio", "audio": audio_array},
                    {
                        "type": "text",
                        "text": (
                            "Transcribe the following speech segment in English into English text. "
                            "Only output the transcription, with no newlines. "
                            "When transcribing numbers, write the digits."
                        ),
                    },
                ],
            }
        ]

        inputs = _processor.apply_chat_template(
            messages,
            add_generation_prompt=True,
            tokenize=True,
            return_dict=True,
            return_tensors="pt",
        )
        inputs = {k: v.to(_device) for k, v in inputs.items()}

        t0 = time.perf_counter()
        with torch.inference_mode():
            output_ids = _model.generate(
                **inputs,
                max_new_tokens=_max_new_tokens,
                do_sample=False,
                temperature=1.0,
                top_p=1.0,
                top_k=1,
            )
        latency_ms = (time.perf_counter() - t0) * 1000

        # Decode only the newly generated tokens
        input_len = inputs["input_ids"].shape[1]
        generated_ids = output_ids[0][input_len:]
        text = _processor.decode(generated_ids, skip_special_tokens=True).strip()

        return TranscribeResponse(text=text, latency_ms=round(latency_ms, 1))

    except Exception as e:
        logger.error("Transcription failed: %s", e, exc_info=True)
        raise HTTPException(status_code=500, detail=str(e)) from e


@app.post("/shutdown")
async def shutdown():
    logger.info("Shutdown requested")
    sys.exit(0)


def load_model(model_path: str, quantization: str, device_map: str):
    """Load the Gemma 4 model and processor."""
    global _model, _processor, _device, _health_state  # noqa: PLW0603

    _health_state["status"] = "loading_model"
    _health_state["model_id"] = model_path

    # Resolve device: "auto" tries CUDA first, then CPU
    if device_map == "auto":
        resolved_device = "cuda" if torch.cuda.is_available() else "cpu"
    else:
        resolved_device = device_map

    logger.info("CUDA available: %s", torch.cuda.is_available())
    if torch.cuda.is_available():
        logger.info("CUDA device: %s", torch.cuda.get_device_name(0))
    logger.info("Resolved device: %s", resolved_device)

    logger.info("Loading processor from %s", model_path)
    _processor = AutoProcessor.from_pretrained(model_path)

    logger.info("Loading model from %s (quantization=%s, device=%s)", model_path, quantization, resolved_device)

    load_kwargs: dict = {"device_map": resolved_device}

    # Use flash_attention_2 if available, otherwise eager
    try:
        import flash_attn  # noqa: F401

        load_kwargs["attn_implementation"] = "flash_attention_2"
        logger.info("Using Flash Attention 2")
    except ImportError:
        load_kwargs["attn_implementation"] = "eager"

    if quantization == "4bit":
        from transformers import BitsAndBytesConfig

        load_kwargs["quantization_config"] = BitsAndBytesConfig(
            load_in_4bit=True,
            bnb_4bit_compute_dtype=torch.bfloat16,
        )
        _health_state["quantization"] = "4bit"
    elif quantization == "8bit":
        from transformers import BitsAndBytesConfig

        load_kwargs["quantization_config"] = BitsAndBytesConfig(load_in_8bit=True)
        _health_state["quantization"] = "8bit"
    else:
        load_kwargs["torch_dtype"] = _torch_dtype
        _health_state["quantization"] = "none"

    _model = Gemma4ForConditionalGeneration.from_pretrained(model_path, **load_kwargs)
    _model.eval()

    _device = resolved_device
    _health_state["device"] = resolved_device
    if torch.cuda.is_available():
        _health_state["cuda_device"] = torch.cuda.get_device_name(0)
        _health_state["vram_allocated_mb"] = round(torch.cuda.memory_allocated() / 1e6, 1)
    _health_state["status"] = "ready"
    logger.info("Model loaded successfully on %s", resolved_device)


def main():
    parser = argparse.ArgumentParser(description="Gemma 4 sidecar server")
    parser.add_argument("--model-path", required=True, help="Path to the pre-downloaded model")
    parser.add_argument("--port", type=int, default=8321, help="Port to listen on")
    parser.add_argument("--quantization", choices=["4bit", "8bit", "none"], default="none")
    parser.add_argument("--dtype", choices=["bfloat16", "float32", "float16"], default="bfloat16",
                        help="Torch dtype for model weights (bfloat16 recommended, float32 for compatibility)")
    parser.add_argument("--device-map", default="auto",
                        help="Device: 'auto' (CUDA if available, else CPU), 'cuda', 'cuda:0', 'cpu'")
    parser.add_argument("--max-new-tokens", type=int, default=200)
    parser.add_argument("--auth-token", default=None, help="Bearer token for authentication")
    parser.add_argument("--allowed-dir", default=None, help="Restrict audio file reads to this directory")
    args = parser.parse_args()

    global _auth_token, _max_new_tokens, _allowed_dir, _torch_dtype  # noqa: PLW0603
    _auth_token = args.auth_token
    _max_new_tokens = args.max_new_tokens
    _allowed_dir = args.allowed_dir
    _torch_dtype = {"bfloat16": torch.bfloat16, "float32": torch.float32, "float16": torch.float16}[args.dtype]

    # Load model before starting the server
    load_model(args.model_path, args.quantization, args.device_map)

    uvicorn.run(app, host="127.0.0.1", port=args.port, log_level="info")


if __name__ == "__main__":
    main()
