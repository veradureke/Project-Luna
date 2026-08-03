using System.Collections;
using UnityEngine;
using TMPro;

// Proximity narration, triggered by WaypointTriggerZone's colliders.
// Hands the prompt off to GuideAIServiceHub — no AI/TTS logic lives here.
public class WaypointTour : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI responseText;
    public TextMeshProUGUI statusText;

    [Header("Entry Cue")]
    [Tooltip("Plays the instant the player enters a waypoint's zone, before narration starts.")]
    public AudioSource sfxSource;
    public AudioClip waypointChime;

    [Header("Response Pacing")]
    [Tooltip("Minimum seconds between any two waypoint triggers, regardless of which waypoint, " +
             "so walking between closely spaced exhibits doesn't fire narration back-to-back.")]
    public float retriggerCooldown = 3f;

    private float lastTriggerTime = -999f;

    // Called by WaypointTriggerZone when the player enters a waypoint's collider.
    public void TryEnterWaypoint(string locationName)
    {
        if (GuideBusyLock.IsBusy) return;
        if (Time.time - lastTriggerTime < retriggerCooldown) return;

        lastTriggerTime = Time.time;

        Debug.Log("Reached waypoint: " + locationName);
        statusText.text = "Arrived at " + locationName + "...";

        if (sfxSource != null && waypointChime != null)
            sfxSource.PlayOneShot(waypointChime);

        string prompt = "A visitor just walked up to the '" + locationName + "' exhibit.\n\n" +
                        "Describe it directly to them in 2-3 enthusiastic, scientifically " +
                        "accurate sentences, as if you already know this place well. " +
                        "Don't open with a greeting or filler like 'Sure!' — start right " +
                        "in on the exhibit.";

        // Acknowledge the visitor first, then think, then narrate.
        StartCoroutine(AckThenNarrate(prompt));
    }

    IEnumerator AckThenNarrate(string prompt)
    {
        statusText.text = GuideBusyLock.RandomAcknowledgmentPhrase();
        yield return new WaitForSeconds(0.4f);
        statusText.text = GuideBusyLock.RandomThinkingPhrase();

        GuideAIServiceHub.Instance.RequestNarration(prompt, response =>
        {
            responseText.text = response;
            statusText.text = "Walk to explore more locations";
        });
    }
}