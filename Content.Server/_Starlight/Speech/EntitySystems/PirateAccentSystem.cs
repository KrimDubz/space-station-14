using System.Linq;
using Content.Server.Speech.Components;
using Content.Server.Speech.EntitySystems;
using Content.Shared.Speech;
using Robust.Shared.Random;
using System.Text.RegularExpressions;

namespace Content.Server._Starlight.Speech.EntitySystems;

public sealed partial class PirateAccentSystem : EntitySystem
{
    [GeneratedRegex(@"^(\S+)")]
    private static partial Regex FirstWordAllCapsRegex();

    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PirateAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    public (string message, string tts) Accentuate(string message, string ttsMessage, PirateAccentComponent component)
    {
        var (msg, tts) = _replacement.ApplyReplacements((message, ttsMessage), "pirate");

        if (!_random.Prob(component.YarrChance))
            return (msg, tts);

        var firstWordAllCaps = !FirstWordAllCapsRegex().Match(msg).Value.Any(char.IsLower);

        var pick = _random.Pick(component.PirateWords);
        var pirateWord = Loc.GetString(pick);

        if (!firstWordAllCaps)
        {
            msg = msg[0].ToString().ToLower() + msg[1..];
            if(tts.Length == msg.Length) 
                tts = tts[0].ToString().ToLower() + tts[1..];
        }
        else
        {
            pirateWord = pirateWord.ToUpper();
        }

        msg = pirateWord + " " + msg;
        tts = pirateWord + " " + tts;

        return (msg, tts);
    }

    private void OnAccentGet(EntityUid uid, PirateAccentComponent component, AccentGetEvent args) 
        => (args.Message, args.TTSMessage) = Accentuate(args.Message, args.TTSMessage, component);
}
