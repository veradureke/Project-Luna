using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Full audio-in/audio-out AI pipeline via a tethered PC server, reached
// over USB through `adb reverse tcp:8000 tcp:8000`. Replaces LLMUnity,
// on-device Whisper, and AndroidTTSBridge entirely — STT, chat, and TTS
// all happen server-side on the PC now.
//
// Two request paths:
//   RequestNarrationFromAudio() — for voice questions (VoiceQAHub calls
//     this with recorded mic audio). Hits POST /chat-audio.
//   RequestNarrationFromText()  — for point-and-click questions (a model
//     click handler calls this with plain text). Hits POST /chat-text.
// Both get back a WAV file + response text, and this script plays the
// audio through an AudioSource on this GameObject (or one you assign).
public class GuideAIServiceHub : MonoBehaviour
{
    public static GuideAIServiceHub Instance { get; private set; }

    [Header("Server Config")]
    [Tooltip("Reached via adb reverse over USB, NOT Wi-Fi.")]
    public string serverBaseUrl = "http://127.0.0.1:8000";
    public int requestTimeoutSeconds = 30;

    [Header("Audio Playback")]
    [Tooltip("AudioSource that plays LUNA's spoken responses. Assign the " +
        "one on AstronautGuide.")]
    public AudioSource audioSource;

    [Header("Guide Animation")]
    public AstronautGuideAnimator guideAnimator;

    [Header("Response Pacing")]
    public float minThinkingDelay = 1.2f;

    private bool serverReachable = false;
    public bool IsModelReady => serverReachable;

    [Serializable]
    private class TextRequestBody { public string question; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }
        Instance = this;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        StartCoroutine(CheckServerConnection());
    }

    IEnumerator CheckServerConnection()
    {
        string testUrl = $"{serverBaseUrl.TrimEnd('/')}/test";
        using (UnityWebRequest req = UnityWebRequest.Get(testUrl))
        {
            req.timeout = requestTimeoutSeconds;
            yield return req.SendWebRequest();

            serverReachable = req.result == UnityWebRequest.Result.Success;
            if (serverReachable)
                Debug.Log("GuideAIServiceHub: Local audio server reachable.");
            else
                Debug.LogError("GuideAIServiceHub: Could not reach local server: " +
                    req.error + ". Check server.py is running and " +
                    "'adb reverse tcp:8000 tcp:8000' has been run for this " +
                    "USB session.");
        }
    }

    /// <summary>For voice questions. VoiceQAHub records mic audio, converts
    /// it to WAV bytes (WavUtility.FromAudioClip), and passes it here.</summary>
    public void RequestNarrationFromAudio(byte[] wavData,
        Action<string> onResponseText = null)
    {
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", wavData, "recording.wav", "audio/wav");

        string url = $"{serverBaseUrl.TrimEnd('/')}/chat-audio";
        UnityWebRequest req = UnityWebRequest.Post(url, form);

        StartCoroutine(SendAndHandle(req, url, onResponseText));
    }

    /// <summary>For point-and-click questions. Pass the question as plain
    /// text, skips STT server-side, still runs chat + TTS.</summary>
    public void RequestNarrationFromText(string question,
        Action<string> onResponseText = null)
    {
        string url = $"{serverBaseUrl.TrimEnd('/')}/chat-text";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(
            JsonUtility.ToJson(new TextRequestBody { question = question }));

        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        StartCoroutine(SendAndHandle(req, url, onResponseText));
    }

    /// <summary>Backward-compatible alias for RequestNarrationFromText,
    /// kept so existing callers (AIInspector, LaserInspectable,
    /// PedestalArtifact, OpeningNarration, ProximityReaction, Waypoint
    /// Tour, TitleScreenManager) don't all need to be edited. The
    /// 'remember' parameter is accepted for signature compatibility but
    /// currently unused, since this server contract is single-turn.</summary>
    public void RequestNarration(string prompt,
        Action<string> onResponseText = null, bool remember = true)
    {
        RequestNarrationFromText(prompt, onResponseText);
    }

    IEnumerator SendAndHandle(UnityWebRequest req, string url,
        Action<string> onResponseText)
    {
        if (!GuideBusyLock.TryAcquire(this))
        {
            req.Dispose();
            yield break;
        }

        if (!serverReachable)
        {
            onResponseText?.Invoke("AI server isn't connected — check the USB tunnel.");
            req.Dispose();
            GuideBusyLock.Release(this);
            yield break;
        }

        float triggerTime = Time.time;
        guideAnimator?.Trigger("Acknowledge");
        guideAnimator?.SetThinking(true);

        req.timeout = requestTimeoutSeconds;
        yield return req.SendWebRequest();

        guideAnimator?.SetThinking(false);

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("GuideAIServiceHub: Request to " + url + " failed: " + req.error);
            onResponseText?.Invoke("I couldn't generate a response — please try again.");
            guideAnimator?.Trigger("DontKnow");
            req.Dispose();
            GuideBusyLock.Release(this);
            yield break;
        }

        string responseTextHeader = req.GetResponseHeader("X-AI-Response-Text");
        string responseText = string.IsNullOrEmpty(responseTextHeader)
            ? "(no response text)"
            : Uri.UnescapeDataString(responseTextHeader);

        byte[] wavBytes = req.downloadHandler.data;
        req.Dispose();

        responseText = GuideBusyLock.CleanResponseText(responseText);
        onResponseText?.Invoke(responseText);

        float paceElapsed = Time.time - triggerTime;
        if (paceElapsed < minThinkingDelay)
            yield return new WaitForSeconds(minThinkingDelay - paceElapsed);

        yield return StartCoroutine(PlayWavResponse(wavBytes));

        GuideBusyLock.Release(this);
    }

    IEnumerator PlayWavResponse(byte[] wavBytes)
    {
        AudioClip clip = WavUtility.ToAudioClip(wavBytes);
        if (clip == null || audioSource == null)
        {
            Debug.LogError("GuideAIServiceHub: Could not play response audio " +
                "(missing AudioClip or AudioSource — assign one in the Inspector).");
            yield break;
        }

        guideAnimator?.StartTalking();
        audioSource.clip = clip;
        audioSource.Play();
        yield return new WaitForSeconds(clip.length);
        guideAnimator?.StopTalking();
    }
}