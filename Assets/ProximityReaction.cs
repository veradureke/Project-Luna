using UnityEngine;

// Additive, low-priority ambient behaviour for AstronautGuide (LUNA). Watches the
// visitor's distance — reusing the player Transform that GuideCompanionFollow
// already tracks — and occasionally has LUNA acknowledge them: a warm hello when
// they step in close, or a gentle nudge toward an exhibit if they linger nearby
// without interacting.
//
// It never queues. If LUNA is already narrating (GuideBusyLock.IsBusy) the
// reaction is skipped entirely — these are interruptible, throwaway behaviours,
// not critical interactions. Nothing here touches the follow / obstacle logic;
// it only reads GuideCompanionFollow.player.
//
// Distances are measured on the HORIZONTAL plane. The companion floats ~1.4m
// above the player and hovers ~1.2m out, so raw 3D distance never drops below
// ~1.4m — horizontal distance is the meaningful "how close is the visitor" signal.
//
// NOTE on conversation memory: GuideAIServiceHub uses single-turn stateless
// requests — no history is maintained between calls. Ambient asides therefore
// never pollute the visitor's real Q&A context by design, without needing a
// "remember" flag.
[RequireComponent(typeof(GuideCompanionFollow))]
public class ProximityReaction : MonoBehaviour
{
    [Header("Distances (metres, horizontal)")]
    [Tooltip("Visitor is right next to LUNA — triggers a close 'hello' " +
             "the moment they cross in.")]
    public float closeDistance = 0.9f;

    [Tooltip("Visitor is nearby. Standing (roughly still) inside this for " +
             "lingerTime seconds without interacting triggers a gentle nudge.")]
    public float lingerDistance = 2.5f;

    [Header("Timing (seconds)")]
    [Tooltip("How long the visitor must linger (still, nearby, nothing being " +
             "narrated) before LUNA nudges them.")]
    public float lingerTime = 12f;

    [Tooltip("Minimum gap between ANY two proximity reactions, so LUNA " +
             "doesn't repeat herself while the visitor hovers.")]
    public float reactionCooldown = 30f;

    [Tooltip("Walking faster than this (m/s) counts as 'moving', which resets " +
             "the linger timer — linger is meant for standing and examining, " +
             "not passing through.")]
    public float lingerMoveSpeed = 0.25f;

    [Header("Lines")]
    [Tooltip("Said when the visitor first steps in close. One is picked at " +
             "random and delivered through the normal narration pipeline.")]
    [TextArea]
    public string[] closeLines =
    {
        "Hey, good to have you here. Take a look around.",
        "Oh, come take a closer look, I don't bite.",
        "Glad you stopped by. There's a lot to see here.",
        "Right up close is my favourite place to talk. What catches your eye?",
    };

    [Tooltip("Gentle nudges when the visitor lingers without interacting. " +
             "Each should invite them to point at or approach a specific exhibit.")]
    [TextArea]
    public string[] lingerHints =
    {
        "That RS-25 engine over there has a story worth hearing, try pointing at it.",
        "The SLS rocket is worth a closer look. Point at it and I'll tell you about it.",
        "Take a look at the Orion capsule, point at it whenever you're curious.",
        "The Apollo Lunar Module is nearby. Point at it and I'll walk you through it.",
    };

    // Borrowed from GuideCompanionFollow — never duplicated.
    Transform player;

    // Edge-detect so the close line fires once per approach, not every frame.
    bool wasClose;

    // Seconds spent continuously lingering.
    float lingerTimer;

    // Shared cooldown across both reaction types.
    float lastReactionTime = -999f;

    // Previous-frame player position (flattened) for stationarity check.
    Vector3 lastPlayerFlat;

    void Start()
    {
        var follow = GetComponent<GuideCompanionFollow>();
        player = follow != null ? follow.player : null;

        if (player == null)
            Debug.LogWarning("ProximityReaction: GuideCompanionFollow has no " +
                             "player Transform assigned — proximity reactions disabled.");
        else
            lastPlayerFlat = Flat(player.position);
    }

    void Update()
    {
        if (player == null) return;

        Vector3 playerFlat = Flat(player.position);
        float distance = Vector3.Distance(playerFlat, Flat(transform.position));

        float playerSpeed = Time.deltaTime > 0f
            ? Vector3.Distance(playerFlat, lastPlayerFlat) / Time.deltaTime
            : 0f;
        lastPlayerFlat = playerFlat;

        // ---- Close approach: edge-triggered, fires once on entry. ----
        bool isClose = distance <= closeDistance;
        if (isClose && !wasClose)
            TryReact(closeLines);
        wasClose = isClose;

        // ---- Linger: accumulate time while nearby, still, and idle. ----
        bool lingering = distance <= lingerDistance
                      && playerSpeed <= lingerMoveSpeed
                      && !GuideBusyLock.IsBusy;
        if (lingering)
        {
            lingerTimer += Time.deltaTime;
            if (lingerTimer >= lingerTime && TryReact(lingerHints))
                lingerTimer = 0f;
        }
        else
        {
            lingerTimer = 0f;
        }
    }

    // Deliver a random line from the pool through the same pipeline every
    // other interaction uses (Hub chat -> TTS -> busy-lock).
    // Only fires if LUNA is free and the cooldown has elapsed.
    // Ambient asides are fire-and-forget (null callback) and stateless
    // (single-turn requests keep no history), so they never pollute Q&A context.
    // Returns true if a reaction was sent.
    bool TryReact(string[] pool)
    {
        if (pool == null || pool.Length == 0) return false;
        if (GuideBusyLock.IsBusy) return false;
        if (Time.time - lastReactionTime < reactionCooldown) return false;
        if (GuideAIServiceHub.Instance == null) return false;

        lastReactionTime = Time.time;

        string line = pool[Random.Range(0, pool.Length)];

        string prompt =
            "The visitor is near you but hasn't asked anything. Say this to them " +
            "as a brief, warm spoken aside in your own voice — one sentence, no " +
            "added facts, no greeting boilerplate. Use this line or a close " +
            "paraphrase: \"" + line + "\"";

        // Null callback — fire and forget. Busy-lock prevents overlap.
        GuideAIServiceHub.Instance.RequestNarration(prompt, null);

        return true;
    }

    // Horizontal position — height is irrelevant for proximity measurement.
    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}