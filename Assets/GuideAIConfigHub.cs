using UnityEngine;

// Config for the NASA Ask Sage tenant integration.
//
// TWO credential types:
//   teamKey       -> Bearer token for OpenAI-compat chat
//   askSageApiKey -> x-access-tokens for native TTS + STT endpoints
//                   (try same value for both first — confirm with admin)
//
// TWO base URLs:
//   chatBaseUrl    -> OpenAI-compat proxy
//   askSageBaseUrl -> Ask Sage native server (TTS + STT)
//
// SETUP:
// 1. Right-click Project -> Create -> LUNA -> AI Config (Hub Test).
// 2. Fill in teamKey + askSageApiKey.
// 3. Drag into GuideAIServiceHub.config AND VoiceQAHub.config.
// 4. Gitignore: Assets/Config/GuideAIConfigHub.asset
[CreateAssetMenu(fileName = "GuideAIConfigHub", menuName = "LUNA/AI Config (Hub Test)")]
public class GuideAIConfigHub : ScriptableObject
{
    [Header("Chat — Ask Sage OpenAI-compatible")]
    [Tooltip("Bearer token for chat endpoint. Try same value as askSageApiKey first.")]
    public string teamKey = "";

    [Tooltip("Base URL for OpenAI-compatible chat (includes /server/openai/v1).")]
    public string chatBaseUrl = "https://api.asksage.ai.nasa.gov/server/openai/v1";

    [Tooltip("Chat model. e.g. claude-35-sonnet, gpt-4o, gpt-4.1-mini.")]
    public string chatModel = "claude-35-sonnet";

    [Header("Ask Sage Native API (TTS + STT)")]
    [Tooltip("x-access-tokens value (Ask Sage API key). " +
             "Try same value as teamKey first — confirm with admin.")]
    public string askSageApiKey = "";

    [Tooltip("Base URL for Ask Sage native server (no trailing slash).")]
    public string askSageBaseUrl = "https://api.asksage.ai.nasa.gov/server";

    [Header("Text-to-Speech (Gemini)")]
    [Tooltip("TTS provider. 'google' = Gemini TTS. 'azure' = Azure OpenAI TTS.")]
    public string ttsProvider = "google";

    [Tooltip("Gemini TTS model. " +
             "gemini-2.5-flash-tts  -> fast, no streaming. " +
             "gemini-2.5-pro-tts    -> highest quality, no streaming. " +
             "gemini-3.1-flash-tts-preview -> supports streaming (not used here).")]
    public string ttsModel = "gemini-2.5-flash-tts";

    [Tooltip("Voice name. e.g. Charon (Informative), Kore (Firm), " +
             "Puck (Upbeat), Sulafat (Warm), Aoede (Breezy).")]
    public string ttsVoice = "Charon";

    [Tooltip("BCP-47 language code. Gemini auto-detects, but en-US is safe default.")]
    public string ttsLanguageCode = "en-US";

    [Tooltip("Optional audio profile prepended to TTS text to direct Gemini's " +
             "voice style and persona. Leave blank for default behavior.\n\n" +
             "IMPORTANT: End with '### TRANSCRIPT\\n' so Gemini knows where " +
             "the spoken text begins and does not read your directions aloud.\n\n" +
             "Example:\n" +
             "# AUDIO PROFILE: NASA Lunar Guide\n" +
             "Calm, knowledgeable, warmly enthusiastic.\n\n" +
             "### DIRECTOR'S NOTES\n" +
             "Style: Professional yet approachable.\n" +
             "Pacing: Steady and clear.\n\n" +
             "### TRANSCRIPT\n")]
    [TextArea(4, 10)]
    public string ttsAudioProfile = "";

    [Tooltip("Max TTS retry attempts on 500 error. " +
             "Gemini TTS docs recommend retry logic for occasional 500s.")]
    [Range(1, 5)]
    public int ttsMaxRetries = 3;

    [Header("Speech-to-Text")]
    [Tooltip("Model for audio transcription via /server/query_with_file. " +
             "Must support audio input. Recommended: gemini-2.5-pro.")]
    public string sttModel = "gemini-2.5-pro";

    [Tooltip("Transcription prompt sent with the audio file.")]
    [TextArea(2, 4)]
    public string sttPrompt =
        "Transcribe exactly what is spoken in this audio. " +
        "Return only the spoken words, no commentary or formatting.";

    // ---- Computed helpers ----

    // Chat base URL, trailing slash stripped.
    public string ChatBase =>
        string.IsNullOrEmpty(chatBaseUrl)
            ? "https://api.asksage.ai.nasa.gov/server/openai/v1"
            : chatBaseUrl.TrimEnd('/');

    // Native Ask Sage base URL, trailing slash stripped.
    public string NativeBase =>
        string.IsNullOrEmpty(askSageBaseUrl)
            ? "https://api.asksage.ai.nasa.gov/server"
            : askSageBaseUrl.TrimEnd('/');
}