using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Content.Client._Starlight.Radio.Systems;
using Content.Client._Starlight.TextToSpeech;
using Content.Shared.Starlight.CCVar;
using Content.Shared.Starlight.TextToSpeech;
using Robust.Client.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.TTS;

/// <summary>
/// Plays TTS audio
/// </summary>
public sealed class TextToSpeechSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedAudioSystem _sharedAudio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IAudioManager _audioManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly RadioChimeSystem _chime = default!;

    private readonly ConcurrentQueue<(Queue<byte[]> data, SoundSpecifier? specifier, float volume)> _ttsQueue = [];
    private ISawmill _sawmill = default!;
    private readonly MemoryContentRoot _contentRoot = new();
    private (EntityUid Entity, AudioComponent Component)? _currentPlaying;

    private float _volume;
    private float _radioVolume;
    private float _announceVolume;
    private bool _ttsQueueEnabled;

    public override void Initialize()
    {
        _sawmill = Logger.GetSawmill("tts");
        _cfg.OnValueChanged(StarlightCCVars.TTSVolume, OnTtsVolumeChanged, true);
        _cfg.OnValueChanged(StarlightCCVars.TTSAnnounceVolume, OnTtsAnnounceVolumeChanged, true);
        _cfg.OnValueChanged(StarlightCCVars.TTSRadioVolume, OnTtsRadioVolumeChanged, true);
        _cfg.OnValueChanged(StarlightCCVars.TTSRadioQueueEnabled, OnTtsRadioQueueChanged, true);
        _cfg.OnValueChanged(StarlightCCVars.TTSClientEnabled, OnTtsClientOptionChanged, true);
        SubscribeLocalEvent<TTSStream>(OnTTSStream);
        //SubscribeNetworkEvent<AnnounceTtsEvent>(OnAnnounceTTSPlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(StarlightCCVars.TTSVolume, OnTtsVolumeChanged);
        _cfg.UnsubValueChanged(StarlightCCVars.TTSAnnounceVolume, OnTtsAnnounceVolumeChanged);
        _cfg.UnsubValueChanged(StarlightCCVars.TTSRadioVolume, OnTtsRadioVolumeChanged);
        _cfg.UnsubValueChanged(StarlightCCVars.TTSRadioQueueEnabled, OnTtsRadioQueueChanged);
        _cfg.UnsubValueChanged(StarlightCCVars.TTSClientEnabled, OnTtsClientOptionChanged);
        _contentRoot.Dispose();
    }

    public void RequestPreviewTts(string voiceId)
        => RaiseNetworkEvent(new PreviewTTSRequestEvent() { VoiceId = voiceId });

    private void OnTtsVolumeChanged(float volume)
        => _volume = volume;

    private void OnTtsRadioVolumeChanged(float volume)
        => _radioVolume = volume;

    private void OnTtsRadioQueueChanged(bool enabled)
        => _ttsQueueEnabled = enabled;

    private void OnTtsAnnounceVolumeChanged(float volume)
        => _announceVolume = volume;

    private void OnTtsClientOptionChanged(bool option)
        => RaiseNetworkEvent(new ClientOptionTTSEvent { Enabled = option });

    //private void OnAnnounceTTSPlay(AnnounceTtsEvent ev)
    //    => _ttsQueue.Enqueue((ev.Data, ev.AnnouncementSound, _announceVolume));

    private void PlayQueue()
    {
        if (!_ttsQueue.TryDequeue(out var entry))
            return;

        var volume = SharedAudioSystem.GainToVolume(entry.volume);
        var finalParams = AudioParams.Default.WithVolume(volume);

        if (entry.specifier != null)
            _currentPlaying = _audio.PlayGlobal(_sharedAudio.ResolveSound(entry.specifier), EntityUid.Invalid, finalParams.AddVolume(-5f));
        _currentPlaying = PlayTTSBytes(entry.data, null, finalParams);
    }

    private void OnTTSStream(TTSStream ev)
    {
        var volume = ev.Type switch
        {
            TTSType.Announcement => _announceVolume,
            TTSType.Radio => _radioVolume,
            _ => _volume    
        };

        if (ev.Type == TTSType.Announcement || (ev.Type == TTSType.Radio && _ttsQueueEnabled))
        {
            _ttsQueue.Enqueue((ev.Data, !_chime.IsMuted ? ev.Chime : null, _radioVolume));
        }
        else
        {
            volume = SharedAudioSystem.GainToVolume(volume * ev.VolumeModifier);
            var audioParams = AudioParams.Default.WithVolume(volume);
            var entity = GetEntity(ev.SourceUid);

            if (!_chime.IsMuted && ev.Chime is SoundSpecifier chime)
                _currentPlaying = _audio.PlayGlobal(_sharedAudio.ResolveSound(chime), EntityUid.Invalid, audioParams.AddVolume(-3f));

            PlayTTSBytes(ev.Data, entity, audioParams);
        }
    }

    private (EntityUid Entity, AudioComponent Component)? PlayTTSBytes(
        Queue<byte[]> data,
        EntityUid? sourceUid = null,
        AudioParams? audioParams = null,
        float prependSilence = 0f,
        IStopwatch? stopwatch = null)
    {
        try
        {
            if(!data.TryDequeue(out var audioBytes))
            {
                _sawmill.Debug("queue is empty");
                return null;
            }

            if (audioBytes.Length < 10 || (sourceUid != null && sourceUid.Value.Id == 0))
                return null;

            _sawmill.Debug($"Play TTS chunk: {audioBytes.Length}, prependSilence: {prependSilence:F3}s");

            var @params = audioParams ?? AudioParams.Default;
            var audioStream = _audioManager.LoadAudioOggVorbis(new MemoryStream(audioBytes));

            var ent = sourceUid != null
                ? _audio.PlayEntity(audioStream, sourceUid.Value, null, @params)
                : _audio.PlayGlobal(audioStream, null, @params);

            if (ent != null)
            {
                var comp = EnsureComp<TTSAudioStreamComponent>(ent.Value.Entity);
                comp.Data = data;
                comp.SourceUid = sourceUid;
                comp.AudioParams = audioParams;
                var silencePadding = Math.Clamp(0.1f - (prependSilence + (float)(stopwatch?.Elapsed.TotalSeconds ?? 0)), 0f, 0.1f);
                ent.Value.Component.PlaybackPosition = silencePadding;
                _sawmill.Debug($"silencePadding: {silencePadding:F3}s");
            }

            return ent;
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Error playing TTS audio: {ex.Message}", ex);
        }

        return null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var toPlay = new List<(Queue<byte[]> Data, EntityUid? SourceUid, AudioParams? Params, float Silence)>();
        var query = EntityQueryEnumerator<TTSAudioStreamComponent, TimedDespawnComponent>();
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        while (query.MoveNext(out var uid, out var ttsComp, out var despawnComponent))
        {
            if (ttsComp.Handled)
                continue;
            var timeRemaining = despawnComponent.Lifetime - SharedAudioSystem.AudioDespawnBuffer - 0.2f;

            if (timeRemaining < 0.066f)
            {
                ttsComp.Handled = true;
                toPlay.Add((ttsComp.Data, ttsComp.SourceUid, ttsComp.AudioParams, timeRemaining));
            }
        }

        foreach (var (data, sourceUid, audioParams, silence) in toPlay)
            PlayTTSBytes(data, sourceUid, audioParams, silence, stopwatch);

        if (_currentPlaying.HasValue)
        {
            var (entity, _) = _currentPlaying.Value;

            if (Deleted(entity))
                _currentPlaying = null;
            else
                return;
        }

        PlayQueue();
    }
}
