using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions;

[System.Serializable]
public class NASADocument
{
    public string title;
    public string objectTag;
    public TextAsset textFile; // drag TXT file here
    [TextArea(5, 20)]
    public string content; // or paste content manually
}

public class NASADocumentLoader : MonoBehaviour
{
    [Header("NASA Documents")]
    public List<NASADocument> documents = new List<NASADocument>();

    public static NASADocumentLoader Instance;

    void Awake()
    {
        Instance = this;

        // Auto-load content from TextAsset files
        foreach (NASADocument doc in documents)
        {
            if (doc.textFile != null)
            {
                doc.content = doc.textFile.text;
                Debug.Log("Loaded: " + doc.title +
                          " (" + doc.content.Length + " chars)");
            }
        }
    }

    public string GetRelevantContext(string objectName)
    {
        objectName = objectName.ToLower();
        string context = "";

        foreach (NASADocument doc in documents)
        {
            if (objectName.Contains(doc.objectTag.ToLower()) ||
                doc.objectTag.ToLower().Contains(objectName))
            {
                context += "=== " + doc.title + " ===\n";
                context += doc.content + "\n\n";
            }
        }

        if (string.IsNullOrEmpty(context))
        {
            foreach (NASADocument doc in documents)
            {
                if (doc.objectTag.ToLower() == "artemis")
                {
                    context += "=== " + doc.title + " ===\n";
                    context += doc.content + "\n\n";
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(context))
            context = "NASA Moon Base: Building a sustained lunar presence " +
                      "at the South Pole as part of the Artemis program.";

        return context;
    }
}