namespace Content.Shared._Starlight.Speech;

public sealed class SpeechMessage
{
    public required string Text { get; set; }
    public string? Tts { get; set; }
    public TTSModifier Modifier { get; set; } = TTSModifier.None;

    public static implicit operator SpeechMessage(string text) => new() { Text = text, Tts = text };
}

public enum TTSModifier
{
    None,
    Spell,
}
