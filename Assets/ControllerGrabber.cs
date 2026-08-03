using UnityEngine;

/// <summary>
/// Grip-to-grab for an OVR controller anchor. On grip press it grabs the
/// nearest <see cref="GrabbableObject"/> within reach, or (if none is in reach)
/// the one the controller is pointing at; the object follows the hand until the
/// grip is released.
/// </summary>
public class ControllerGrabber : MonoBehaviour
{
    public OVRInput.Controller controller = OVRInput.Controller.RTouch;
    public OVRInput.Button grabButton = OVRInput.Button.PrimaryHandTrigger;   // grip
    public float grabRadius = 0.15f;          // direct (touch) grab range
    public float rayGrabDistance = 20f;       // distance grab along controller forward (0 = disabled)
    public LayerMask grabbableMask = ~0;

    GrabbableObject held;

    void Update()
    {
        if (held == null && OVRInput.GetDown(grabButton, controller))
            TryGrab();
        else if (held != null && OVRInput.GetUp(grabButton, controller))
            Release();
    }

    void TryGrab()
    {
        GrabbableObject target = FindNearbyGrabbable();
        if (target == null && rayGrabDistance > 0f)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, transform.forward, out hit,
                    rayGrabDistance, grabbableMask, QueryTriggerInteraction.Collide))
                target = hit.collider.GetComponentInParent<GrabbableObject>();
        }
        if (target != null && !target.IsHeld) Grab(target);
    }

    GrabbableObject FindNearbyGrabbable()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, grabRadius,
            grabbableMask, QueryTriggerInteraction.Collide);
        GrabbableObject best = null;
        float bestDist = float.MaxValue;
        foreach (var col in cols)
        {
            var g = col.GetComponentInParent<GrabbableObject>();
            if (g == null || g.IsHeld) continue;
            float d = Vector3.Distance(transform.position, col.ClosestPoint(transform.position));
            if (d < bestDist) { bestDist = d; best = g; }
        }
        return best;
    }

    void Grab(GrabbableObject target)
    {
        held = target;
        target.OnGrabbed(this);
        target.transform.SetParent(transform, true);   // keep world pose, then follow the hand
    }

    void Release()
    {
        if (held == null) return;
        // OnReleased -> ReturnHome() detaches from the hand and re-homes the replica into
        // its OWN model scene at its pedestal pose. We no longer restore a grab-time parent
        // here: these replicas are scene-root objects, so the old SetParent(null) left them
        // stranded in the Persistent scene (the hand's scene) and they leaked everywhere.
        held.OnReleased(this);
        held = null;
    }

    // Let other systems (e.g. SceneTransitionManager, just before it unloads a scene)
    // force a held replica back home so it can never be carried into another scene.
    public void ForceRelease() => Release();

    void OnDisable()
    {
        if (held != null) Release();
    }
}
