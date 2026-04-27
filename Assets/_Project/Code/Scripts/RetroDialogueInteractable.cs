using UnityEngine;

public sealed class RetroDialogueInteractable : RetroInteractableBehaviour
{
    [Header("Dialogue")]
    [SerializeField] private string speakerName;
    [SerializeField, TextArea(1, 4)] private string[] lines = new string[0];
    [SerializeField] private bool randomizeLines;
    [SerializeField] private bool cycleLines = true;
    [SerializeField, Min(0.2f)] private float displayDuration = 3f;

    private int nextLineIndex;

    protected override string DefaultInteractionVerb => "Talk to";

    protected override void InteractInternal(in RetroInteractionContext context)
    {
        string line = PickLine();
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        string resolvedSpeaker = string.IsNullOrWhiteSpace(speakerName) ? gameObject.name : speakerName;
        string message = string.IsNullOrWhiteSpace(resolvedSpeaker) ? line : $"{resolvedSpeaker}: {line}";
        context.Interactor?.ShowStatusMessage(message, displayDuration);
    }

    private string PickLine()
    {
        if (lines == null || lines.Length == 0)
        {
            return string.Empty;
        }

        if (randomizeLines)
        {
            return lines[Random.Range(0, lines.Length)];
        }

        int index = Mathf.Clamp(nextLineIndex, 0, lines.Length - 1);
        string line = lines[index];
        if (cycleLines)
        {
            nextLineIndex = (index + 1) % lines.Length;
        }

        return line;
    }
}
