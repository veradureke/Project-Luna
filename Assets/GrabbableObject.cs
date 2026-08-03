using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Marks a Rigidbody + Collider object as grabbable by <see cref="ControllerGrabber"/>.
/// While held the object is kinematic so it follows the hand smoothly. On release it
/// returns to its "home" pose — the pedestal spot it started at, in its OWN model scene.
///
/// The home pose is captured once at startup. It is essential for two reasons:
///  1. Snap-back: releasing glides the replica back onto its pedestal instead of
///     leaving it floating wherever it was dropped.
///  2. Scene integrity: grabbing re-parents the replica under the hand anchor, which
///     lives in the Persistent scene — so while held the replica moves INTO Persistent.
///     These replicas are root objects (no parent), so simply un-parenting on release
///     would leave them as roots of Persistent (which never unloads) and they would
///     leak into every later scene. ReturnHome() moves the object back into its home
///     model scene, so it always belongs to — and unloads with — the correct scene.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GrabbableObject : MonoBehaviour
{
    public bool keepKinematicOnRelease = true;
    [Tooltip("Seconds to glide back to the pedestal on release. 0 = snap instantly.")]
    public float returnDuration = 0.3f;

    Rigidbody _rb;
    public Rigidbody Rigidbody => _rb != null ? _rb : (_rb = GetComponent<Rigidbody>());

    public bool IsHeld { get; private set; }

    // Home = resting pedestal pose in the object's own model scene, captured at startup.
    Transform _homeParent;
    Vector3 _homeWorldPos;
    Quaternion _homeWorldRot;
    Scene _homeScene;
    bool _homeCaptured;
    Coroutine _returning;

    void Start() => CaptureHome();

    // Record where this replica belongs. Called at startup (and defensively on grab).
    public void CaptureHome()
    {
        if (_homeCaptured) return;
        _homeParent = transform.parent;
        _homeWorldPos = transform.position;
        _homeWorldRot = transform.rotation;
        _homeScene = gameObject.scene;
        _homeCaptured = true;
    }

    public void OnGrabbed(ControllerGrabber grabber)
    {
        CaptureHome();
        if (_returning != null) { StopCoroutine(_returning); _returning = null; }

        IsHeld = true;
        Rigidbody.isKinematic = true;   // follow the hand smoothly while held

        // Bridge the custom grab to the narration pipeline. PedestalArtifact builds
        // the RAG-grounded "you're holding this" prompt and hands off to
        // GuideAIServiceHub, but it was wired only to the Meta Interaction SDK's
        // WhenSelect event — which the custom ControllerGrabber never fires, so
        // grabbing produced no narration. PedestalArtifact's own re-grab cooldown
        // de-dupes if the SDK event ever fires too.
        var artifact = GetComponent<PedestalArtifact>();
        if (artifact != null) artifact.OnPickedUp();
    }

    public void OnReleased(ControllerGrabber grabber)
    {
        IsHeld = false;
        Rigidbody.isKinematic = keepKinematicOnRelease;
        ReturnHome();
    }

    /// <summary>
    /// Detach from the hand, put the replica back in its own model scene, and glide it
    /// to its pedestal pose. Safe to call even if the home scene was unloaded while the
    /// object was held (in which case the replica belongs nowhere and is destroyed).
    /// </summary>
    public void ReturnHome()
    {
        if (!_homeCaptured) return;

        // Home scene gone (unloaded while still held) -> this replica has no valid home
        // left; destroy it rather than let it dangle in Persistent / the active scene.
        if (!_homeScene.IsValid() || !_homeScene.isLoaded)
        {
            Destroy(gameObject);
            return;
        }

        // Reverse the grab re-parent: detach from the hand (keeping world pose), then
        // move back into the home model scene if we drifted out of it (we always do,
        // since the hand anchor lives in Persistent and these replicas are roots).
        transform.SetParent(_homeParent, true);
        if (_homeParent == null && gameObject.scene != _homeScene)
            SceneManager.MoveGameObjectToScene(gameObject, _homeScene);

        if (_returning != null) StopCoroutine(_returning);
        if (returnDuration <= 0f || !isActiveAndEnabled)
            transform.SetPositionAndRotation(_homeWorldPos, _homeWorldRot);
        else
            _returning = StartCoroutine(GlideHome());
    }

    IEnumerator GlideHome()
    {
        Vector3 p0 = transform.position;
        Quaternion r0 = transform.rotation;
        float t = 0f;
        while (t < returnDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / returnDuration);
            transform.SetPositionAndRotation(
                Vector3.Lerp(p0, _homeWorldPos, k), Quaternion.Slerp(r0, _homeWorldRot, k));
            yield return null;
        }
        transform.SetPositionAndRotation(_homeWorldPos, _homeWorldRot);
        _returning = null;
    }
}
