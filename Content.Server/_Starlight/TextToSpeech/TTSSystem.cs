using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Content.Server._Starlight.Language;
using Content.Server._Starlight.Radio.Systems;
using Content.Server._Starlight.TextToSpeech;
using Content.Server.Chat.Systems;
using Content.Shared._Starlight.Language;
using Content.Shared.Chat;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Radio.Components;
using Content.Shared.Starlight;
using Content.Shared.Starlight.CCVar;
using Content.Shared.Starlight.TextToSpeech;
using Microsoft.CodeAnalysis.Differencing;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Content.Server.Starlight.TTS;

public sealed partial class TTSSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xforms = default!;
    [Dependency] private readonly RadioChimeSystem _chime = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ITTSClient _client = default!;
    [Dependency] private readonly IRobustRandom _rng = default!;
    [Dependency] private readonly LanguageSystem _language = default!;

    private readonly List<string> _sampleText =
    [
        "Can someone bring me a pair of insulating gloves, please?",
        "Security, the clown has stolen the captain's ID!",
        "The singularity has reached the arrivals area!",
    ];

    private const int DefaultAnnounceVoice = 510000;
    private const int DefaultVoice = 0;
    private const int MaxChars = 200;
    private const float WhisperVoiceVolumeModifier = 0.6f;
    private const int WhisperVoiceRange = 3;
    private readonly ISawmill _sawmill = Logger.GetSawmill(nameof(TTSSystem));
    private readonly List<ICommonSession> _ignoredRecipients = [];

    private bool _isEnabled;

    public override void Initialize()
    {
        _cfg.OnValueChanged(StarlightCCVars.TTSEnabled, v => _isEnabled = v, true);

        SubscribeNetworkEvent<PreviewTTSRequestEvent>(OnRequestPreviewTTS);
        SubscribeNetworkEvent<ClientOptionTTSEvent>(OnClientOptionTTS);

        SubscribeLocalEvent<TextToSpeechComponent, EntitySpokeEvent>(OnEntitySpoke);
        SubscribeLocalEvent<RadioSpokeEvent>(OnRadioReceiveEvent);
        SubscribeLocalEvent<CollectiveMindSpokeEvent>(OnCollectiveMindReceiveEvent);
        SubscribeLocalEvent<AnnouncementSpokeEvent>(OnAnnouncementSpoke);
        SubscribeLocalEvent<TransformSpeechEvent>(OnTransformSpeech);
    }

    private async void OnRequestPreviewTTS(PreviewTTSRequestEvent ev, EntitySessionEventArgs args)
    {
        if (!_isEnabled) return;

        await Task.Yield();
        try
        {
            if (!_prototypeManager.TryIndex<VoicePrototype>(ev.VoiceId, out var protoVoice))
                return;

            var previewText = _rng.Pick(_sampleText);
            var filter = Filter.SinglePlayer(args.SenderSession);

            await GenerateAndStream(TTSType.System, protoVoice.Voice, previewText, filter);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"TTS Preview error: {ex.Message}");
        }
    }

    private async void OnRadioReceiveEvent(RadioSpokeEvent args)
    {
        if (!_isEnabled
            || args.Message.Length > MaxChars
            || args.SuppressTTS)
            return;

        await Task.Yield();
        try
        {
            var text = CleanText(args.Message);
            _chime.TryGetSenderHeadsetChime(args.Source, out var chime);
            var filter = Filter.Entities(args.Receivers).RemovePlayers(_ignoredRecipients);
            var voice = GetOrAssignVoice(args.Source);

            await GenerateAndStream(TTSType.Radio, voice, text, filter, TTSEffect.Walkie, chime, args.Source);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"TTS Radio error: {ex.Message}");
        }
    }

    private async void OnCollectiveMindReceiveEvent(CollectiveMindSpokeEvent args)
    {
        if (!_isEnabled
            || args.Message.Length > MaxChars)
            return;

        await Task.Yield();
        try
        {
            var text = CleanText(args.Message);
            var filter = Filter.Entities(args.Receivers).RemovePlayers(_ignoredRecipients);
            var voice = GetOrAssignVoice(args.Source);

            await GenerateAndStream(TTSType.Mind, voice, text, filter, TTSEffect.Underwater);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"TTS Mind error: {ex.Message}");
        }
    }

    private async void OnAnnouncementSpoke(AnnouncementSpokeEvent args)
    {
        if (!_isEnabled
            || args.Message.Length > MaxChars * 2)
            return;

        await Task.Yield();
        try
        {
            var text = CleanText(args.Message);
            var filter = args.Receivers.RemovePlayers(_ignoredRecipients);
            var fallbackVoice = _prototypeManager.TryIndex(args.AnnounceVoice ?? "", out VoicePrototype? proto)
                ? proto.Voice
                : DefaultAnnounceVoice;
            var voice = args.SpeakerUid.HasValue
                ? GetOrAssignVoice(GetEntity(args.SpeakerUid.Value), fallbackVoice: fallbackVoice)
                : fallbackVoice;

            await GenerateAndStream(TTSType.Announcement, voice, text, filter, TTSEffect.Megaphone, args.AnnouncementSound);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"TTS Announcement error: {ex.Message}");
        }
    }

    private async void OnEntitySpoke(EntityUid uid, TextToSpeechComponent component, EntitySpokeEvent args)
    {
        if (!_isEnabled
            || args.Message.Length > MaxChars
            || !args.Language.SpeechOverride.RequireSpeech)
            return;

        await Task.Yield();
        try
        {
            var text = CleanText(args.Message);
            _chime.TryGetSenderHeadsetChime(args.Source, out var chime);
            var filter = args.IsWhisper
                ? Filter.Entities(args.Receivers).RemovePlayers(_ignoredRecipients)
                : ;
            var voice = GetOrAssignVoice(args.Source);


        }
        catch (Exception ex)
        {
            _sawmill.Error($"TTS Entity error: {ex.Message}");
        }

        if (!_isEnabled || args.Message.Length > MaxChars || !args.Language.SpeechOverride.RequireSpeech) return;

        if (args.IsWhisper)
        {
            HandleWhisper(uid, args.Message, voice, args.Language);
            return;
        }

        HandleSay(uid, args.Message, voice, args.Language);
    }

    private async Task GenerateAndStream(TTSType type, int voice, string text, Filter filter, TTSEffect effect = TTSEffect.None, SoundSpecifier? chime = null, EntityUid? SourceUid = null)
    {
        var id = Guid.NewGuid();
        RaiseNetworkEvent(new TTSHeaderEvent
        {
            Id = id,
            Type = type,
            Chime = chime,
            SourceUid = SourceUid.HasValue ? GetNetEntity(SourceUid.Value) : null,
        }, filter, false);

        await foreach (var chunk in _client.GenerateTTS(text, voice, effect))
            RaiseNetworkEvent(new TTSChunkEvent { Id = id, Data = chunk }, filter, false);
    }

    private async void OnClientOptionTTS(ClientOptionTTSEvent ev, EntitySessionEventArgs args)
    {
        if (ev.Enabled)
            _ignoredRecipients.Remove(args.SenderSession);
        else
            _ignoredRecipients.Add(args.SenderSession);
    }

    private async void HandleSay(EntityUid uid, string message, int voice, LanguagePrototype language)
    {
        var recipients = Filter.Pvs(uid, 1F).RemovePlayers(_ignoredRecipients);

        var soundData = await GenerateTTS(message, voice);

        if (soundData is null)
            return;

        foreach (var session in recipients.Recipients)
            if (session.AttachedEntity.HasValue
            && session.AttachedEntity != uid
            && !_language.CanUnderstand(session.AttachedEntity.Value, language.ID))
                recipients.RemovePlayer(session);

        if (TryComp<EyeComponent>(uid, out var eye) && eye is not null)
        {
            recipients.RemovePlayerByAttachedEntity(uid);

            if (_language.CanUnderstand(uid, language.ID))
            {
                RaiseNetworkEvent(new PlayTTSEvent
                {
                    Data = soundData,
                    SourceUid = GetNetEntity(eye.Target)
                }, Filter.Empty().FromEntities(uid), false);
            }
        }

        RaiseNetworkEvent(new PlayTTSEvent
        {
            Data = soundData,
            SourceUid = GetNetEntity(uid)
        }, recipients, false);
    }

    private async void HandleWhisper(EntityUid uid, string message, int voice, LanguagePrototype language)
    {
        var soundData = await GenerateTTS(message, voice);
        if (soundData is null)
            return;

        var transformQuery = GetEntityQuery<TransformComponent>();
        var sourcePos = _xforms.GetWorldPosition(transformQuery.GetComponent(uid), transformQuery);
        var receptions = Filter.Pvs(uid).Recipients;
        foreach (var session in receptions)
        {
            if (!session.AttachedEntity.HasValue
                || _ignoredRecipients.Contains(session))
                continue;

            if (!_language.CanUnderstand(session.AttachedEntity.Value, language.ID))
                continue;

            var transform = transformQuery.GetComponent(session.AttachedEntity.Value);
            var distance = (sourcePos - _xforms.GetWorldPosition(transform, transformQuery)).LengthSquared();

            if (distance > WhisperVoiceRange)
                continue;

            if (session.AttachedEntity == uid && TryComp<EyeComponent>(uid, out var eye) && eye is not null)
            {
                RaiseNetworkEvent(new PlayTTSEvent
                {
                    Data = soundData,
                    SourceUid = GetNetEntity(eye.Target)
                }, Filter.Empty().FromEntities(uid), false);
            }
            else
            {
                RaiseNetworkEvent(new PlayTTSEvent
                {
                    Data = soundData,
                    SourceUid = GetNetEntity(uid),
                    VolumeModifier = WhisperVoiceVolumeModifier * (1f - distance / WhisperVoiceRange)
                }, session);
            }
        }
    }

    private static string CleanText(string text)
    {
        text = TagStripperRegex().Replace(text, "");
        text = CharFilter().Replace(text, "");
        text = NumberConverter.NumberPattern().Replace(text, match => NumberConverter.Convert(match.Value));
        return text;
    }

    [GeneratedRegex(@"[^a-zA-Z0-9,.\-?! ]")]
    private static partial Regex CharFilter();

    [GeneratedRegex(@"\[[^\]]*\]")]
    private static partial Regex TagStripperRegex();
}
