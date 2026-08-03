using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.XR;

// Point-and-click object inspection. Detects what was clicked and hands the
// prompt off to GuideAIServiceHub — no AI/TTS logic lives here.
public class AIInspector : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI responseText;
    public Camera mainCam; // defaults to Camera.main if left empty
    public Transform rightControllerTransform;

    [Header("Response Pacing")]
    [Tooltip("Minimum seconds between accepted clicks/trigger-pulls.")]
    public float clickCooldown = 1.5f;

    private bool triggerWasPressed = false;
    private float lastClickTime = -999f;

    void Start()
    {
        if (mainCam == null) mainCam = Camera.main;
    }

    void Update()
    {
        bool mouseClick = Mouse.current != null &&
                          Mouse.current.leftButton.wasPressedThisFrame;

        // Quest controller trigger — 0.8 threshold so a light touch doesn't count.
        bool triggerClick = false;
        UnityEngine.XR.InputDevice rightHand =
            UnityEngine.XR.InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightHand.isValid)
        {
            float triggerValue = 0f;
            rightHand.TryGetFeatureValue(
                UnityEngine.XR.CommonUsages.trigger, out triggerValue);
            if (triggerValue > 0.8f && !triggerWasPressed)
            {
                triggerClick = true;
                triggerWasPressed = true;
            }
            else if (triggerValue <= 0.8f)
                triggerWasPressed = false;
        }

        // Must have input, guide must be free, and cooldown must have elapsed.
        if ((mouseClick || triggerClick) && !GuideBusyLock.IsBusy &&
            Time.time - lastClickTime >= clickCooldown)
        {
            lastClickTime = Time.time;
            TryInspectObject();
        }
    }

    void TryInspectObject()
    {
        Ray ray;

        // Controller wins over mouse whenever an XR device is active.
        if (rightControllerTransform != null && XRSettings.isDeviceActive)
        {
            ray = new Ray(rightControllerTransform.position, rightControllerTransform.forward);
        }
        else if (Mouse.current != null && mainCam != null)
        {
            ray = mainCam.ScreenPointToRay(Mouse.current.position.ReadValue());
        }
        else
        {
            Debug.LogError("No input source / camera available!");
            return;
        }

        // Exclude Ground so aiming near the floor doesn't out-compete exhibits.
        int groundLayer = LayerMask.GetMask("Ground");
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, ~groundLayer))
        {
            AskAboutObject(hit.collider.gameObject.name);
        }
    }

    void AskAboutObject(string objectName)
    {
        // Skip the AI call if there's no documentation — don't let it guess.
        string nasaContext = NASAKnowledgeBase.GetContext(objectName);
        if (string.IsNullOrWhiteSpace(nasaContext))
        {
            responseText.text = "I don't have documentation on that yet.";
            return;
        }

        string prompt = "A visitor just pointed at the '" + objectName + "' exhibit.\n\n" +
                        "Background on this exhibit:\n" + nasaContext + "\n\n" +
                        "Using only the facts above, describe it directly to the visitor in " +
                        "2-3 enthusiastic sentences, the way a NASA guide would explain it " +
                        "in person. Don't reference 'the documentation' or 'the text' — " +
                        "state the facts as something you already know. Don't open with a " +
                        "greeting or filler like 'Sure!' — start right in on the exhibit.";

        // Acknowledge the visitor first, then think, then narrate.
        StartCoroutine(AckThenNarrate(prompt));
    }

    // GuideAIServiceHub handles Claude/local-LLM fallback, TTS, and pacing once we hand off.
    IEnumerator AckThenNarrate(string prompt)
    {
        responseText.text = GuideBusyLock.RandomAcknowledgmentPhrase();
        yield return new WaitForSeconds(0.4f);
        responseText.text = GuideBusyLock.RandomThinkingPhrase();

        GuideAIServiceHub.Instance.RequestNarration(prompt, response =>
        {
            responseText.text = response;
            ChatBubbleLayout.FitCanvasToContent(responseText);
        });
    }
}