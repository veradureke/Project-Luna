using UnityEngine;
using UnityEngine.UI;
using TMPro;

// OVROverlayCanvas frames its render texture using the Canvas's own
// RectTransform, not Background's. Background's Content Size Fitter grows
// freely with response length, so long responses can exceed the Canvas's
// fixed size and get clipped outside OVROverlayCanvas's capture viewport.
// Call this right after setting responseText.text so the Canvas always
// covers the resolved content height.
public static class ChatBubbleLayout
{
    public static void FitCanvasToContent(TextMeshProUGUI responseText, float maxCanvasHeight = 4000f)
    {
        ContentSizeFitter fitter = responseText.GetComponentInParent<ContentSizeFitter>();
        if (fitter == null) return;
        RectTransform background = fitter.GetComponent<RectTransform>();

        LayoutRebuilder.ForceRebuildLayoutImmediate(background);

        Canvas canvas = responseText.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();

        Vector2 size = canvasRect.sizeDelta;
        size.y = Mathf.Min(background.rect.height, maxCanvasHeight);
        canvasRect.sizeDelta = size;
    }
}
