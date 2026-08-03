This technical overview outlines the architecture and implementation details of the NASA Lunar VR project, an educational VR experience built for Meta Quest, featuring AI-driven narration and a Retrieval-Augmented Generation (RAG) system for NASA documentation.

## 1. Project Description
The project is an educational Virtual Reality (VR) experience designed to simulate a lunar exploration mission. It allows users to explore a lunar base environment (modeled after NASA's Artemis program) and interact with an AI-driven "Astronaut Guide." The core pillars are **Immersive Exploration**, **AI-Assisted Learning**, and **Scientific Accuracy**. It targets educational institutions and space enthusiasts, providing an interactive way to learn about the Moon, the SLS rocket, and the Orion spacecraft using real-world NASA documentation as a knowledge base.

## 2. Gameplay Flow / User Loop
1.  **Boot & Initialization**: The user starts in the `LunarVRScene`. Systems initialize the OVR Camera Rig, XR Input, and local LLM servers via `LLMUnity`.
2.  **Exploration**: The player moves through the lunar environment using thumbstick locomotion or physical movement.
3.  **Waypoint Activation**: As the player reaches specific points of interest (e.g., the SLS launcher or Lunar Module), the `Waypoint Tour` system triggers contextual AI narration.
4.  **Interactive Q&A**: At any time, the user can hold the 'A' button (Quest) or 'V' key (Editor) to ask a question. 
5.  **Processing & Feedback**: The voice is transcribed via Whisper, contextualized using the `NASADocumentLoader` RAG system, and answered by an LLM (Claude or local LLM).
6.  **Loop Persistence**: The AI maintains conversation history via `LLMAgent` saving/loading, allowing for a continuous narrative throughout the session.

## 3. Architecture
The project follows a **Component-Based Architecture** with centralized managers for high-level services.
*   **Input System**: Uses the new Unity `InputSystem` alongside XR-specific feature polling for VR controllers.
*   **AI Service Layer**: A hybrid approach using both local LLM execution (`LLMUnity` via LlamaLib) and cloud APIs (OpenAI for Whisper, Anthropic for Claude).
*   **RAG Pattern**: Uses a simplified Retrieval-Augmented Generation pattern where local TextAssets are searched for keywords to provide context to the LLM.
*   **VR Integration**: Built on Meta's XR SDK, utilizing the `OVRCameraRig` for tracking and `CharacterController` for locomotion.

## 4. Game Systems & Domain Concepts

### AI Narration & Dialogue System
The core interaction system handles voice recording, transcription, and response generation. It uses a singleton pattern for global access to the knowledge base.
*   `VoiceQA`: Manages the recording state, calls Whisper for STT (Speech-to-Text), and handles the API communication with LLM providers.
*   `LLMAgent`: Provides a wrapper around the LLM engine, managing system prompts, message history, and context window overflow strategies.
*   `LLMClient`: Handles the low-level communication with the LLM server (either local or remote).
*   **Extension**: New LLM providers can be added by implementing a new `ICompletionCustom` or extending `LLMClient`.

`Location: Assets/LLMUnity/Runtime` and `Assets/VoiceQA.cs`

### NASA Knowledge Base (RAG)
A data-driven system that links physical locations and user queries to scientific documentation.
*   `NASADocumentLoader`: A singleton that loads TXT files into memory and performs keyword-based lookups to find relevant "context chunks."
*   `NASADocument`: A serializable class/data structure representing a specific NASA guide or reference.
*   **Extension**: Add new documentation by adding `.txt` files to `Assets/NASADocs` and registering them in the `NASADocumentLoader` inspector list.

`Location: Assets/` (Main folder)

### Locomotion & Navigation
Handles VR-safe movement and physical presence within the lunar environment.
*   `ThumbstickLocomotion`: Implements gaze-oriented movement and snap-turning for VR comfort.
*   `WaypointTour`: Monitors the player's proximity to `Transform` markers and triggers events based on location.
*   `SmoothObjectFollower`: Used for UI or companion characters to follow the player's view smoothly.

`Location: Assets/` (Main folder)

## 5. Scene Overview
*   `LunarVRScene.unity`: The primary simulation scene containing the lunar terrain, NASA assets (SLS, Orion, Lunar Module), and the AI Guide character.
*   `SampleScene.unity`: A test-bed for prototyping new AI behaviors or UI layouts.
*   **Scene Constraints**: The project uses a single-scene architecture for the core experience to avoid loading hitches in VR, with assets managed via Prefabs.

## 6. UI System
The project uses a mix of **World-Space UGUI** and **Billboard** techniques.
*   `responseText`: A `TextMeshProUGUI` component used for displaying AI-generated text.
*   `statusText`: Provides immediate feedback on system state (e.g., "Listening...", "Processing...").
*   `Billboard`: A utility script that ensures UI panels always face the player's `CenterEyeAnchor`.
*   **Extension**: To add a new UI screen, create a World-Space Canvas and add the `Billboard` component to ensure readability in VR.

`Location: Assets/TextMesh Pro` and `Assets/Billboard.cs`

## 7. Asset & Data Model
*   **3D Models**: High-fidelity NASA FBX/GLB assets (Orion, SLS, Apollo LM) located in specific folders named by vehicle.
*   **Documentation**: Raw text files in `Assets/NASADocs`.
*   **Settings**: ScriptableObjects in `Assets/Resources` (e.g., `DevAgentSettings.asset`, `InputActions.asset`) store system configurations.
*   **LLM Models**: Local GGUF models are stored in `StreamingAssets/LlamaLib-v2.0.5`.

## 8. Notes, Caveats & Gotchas
*   **API Keys**: `VoiceQA.cs` and `WaypointTour.cs` contain hardcoded placeholders for Claude and OpenAI keys. These must be moved to a secure configuration or environment variable before distribution.
*   **Microphone Input**: The `VoiceQA` script defaults to `Microphone.devices[0]`. In some VR headsets, the active mic might not be the primary index.
*   **Physics/Locomotion**: The `CharacterController` in `ThumbstickLocomotion.cs` dynamically adjusts its height based on the `CenterEyeAnchor`. Ensure the `OVRCameraRig` is a child of the player object for correct height calculation.
*   **Keyword Matching**: The RAG system in `NASADocumentLoader` uses simple string containment. Ensure `objectTag` values in the inspector are unique and descriptive to-the-point (e.g., "SLS" instead of "The Space Launch System").