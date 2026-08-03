using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using TMPro;

// Lets the visitor cut the guide's current speech short.
//
// Why this is so simple: AIInspector/VoiceQA/WaypointTour's SpeakResponse all
// end with
//     yield return new WaitWhile(() => guideAudioSource.isPlaying);
// before control returns to whichever script is holding GuideBusyLock. So
// just stopping the AudioSource here is enough — the calling script's normal
// "done talking" cleanup (releasing the lock, resetting statusText) runs
// exactly as if the clip had finished on its own. No per-script changes needed.
//
// SCOPE: this only interrupts the TALKING phase (audio actively playing).
// If the guide is still THINKING — waiting on Claude/local LLM/TTS over the
// network — this does nothing yet, since there's no cancel hook wired into
// those in-flight requests. Extending this to also abort a pending request
// would need each script to register its active UnityWebRequest somewhere
// shared (similar to GuideBusyLock itself) — straightforward to add later
// if the Thinking phase turns out to be the part visitors actually want to
// skip past, but talking is usually the longer, more noticeable wait.
public class GuideSkipInput : MonoBehaviour
{
    [Header("References")]
    [Tooltip("AstronautGuide's AudioSource — same one used by AIInspector/VoiceQA/WaypointTour.")]
    public AudioSource guideAudioSource;

    [Tooltip("Optional — shows a brief status message on skip. Leave blank to skip silently.")]
    public TextMeshProUGUI statusText;

    [Header("Input")]
    [Tooltip("Quest controller button that triggers skip — B button (secondaryButton) on the right hand.")]
    public bool useRightHandSecondaryButton = true;

    private bool buttonHeld = false;

    void Update()
    {
        bool skipPressed = false;

        // Quest — B button (secondaryButton) on right controller.
        // Chosen so it doesn't collide with the A button (push-to-talk) or
        // the trigger (point-and-click inspect) already in use on this hand.
        if (useRightHandSecondaryButton)
        {
            UnityEngine.XR.InputDevice rightHand =
                UnityEngine.XR.InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (rightHand.isValid)
            {
                bool bButton = false;
                rightHand.TryGetFeatureValue(
                    UnityEngine.XR.CommonUsages.secondaryButton, out bButton);
                if (bButton && !buttonHeld) skipPressed = true;
                buttonHeld = bButton;
            }
        }

        // Editor — Space bar.
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            skipPressed = true;

        if (skipPressed)
            TrySkip();
    }

    void TrySkip()
    {
        if (guideAudioSource != null && guideAudioSource.isPlaying)
        {
            guideAudioSource.Stop();
            Debug.Log("Guide speech skipped by visitor.");
            if (statusText != null)
                statusText.text = "Skipped";
        }
        else if (GuideBusyLock.IsBusy)
        {
            // Still thinking — nothing to actually skip yet, but let the
            // visitor know their press registered rather than seeming ignored.
            if (statusText != null)
                statusText.text = "Still thinking...";
        }
    }
}