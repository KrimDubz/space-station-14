namespace Content.Shared.Speech;

public sealed class AccentGetEvent : EntityEventArgs
{
    /// <summary>
    ///     The entity to apply the accent to.
    /// </summary>
    public EntityUid Entity { get; }

    /// <summary>
    ///     The message to apply the accent transformation to.
    ///     Modify this to apply the accent.
    /// </summary>
    public string Message { get; set; }

    public string TTSMessage { get; set; } // Starlight

    public AccentGetEvent(EntityUid entity, string message, string ttsMessage) // Starlight
    {
        Entity = entity;
        Message = message;
        TTSMessage = ttsMessage; // Starlight
    }
}
