using System.Text;
using Content.Server.Speech.Components;
using Content.Server.Speech.EntitySystems;
using Content.Shared.Speech;

namespace Content.Server._Starlight.Speech.EntitySystems;

public sealed class RussianAccentSystem : EntitySystem
{
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;

    public override void Initialize() 
        => SubscribeLocalEvent<RussianAccentComponent, AccentGetEvent>(OnAccent);

    public (string message, string tts) Accentuate(string message, string ttsMessage)
    {
        var (msg, tts) = _replacement.ApplyReplacements((message, ttsMessage), "russian");

        // Visual cyrillic replacement (only for displayed text)
        var accentedMessage = new StringBuilder(msg);

        for (var i = 0; i < accentedMessage.Length; i++)
        {
            var c = accentedMessage[i];

            accentedMessage[i] = c switch
            {
                'A' => 'Д',
                'b' => 'в',
                'N' => 'И',
                'n' => 'и',
                'K' => 'К',
                'k' => 'к',
                'm' => 'м',
                'h' => 'н',
                't' => 'т',
                'R' => 'Я',
                'r' => 'я',
                'Y' => 'У',
                'W' => 'Ш',
                'w' => 'ш',
                _ => accentedMessage[i]
            };
        }

        return (accentedMessage.ToString(), tts);
    }

    private void OnAccent(EntityUid uid, RussianAccentComponent component, AccentGetEvent args) 
        => (args.Message, args.TTSMessage) = Accentuate(args.Message, args.TTSMessage);
}
