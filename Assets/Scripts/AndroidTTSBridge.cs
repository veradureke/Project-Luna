using UnityEngine;

// Calls Android's built-in TextToSpeech engine directly via JNI.
// No custom Java plugin needed — android.speech.tts.TextToSpeech is part
// of the Android SDK itself and is reachable straight from C#.
// No network, no model download, works on any Android device including Quest 3.
public static class AndroidTTSBridge
{
    private static AndroidJavaObject ttsInstance;
    private static TTSInitListener initListener;
    // Only referenced inside the Android-only blocks below, so scope the field to
    // that same platform. Otherwise its unconditional initializer reads as
    // "assigned but never used" (CS0414) in the Editor compile, where none of
    // those blocks are included.
#if UNITY_ANDROID && !UNITY_EDITOR
    private static bool initialized = false;
#endif

    // Implements android.speech.tts.TextToSpeech.OnInitListener so we get
    // notified when the TTS engine has actually finished loading.
    private class TTSInitListener : AndroidJavaProxy
    {
        public bool isReady = false;

        public TTSInitListener() : base("android.speech.tts.TextToSpeech$OnInitListener") { }

        // Must be named exactly "onInit" to match the Java interface method.
        void onInit(int status)
        {
            // TextToSpeech.SUCCESS == 0
            isReady = (status == 0);
            Debug.Log("AndroidTTSBridge: TTS engine init " +
                (isReady ? "succeeded" : "FAILED, status=" + status));
        }
    }

    public static void Initialize()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (initialized) return;

        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                initListener = new TTSInitListener();
                ttsInstance = new AndroidJavaObject(
                    "android.speech.tts.TextToSpeech", activity, initListener);
            }
            initialized = true;
            Debug.Log("AndroidTTSBridge: Initialize() called, waiting on onInit callback.");
        }
        catch (System.Exception e)
        {
            // Deliberately caught rather than left to propagate: an
            // exception here previously aborted the rest of Start() in
            // GuideAIServiceHub, silently preventing the LLM warmup call
            // from ever running. Never let TTS setup problems block chat.
            Debug.LogError("AndroidTTSBridge: Initialize() failed: " + e.Message);
            initialized = false;
        }
#endif
    }

    public static void Speak(string text)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!initialized) Initialize();
        if (ttsInstance == null) return;

        try
        {
            // Modern API: speak(CharSequence text, int queueMode, Bundle params, String utteranceId)
            // QUEUE_FLUSH = 0 — interrupts any current speech and starts this one immediately.
            ttsInstance.Call<int>("speak", text, 0, null, "lunaUtterance");
        }
        catch (System.Exception e)
        {
            Debug.LogError("AndroidTTSBridge: Speak() failed: " + e.Message);
        }
#else
        // Editor fallback — just log, no audio
        Debug.Log("TTS (Editor): " + text);
#endif
    }

    public static void Stop()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            ttsInstance?.Call<int>("stop");
        }
        catch (System.Exception e)
        {
            Debug.LogError("AndroidTTSBridge: Stop() failed: " + e.Message);
        }
#endif
    }

    public static bool IsSpeaking()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!initialized || ttsInstance == null) return false;
        try
        {
            return ttsInstance.Call<bool>("isSpeaking");
        }
        catch (System.Exception e)
        {
            Debug.LogError("AndroidTTSBridge: IsSpeaking() failed: " + e.Message);
            return false;
        }
#else
        return false;
#endif
    }
}