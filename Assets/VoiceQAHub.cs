using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.XR;
using UnityEngine.Android;

// Push-to-talk voice input. STT no longer happens on-device — this just
// records the mic and uploads the raw audio to GuideAIServiceHub, which
// sends it to the tethered PC server for transcription, chat, and TTS.
public class VoiceQAHub : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI responseText;
    public TextMeshProUGUI statusText;

    private bool isRecording = false;
    private bool aButtonHeld = false;
    private AudioClip recording;

    private const int SAMPLE_RATE = 16000;
    private const int MAX_RECORDING_TIME = 10;

    IEnumerator Start()
    {
        statusText.text = "Checking microphone...";

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
            yield return new WaitForSeconds(1f);
        }
#else
        yield return null;
#endif

        if (Microphone.devices.Length == 0)
            Debug.LogError("VoiceQAHub: No microphone found.");
        else
            Debug.Log("VoiceQAHub: Microphone = " + Microphone.devices[0]);

        statusText.text = "Hold A button or V key to ask";
        Debug.Log("VoiceQAHub: Ready (server-side STT via tethered PC).");
    }

    void Update()
    {
        bool aButtonDown = false, aButtonUp = false;

        UnityEngine.XR.InputDevice rightHand =
            UnityEngine.XR.InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        if (rightHand.isValid)
        {
            bool aButton = false;
            rightHand.TryGetFeatureValue(
                UnityEngine.XR.CommonUsages.primaryButton, out aButton);
            if (aButton && !aButtonHeld) { aButtonDown = true; aButtonHeld = true; }
            if (!aButton && aButtonHeld) { aButtonUp = true; aButtonHeld = false; }
        }

        bool keyDown = Keyboard.current != null &&
                       Keyboard.current.vKey.wasPressedThisFrame;
        bool keyUp = Keyboard.current != null &&
                       Keyboard.current.vKey.wasReleasedThisFrame;

        if ((aButtonDown || keyDown) && !GuideBusyLock.IsBusy) StartRecording();
        if ((aButtonUp || keyUp) && isRecording) StopAndSend();
    }

    void StartRecording()
    {
        isRecording = true;
        statusText.text = "Listening...";
        recording = Microphone.Start(null, false, MAX_RECORDING_TIME, SAMPLE_RATE);
    }

    void StopAndSend()
    {
        isRecording = false;
        int position = Microphone.GetPosition(null);
        Microphone.End(null);

        if (position <= 0)
        {
            statusText.text = "No audio — try again";
            return;
        }

        AudioClip trimmed = TrimClip(recording, position);
        statusText.text = GuideBusyLock.RandomThinkingPhrase();

        byte[] wavData = WavUtility.FromAudioClip(trimmed);

        if (GuideAIServiceHub.Instance == null)
        {
            Debug.LogError("VoiceQAHub: GuideAIServiceHub.Instance is null.");
            statusText.text = "AI guide not ready — try again in a moment";
            return;
        }

        GuideAIServiceHub.Instance.RequestNarrationFromAudio(wavData, OnGuideResponse);
    }

    void OnGuideResponse(string response)
    {
        responseText.text = response;
        statusText.text = "Hold A or V to ask again";
    }

    AudioClip TrimClip(AudioClip clip, int position)
    {
        float[] data = new float[position * clip.channels];
        clip.GetData(data, 0);
        AudioClip trimmed = AudioClip.Create(
            "trimmed", position, clip.channels, clip.frequency, false);
        trimmed.SetData(data, 0);
        return trimmed;
    }
}