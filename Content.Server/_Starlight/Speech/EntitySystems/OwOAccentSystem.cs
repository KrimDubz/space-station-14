using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.StatusEffectNew;

namespace Content.Server._Starlight.Speech.EntitySystems;

public sealed class OwOAccentSystem : EntitySystem
{
    private static readonly IReadOnlyDictionary<string, string> _specialWords = new Dictionary<string, string>()
    {
        { "you", "wu" },
        { "are", "r" },
        { "hello", "mew" },
        { "love", "luv" },
        { "please", "plez" },
        { "food", "noms" },
        { "cute", "koot" },
        { "now", "meow" },
        { "look", "lookee" },
        { "little", "lil" },
    };

    public override void Initialize()
    {
        SubscribeLocalEvent<OwOAccentComponent, AccentGetEvent>(OnAccent);
        SubscribeLocalEvent<OwOAccentComponent, StatusEffectRelayedEvent<AccentGetEvent>>(OnAccentRelayed);
    }

    public (string text, string tts) Accentuate((string text, string tts) message)
    {
        foreach (var (word, repl) in _specialWords)
        {
            message.text = message.text.Replace(word, repl);
            message.tts = message.tts.Replace(word, repl);
        }
        message.text = message.text
            .Replace("r", "w").Replace("R", "W")
            .Replace("l", "w").Replace("L", "W");

        return message;
    }

    private void OnAccent(Entity<OwOAccentComponent> entity, ref AccentGetEvent args) 
        => (args.Message, args.TTSMessage) = Accentuate((args.Message, args.TTSMessage));

    private void OnAccentRelayed(Entity<OwOAccentComponent> entity, ref StatusEffectRelayedEvent<AccentGetEvent> args)
        => (args.Args.Message, args.Args.TTSMessage) = Accentuate((args.Args.Message, args.Args.TTSMessage));
}
