using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Server.Speech.EntitySystems;
using Content.Shared.Speech;

namespace Content.Server._Starlight.Speech.EntitySystems;

public sealed partial class FrenchAccentSystem : EntitySystem
{
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;

    [GeneratedRegex(@"th", RegexOptions.IgnoreCase)]
    private static partial Regex RegexTh();

    [GeneratedRegex(@"(?<!\w)h", RegexOptions.IgnoreCase)]
    private static partial Regex RegexStartH();

    [GeneratedRegex(@"(?<=\w\w)[!?;:](?!\w)", RegexOptions.IgnoreCase)]
    private static partial Regex RegexSpacePunctuation();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FrenchAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    public (string message, string tts) Accentuate(string message, string ttsMessage, FrenchAccentComponent component)
    {
        var msg = message;
        var tts = ttsMessage;

        (msg, tts) = _replacement.ApplyReplacements((msg, tts), "french");

        // replaces h with ' at the start of words (visual only)
        msg = RegexStartH().Replace(msg, "'");

        // spaces out ! ? : and ;
        msg = RegexSpacePunctuation().Replace(msg, " $&");

        // replaces th with 'z or 's depending on the case
        msg = ApplyThReplacement(msg);
        return (msg, tts);
    }

    private static string ApplyThReplacement(string msg)
    {
        foreach (Match match in RegexTh().Matches(msg))
        {
            var uppercase = msg.Substring(match.Index, 2).Contains("TH");
            var Z = uppercase ? "Z" : "z";
            var S = uppercase ? "S" : "s";
            var idxLetter = match.Index + 2;

            if (msg.Length <= idxLetter)
            {
                msg = string.Concat(msg.AsSpan(0, match.Index), "'", Z);
            }
            else
            {
                var c = "aeiouy".Contains(msg.Substring(idxLetter, 1), StringComparison.CurrentCultureIgnoreCase) ? Z : S;
                msg = string.Concat(msg.AsSpan(0, match.Index), "'", c, msg.AsSpan(idxLetter));
            }
        }
        return msg;
    }

    private void OnAccentGet(EntityUid uid, FrenchAccentComponent component, AccentGetEvent args) 
        => (args.Message, args.TTSMessage) = Accentuate(args.Message, args.TTSMessage, component);
}
