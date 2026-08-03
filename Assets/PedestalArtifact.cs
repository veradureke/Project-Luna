using System.Collections;
using UnityEngine;

// Attach to each individual pickup object on a museum pedestal. Hooks into
// Meta Interaction SDK's grab events — wire it up using an
// InteractableUnityEventWrapper component on the same object as the
// Grabbable: add a listener on its "WhenSelect" UnityEvent pointing at
// this script's OnPickedUp() method.
//
// Builds its own "holding it in your hands" narration prompt, grounded in
// NASA documentation when available, and hands off entirely to
// GuideAIServiceHub — no Claude/local-LLM/TTS logic lives here anymore.
// (This replaces the old PedestalNarrationManager.cs — with GuideAIServiceHub
// now centralizing that pipeline for every interaction type, a separate
// pedestal-only manager was redundant.)
public class PedestalArtifact : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Name used for NASA documentation lookup and the narration prompt. " +
             "Leave blank to use this GameObject's name instead.")]
    public string objectName = "";

    [Header("Display")]
    public TMPro.TextMeshProUGUI responseText;
    public TMPro.TextMeshProUGUI statusText;

    [Header("Re-grab Cooldown")]
    [Tooltip("Minimum seconds between narrations triggered by THIS specific object. " +
             "Prevents spamming if it's quickly dropped and picked back up.")]
    public float regrabCooldown = 3f;

    [Header("Pickup Cue (optional)")]
    [Tooltip("Plays the instant this object is picked up, before narration starts.")]
    public AudioSource sfxSource;
    public AudioClip pickupChime;

    private float lastGrabTime = -999f;
    private bool _resolvedText;

    // Wire this to Meta Interaction SDK's "WhenSelect" event (via
    // InteractableUnityEventWrapper) on this object's Grabbable.
    public void OnPickedUp()
    {
        if (GuideBusyLock.IsBusy) return;
        if (Time.time - lastGrabTime < regrabCooldown) return;

        lastGrabTime = Time.time;

        // Replicas live in a model scene while the shared Response/Status Text
        // live on the Astronaut Guide in Persistent — a different scene, so the
        // Inspector refs can't be serialized across scenes. Resolve them once.
        ResolveTextIfNeeded();

        if (sfxSource != null && pickupChime != null)
            sfxSource.PlayOneShot(pickupChime);

        string nameForLookup = string.IsNullOrWhiteSpace(objectName) ? gameObject.name : objectName;

        if (statusText != null) statusText.text = "Examining " + nameForLookup + "...";

        // Ground in real NASA documentation if available, but unlike
        // AIInspector, don't refuse to narrate if nothing's found — a
        // hands-on museum moment still works with a slightly more general
        // sense of wonder rather than flatly saying "no documentation."
        string nasaContext = NASAKnowledgeBase.GetContext(nameForLookup);
        string contextSection = string.IsNullOrWhiteSpace(nasaContext)
            ? ""
            : "Here is real NASA documentation about this artifact for grounding:\n\n" +
              nasaContext + "\n\n";

        string prompt = "The visitor just picked up and is now physically holding a replica " +
                        "of the '" + nameForLookup + "' in their own hands.\n\n" +
                        contextSection +
                        "Describe what they're holding in 2-3 sentences, with a sense of " +
                        "wonder about touching something connected to space history. " +
                        "Mention its weight, texture, or feel where it fits naturally. If " +
                        "facts are given above, ground the description in only those — " +
                        "otherwise keep it general rather than inventing specifics. Don't " +
                        "reference 'the documentation' or 'the text' — speak as if you " +
                        "already know this. Don't open with a greeting or filler like " +
                        "'Sure!' — start right in on what they're holding.";

        // Acknowledge the visitor first, then think, then narrate.
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

    // The guide's Response/Status Text live in the Persistent scene; resolve them
    // by name at runtime (once) when the serialized cross-scene refs are empty.
    void ResolveTextIfNeeded()
    {
        if (_resolvedText) return;
        _resolvedText = true;
        if (responseText != null && statusText != null) return;
        foreach (var t in FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (responseText == null && t.gameObject.name == "Response Text") responseText = t;
            if (statusText == null && t.gameObject.name == "Status Text") statusText = t;
        }
    }
}