using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Shared.Speech;

namespace Content.Server._Starlight.Speech.EntitySystems;

public sealed partial class LizardAccentSystem : EntitySystem
{
    [GeneratedRegex("s+")]
    private static partial Regex RegexLowerS();

    [GeneratedRegex("S+")]
    private static partial Regex RegexUpperS();

    [GeneratedRegex(@"(\w)x")]
    private static partial Regex RegexInternalX();

    [GeneratedRegex(@"\bx([\-|r|R]|\b)")]
    private static partial Regex RegexLowerEndX();

    [GeneratedRegex(@"\bX([\-|r|R]|\b)")]
    private static partial Regex RegexUpperEndX();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LizardAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, LizardAccentComponent component, AccentGetEvent args)
    {
        // hissss
        args.Message = RegexLowerS().Replace(args.Message, "sss");
        // hiSSS
        args.Message = RegexUpperS().Replace(args.Message, "SSS");
        // ekssit
        args.Message = RegexInternalX().Replace(args.Message, "$1kss");
        // ecks
        args.Message = RegexLowerEndX().Replace(args.Message, "ecks$1");
        // eckS
        args.Message = RegexUpperEndX().Replace(args.Message, "ECKS$1");
    }
}
