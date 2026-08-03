using UnityEngine;

/// <summary>
/// Drives AstronautGuide's Animator parameters.
///
/// "IsTalking" updates automatically based on AudioSource playback (polling).
///
/// "IsThinking" is controlled externally (e.g. by VoiceQA / WaypointTour)
/// during the gap between an event happening and the guide's response audio
/// actually starting — bridges Whisper + Claude + TTS latency so the guide
/// doesn't just stand frozen in Idle during that wait.
///
/// Trigger() fires one-shot gestures (Acknowledge, Shrug) that play once
/// and return to whatever the Any State node would otherwise pick.
/// </summary>
[RequireComponent(typeof(Animator))]
public class AstronautGuideAnimator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("AudioSource that plays the guide's TTS/voice output.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Animator with the IsTalking / IsThinking parameters wired into the controller.")]
    [SerializeField] private Animator animator;

    [Tooltip("The GuideCompanionFollow on this same GameObject. Its IsActivelyMoving " +
             "state drives the IsMoving animator bool (Idle <-> Walking).")]
    [SerializeField] private GuideCompanionFollow companionFollow;

    [Header("Behavior")]
    [Tooltip("If true, automatically checks audioSource.isPlaying every frame. " +
             "Turn off if you're driving talking state manually via events instead.")]
    [SerializeField] private bool useAudioSourcePolling = true;

    [Tooltip("Name of the bool parameter in the Animator Controller for talking.")]
    [SerializeField] private string isTalkingParam = "IsTalking";

    [Tooltip("Name of the bool parameter in the Animator Controller for thinking.")]
    [SerializeField] private string isThinkingParam = "IsThinking";

    [Tooltip("Name of the bool parameter in the Animator Controller for walking/moving. " +
             "Driven from GuideCompanionFollow.IsActivelyMoving.")]
    [SerializeField] private string isMovingParam = "IsWalking";

    [Tooltip("How many seconds audioSource.isPlaying must read false continuously " +
             "before IsTalking is set to false. A small buffer (0.2s) prevents a " +
             "single frame gap between clip assignment and Play() from cutting the " +
             "Talk animation back to Idle mid-sentence.")]
    [SerializeField] private float stopTalkingDebounce = 0.2f;

    private bool _isTalking;
    private float _notPlayingTimer = 0f;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (companionFollow == null)
            companionFollow = GetComponent<GuideCompanionFollow>();
    }

    private void Update()
    {
        // Drive the walking state from the companion's movement, independently of
        // the audio-polling early-return below so it updates even when talking
        // polling is turned off.
        if (companionFollow != null && animator != null)
            animator.SetBool(isMovingParam, companionFollow.IsActivelyMoving);

        if (!useAudioSourcePolling || audioSource == null)
            return;

        bool currentlyPlaying = audioSource.isPlaying;

        if (currentlyPlaying)
        {
            // Audio is playing — reset the debounce timer and ensure
            // IsTalking is true immediately (no delay going into Talk state).
            _notPlayingTimer = 0f;
            if (!_isTalking)
            {
                _isTalking = true;
                animator.SetBool(isTalkingParam, true);
            }
        }
        else
        {
            // Audio stopped (or never started yet) — don't immediately flip
            // IsTalking off. Wait for the debounce window to confirm this
            // isn't just a single-frame gap between clip.set and Play().
            _notPlayingTimer += Time.deltaTime;
            if (_notPlayingTimer >= stopTalkingDebounce && _isTalking)
            {
                _isTalking = false;
                animator.SetBool(isTalkingParam, false);
            }
        }
    }

    /// <summary>
    /// Call this when the guide should enter/exit its "thinking" pose —
    /// typically true right when processing starts and false the moment
    /// the response audio actually begins (or immediately on failure,
    /// so it never gets stuck).
    /// </summary>
    public void SetThinking(bool thinking)
    {
        animator.SetBool(isThinkingParam, thinking);
    }

    /// <summary>
    /// Fires a one-shot Trigger parameter on the Animator — used for
    /// gestures like "Acknowledge" (something just happened, guide reacts)
    /// or "Shrug" (couldn't answer / something failed). The parameter name
    /// passed in must match a Trigger parameter that exists on the
    /// Animator Controller.
    /// </summary>
    public void Trigger(string paramName)
    {
        animator.SetTrigger(paramName);
    }

    /// <summary>
    /// Call this from Voice Manager when speech playback begins,
    /// if using the event-driven approach instead of polling.
    /// </summary>
    public void StartTalking()
    {
        _isTalking = true;
        animator.SetBool(isTalkingParam, true);
    }

    /// <summary>
    /// Call this from Voice Manager when speech playback ends,
    /// if using the event-driven approach instead of polling.
    /// </summary>
    public void StopTalking()
    {
        _isTalking = false;
        animator.SetBool(isTalkingParam, false);
    }
}