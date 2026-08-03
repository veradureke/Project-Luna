using UnityEngine;

// Attach to each Waypoint GameObject, alongside its new BoxCollider (must
// have "Is Trigger" checked). Detects the player walking into this specific
// zone and reports it to the shared WaypointTour script, which owns all the
// actual narration/AI/TTS/animator logic — this script only answers "did
// the player just enter this exact zone."
//
// IMPORTANT — for OnTriggerEnter to fire at all, Unity requires at least one
// side of the collision to have a Rigidbody. Box colliders alone on static
// waypoints won't trigger anything by themselves. Something on the player
// rig needs:
//   - A Collider (a CapsuleCollider on OVRCameraRig representing the
//     player's body is the right choice — NOT the hand anchors, since this
//     is about walking into a location, not reaching into one)
//   - A Rigidbody on that same object, with "Is Kinematic" checked (so
//     physics forces/gravity never actually move the VR rig — it only
//     exists so trigger events register)
//   - The Tag "Player" set on that GameObject, since OnTriggerEnter below
//     checks for that tag specifically to ignore anything else that might
//     wander through (exhibit colliders, hand colliders, etc.)
public class WaypointTriggerZone : MonoBehaviour
{
    [Tooltip("Name used in the narration prompt and NASA documentation lookup.")]
    public string waypointName;

    [Tooltip("The shared WaypointTour script — one instance handles every waypoint's " +
             "narration. Left unassigned it is found at runtime: the single WaypointTour " +
             "lives in the Persistent scene while this zone may live in an on-demand model " +
             "scene, and Unity can't serialize a cross-scene reference.")]
    public WaypointTour waypointTour;

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Resolve the shared WaypointTour at runtime when the Inspector reference
        // is empty (the common case now that it lives in a different scene from
        // this trigger). Cached on first hit so this only searches once.
        if (waypointTour == null)
            waypointTour = FindAnyObjectByType<WaypointTour>();

        if (waypointTour == null)
        {
            Debug.LogWarning("WaypointTriggerZone on " + gameObject.name +
                              " could not find a WaypointTour in any loaded scene.");
            return;
        }

        waypointTour.TryEnterWaypoint(waypointName);
    }
}