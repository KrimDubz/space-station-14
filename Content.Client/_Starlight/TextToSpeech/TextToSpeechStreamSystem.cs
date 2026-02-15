using Content.Shared.GameTicking;
using Content.Shared.Radio;
using Content.Shared.Starlight.TextToSpeech;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.TextToSpeech;

public sealed class TextToSpeechStreamSystem : EntitySystem
{
    private ISawmill _sawmill = default!;
    private readonly Dictionary<Guid, TTSStream> _streams = [];
    private HashSet<ProtoId<RadioChannelPrototype>> _ignore = [];

    public override void Initialize()
    {
        _sawmill = Logger.GetSawmill("tts.stream");

        SubscribeNetworkEvent<TTSHeaderEvent>(OnHeader);
        SubscribeNetworkEvent<TTSChunkEvent>(OnChunk);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnReset);
    }

    private void OnHeader(TTSHeaderEvent ev)
    {
        if (_streams.ContainsKey(ev.Id))
        {
            _sawmill.Warning("Duplicate TTS header received for {Id}", ev.Id);
            return;
        }

        if(ev.Channel != null && _ignore.Contains(ev.Channel.Value))
            return;

        var stream = new TTSStream
        {
            Id = ev.Id,
            SourceUid = ev.SourceUid,
            Chime = ev.Chime,
            Type = ev.Type,
            VolumeModifier = ev.VolumeModifier,
        };

        _streams[ev.Id] = stream;
        _sawmill.Debug("TTS stream started: {Id}", ev.Id);
    }

    private void OnChunk(TTSChunkEvent ev)
    {
        if (!_streams.TryGetValue(ev.Id, out var stream))
            return;

        if (ev.Data.Length == 0)
        {
            _sawmill.Debug("TTS stream completed: {Id}", ev.Id);
            _streams.Remove(ev.Id);
        }
        else
        {
            _sawmill.Debug("TTS stream {Id} received chunk of {Size} bytes", ev.Id, ev.Data.Length);
            stream.Data.Enqueue(ev.Data);
            if (!stream.IsStarted)
            {
                _sawmill.Debug("TTS stream started playback: {Id}", ev.Id);
                stream.IsStarted = true;
                RaiseLocalEvent(stream);
            }
        }
    }

    private void OnReset(RoundRestartCleanupEvent ev) => Reset();

    private void Reset() => _streams.Clear();
}
