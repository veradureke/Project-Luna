using System.Collections.Generic;
using UnityEngine;

// Makes AstronautGuide follow the visitor as a real 3D companion. It stays a
// comfortable distance in front of the player and, when they move far enough that
// it's out of position, WALKS to catch up — moving at a constant walking pace and
// driving the Walking animation (via IsActivelyMoving) rather than gliding,
// snapping, or floating into place. It follows the player's yaw (Y) only, never
// pitch/roll, and never repositions while the guide is mid-narration
// (talking/thinking) so walking never fights those animator states.
public class GuideCompanionFollow : MonoBehaviour
{
    [Header("Player Reference")]
    [Tooltip("OVRCameraRig root — the companion's anchor position.")]
    public Transform player;
    [Tooltip("CenterEyeAnchor (the head). Its Y yaw sets which way 'in front' is, " +
             "and it's what the companion faces. Falls back to the body if empty.")]
    public Transform playerHead;
    [Tooltip("Optional start location; snapped here on Start and held until the " +
             "player first walks away.")]
    public Transform spawnPoint;

    [Header("Placement (in front of the visitor)")]
    [Tooltip("Metres in front of the player — 1.5-2 is comfortable.")]
    public float followDistance = 1.7f;
    [Tooltip("Angle from straight ahead, in degrees. Nonzero keeps the guide off " +
             "to the side so it doesn't block the exhibit straight ahead.")]
    public float angleOffset = 30f;
    [Tooltip("Vertical offset from the player anchor. With eye-level tracking the " +
             "anchor is at eye height, so a negative value stands the guide on the floor.")]
    public float heightOffset = -1.55f;

    [Header("Walk-to-reposition")]
    [Tooltip("Start walking once the guide is more than this far (m) from its ideal " +
             "spot — the leash. Below it, the guide just stands and faces you.")]
    public float leashDistance = 1.5f;
    [Tooltip("Stop walking once within this distance (m) of the ideal spot.")]
    public float stopDistance = 0.25f;
    [Tooltip("Walking movement speed (m/s). Match to the Walking animation's pace.")]
    public float walkSpeed = 1.4f;
    [Tooltip("How far (m) the player must first move before the guide leaves its " +
             "spawn spot and begins following.")]
    public float playerMoveThreshold = 0.4f;
    [Tooltip("Yaw turn speed (deg/s) for facing the player.")]
    public float rotateSpeed = 120f;

    [Header("Obstacle Avoidance")]
    [Tooltip("Layers the guide shouldn't stand inside. Nothing = disabled.")]
    public LayerMask obstacleLayers;
    public float obstacleCheckRadius = 0.4f;
    public float[] fallbackAngleOffsets = new float[] { -30f, 60f, -60f, 90f };

    [Header("Interactable Avoidance")]
    [Tooltip("Step aside when the guide would sit between the visitor and a nearby " +
             "LaserInspectable / GrabbableObject they're trying to point at or grab.")]
    public bool avoidBlockingInteractables = true;
    [Tooltip("Only interactables within this distance (m) of the visitor count as " +
             "'something they're interacting with' and worth stepping aside for.")]
    public float interactableRange = 4f;
    [Tooltip("How close (m) the guide's stance may come to the visitor->object sightline " +
             "before it counts as blocking — roughly the guide's shoulder half-width plus margin.")]
    public float sightlineClearance = 0.5f;
    [Tooltip("Ignore objects nearer than this (m) to the visitor, so something basically " +
             "in their hands doesn't trigger constant sidestepping.")]
    public float minInteractableDistance = 0.5f;
    [Tooltip("Seconds between rescans for nearby interactables (they change as model " +
             "scenes load/unload). Per-frame checks reuse the cached list for free.")]
    public float interactableRefreshInterval = 1f;

    // Read every frame by AstronautGuideAnimator to drive the IsWalking bool
    // (Idle <-> Walking). True only while actively walking to reposition.
    public bool IsActivelyMoving { get; private set; }

    // Fires once, when the guide finishes its first walk-in to the player. Used by
    // OpeningNarration to kick off the mission briefing.
    public System.Action OnArrivalComplete;

    Vector3 playerStartPosition;
    bool hasPlayerMoved;
    bool isRepositioning;      // hysteresis latch: true while walking toward the target
    bool hasCompletedArrival;
    bool wasActivelyMoving;

    // Throttled cache of nearby interactables (LaserInspectable / GrabbableObject).
    readonly List<Transform> interactables = new List<Transform>();
    float nextInteractableRefresh;

    void Start()
    {
        if (spawnPoint != null)
        {
            transform.position = spawnPoint.position;
            transform.rotation = spawnPoint.rotation;
        }
        if (player != null) playerStartPosition = player.position;
    }

    void Update()
    {
        if (player == null) return;

        // Never walk while the guide is talking/thinking/narrating — hold position
        // and just keep facing the visitor. Walking mid-narration would fight the
        // Talk/Think animator states and look broken. (GuideBusyLock is held for the
        // whole narration + TTS by GuideAIServiceHub.)
        if (GuideBusyLock.IsBusy)
        {
            IsActivelyMoving = false;
            isRepositioning = false;
            wasActivelyMoving = false;
            FacePlayer();
            return;
        }

        // Spawn-and-wait: hold at the spawn spot (just facing the player) until the
        // visitor first walks off, so the intro isn't the guide chasing on frame 1.
        if (!hasPlayerMoved)
        {
            if (Vector3.Distance(player.position, playerStartPosition) > playerMoveThreshold)
                hasPlayerMoved = true;
            else { IsActivelyMoving = false; FacePlayer(); return; }
        }

        float yaw = (playerHead != null ? playerHead.eulerAngles.y : player.eulerAngles.y);
        Vector3 ideal = ComputeIdealPosition(yaw, angleOffset);
        // A stance is only acceptable if it clears physical obstacles AND doesn't sit on
        // the visitor's line to something they're trying to interact with. If the default
        // "in front" spot fails either test, fall back through the side angles for the
        // first one that clears both — stepping the guide aside rather than blocking.
        if (!IsAcceptable(ideal))
        {
            foreach (float fb in fallbackAngleOffsets)
            {
                Vector3 cand = ComputeIdealPosition(yaw, fb);
                if (IsAcceptable(cand)) { ideal = cand; break; }
            }
        }

        float dist = Vector3.Distance(transform.position, ideal);

        // Hysteresis: begin walking once past the leash; keep walking until settled
        // within stopDistance. Prevents rapid start/stop jitter at the boundary.
        // Also step aside immediately — regardless of the leash — when the guide's
        // CURRENT spot blocks the visitor's line to a nearby interactable, so it clears
        // the exhibit they're pointing at or reaching for. The sidestep uses the exact
        // same constant-pace walk + IsActivelyMoving (Walking animation) as normal follow.
        bool blockingNow = BlocksInteractable(transform.position);
        if (!isRepositioning && (dist > leashDistance || blockingNow) && IsAcceptable(ideal))
            isRepositioning = true;

        if (isRepositioning)
        {
            // Constant walking pace toward the target (NOT SmoothDamp/glide), so it
            // reads as walking. The Walking animation plays via IsActivelyMoving.
            transform.position = Vector3.MoveTowards(
                transform.position, ideal, walkSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, ideal) <= stopDistance)
                isRepositioning = false;
        }
        IsActivelyMoving = isRepositioning;

        FacePlayer();

        // One-shot arrival: the first time it finishes a walk-in near the player.
        if (!hasCompletedArrival && hasPlayerMoved && wasActivelyMoving && !IsActivelyMoving)
        {
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.magnitude <= followDistance + leashDistance)
            {
                hasCompletedArrival = true;
                OnArrivalComplete?.Invoke();
            }
        }
        wasActivelyMoving = IsActivelyMoving;
    }

    // Face the player on the horizontal plane only (yaw) — never pitch/roll, so the
    // astronaut never tilts when the visitor looks up/down or tilts their head.
    void FacePlayer()
    {
        Vector3 facePoint = (playerHead != null ? playerHead.position : player.position);
        Vector3 lookDir = facePoint - transform.position;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.01f)
        {
            Quaternion target = Quaternion.LookRotation(lookDir);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, target, rotateSpeed * Time.deltaTime);
        }
    }

    // Ideal spot: followDistance ahead of the player along their yaw, offset to the
    // side by angleOffset, at heightOffset. Yaw-only — no pitch/roll leaks in.
    Vector3 ComputeIdealPosition(float yaw, float angle)
    {
        Quaternion rot = Quaternion.Euler(0f, yaw + angle, 0f);
        return player.position + rot * Vector3.forward * followDistance + Vector3.up * heightOffset;
    }

    bool IsBlocked(Vector3 position)
    {
        if (obstacleLayers.value == 0) return false; // unassigned = disabled
        return Physics.CheckSphere(position, obstacleCheckRadius, obstacleLayers);
    }

    // A candidate stance is acceptable only if it's clear of physical obstacles AND
    // doesn't sit between the visitor and something they're trying to interact with.
    bool IsAcceptable(Vector3 position)
    {
        return !IsBlocked(position) && !BlocksInteractable(position);
    }

    // True if a guide standing at guidePos would sit on the visitor's straight-line
    // view to any nearby interactable. Tested in the horizontal (XZ) plane because the
    // guide is a tall standing body: if it's on the sightline from above, it blocks the
    // view regardless of the object's height. Cheap — reuses a throttled cache scan.
    bool BlocksInteractable(Vector3 guidePos)
    {
        if (!avoidBlockingInteractables) return false;

        Transform headT = (playerHead != null ? playerHead : player);
        if (headT == null) return false;

        RefreshInteractablesIfNeeded();

        Vector3 head = headT.position; head.y = 0f;
        Vector3 g = guidePos; g.y = 0f;

        foreach (var t in interactables)
        {
            if (t == null) continue;
            Vector3 obj = t.position; obj.y = 0f;

            Vector3 toObj = obj - head;
            float objDist = toObj.magnitude;
            // Only things the visitor is plausibly interacting with: within range, and
            // not basically in their hands (which would cause constant sidestepping).
            if (objDist < minInteractableDistance || objDist > interactableRange) continue;

            Vector3 dir = toObj / objDist;
            float proj = Vector3.Dot(g - head, dir);
            // The guide must be BETWEEN the visitor and the object to be in the way.
            if (proj <= minInteractableDistance || proj >= objDist) continue;

            Vector3 onSightline = head + dir * proj;
            if (Vector3.Distance(g, onSightline) <= sightlineClearance) return true;
        }
        return false;
    }

    // Rescan the scene for LaserInspectable / GrabbableObject occasionally — they come
    // and go as model scenes load/unload. Between refreshes the cached list is reused
    // for free, so the per-frame BlocksInteractable checks stay cheap.
    void RefreshInteractablesIfNeeded()
    {
        if (Time.time < nextInteractableRefresh) return;
        nextInteractableRefresh = Time.time + Mathf.Max(0.1f, interactableRefreshInterval);

        interactables.Clear();
        foreach (var li in FindObjectsByType<LaserInspectable>(FindObjectsSortMode.None))
            interactables.Add(li.transform);
        foreach (var grab in FindObjectsByType<GrabbableObject>(FindObjectsSortMode.None))
            interactables.Add(grab.transform);
    }
}
