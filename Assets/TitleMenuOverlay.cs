using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Controller for the lightweight, re-enterable TitleMenu OVERLAY scene.
//
// Unlike the launch-only Title scene (driven by TitleScreenManager), the TitleMenu
// scene carries NO OVRCameraRig, NO EventSystem and NO bootstrap sequence. It rides
// on top of the already-running Persistent scene (which owns the rig, EventSystem
// and SceneTransitionManager), so it is safe to load/unload additively at ANY time
// in-session — e.g. from the nav "Return to Menu" item — with none of the
// duplicate-rig / duplicate-Persistent hazards that make the launch Title scene
// re-entry-unsafe.
//
// On load it:
//   (a) anchors the title composition (title art + waving astronaut + backdrop) in
//       front of the player's CURRENT head pose, so it reads the same as at launch
//       no matter where in the hub they've walked, and
//   (b) points the world-space menu canvas at the live rig camera so the controller
//       laser's world-UI raycast can click it.
// Picking an exhibit routes straight through the session SceneTransitionManager,
// then unloads this overlay so the title art and waving astronaut fully disappear.
public class TitleMenuOverlay : MonoBehaviour
{
    // One menu row: the world-space Button and the model scene it opens.
    [Serializable]
    public class MenuItem
    {
        public Button button;
        [Tooltip("Model scene to load, e.g. \"OrionScene\". Must be in Build Settings.")]
        public string modelScene;
    }

    [Header("Menu")]
    [Tooltip("One entry per exhibit — the same six as the launch Title menu.")]
    public MenuItem[] menuItems;

    [Header("Composition")]
    [Tooltip("Root holding the title canvas + waving astronaut + backdrop. Re-anchored " +
             "in front of the player on load. If empty, nothing is repositioned.")]
    public Transform contentRoot;
    [Tooltip("World-space menu canvas; its worldCamera is set to the live rig camera on load.")]
    public Canvas menuCanvas;
    [Tooltip("Where the player's head sat, in composition-local space, when this scene was " +
             "authored (the launch pose, eye height at the origin). Used to re-anchor the " +
             "whole composition to the player's current head pose.")]
    public Vector3 authoredHeadLocalPos = new Vector3(0f, 1.7f, 0f);

    [Tooltip("This overlay scene's name; unloaded after a selection is made.")]
    public string overlaySceneName = "TitleMenu";

    bool started;

    // While this overlay is up, the composition's OWN decorative waving astronaut is the
    // one on screen (matching the launch experience). Persistent's real, follow-behaviour
    // Astronaut Guide is still alive and would otherwise appear as a SECOND astronaut, so
    // we hide it for the duration and restore it the moment an exhibit is chosen.
    GameObject _hiddenRealGuide;

    void Awake()
    {
        if (menuItems == null) return;
        foreach (var item in menuItems)
        {
            if (item == null || item.button == null) continue;
            string scene = item.modelScene;              // capture per row
            item.button.onClick.AddListener(() => Select(scene));
        }
    }

    void Start()
    {
        Camera eyeCam = FindEyeCamera();

        // (b) Point the menu canvas at the live rig camera so the laser can click it.
        if (menuCanvas != null && eyeCam != null)
            menuCanvas.worldCamera = eyeCam;

        // (a) Re-anchor the composition in front of the player, reproducing the launch
        //     framing wherever they happen to be standing. Yaw only — never tilt.
        if (contentRoot != null && eyeCam != null)
        {
            Transform head = eyeCam.transform;
            Quaternion rot = Quaternion.Euler(0f, head.eulerAngles.y, 0f);
            contentRoot.rotation = rot;
            contentRoot.position = head.position - rot * authoredHeadLocalPos;
        }

        // Hide Persistent's real guide LAST — after the camera lookup above, which relies
        // on finding the (still-active) guide's playerHead. From here only the overlay's
        // decorative waver is visible, so exactly one astronaut shows while Title is up.
        HideRealGuide();
    }

    // Deactivate the real follow-behaviour Astronaut Guide (in Persistent) so it doesn't
    // show as a second astronaut behind the title composition.
    void HideRealGuide()
    {
        var follow = FindFirstObjectByType<GuideCompanionFollow>();
        if (follow == null) return;
        _hiddenRealGuide = follow.gameObject;
        _hiddenRealGuide.SetActive(false);
    }

    // Bring the real guide back so it resumes following once we leave the menu.
    void RestoreRealGuide()
    {
        if (_hiddenRealGuide == null) return;
        _hiddenRealGuide.SetActive(true);
        _hiddenRealGuide = null;
    }

    // Safety net: never leave the real guide stranded-hidden if this overlay goes away by
    // any path other than Select() (it should always be Select, but don't rely on it).
    void OnDestroy() => RestoreRealGuide();

    // The live head is in the Persistent scene, a different scene, so it can't be a
    // serialized reference. Prefer the guide's configured playerHead (CenterEyeAnchor),
    // then the tagged main camera, then any camera.
    Camera FindEyeCamera()
    {
        var follow = FindFirstObjectByType<GuideCompanionFollow>();
        if (follow != null && follow.playerHead != null)
        {
            var c = follow.playerHead.GetComponent<Camera>();
            if (c != null) return c;
        }
        if (Camera.main != null) return Camera.main;
        return FindFirstObjectByType<Camera>();
    }

    // Wired to each row's Button (public so an Inspector UnityEvent could drive it too).
    public void Select(string modelScene)
    {
        if (started) return;             // ignore double-clicks / a second item
        started = true;

        var stm = SceneTransitionManager.Instance;
        if (stm != null && !string.IsNullOrEmpty(modelScene))
            stm.GoToScene(modelScene);   // session-owned load/unload, unchanged

        // Drop this overlay so the title art + waving astronaut vanish immediately.
        Scene scene = SceneManager.GetSceneByName(overlaySceneName);
        if (scene.IsValid() && scene.isLoaded)
            SceneManager.UnloadSceneAsync(scene);
    }
}
