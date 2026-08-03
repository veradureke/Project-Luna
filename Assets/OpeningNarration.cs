using System.Collections;
using UnityEngine;
using TMPro;

// One-shot opening mission briefing. Subscribes to GuideCompanionFollow's
// OnArrivalComplete (fired once, when LUNA finishes her first walk-in to the
// player), waits a natural beat, then delivers a scene-setting briefing through
// the shared narration pipeline. Uses GuideBusyLock exactly like every other
// interaction (via GuideAIServiceHub.RequestNarration) — no special-casing.
[RequireComponent(typeof(GuideCompanionFollow))]
public class OpeningNarration : MonoBehaviour
{
    [Header("Speech Bubble (WorldSpaceCanvas)")]
    [Tooltip("Same Response Text / Status Text the other interaction scripts drive.")]
    public TextMeshProUGUI responseText;
    public TextMeshProUGUI statusText;

    [Header("Timing")]
    [Tooltip("A natural pause after LUNA arrives before she starts speaking — reads " +
             "better than talking the instant she stops walking.")]
    public float preSpeechBeat = 1.2f;

    // Used verbatim per the briefing spec.
    const string OpeningPrompt =
        "You are LUNA, the AI guide at NASA Glenn Research Center's Artemis " +
        "lunar exhibit. You have just walked over to greet this visitor for " +
        "the first time. Deliver a mission briefing in 3-4 sentences that: " +
        "sets the scene (they are standing on a simulated lunar surface " +
        "surrounded by real NASA hardware), names 2-3 of the specific " +
        "exhibits present (SLS rocket, RS-25 engine, Orion capsule, Apollo " +
        "Lunar Module — pick the most visually prominent ones), tells them " +
        "what they can do (point at objects to learn about them, ask you " +
        "questions by holding the A button, walk up to exhibits to hear " +
        "about them), and ends with a line that conveys genuine excitement " +
        "about what they're about to see. Speak directly, as a NASA mission " +
        "specialist briefing a crew before a mission — not as a tour guide " +
        "reading a script. Do not start with 'Welcome' or any generic " +
        "greeting. Start with something that immediately puts them in the moment.";

    GuideCompanionFollow follow;
    bool triggered;

    void Awake()
    {
        follow = GetComponent<GuideCompanionFollow>();
    }

    void OnEnable()
    {
        if (follow != null) follow.OnArrivalComplete += HandleArrival;
    }

    void OnDisable()
    {
        if (follow != null) follow.OnArrivalComplete -= HandleArrival;
    }

    void HandleArrival()
    {
        // OnArrivalComplete is already one-shot; this is belt-and-braces.
        if (triggered) return;
        triggered = true;
        StartCoroutine(SpeakOpening());
    }

    IEnumerator SpeakOpening()
    {
        // Let the arrival land before speaking.
        yield return new WaitForSeconds(preSpeechBeat);

        if (GuideAIServiceHub.Instance == null)
        {
            Debug.LogWarning("OpeningNarration: no GuideAIServiceHub in scene — opening briefing skipped.");
            yield break;
        }

        // Immediate feedback so the speech bubble isn't blank during Hub/LLM generation.
        if (statusText != null) statusText.text = "LUNA is ready...";

        GuideAIServiceHub.Instance.RequestNarration(OpeningPrompt, response =>
        {
            if (responseText != null)
            {
                responseText.text = response;
                ChatBubbleLayout.FitCanvasToContent(responseText);
            }
            if (statusText != null) statusText.text = "Point at an exhibit, or hold A to ask me anything";
        });
    }
}
