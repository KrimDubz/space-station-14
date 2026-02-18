using Content.Shared.StatusEffectNew;
using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Speech.EntitySystems;

public sealed class BarkAccentSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly IReadOnlyList<string> _barks = new List<string>{
        " Woof!", " WOOF", " wof-wof"
    }.AsReadOnly();

    private static readonly IReadOnlyDictionary<string, string> _specialWords = new Dictionary<string, string>()
    {
        { "ah", "arf" },
        { "Ah", "Arf" },
        { "oh", "oof" },
        { "Oh", "Oof" },
    };

    public override void Initialize()
    {
        SubscribeLocalEvent<BarkAccentComponent, AccentGetEvent>(OnAccent);
        SubscribeLocalEvent<BarkAccentComponent, StatusEffectRelayedEvent<AccentGetEvent>>(OnAccentRelayed);
    }

    public (string message, string ttsMessage) Accentuate(string message, string ttsMessage)
    {
        foreach (var (word, repl) in _specialWords)
        {
            message = message.Replace(word, repl);
            ttsMessage = ttsMessage.Replace(word, repl);
        }

        message = message.Replace("!", _random.Pick(_barks))
            .Replace("l", "r")
            .Replace("L", "R");

        ttsMessage = ttsMessage.Replace("!", " Woof!");

        return (message, ttsMessage);
    }

    private void OnAccent(Entity<BarkAccentComponent> entity, ref AccentGetEvent args)
        => (args.Message, args.TTSMessage) = Accentuate(args.Message, args.TTSMessage);

    private void OnAccentRelayed(Entity<BarkAccentComponent> entity, ref StatusEffectRelayedEvent<AccentGetEvent> args)
        => (args.Args.Message, args.Args.TTSMessage) = Accentuate(args.Args.Message, args.Args.TTSMessage);
}
