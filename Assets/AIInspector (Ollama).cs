using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.Networking;
using UnityEngine.XR;

public class AIInspectorOllama : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI responseText;
    public Camera mainCam;
    public Transform rightControllerTransform;

    [Header("Ollama Settings")]
    public string ollamaUrl = "http://localhost:11434/api/chat";
    public string ollamaModel = "llama3";

    [Header("Fallback Claude API")]
    public string claudeApiKey = "YOUR_CLAUDE_KEY_HERE";
    public bool useClaude = false; // Toggle to switch between Ollama and Claude

    private bool isQuerying = false;
    private bool triggerWasPressed = false;

    // Conversation history
    private List<string> conversationHistory = new List<string>();
    private const int MAX_HISTORY = 6;

    void Start()
    {
        Debug.Log("AIInspectorOllama started!");
        Debug.Log("Using: " + (useClaude ? "Claude API" : "Ollama at " + ollamaUrl));
        if (mainCam == null)
            mainCam = Camera.main;
    }

    void Update()
    {
        // Editor: left mouse click
        bool mouseClick = Mouse.current != null &&
                          Mouse.current.leftButton.wasPressedThisFrame;

        // Quest 3: right controller index trigger
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
            {
                triggerWasPressed = false;
            }
        }

        if ((mouseClick || triggerClick) && !isQuerying)
        {
            Debug.Log("Input detected!");
            TryInspectObject();
        }
    }

    void TryInspectObject()
    {
        Ray ray;

        // On Quest 3 — raycast from controller tip
        if (rightControllerTransform != null && XRSettings.isDeviceActive)
        {
            ray = new Ray(
                rightControllerTransform.position,
                rightControllerTransform.forward);
            Debug.Log("Using controller ray");
        }
        // In editor — raycast from mouse position
        else if (Mouse.current != null)
        {
            if (mainCam == null)
            {
                Debug.LogError("No camera!");
                return;
            }
            ray = mainCam.ScreenPointToRay(Mouse.current.position.ReadValue());
            Debug.Log("Using mouse ray");
        }
        else
        {
            Debug.LogError("No input source!");
            return;
        }

        // Ignore ground layer
        int groundLayer = LayerMask.GetMask("Ground");
        int raycastMask = ~groundLayer;

        Debug.DrawRay(ray.origin, ray.direction * 20f, Color.green, 2f);

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, raycastMask))
        {
            Debug.Log("Hit: " + hit.collider.gameObject.name);
            StartCoroutine(AskAI(hit.collider.gameObject.name));
        }
        else
        {
            Debug.Log("Hit nothing");
        }
    }

    IEnumerator AskAI(string objectName)
    {
        isQuerying = true;
        responseText.text = "Inspecting " + objectName + "...";

        // RAG — get NASA context
        string nasaContext = NASAKnowledgeBase.GetContext(objectName);

        string prompt = "You are LUNA, an AI guide in a NASA Artemis VR experience. " +
                        "Use the following NASA documentation to answer accurately:\n\n" +
                        nasaContext + "\n\n" +
                        "The user is pointing at: '" + objectName + "'\n" +
                        "Give an exciting 2-3 sentence description grounded in the " +
                        "facts above. Speak directly to the visitor as their guide.";

        // Add to conversation history
        conversationHistory.Add("{\"role\":\"user\",\"content\":\"" +
            prompt.Replace("\"", "\\\"").Replace("\n", "\\n") + "\"}");

        if (conversationHistory.Count > MAX_HISTORY)
            conversationHistory.RemoveAt(0);

        string messagesJson = string.Join(",", conversationHistory);

        if (useClaude)
        {
            yield return StartCoroutine(AskClaude(messagesJson, objectName));
        }
        else
        {
            yield return StartCoroutine(AskOllama(messagesJson, objectName));
        }

        isQuerying = false;
    }

    IEnumerator AskOllama(string messagesJson, string objectName)
    {
        Debug.Log("Sending to Ollama...");

        string json = "{\"model\":\"" + ollamaModel + "\"," +
                      "\"messages\":[" + messagesJson + "]," +
                      "\"stream\":false}";

        UnityWebRequest request = new UnityWebRequest(ollamaUrl, "POST");
        byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string raw = request.downloadHandler.text;
            Debug.Log("Ollama raw response: " + raw);

            // Parse Ollama response
            int start = raw.IndexOf("\"content\":\"") + 11;
            int end = raw.IndexOf("\"", start);

            if (start > 10 && end > start)
            {
                string responseStr = raw.Substring(start, end - start)
                                        .Replace("\\n", "\n");

                // Add to history
                conversationHistory.Add("{\"role\":\"assistant\",\"content\":\"" +
                    responseStr.Replace("\"", "\\\"").Replace("\n", "\\n") + "\"}");

                responseText.text = responseStr;
                Debug.Log("Ollama responded!");
            }
            else
            {
                responseText.text = "Could not parse Ollama response.";
                Debug.Log("Parse error. Raw: " + raw);
            }
        }
        else
        {
            responseText.text = "Ollama Error: " + request.error +
                                "\nIs Ollama running on your PC?";
            Debug.Log("Ollama error: " + request.error);
            Debug.Log("Make sure Ollama is running: ollama run " + ollamaModel);
        }
    }

    IEnumerator AskClaude(string messagesJson, string objectName)
    {
        Debug.Log("Sending to Claude...");

        string json = "{\"model\":\"claude-sonnet-4-5\"," +
                      "\"max_tokens\":200," +
                      "\"messages\":[" + messagesJson + "]}";

        UnityWebRequest request = new UnityWebRequest(
            "https://api.anthropic.com/v1/messages", "POST");
        byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("x-api-key", claudeApiKey);
        request.SetRequestHeader("anthropic-version", "2023-06-01");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string raw = request.downloadHandler.text;
            int start = raw.IndexOf("\"text\":\"") + 8;
            int end = raw.IndexOf("\"", start);
            string responseStr = raw.Substring(start, end - start)
                                    .Replace("\\n", "\n");

            // Add to history
            conversationHistory.Add("{\"role\":\"assistant\",\"content\":\"" +
                responseStr.Replace("\"", "\\\"").Replace("\n", "\\n") + "\"}");

            responseText.text = responseStr;
            Debug.Log("Claude responded!");
        }
        else
        {
            responseText.text = "Claude Error: " + request.error;
            Debug.Log("Claude error: " + request.downloadHandler.text);
        }
    }
}