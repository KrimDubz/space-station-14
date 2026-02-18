using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Server.Speech.EntitySystems;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Speech.EntitySystems;

public sealed partial class SkeletonAccentSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;

    [GeneratedRegex(@"(?<!\w)[^aeiou]one", RegexOptions.IgnoreCase)]
    private static partial Regex BoneRegex();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SkeletonAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    public (string message, string tts) Accentuate(string msg, string tts, SkeletonAccentComponent component)
    {
        // bone replacements
        msg = BoneRegex().Replace(msg, "bone");
        tts = BoneRegex().Replace(tts, "bone");

        // apply word replacements
        (msg, tts) = _replacement.ApplyReplacements((msg, tts), "skeleton");

        // Suffix
        if (_random.Prob(component.ackChance))
        {
            var suffix = " " + Loc.GetString("skeleton-suffix");
            msg += suffix;
            tts += suffix;
        }

        return (msg, tts);
    }

    private void OnAccentGet(EntityUid uid, SkeletonAccentComponent component, AccentGetEvent args) 
        => (args.Message, args.TTSMessage) = Accentuate(args.Message, args.TTSMessage, component);
}
