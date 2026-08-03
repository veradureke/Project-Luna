# Project LUNA

An Artemis-themed interactive VR exhibit for Meta Quest 3, featuring LUNA, an AI astronaut guide. Built at NASA Glenn Research Center's GVIS Lab (Code 693, Planetary Systems Laboratory).

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
| Knowledge grounding | NASA Luna proxy |
| Text-to-speech | pyttsx3 |
| Backend | FastAPI (tethered PC server, connected via USB) |

## Architecture

Audio is captured on the headset and streamed to a tethered PC server over USB, which handles the full round trip: Whisper transcribes the question locally, the query is routed through the NASA Luna proxy for an answer, and pyttsx3 synthesizes the response back to speech. The headset stays untethered for the visitor while the heavier inference work runs on the PC.

Unity and the server only share two things: the shape of the `/chat-audio` and `/chat-text` requests, and the port number. Full protocol details live in [`server/README.md`](server/README.md).

## Project structure

```
├── Assets/                # Unity assets, scenes, scripts
├── Packages/               # Unity package manifest
├── ProjectSettings/         # Unity project settings
├── docs/                   # GitHub Pages site (this repo's landing page)
│   └── index.html
├── server/                 # Python AI backend, run separately on the tethered PC
│   └── README.md
└── README.md
```

Unity scenes are organized additively: a Persistent scene holds cross-scene systems (audio, LUNA's companion controller), with Title, Hub, and individual model scenes loaded on top.

## Setup

1. Clone the repo
2. Open in Unity (see `ProjectSettings/ProjectVersion.txt` for the required editor version)
3. Open the `Persistent` scene, then additively load `Title` to start from the main flow
4. Set up and run the backend server, see [`server/README.md`](server/README.md) for first-time setup and the every-time run steps
5. Build and deploy to Meta Quest 3 via Unity's Android build pipeline

## Team

Built under mentor Herb Schilling, with Lance Santana, Minh Dinh, Aryan Dalal, and Brooke Weborg.

## GitHub Pages site

The `docs/` folder contains the project landing page. It's served via **Settings → Pages → Deploy from a branch → main → /docs**. Screenshots referenced on the page live in `docs/screenshots/` and can be swapped in as captures become available.
