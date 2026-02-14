using Content.Server.Speech.Components;
using Content.Server.Speech.EntitySystems;
using Content.Shared.Speech;

namespace Content.Server.Speech.EntitySystems;

public sealed class ChavAccentSystem : EntitySystem
{
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChavAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, ChavAccentComponent component, AccentGetEvent args)
    {
        (args.Message, args.TTSMessage) = _replacement.ApplyReplacements((args.Message, args.TTSMessage), "chav");

        args.Message = args.Message
            .Replace("th", "ff")
            .Replace("Th", "Ff")
            .Replace("TH", "FF");
    }
}
