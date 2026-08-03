using System.Collections;
using UnityEngine;

// Fixes the "correct in the Editor Game view, tiny/distant on Quest 3" title framing.
//
// The Title composition (menu canvas + waving astronaut + moon backdrop, all grouped
// under contentRoot) is authored around a head sitting at the world origin at eye
// height. In the Editor that's exactly where the fake CenterEyeAnchor sits, so it
// looks perfect. On real hardware the OVRCameraRig's CenterEyeAnchor is driven by the
// headset's TRACKED pose, which:
//   (a) is NOT valid/meaningful on the first frame (Start/Awake run before tracking), and
//   (b) gets moved by the OpenXR/OVR runtime's startup recenter a few frames in.
// So anchoring once at Start() — or leaving the composition at static world coords —
// leaves it wherever the player physically happens to stand relative to the tracking
// origin, usually off to the side and far away => "tiny and distant."
//
// This waits until the tracked head pose has STABILIZED (runtime initialized + recenter
// done), then places the composition in front of the actual head, and re-anchors if the
// head pose later JUMPS (a runtime recenter / teleport). Normal head sway stays below the
// jump threshold, so the menu doesn't swim with head movement.
public class TitleCompositionAnchor : MonoBehaviour
{
    [Tooltip("The tracked head — OVRCameraRig/TrackingSpace/CenterEyeAnchor of the Title rig.")]
    public Transform head;
    [Tooltip("Root holding the whole title composition (canvas + waving astronaut + backdrop).")]
    public Transform contentRoot;
    [Tooltip("Where the head sat, in composition-local space, when the scene was authored " +
             "(eye height at the origin). The composition is placed so the LIVE head maps here.")]
    public Vector3 authoredHeadLocalPos = new Vector3(0f, 1.7f, 0f);

    [Header("Tracking-stable gate")]
    [Tooltip("Head pose must move less than this (m) between frames to count as 'settled'.")]
    public float stableEpsilon = 0.005f;
    [Tooltip("Consecutive settled frames required before the first anchor.")]
    public int stableFrames = 6;
    [Tooltip("Give up waiting and anchor anyway after this long (s), so it never stalls " +
             "(also the Editor path, where the pose is stable from frame one).")]
    public float stableTimeout = 5f;
    [Tooltip("After the initial anchor, re-anchor if the head jumps more than this (m) from " +
             "the pose we anchored to — catches a runtime recenter/teleport. Keep above normal " +
             "head-sway so the composition doesn't swim.")]
    public float reanchorJump = 0.5f;

    Vector3 anchoredHeadPos;
    bool anchored;

    void OnEnable()
    {
        anchored = false;
        StartCoroutine(AnchorWhenStable());
    }

    // TEMPORARY diagnostic logging for the Title framing issue. Remove once resolved.
    void Start()
    {
        LogState("Start");
        StartCoroutine(LogStateAfterDelay());
    }

    IEnumerator LogStateAfterDelay()
    {
        yield return new WaitForSeconds(2f);
        LogState("+2s");
    }

    void LogState(string phase)
    {
        Canvas canvas = contentRoot != null ? contentRoot.GetComponentInChildren<Canvas>(true) : null;
        Transform astro = null;
        if (contentRoot != null)
            foreach (var t in contentRoot.GetComponentsInChildren<Transform>(true))
                if (t.name.ToLower().Contains("astronaut")) { astro = t; break; }

        if (head != null)
            Debug.Log($"[TITLE_DEBUG] ({phase}) CenterEyeAnchor worldPos={head.position} worldRot={head.eulerAngles}");
        else
            Debug.Log($"[TITLE_DEBUG] ({phase}) head (CenterEyeAnchor) is NULL");

        if (canvas != null)
            Debug.Log($"[TITLE_DEBUG] ({phase}) Canvas '{canvas.name}' worldPos={canvas.transform.position} " +
                      $"localScale={canvas.transform.localScale} renderMode={canvas.renderMode} planeDistance={canvas.planeDistance}");
        else
            Debug.Log($"[TITLE_DEBUG] ({phase}) Canvas not found under contentRoot");

        if (astro != null)
            Debug.Log($"[TITLE_DEBUG] ({phase}) AstronautGuide '{astro.name}' worldPos={astro.position} localScale={astro.localScale}");
        else
            Debug.Log($"[TITLE_DEBUG] ({phase}) AstronautGuide not found under contentRoot");

        if (head != null && canvas != null)
            Debug.Log($"[TITLE_DEBUG] ({phase}) distance camera->Canvas = {Vector3.Distance(head.position, canvas.transform.position):F3} m");
        if (head != null && astro != null)
            Debug.Log($"[TITLE_DEBUG] ({phase}) distance camera->AstronautGuide = {Vector3.Distance(head.position, astro.position):F3} m");
    }

    // Wait for the tracked head pose to stop changing frame-to-frame — i.e. the runtime
    // has initialized tracking and finished its startup recenter — bounded by a timeout so
    // the Editor (no real tracking; pose stable immediately) and any edge case still anchor.
    IEnumerator AnchorWhenStable()
    {
        if (head == null || contentRoot == null) yield break;

        float t = 0f;
        int settled = 0;
        Vector3 prev = head.position;
        while (t < stableTimeout)
        {
            yield return null;
            t += Time.unscaledDeltaTime;
            float delta = Vector3.Distance(head.position, prev);
            prev = head.position;
            settled = delta <= stableEpsilon ? settled + 1 : 0;
            if (settled >= stableFrames) break;
        }

        Anchor();
    }

    void Update()
    {
        // Re-anchor on a large head-pose jump (recenter / teleport) after the first anchor.
        if (!anchored || head == null) return;
        if (Vector3.Distance(head.position, anchoredHeadPos) > reanchorJump)
            Anchor();
    }

    // Place the composition so the live head maps to authoredHeadLocalPos, yaw-aligned to
    // the head — reproducing the authored framing wherever the player is actually standing.
    void Anchor()
    {
        if (head == null || contentRoot == null) return;
        Quaternion rot = Quaternion.Euler(0f, head.eulerAngles.y, 0f);
        contentRoot.rotation = rot;
        contentRoot.position = head.position - rot * authoredHeadLocalPos;
        anchoredHeadPos = head.position;
        anchored = true;
    }
}
