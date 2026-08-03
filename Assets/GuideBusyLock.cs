using UnityEngine;
using System.Text.RegularExpressions;

// Shared exclusivity lock so WaypointTour, AIInspector, and VoiceQA never
// talk over each other through the same guideAudioSource. Acquire before
// starting an interaction; release only once the response has actually
// finished playing (or the flow has failed/been skipped) — not just when
// the AI's text comes back — so two scripts can never overlap audio.
public static class GuideBusyLock
{
    // Shared timeout (seconds) for all Claude/Ollama/TTS/Whisper requests.
    // Without this, a UnityWebRequest can hang on a bad connection for the
    // platform default (often well over a minute) before failing, leaving
    // the guide stuck in its Thinking pose the whole time.
    public const int RequestTimeoutSeconds = 15;

    // Append this to any prompt sent to Claude or the local LLM — reduces
    // how often markdown formatting or em dashes show up in the first place,
    // so CleanResponseText below has less cleanup work to do.
    public const string PlainTextInstruction =
        " Respond in plain spoken sentences only — no markdown formatting " +
        "(no asterisks, bold, underscores, headers, or bullet points), and " +
        "do not use em dashes; use commas or periods instead.";

    private static readonly string[] thinkingPhrases =
    {
        "Let me think about that...",
        "Hmm, give me a moment...",
        "Let me see...",
        "One moment...",
        "Just a second...",
        "Let me consider that...",
    };

    public static string RandomThinkingPhrase()
    {
        return thinkingPhrases[Random.Range(0, thinkingPhrases.Length)];
    }

    // Said out loud (well, on-screen) the instant LUNA notices an interaction,
    // just before she starts "thinking" — a brief, spontaneous acknowledgment
    // so she feels like she reacted to the visitor rather than a loading spinner.
    private static readonly string[] acknowledgmentPhrases =
    {
        "Oh, this one's fascinating —",
        "Good eye —",
        "Let me tell you about this —",
        "This is one of my favorites —",
        "You picked a great one —",
        "Ah, great choice —",
        "Now this is special —",
        "Oh, I love this one —",
        "Great thing to point at —",
        "You're going to like this —",
    };

    public static string RandomAcknowledgmentPhrase()
    {
        return acknowledgmentPhrases[Random.Range(0, acknowledgmentPhrases.Length)];
    }

    public static bool IsBusy { get; private set; }
    private static Object owner;

    // Returns true if the lock was free and is now held by requester.
    public static bool TryAcquire(Object requester)
    {
        if (IsBusy) return false;
        IsBusy = true;
        owner = requester;
        return true;
    }

    // Only the script that acquired the lock can release it — guards
    // against a stray call from the wrong flow clearing someone else's lock.
    public static void Release(Object requester)
    {
        if (owner == requester)
        {
            IsBusy = false;
            owner = null;
        }
    }

    // Strips markdown artifacts and em/en dashes from AI responses before
    // they hit the display or TTS. This is a safety net for whatever gets
    // through despite PlainTextInstruction above — models don't always
    // follow formatting instructions perfectly, especially smaller local
    // models, so both layers matter together.
    public static string CleanResponseText(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        // Bold / italic / underline markers — strip the markers, keep the text.
        text = Regex.Replace(text, @"\*\*(.+?)\*\*", "$1");
        text = Regex.Replace(text, @"\*(.+?)\*", "$1");
        text = Regex.Replace(text, @"__(.+?)__", "$1");
        text = Regex.Replace(text, @"(?<!\w)_(.+?)_(?!\w)", "$1");

        // Markdown headers at the start of a line ("# ", "## ", etc.)
        text = Regex.Replace(text, @"^#+\s*", "", RegexOptions.Multiline);

        // Leading bullet/numbered list markers at the start of a line.
        text = Regex.Replace(text, @"^[\-\*]\s+", "", RegexOptions.Multiline);
        text = Regex.Replace(text, @"^\d+\.\s+", "", RegexOptions.Multiline);

        // Inline code backticks.
        text = text.Replace("`", "");

        // Em dashes and en dashes — replace with a comma so spoken/displayed
        // text reads as plain, direct prose without a stylistic dash.
        text = text.Replace("—", ", ").Replace("–", ", ");

        // Collapse any double-spacing/blank lines the replacements above left behind.
        text = Regex.Replace(text, @"[ \t]{2,}", " ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        return text.Trim();
    }
}