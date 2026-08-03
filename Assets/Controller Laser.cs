using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Controller laser pointer. Draws a line from the controller, places an
/// optional reticle at the hit point, and on trigger-press clicks world-space
/// UI (buttons, toggles, ...) or any world object implementing ILaserClickable.
/// </summary>
public class ControllerLaser : MonoBehaviour
{
    public LineRenderer lineRenderer;
    public float maxDistance = 20f;
    public LayerMask ignoreLayers;                       // physics layers to skip (e.g. Ground)
    public OVRInput.Controller controller = OVRInput.Controller.RTouch;
    public OVRInput.Button clickButton = OVRInput.Button.PrimaryIndexTrigger;
    public Transform reticle;                            // optional dot placed at the hit point
    public Color hitColor = Color.cyan;
    public Color missColor = new Color(1f, 1f, 1f, 0.35f);

    static readonly List<RaycastResult> s_UiResults = new List<RaycastResult>();

    void Reset()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    void Update()
    {
        Ray ray = new Ray(transform.position, transform.forward);

        // World (physics) hit.
        RaycastHit physicsHit;
        bool hasPhysics = Physics.Raycast(ray, out physicsHit, maxDistance,
            ~ignoreLayers.value, QueryTriggerInteraction.Ignore);
        float physicsDist = hasPhysics ? physicsHit.distance : float.MaxValue;

        // World-space UI hit.
        GameObject uiTarget; Vector3 uiPoint; Vector2 uiScreenPoint; float uiDist;
        bool hasUi = RaycastWorldUI(ray, maxDistance, out uiTarget, out uiPoint, out uiScreenPoint, out uiDist);

        // The laser points at whichever of the two is nearer.
        bool uiIsNearer = hasUi && uiDist <= physicsDist;
        bool hitSomething = hasPhysics || hasUi;
        Vector3 endPoint = uiIsNearer ? uiPoint
            : hasPhysics ? physicsHit.point
            : ray.GetPoint(maxDistance);

        DrawLaser(endPoint, hitSomething);

        // Drive UI hover/highlight from the laser. Standard Selectables (Button,
        // Toggle, ...) light up on OnPointerEnter/Exit, which the EventSystem only
        // dispatches for a real mouse pointer — so with a controller laser we send
        // those events ourselves whenever the pointed-at UI element changes.
        UpdateHover(uiIsNearer ? uiTarget : null, uiScreenPoint);

        if (OVRInput.GetDown(clickButton, controller))
        {
            if (uiIsNearer)
                ClickUi(uiTarget, uiScreenPoint);
            else if (hasPhysics)
            {
                var clickable = physicsHit.collider.GetComponentInParent<ILaserClickable>();
                if (clickable != null) clickable.OnLaserClick(this, physicsHit);
            }
        }
    }

    void DrawLaser(Vector3 endPoint, bool hit)
    {
        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, endPoint);
            Color c = hit ? hitColor : missColor;
            lineRenderer.startColor = c;
            lineRenderer.endColor = c;
        }
        if (reticle != null)
        {
            reticle.gameObject.SetActive(hit);
            if (hit) reticle.position = endPoint;
        }
    }

    // Intersects the ray with each world-space canvas plane and hit-tests its
    // graphics, returning the nearest UI element under the pointer.
    bool RaycastWorldUI(Ray ray, float maxDist, out GameObject target,
        out Vector3 worldPoint, out Vector2 screenPoint, out float distance)
    {
        target = null; worldPoint = Vector3.zero; screenPoint = Vector2.zero; distance = float.MaxValue;
        bool found = false;

        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
        foreach (var canvas in canvases)
        {
            if (!canvas.isActiveAndEnabled || canvas.renderMode != RenderMode.WorldSpace) continue;
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            var cam = canvas.worldCamera;
            if (raycaster == null || cam == null) continue;

            Plane plane = new Plane(canvas.transform.forward, canvas.transform.position);
            float enter;
            if (!plane.Raycast(ray, out enter) || enter > maxDist || enter >= distance) continue;

            Vector3 hitPoint = ray.GetPoint(enter);
            Vector2 sp = cam.WorldToScreenPoint(hitPoint);

            var ped = new PointerEventData(EventSystem.current) { position = sp };
            s_UiResults.Clear();
            raycaster.Raycast(ped, s_UiResults);
            if (s_UiResults.Count == 0) continue;

            target = s_UiResults[0].gameObject;
            worldPoint = hitPoint;
            screenPoint = sp;
            distance = enter;
            found = true;
        }
        return found;
    }

    void ClickUi(GameObject target, Vector2 screenPoint)
    {
        if (target == null) return;
        var ped = new PointerEventData(EventSystem.current)
        {
            position = screenPoint,
            button = PointerEventData.InputButton.Left
        };
        ExecuteEvents.ExecuteHierarchy(target, ped, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(target, ped, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(target, ped, ExecuteEvents.pointerClickHandler);
    }

    GameObject hoverTarget;

    // Sends pointerExit to the previously-hovered element and pointerEnter to the
    // newly-hovered one, but only when the target actually changes — so a
    // Selectable's Highlighted transition tracks the laser like a mouse cursor.
    void UpdateHover(GameObject newHover, Vector2 screenPoint)
    {
        if (newHover == hoverTarget) return;

        if (hoverTarget != null)
        {
            var exitData = new PointerEventData(EventSystem.current) { position = screenPoint };
            ExecuteEvents.ExecuteHierarchy(hoverTarget, exitData, ExecuteEvents.pointerExitHandler);
        }
        if (newHover != null)
        {
            var enterData = new PointerEventData(EventSystem.current) { position = screenPoint };
            ExecuteEvents.ExecuteHierarchy(newHover, enterData, ExecuteEvents.pointerEnterHandler);
        }
        hoverTarget = newHover;
    }

    // Clear any lingering highlight if this laser is switched off while pointing
    // at UI (e.g. the Title rig being deactivated on Start).
    void OnDisable()
    {
        if (hoverTarget != null)
        {
            var exitData = new PointerEventData(EventSystem.current);
            ExecuteEvents.ExecuteHierarchy(hoverTarget, exitData, ExecuteEvents.pointerExitHandler);
            hoverTarget = null;
        }
    }
}
