<<<<<<< HEAD
# LUNA Server

The local AI backend for LUNA. Runs as a separate process on the tethered
PC — the Quest 3 reaches it over USB, never Wi-Fi. Lives outside your
Unity project folder since it's not a Unity asset.

Handles the full pipeline server-side: speech-to-text (local Whisper),
chat (NASA Luna proxy), and text-to-speech (pyttsx3). Unity just records
the mic, uploads audio (or plain text for point-and-click), and plays
back whatever WAV file comes back.

## First time only

Nothing extra to do, just run `run_luna.bat` (see below). It sets itself
up automatically the first time: creates a virtual environment, installs
everything needed (this includes `torch`, so the very first run can take
several minutes), and opens `.env` for you to paste in your API key.

## Every time

1. Plug the Quest 3 into the PC via USB.
2. Double-click **`run_luna.bat`**. It connects to the headset and starts
   the server in one step — leave that window open.
3. Launch LUNA on the headset.

One file, every time: `run_luna.bat`. It figures out on its own whether
setup is needed.

## How it connects to Unity

```
LUNA (Unity)                --HTTP over USB tunnel-->   LUNA Server (this folder)
GuideAIServiceHub.cs / VoiceQAHub.cs                      server.py

Voice question:
  RequestNarrationFromAudio(wavBytes)
    POST /chat-audio (audio file)     -->  local Whisper transcribes it
                                       -->  NASA Luna proxy generates a reply
                                       -->  pyttsx3 synthesizes speech
  <-- WAV file + X-AI-Response-Text header

Click question:
  RequestNarrationFromText(question)
    POST /chat-text {"question": "..."}  -->  (skips Whisper, straight to chat)
                                          -->  NASA Luna proxy + pyttsx3, same as above
  <-- WAV file + X-AI-Response-Text header
```

Unity never runs any of this code directly. The two projects only share
the request/response shape of `/chat-audio` and `/chat-text`, and the port
number (8000). If you ever change the port, update it in `server.py`,
`run_luna.bat`, and `GuideAIServiceHub.cs`'s `serverBaseUrl` field.

## If something won't connect

- Re-run `run_luna.bat`, the USB tunnel drops every time the cable
  disconnects, and needs to be re-established.
- Confirm USB debugging is allowed on the headset (you'll get a prompt
  inside the Quest the first time, or after re-pairing).
- If `adb` can't be found and your Unity version isn't `6000.4.10f1`, open
  `run_luna.bat` in a text editor and update the `ADB_PATH` line near the
  top to match your actual Unity Hub install folder.
- If package installs seem to be re-running every time instead of being
  skipped, check that `venv\.installed` exists, that marker file is what
  tells the script it can skip straight past the install check.
- Confirm no audio is playing in Unity? Check that `GuideAIServiceHub`'s
  `audioSource` field is actually assigned in the Inspector, this is
  required now since responses come back as real WAV audio, not
  device-native TTS.

## Confirm before relying on this for a real demo

`server.py` currently uses the NASA Luna proxy (`proxy.fast.luna.nasa.gov`),
not Ask Sage. Check with Herb/Lance on whether this is the approved
backend for this project before treating it as production-ready.
=======
# Project LUNA

An Artemis-themed interactive VR exhibit for Meta Quest 3, featuring LUNA, an AI astronaut guide. Built at NASA Glenn Research Center's GVIS Lab.

**[View the project site](https://veradureke.github.io/Luna_Clean/)**

## Overview

Project LUNA lets visitors explore the Artemis program through three guided experiences:

- **AI Object Inspector**: pick up NASA hardware models (landers, rovers, suits) and ask LUNA what they are
- **Voice Q&A**: ask LUNA anything by voice, answered in real time and grounded in NASA mission data
- **Waypoint Tour Guide**: follow a guided path through a lunar terrain scene with narrated stops along the mission timeline

LUNA appears as a world-space 3D companion who follows the visitor through the experience rather than a static menu or HUD.

## Tech stack

| Layer | Tool |
|---|---|
| Engine | Unity |
| Target device | Meta Quest 3 |
| Speech-to-text | Whisper |
| Knowledge grounding | NASA Luna proxy / NASA knowledge base |
| Text-to-speech | pyttsx3 |
| Backend | FastAPI (tethered PC server) |

## Architecture

Audio is captured on the headset and streamed to a tethered PC server, which handles the full round trip: Whisper transcribes the question, the query is routed through a NASA-grounded knowledge base for an accurate answer, and pyttsx3 synthesizes the response back to speech. The headset stays untethered for the visitor while the heavier inference work runs on the PC.

## Project structure

```
├── Assets/                # Unity assets, scenes, scripts
├── Packages/               # Unity package manifest
├── ProjectSettings/         # Unity project settings
├── docs/                   # GitHub Pages site (this repo's landing page)
│   └── index.html
└── README.md
```

Unity scenes are organized additively: a Persistent scene holds cross-scene systems (audio, LUNA's companion controller), with Title, Hub, and individual model scenes loaded on top.

## Setup

1. Clone the repo
2. Open in Unity (see `ProjectSettings/ProjectVersion.txt` for the required editor version)
3. Open the `Persistent` scene, then additively load `Title` to start from the main flow
4. Start the backend server (Whisper + FastAPI + pyttsx3) on a tethered PC before running on-device
5. Build and deploy to Meta Quest 3 via Unity's Android build pipeline

## Team

Built under mentor Herb Schilling, with Lance Santana, Minh Dinh, Aryan Dalal, and Brooke Weborg.

## GitHub Pages site

The `docs/` folder contains the project landing page. It's served via **Settings → Pages → Deploy from a branch → main → /docs**. Screenshots referenced on the page live in `docs/screenshots/` and can be swapped in as captures become available.
>>>>>>> bf4da84605667b1fe9a64b044fac450fe137ee38
