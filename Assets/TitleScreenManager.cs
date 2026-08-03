using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Lives ONLY in the Title scene. This is the launch entry point: the Title
// scene is the single scene in Build Settings at index 0, so it is all that
// auto-loads. The menu presents one item per explorable model; selecting one
// brings the rest of the app up in the background and drops the visitor
// straight into that model's scene:
//   1. LoadSceneAsync("Persistent", Additive)  -> spins up GuideAIServiceHub,
//      which kicks off LLM warmup, so the model loads WHILE the title is shown.
//   1b. On the FIRST selection of the session, once the guide is ready, LUNA
//       delivers a spoken welcome and we wait for it to finish before loading
//       the (heavy) model scene, so the greeting isn't cut off by the load.
//   2. LoadSceneAsync("HubScene", Additive)     -> the shared environment base
//      (ground, lighting, skybox); stays loaded as the base for the session.
//   3. LoadSceneAsync(<selected model scene>, Additive) -> e.g. "OrionScene".
//   4. UnloadSceneAsync("Title")                -> drops the title + its temp rig.
public class TitleScreenManager : MonoBehaviour
{
    // One menu entry: the Button the visitor clicks, the model scene it opens,
    // and the human-readable model name used in LUNA's welcome.
    [Serializable]
    public class MenuItem
    {
        [Tooltip("The world-space menu Button the visitor points at and clicks.")]
        public Button button;
        [Tooltip("Model scene this item loads additively, e.g. \"OrionScene\". " +
                 "Must be in Build Settings.")]
        public string modelScene;
        [Tooltip("Spoken model name for the welcome, e.g. \"Orion\".")]
        public string modelName;
    }

    [Header("Menu")]
    [Tooltip("One entry per model. Each stores its Button, the scene it opens, " +
             "and the spoken model name.")]
    public MenuItem[] menuItems;

    [Header("Title-only rig")]
    [Tooltip("The Title scene's own OVRCameraRig. Deactivated the instant a menu " +
             "item is picked so there are never two OVRCameraRigs / OVRManagers " +
             "alive at once when the Persistent rig loads. Optional.")]
    public GameObject titleRig;

    [Header("Title Scene Objects (hidden on selection)")]
    [Tooltip("World-space canvas showing the title and menu. Deactivated immediately " +
             "when a selection is made so it vanishes before scenes stream in. " +
             "The Title scene stays loaded (TitleScreenManager lives here) and is " +
             "properly unloaded at the end of LoadSequence.")]
    public GameObject titleCanvas;
    [Tooltip("Decorative waving astronaut in the title scene. Deactivated at the " +
             "same instant as titleCanvas so neither bleeds through while scenes stream in.")]
    public GameObject titleAstronautGuide;

    [Header("UI")]
    [Tooltip("Optional status/hint label shown while the scenes stream in.")]
    public TMPro.TextMeshProUGUI statusText;

    [Header("Loading")]
    [Tooltip("Minimum seconds the loading state is shown, so light scenes don't " +
             "load suspiciously fast compared to heavy ones. 0 = no minimum.")]
    public float minLoadDuration = 2.5f;

    [Header("Welcome")]
    [Tooltip("True until LUNA has delivered her one-time welcome. The first menu " +
             "selection triggers the welcome; later selections skip it.")]
    public bool isFirstSelection = true;
    [Tooltip("Max seconds to wait for the guide's LLM to warm up before giving up " +
             "on the welcome (so a failed model load can't strand the visitor).")]
    public float welcomeReadyTimeout = 60f;
    [Tooltip("Max seconds to wait for the welcome narration + speech to finish.")]
    public float welcomeSpeakTimeout = 45f;

    bool started;

    void Awake()
    {
        if (menuItems == null) return;
        foreach (var item in menuItems)
        {
            if (item == null || item.button == null) continue;
            // Capture the scene name per iteration so every listener keeps its own.
            string scene = item.modelScene;
            item.button.onClick.AddListener(() => Select(scene));
        }
    }

    // Called by a menu item's Button. Public so it can also be driven from an
    // ILaserClickable object or an Inspector-wired UnityEvent.
    public void Select(string modelScene)
    {
        if (started) return;          // ignore double-clicks / a second item
        started = true;
        StartCoroutine(LoadSequence(modelScene, LookupModelName(modelScene)));
    }

    string LookupModelName(string modelScene)
    {
        if (menuItems != null)
            foreach (var it in menuItems)
                if (it != null && it.modelScene == modelScene && !string.IsNullOrEmpty(it.modelName))
                    return it.modelName;
        return modelScene; // fallback: better than nothing in the prompt
    }

    IEnumerator LoadSequence(string modelScene, string modelName)
    {
        if (statusText != null) statusText.text = "Loading...";

        // Fully DESTROY the Title-only rig (not just deactivate) BEFORE Persistent
        // loads. Deactivating with SetActive(false) is NOT enough: a disabled
        // OVRManager still holds the OVRManager singleton, which then suppresses
        // the Persistent rig's OVRManager when it awakes — leaving the controllers
        // frozen and movement dead. Destroying the rig tears that singleton down so
        // the Persistent rig comes up clean, with no window where two rigs / two
        // OVRManagers are live at once. TitleScreenManager lives on 'Title Manager'
        // (not on the rig), so this coroutine keeps running after the rig is gone.
        if (titleRig != null)
        {
            Destroy(titleRig);
            yield return null; // let OnDestroy run so the OVRManager singleton clears
        }

        // 1. Persistent — brings up GuideAIServiceHub -> begins LLM warmup. With the
        //    Title rig already destroyed, this rig's OVRManager initializes cleanly.
        AsyncOperation persistent =
            SceneManager.LoadSceneAsync("Persistent", LoadSceneMode.Additive);
        while (persistent != null && !persistent.isDone) yield return null;

        // 1b. First-selection welcome — before loading the heavy model scene so
        //     the greeting plays uninterrupted. Only ever runs once per session.
        if (isFirstSelection)
        {
            isFirstSelection = false; // latch immediately so it can never repeat
            yield return StartCoroutine(PlayWelcome(modelName));
        }

        // 2. HubScene — the shared environment base (ground, lighting, skybox).
        //    Loaded once and kept for the whole session; model scenes swap on
        //    top of it via SceneTransitionManager.
        AsyncOperation hub =
            SceneManager.LoadSceneAsync("HubScene", LoadSceneMode.Additive);
        while (hub != null && !hub.isDone) yield return null;

        // 3. The specific model scene the visitor selected.
        if (!string.IsNullOrEmpty(modelScene))
        {
            AsyncOperation model =
                SceneManager.LoadSceneAsync(modelScene, LoadSceneMode.Additive);
            while (model != null && !model.isDone) yield return null;

            // Keep the session's SceneTransitionManager in sync so a later
            // GoToScene() knows this model is already loaded and unloads it
            // (never HubScene) when swapping.
            if (SceneTransitionManager.Instance != null)
                SceneTransitionManager.Instance.currentScene = modelScene;
        }

        // 4. Make HubScene the active scene so its lighting/skybox drives the
        //    view. It stays active as the base while models load/unload on top.
        Scene hubScene = SceneManager.GetSceneByName("HubScene");
        if (hubScene.IsValid() && hubScene.isLoaded)
            SceneManager.SetActiveScene(hubScene);

        // 5. Unload the Title scene (also destroys the Title-only rig + EventSystem).
        AsyncOperation unload = SceneManager.UnloadSceneAsync("Title");
        while (unload != null && !unload.isDone) yield return null;
    }

    // LUNA's one-time spoken welcome. GuideAIServiceHub lives in Persistent
    // (just loaded), so wait for it to exist AND finish warming up — otherwise
    // RequestNarration answers "model is still loading" instantly. Then wait for
    // GuideBusyLock to clear, i.e. the narration + TTS to fully finish. Both
    // waits are bounded so nothing can strand the visitor on the title screen.
    //
    // Two-astronaut handoff (Approach A): the astronaut the visitor SEES in the title
    // composition is the decorative waver (Astronaut Guide (Title)), but the real
    // narrator is the Persistent Astronaut Guide that the hub already drives
    // (guideAnimator + audioSource). To avoid a hidden second instance narrating while
    // the waver just idles, we PROMOTE the real astronaut into the composition for the
    // welcome — stand it exactly where the waver is (in front of the visitor), freeze
    // its follow so it delivers the greeting in place, and DESTROY the decorative waver
    // outright. After the welcome we re-enable follow, so from the model scenes on the
    // visitor sees only the one real, follow-behaviour astronaut (which then walks in and
    // triggers OpeningNarration's mission briefing on arrival, exactly as before).
    IEnumerator PlayWelcome(string modelName)
    {
        if (statusText != null) statusText.text = "Waking up your guide...";

        float t = 0f;
        while ((GuideAIServiceHub.Instance == null || !GuideAIServiceHub.Instance.IsModelReady)
               && t < welcomeReadyTimeout)
        {
            t += Time.deltaTime;
            yield return null;
        }

        var hub = GuideAIServiceHub.Instance;

        // --- Promote the REAL (Persistent) astronaut into the title composition. ---
        // No repoint is needed: the hub already targets this astronaut, so it animates
        // (mouth/gesture) and speaks. We just make it the one on screen.
        GuideCompanionFollow follow = null;
        if (hub != null && hub.guideAnimator != null)
        {
            GameObject realGuide = hub.guideAnimator.gameObject;
            follow = realGuide.GetComponent<GuideCompanionFollow>();
            // Freeze follow so LUNA stands and delivers the welcome in place instead of
            // walking off — this also keeps OnArrivalComplete (-> OpeningNarration) from
            // firing mid-welcome.
            if (follow != null) follow.enabled = false;
            // Stand her exactly where the visitor is already looking: the decorative
            // waver's (head-anchored) spot, facing the same way.
            if (titleAstronautGuide != null)
                realGuide.transform.SetPositionAndRotation(
                    titleAstronautGuide.transform.position, titleAstronautGuide.transform.rotation);
        }

        // Retire the decorative Title waver for good — fully DESTROYED, not just hidden —
        // so there is never a second astronaut lingering behind the model scene.
        if (titleAstronautGuide != null)
        {
            Destroy(titleAstronautGuide);
            titleAstronautGuide = null;
        }

        // If the guide never became ready, skip the narration (the decorative waver is
        // already retired above); restore follow so LUNA behaves normally going forward.
        if (hub == null || !hub.IsModelReady)
        {
            Debug.LogWarning("TitleScreenManager: guide not ready in time — skipping welcome.");
            if (follow != null) follow.enabled = true;
            yield break;
        }

        string prompt =
            "You are LUNA, the AI guide at NASA Glenn Research Center's Artemis " +
            "lunar exhibit. A brand-new visitor has just arrived and chosen to " +
            "explore the " + modelName + ". Warmly welcome them to Project LUNA in " +
            "2-3 short spoken sentences, and let them know you'll be exploring the " +
            modelName + " together. Speak directly to the visitor — no greeting " +
            "filler like 'Sure!', and no markdown.";

        if (statusText != null) statusText.text = "LUNA is welcoming you...";

        hub.RequestNarration(prompt);

        // Let the request acquire the busy lock, then wait it out (text + speech).
        yield return null;
        float spoke = 0f;
        while (GuideBusyLock.IsBusy && spoke < welcomeSpeakTimeout)
        {
            spoke += Time.deltaTime;
            yield return null;
        }

        // Hand back to follow behaviour for the model scenes.
        if (follow != null) follow.enabled = true;
    }
}
