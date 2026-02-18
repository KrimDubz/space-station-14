using System.Linq;
using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Server.Speech.EntitySystems;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Speech.EntitySystems;

public sealed partial class MobsterAccentSystem : EntitySystem
{
    [GeneratedRegex(@"(?<=\w\w)(in)g(?!\w)", RegexOptions.IgnoreCase)]
    private static partial Regex RegexIng();

    [GeneratedRegex(@"(?<=\w)o[Rr](?=\w)")]
    private static partial Regex RegexLowerOr();

    [GeneratedRegex(@"(?<=\w)O[Rr](?=\w)")]
    private static partial Regex RegexUpperOr();

    [GeneratedRegex(@"(?<=\w)a[Rr](?=\w)")]
    private static partial Regex RegexLowerAr();

    [GeneratedRegex(@"(?<=\w)A[Rr](?=\w)")]
    private static partial Regex RegexUpperAr();

    [GeneratedRegex(@"^(\S+)")]
    private static partial Regex RegexFirstWord();

    [GeneratedRegex(@"(\S+)$")]
    private static partial Regex RegexLastWord();

    [GeneratedRegex(@"([.!?]+$)(?!.*[.!?])|(?<![.!?])$")]
    private static partial Regex RegexLastPunctuation();

    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobsterAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    public (string message, string tts) Accentuate(string message, string ttsMessage, MobsterAccentComponent component)
    {
        var (msg, tts) = _replacement.ApplyReplacements((message, ttsMessage), "mobster");

        // thinking -> thinkin'
        msg = RegexIng().Replace(msg, "$1'");

        // or -> uh and ar -> ah
        msg = RegexLowerOr().Replace(msg, "uh");
        tts = RegexLowerOr().Replace(tts, "uh");

        msg = RegexUpperOr().Replace(msg, "UH");
        tts = RegexUpperOr().Replace(tts, "UH");

        msg = RegexLowerAr().Replace(msg, "ah");
        tts = RegexLowerAr().Replace(tts, "ah");

        msg = RegexUpperAr().Replace(msg, "AH");
        tts = RegexUpperAr().Replace(tts, "AH");

        // Prefix
        if (_random.Prob(0.15f))
        {
            var firstWordAllCaps = !RegexFirstWord().Match(msg).Value.Any(char.IsLower);
            var pick = _random.Next(1, 2);
            var prefix = Loc.GetString($"accent-mobster-prefix-{pick}");

            if (!firstWordAllCaps)
            {
                msg = msg[0].ToString().ToLower() + msg.Remove(0, 1);
                tts = tts[0].ToString().ToLower() + tts.Remove(0, 1);
            }
            else
            {
                prefix = prefix.ToUpper();
            }

            msg = prefix + " " + msg;
            tts = prefix + " " + tts;
        }

        msg = msg[0].ToString().ToUpper() + msg.Remove(0, 1);
        tts = tts[0].ToString().ToUpper() + tts.Remove(0, 1);

        // Suffixes
        if (_random.Prob(0.4f))
        {
            var lastWordAllCaps = !RegexLastWord().Match(msg).Value.Any(char.IsLower);
            var suffix = component.IsBoss
                ? Loc.GetString($"accent-mobster-suffix-boss-{_random.Next(1, 4)}")
                : Loc.GetString($"accent-mobster-suffix-minion-{_random.Next(1, 3)}");

            if (lastWordAllCaps)
                suffix = suffix.ToUpper();

            msg = RegexLastPunctuation().Replace(msg, suffix);
            tts = RegexLastPunctuation().Replace(tts, suffix);
        }

        return (msg, tts);
    }

    private void OnAccentGet(EntityUid uid, MobsterAccentComponent component, AccentGetEvent args) 
        => (args.Message, args.TTSMessage) = Accentuate(args.Message, args.TTSMessage, component);
}
