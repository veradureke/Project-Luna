using UnityEngine;

// Substitute for facial lipsync on a character with a sealed, opaque
// helmet — there's no visible mouth to drive blend shapes on, so instead
// this reads the guide's AudioSource amplitude in real time and pulses
// either a Light or an emissive material color (or both) to give a clear
// "this character is talking right now" cue.
//
// Reacts to the SAME shared guideAudioSource already used by AIInspector,
// VoiceQA, WaypointTour, and PedestalNarrationManager — no new audio
// wiring needed, just point this at the existing AudioSource.
public class GuideVisorGlow : MonoBehaviour
{
    [Header("Audio source to react to")]
    [Tooltip("The same shared guideAudioSource used everywhere else.")]
    public AudioSource guideAudioSource;

    [Header("Visual target — use either or both")]
    [Tooltip("Optional — a Light component representing the visor glow " +
             "(e.g. a small point/spot light placed at the helmet). " +
             "This is the simpler, more foolproof option — it works " +
             "regardless of material/shader setup or post-processing.")]
    public Light visorLight;

    [Tooltip("Optional — the visor's Renderer, if its material has an " +
             "emissive color slot you'd rather pulse instead of/alongside " +
             "a Light. REQUIRES the material's shader to have Emission " +
             "actually enabled — if it doesn't, this will run with no " +
             "visible effect. Also worth knowing: if Post Processing / " +
             "Bloom isn't enabled in your URP Volume (common on Quest " +
             "builds for performance), this will read as a brightness/" +
             "color shift rather than a dramatic bloom-style glow.")]
    public Renderer visorRenderer;
    [Tooltip("Which material slot on visorRenderer to affect, if it has multiple materials.")]
    public int materialIndex = 0;
    [Tooltip("Emission color at full glow intensity — match your visor's existing tint.")]
    public Color emissionColor = Color.cyan;

    [Header("Glow tuning")]
    [Tooltip("Idle intensity when not talking — keep above 0 so it's a dim glow, not pitch black.")]
    public float idleIntensity = 0.1f;
    [Tooltip("Maximum intensity reached at the loudest moment of speech.")]
    public float maxIntensity = 2f;
    [Tooltip("How quickly the glow follows amplitude changes. Higher = snappier/twitchier, lower = smoother/laggier.")]
    public float responsiveness = 12f;
    [Tooltip("Amplitude sample window size — must stay a power of 2.")]
    public int sampleWindow = 256;
    [Tooltip("Multiplies the raw RMS amplitude before clamping to 0-1 — speech RMS is " +
             "usually small, so this scales it up to actually reach noticeable brightness. " +
             "Tune by ear once you're testing with real TTS audio.")]
    public float amplitudeGain = 6f;

    private float[] samples;
    private float currentIntensity;
    private MaterialPropertyBlock propBlock;
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    void Start()
    {
        samples = new float[sampleWindow];
        propBlock = new MaterialPropertyBlock();
        currentIntensity = idleIntensity;
        ApplyIntensity(idleIntensity);
    }

    void Update()
    {
        float targetIntensity = idleIntensity;

        if (guideAudioSource != null && guideAudioSource.isPlaying)
        {
            float amplitude = GetCurrentAmplitude();
            targetIntensity = Mathf.Lerp(idleIntensity, maxIntensity, amplitude);
        }

        // Smooth toward the target rather than snapping, so the pulse
        // reads as a natural glow rather than a flickering strobe.
        currentIntensity = Mathf.Lerp(currentIntensity, targetIntensity, Time.deltaTime * responsiveness);
        ApplyIntensity(currentIntensity);
    }

    float GetCurrentAmplitude()
    {
        guideAudioSource.GetOutputData(samples, 0);
        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
            sum += samples[i] * samples[i];
        float rms = Mathf.Sqrt(sum / samples.Length);
        return Mathf.Clamp01(rms * amplitudeGain);
    }

    void ApplyIntensity(float intensity)
    {
        if (visorLight != null)
            visorLight.intensity = intensity;

        if (visorRenderer != null)
        {
            // MaterialPropertyBlock avoids creating a material instance or
            // editing the shared material asset directly — without this,
            // every other object using the same material would also glow,
            // and the change could persist outside Play mode.
            visorRenderer.GetPropertyBlock(propBlock, materialIndex);
            propBlock.SetColor(EmissionColorID, emissionColor * intensity);
            visorRenderer.SetPropertyBlock(propBlock, materialIndex);
        }
    }
}