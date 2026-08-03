"""
server.py
LUNA local AI server — full audio-in/audio-out pipeline. Runs on the
tethered PC; the Quest 3 reaches it over USB via
`adb reverse tcp:8000 tcp:8000`.

STT:  local Whisper (runs on the PC, not the headset)
Chat: NASA Luna proxy (proxy.fast.luna.nasa.gov) — confirm with Herb/Lance
      whether this or Ask Sage is the actually-approved backend before
      using this for a real demo.
TTS:  pyttsx3 (local, PC-side, SAPI5 on Windows). Tried routing TTS
      through the same NASA Luna proxy used for chat, but the current
      token is scoped to gpt-4o-mini only and denied access to tts-1
      (401 key_model_access_denied). Falling back to fully local TTS
      avoids any dependency on proxy permissions.

Two endpoints:
  POST /chat-audio — for voice questions (Unity uploads recorded mic audio)
  POST /chat-text   — for point-and-click questions (Unity sends plain
                      text, skips STT, still does chat + TTS)
Both return a WAV file as the response body, with the AI's response text
in the X-AI-Response-Text header.
"""

from fastapi import FastAPI, UploadFile, File, BackgroundTasks, HTTPException
from fastapi.responses import FileResponse
from pydantic import BaseModel
import uvicorn
import urllib.parse

import os
from dotenv import load_dotenv

import whisper
import tempfile

import pyttsx3

import time
import httpx

load_dotenv()

print("Loading local Whisper model...")
stt_model = whisper.load_model("base")
print("Speech-to-Text model loaded!")

openai_api_key = os.getenv("OPENAI_API_KEY")

# TEMPORARY DEBUG — remove once fully confirmed stable.
if openai_api_key:
    print(f"Loaded key: {openai_api_key[:10]}...{openai_api_key[-4:]}")
else:
    print("WARNING: OPENAI_API_KEY is empty or not set!")

chat_url = "https://proxy.fast.luna.nasa.gov/v1/chat/completions"
ai_model = "gpt-4o-mini"

headers = {
    "Content-Type": "application/json",
    "Authorization": f"Bearer {openai_api_key}",
}

LUNA_SYSTEM_PROMPT = (
    "You are LUNA, the AI guide at NASA Glenn Research Center's Artemis "
    "lunar exhibit. You speak with genuine enthusiasm for the science. "
    "Keep every response to 2-3 short sentences. Speak directly to the "
    "visitor. Always respond in plain spoken sentences — no markdown, no "
    "bullet points, no headers, since your response is spoken aloud, not "
    "displayed as text. "
    "IMPORTANT: Only discuss the specific exhibit or object named in the "
    "visitor's current message. Do not mention, describe, or reference any "
    "other exhibit, model, or hardware unless the visitor's message "
    "explicitly asks you to compare it to something else. If you're unsure "
    "what's currently in front of the visitor, ask them rather than "
    "guessing or referencing something you discussed earlier."
)

main_messages = [{"role": "system", "content": LUNA_SYSTEM_PROMPT}]


async def ask_async():
    async with httpx.AsyncClient(timeout=60.0) as client:
        payload = {"model": ai_model, "messages": main_messages}
        response = await client.post(chat_url, headers=headers, json=payload)

        if response.status_code != 200:
            print(f"Chat API Error! Status Code: {response.status_code}")
            print(f"Response Text: {response.text}")
            response.raise_for_status()

        message = response.json()["choices"][0]["message"]
        return message["content"]


async def ask_ai(input_text: str) -> str:
    main_messages.append({"role": "user", "content": input_text})
    result = await ask_async()
    main_messages.append({"role": "assistant", "content": result})
    return result


def generate_tts_sync(text: str, file_path: str):
    engine = pyttsx3.init()
    engine.setProperty("rate", 225)

    # Using Zira (the other installed default is David). Run
    # list_voices.py to see what's actually installed on this PC if you
    # want to try a different one.
    voices = engine.getProperty("voices")
    engine.setProperty("voice", voices[1].id)  # Zira

    engine.save_to_file(text, file_path)
    engine.runAndWait()
    del engine


def cleanup_file(filepath: str):
    if os.path.exists(filepath):
        os.remove(filepath)


def build_wav_response(ai_response_text: str, background_tasks: BackgroundTasks):
    timestamp = int(time.time() * 1000)
    output_wav = f"ai_response_{timestamp}.wav"
    generate_tts_sync(ai_response_text, output_wav)
    background_tasks.add_task(cleanup_file, output_wav)

    safe_text = urllib.parse.quote(ai_response_text)
    custom_headers = {"X-AI-Response-Text": safe_text}
    return FileResponse(output_wav, media_type="audio/wav", headers=custom_headers)


class TextRequest(BaseModel):
    question: str


app = FastAPI()


@app.post("/chat-audio")
async def chat_audio(file: UploadFile = File(...), background_tasks: BackgroundTasks = BackgroundTasks()):
    """For voice questions: Unity uploads recorded mic audio."""
    audio_bytes = await file.read()
    with tempfile.NamedTemporaryFile(delete=False, suffix=".wav") as temp_in:
        temp_in.write(audio_bytes)
        temp_in_path = temp_in.name

    try:
        result = stt_model.transcribe(temp_in_path)
        user_text = result["text"]
        print(f"User said: {user_text}")

        if len(user_text.strip()) < 3:
            print("Input too short, ignoring.")
            raise HTTPException(status_code=204, detail="Input too short")

        ai_response_text = await ask_ai(user_text)
        print(f"AI Response: {ai_response_text}")

        return build_wav_response(ai_response_text, background_tasks)
    finally:
        if os.path.exists(temp_in_path):
            os.remove(temp_in_path)


@app.post("/chat-text")
async def chat_text(data: TextRequest, background_tasks: BackgroundTasks = BackgroundTasks()):
    """For point-and-click questions: Unity sends plain text, no STT needed."""
    user_text = data.question
    print(f"User clicked/asked (text): {user_text}")

    if len(user_text.strip()) < 3:
        raise HTTPException(status_code=204, detail="Input too short")

    ai_response_text = await ask_ai(user_text)
    print(f"AI Response: {ai_response_text}")

    return build_wav_response(ai_response_text, background_tasks)


@app.get("/test")
async def test_connection():
    return {"message": "Connected"}


@app.post("/clear-history")
async def clear_history():
    """
    Resets the conversation back to just the system prompt. Call this
    every time the user transitions to a different model scene, so
    LUNA doesn't carry over context from a previous exhibit.
    """
    global main_messages
    main_messages = [{"role": "system", "content": LUNA_SYSTEM_PROMPT}]
    print("Conversation history cleared.")
    return {"message": "History cleared"}


if __name__ == "__main__":
    uvicorn.run(app, host="127.0.0.1", port=8000)