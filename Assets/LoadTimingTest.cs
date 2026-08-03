using UnityEngine;
using TMPro;
using LLMUnity;

// Minimal isolation harness for measuring LLMUnity's on-device model-load /
// warmup time, with NO other LUNA systems present. Attach to a GameObject in
// the "LLMUnity_IsolationTest" scene and assign the LLMAgent + status label.
//
// It logs Time.realtimeSinceStartup immediately before calling Warmup() and
// again inside the completion callback, and mirrors the result to an on-canvas
// TextMeshProUGUI so the timing is readable inside the headset without a wired
// Logcat connection. Compare these numbers against the same logging added to
// GuideAIServiceHub in the full LUNA scene to tell whether a load hang is
// LLMUnity itself or resource contention from the full scene.
public class LoadTimingTest : MonoBehaviour
{
    [Tooltip("The LLMAgent that warms up the model. Its 'llm' must point at the " +
             "LLM component configured with the same model as the main scene.")]
    public LLMAgent llmAgent;

    [Tooltip("Status label shown in the headset: 'Loading...' then 'Loaded in X seconds'.")]
    public TextMeshProUGUI statusText;

    private float warmupStartTime;

    void Start()
    {
        if (llmAgent == null)
        {
            Debug.LogError("[LoadTimingTest] No LLMAgent assigned — cannot warm up.");
            if (statusText != null) statusText.text = "ERROR: no LLMAgent assigned";
            return;
        }

        if (statusText != null)
            statusText.text = "Loading...";

        // (a) Timestamp immediately before Warmup().
        warmupStartTime = Time.realtimeSinceStartup;
        Debug.Log($"[LoadTimingTest] Warmup() start — realtimeSinceStartup = {warmupStartTime:F2}s");

        _ = llmAgent.Warmup(OnModelWarmedUp);
    }

    void OnModelWarmedUp()
    {
        // (b) Timestamp inside the completion callback.
        float now = Time.realtimeSinceStartup;
        float elapsed = now - warmupStartTime;
        Debug.Log($"[LoadTimingTest] Warmup() complete — realtimeSinceStartup = {now:F2}s, " +
                  $"loaded in {elapsed:F2}s");

        // (c) Report to the on-canvas label so it's visible in-headset.
        if (statusText != null)
            statusText.text = $"Loaded in {elapsed:F1} seconds";
    }
}
