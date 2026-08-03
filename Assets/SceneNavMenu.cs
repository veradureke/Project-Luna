using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// In-experience navigation panel: lets the visitor jump straight between exhibit
// (model) scenes, or return to the hub, without going back to the Title screen.
//
// Lives ONCE in the Persistent scene (attached under the OVRCameraRig so it
// travels with the player), so it's available in every model scene with zero
// per-scene duplication. Drives the Persistent-scene SceneTransitionManager,
// which is the single owner of model-scene load/unload.
public class SceneNavMenu : MonoBehaviour
{
    // One row of the panel: the Button plus the model scene it opens. Leave
    // modelScene blank for the "Return to Menu" row (unloads the current model
    // and drops back to the hub, keeping this panel available to pick again).
    [Serializable]
    public class NavItem
    {
        public Button button;
        [Tooltip("Model scene to load, e.g. \"OrionScene\". Blank = Return to Menu " +
                 "(unloads the current model, back to the hub base).")]
        public string modelScene;
    }

    [Tooltip("Rows of the panel. Same six exhibits as the Title menu, plus a " +
             "Return-to-Menu row with a blank modelScene.")]
    public NavItem[] items;

    [Tooltip("Re-enterable title/menu overlay scene brought up by the \"Return to Menu\" " +
             "row (blank modelScene). Must be in Build Settings. Leave empty to keep the " +
             "old behavior of just unloading the model back to the bare hub.")]
    public string titleMenuScene = "TitleMenu";

    void Awake()
    {
        if (items == null) return;
        foreach (var it in items)
        {
            if (it == null || it.button == null) continue;
            string scene = it.modelScene;               // capture per row
            it.button.onClick.AddListener(() => Navigate(scene));
        }
    }

    // Wired to each row's Button. Public so it can also be driven from an
    // Inspector UnityEvent or an ILaserClickable if ever needed.
    public void Navigate(string modelScene)
    {
        var stm = SceneTransitionManager.Instance;
        if (stm == null)
        {
            Debug.LogWarning("SceneNavMenu: no SceneTransitionManager in the scene.");
            return;
        }

        if (string.IsNullOrEmpty(modelScene))
        {
            // Return to Menu: unload the current model, then bring up the re-enterable
            // TitleMenu overlay (title art + waving astronaut + menu) on top of the hub.
            // The overlay rides on the live Persistent rig and unloads itself once the
            // visitor picks an exhibit, so it's safe to load/unload any number of times.
            stm.ClearCurrent();
            if (!string.IsNullOrEmpty(titleMenuScene) &&
                !SceneManager.GetSceneByName(titleMenuScene).isLoaded)
                SceneManager.LoadSceneAsync(titleMenuScene, LoadSceneMode.Additive);
        }
        else
            stm.GoToScene(modelScene);   // swap to the selected exhibit
    }
}
