using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Small always-visible icon near the AstronautGuide. Laser-clicking it toggles
// the full scene-navigation panel open/closed. The panel auto-collapses after
// autoHideSeconds of inactivity, or immediately when any nav item is selected.
//
// Setup: attach this to the "Nav Menu Icon" child of AstronautGuide. Assign
// navMenuCanvas to the Nav Menu Canvas GameObject. The canvas starts hidden;
// this script hooks every Button in it to also call CollapseMenu() after selection.
public class NavMenuIcon : MonoBehaviour, ILaserClickable
{
    [Tooltip("The full navigation panel to show/hide on click.")]
    public GameObject navMenuCanvas;
    [Tooltip("Seconds of inactivity before the panel auto-collapses. 0 = never.")]
    public float autoHideSeconds = 8f;

    Coroutine _hideTimer;

    void Start()
    {
        if (navMenuCanvas == null) return;
        // Piggyback on every nav Button so the panel collapses after any selection.
        foreach (var btn in navMenuCanvas.GetComponentsInChildren<Button>(true))
            btn.onClick.AddListener(CollapseMenu);
        navMenuCanvas.SetActive(false);
    }

    public void OnLaserClick(ControllerLaser laser, RaycastHit hit)
    {
        if (navMenuCanvas == null) return;
        bool open = !navMenuCanvas.activeSelf;
        navMenuCanvas.SetActive(open);
        if (open)
            RestartHideTimer();
        else
            CancelHideTimer();
    }

    public void CollapseMenu()
    {
        CancelHideTimer();
        if (navMenuCanvas != null) navMenuCanvas.SetActive(false);
    }

    void RestartHideTimer()
    {
        CancelHideTimer();
        if (autoHideSeconds > 0f)
            _hideTimer = StartCoroutine(AutoHideRoutine());
    }

    void CancelHideTimer()
    {
        if (_hideTimer == null) return;
        StopCoroutine(_hideTimer);
        _hideTimer = null;
    }

    IEnumerator AutoHideRoutine()
    {
        yield return new WaitForSeconds(autoHideSeconds);
        CollapseMenu();
    }
}
