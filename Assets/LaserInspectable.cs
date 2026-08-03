using System.Collections;
using UnityEngine;
using TMPro;

// Point-and-click "ask LUNA about this exhibit" for the big display models
// (Orion, SLS, Moon, Apollo Lunar Module, RS-25). Implements ILaserClickable so
// ControllerLaser fires OnLaserClick when the laser is pointed at this model's
// (non-trigger) collider and the trigger is pressed.
//
// This is the looking-at-a-model counterpart to PedestalArtifact (which is the
// holding-a-replica interaction). Both hand off to the same GuideAIServiceHub +
// NASAKnowledgeBase narration pipeline — nothing AI/TTS lives here.
[DisallowMultipleComponent]
public class LaserInspectable : MonoBehaviour, ILaserClickable
{
    [Header("Identity")]
    [Tooltip("Name used for NASA documentation lookup and the narration prompt. " +
             "Leave blank to use this GameObject's name instead.")]
    public string objectName = "";

    [Header("Cooldown")]
    [Tooltip("Minimum seconds between narrations triggered by THIS model, so " +
             "repeated clicks don't spam the guide.")]
    public float cooldown = 3f;

    float lastClickTime = -999f;

    // The shared speech-bubble text lives on the Astronaut Guide in the Persistent
    // scene — a different scene from this model — so it can't be a serialized
    // reference. Found once at runtime instead. Optional: if not found, the guide
    // still speaks the answer via TTS.
    TextMeshProUGUI responseText, statusText;
    bool lookedForText;

    public void OnLaserClick(ControllerLaser laser, RaycastHit hit)
    {
        if (GuideBusyLock.IsBusy) return;
        if (Time.time - lastClickTime < cooldown) return;
        if (GuideAIServiceHub.Instance == null) return;
        lastClickTime = Time.time;

        FindTextOnce();

        string name = string.IsNullOrWhiteSpace(objectName) ? gameObject.name : objectName;
        if (statusText != null) statusText.text = "Examining " + name + "...";

        // Ground in real NASA documentation when available, but don't refuse to
        // narrate if nothing's found — keep it general rather than inventing facts.
        string nasaContext = NASAKnowledgeBase.GetContext(name);
        string contextSection = string.IsNullOrWhiteSpace(nasaContext)
            ? ""
            : "Here is real NASA documentation about this for grounding:\n\n" +
              nasaContext + "\n\n";

        string prompt =
            "The visitor is standing in front of the full-scale '" + name + "' and " +
            "pointing right at it.\n\n" + contextSection +
            "Describe it directly to them in 2-3 enthusiastic, scientifically accurate " +
            "sentences, as if you're right there looking at it together. If facts are " +
            "given above, ground the description in only those — otherwise keep it " +
            "general rather than inventing specifics. Don't reference 'the documentation' " +
            "or 'the text'. Don't open with a greeting or filler like 'Sure!' — start " +
            "right in on what they're looking at.";

        StartCoroutine(AckThenNarrate(prompt));
    }

    IEnumerator AckThenNarrate(string prompt)
    {
        if (statusText != null) statusText.text = GuideBusyLock.RandomAcknowledgmentPhrase();
        yield return new WaitForSeconds(0.4f);
        if (statusText != null) statusText.text = GuideBusyLock.RandomThinkingPhrase();

        GuideAIServiceHub.Instance.RequestNarration(prompt, response =>
        {
            if (responseText != null) responseText.text = response;
            if (statusText != null) statusText.text = "";
        });
    }

    // The guide's Response/Status Text objects live in Persistent; resolve them by
    // name at runtime (cached) so the answer also shows on the speech bubble, the
    // same way the other interactions display it.
    void FindTextOnce()
    {
        if (lookedForText) return;
        lookedForText = true;
        foreach (var t in FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (responseText == null && t.gameObject.name == "Response Text") responseText = t;
            if (statusText == null && t.gameObject.name == "Status Text") statusText = t;
        }
    }
}
