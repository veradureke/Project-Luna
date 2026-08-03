using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Lives in the Persistent scene, so it survives for the whole session (unlike
// TitleScreenManager, which is unloaded with the Title scene). It owns the
// "which mission-hardware scene is currently showing" state and swaps them:
// load the requested model scene additively, then unload whichever model scene
// was previously current. Persistent and HubScene are never touched here.
//
// Call SceneTransitionManager.Instance.GoToScene("OrionScene") from a waypoint,
// a menu button, etc.
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Tooltip("The model scene currently loaded, if any. Empty = none yet.")]
    public string currentScene = "";

    bool busy;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // Load the requested model scene additively and unload the previous one.
    // Safe to call repeatedly; ignores redundant/again-in-flight requests.
    public void GoToScene(string sceneName)
    {
        if (busy) return;
        if (string.IsNullOrEmpty(sceneName)) return;
        if (sceneName == currentScene) return; // already showing it
        StartCoroutine(SwapRoutine(sceneName));
    }

    // Unload the current model scene without loading a new one (back to Hub-only).
    public void ClearCurrent()
    {
        if (busy) return;
        if (string.IsNullOrEmpty(currentScene)) return;
        StartCoroutine(SwapRoutine(null));
    }

    IEnumerator SwapRoutine(string sceneName)
    {
        busy = true;

        string previous = currentScene;

        // Load the new scene first (if any) so we never sit on an empty frame.
        if (!string.IsNullOrEmpty(sceneName))
        {
            Scene target = SceneManager.GetSceneByName(sceneName);
            if (!target.isLoaded)
            {
                AsyncOperation load =
                    SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                while (load != null && !load.isDone) yield return null;
            }
        }

        // Track the new current before unloading, so state is correct even if
        // the caller inspects it mid-unload.
        currentScene = sceneName ?? "";

        // Safety: if the visitor is still holding a replica from the outgoing scene,
        // put it back BEFORE we unload. Releasing re-homes the replica into 'previous'
        // (still loaded at this point), which then unloads with it — so a hand-parented
        // replica can never be carried into the next scene or the Title menu.
        foreach (var grabber in FindObjectsByType<ControllerGrabber>(FindObjectsSortMode.None))
            grabber.ForceRelease();

        // Unload whichever model scene was previously showing.
        if (!string.IsNullOrEmpty(previous) && previous != sceneName)
        {
            Scene prev = SceneManager.GetSceneByName(previous);
            if (prev.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(previous);
                while (unload != null && !unload.isDone) yield return null;
            }
        }

        busy = false;
    }
}
