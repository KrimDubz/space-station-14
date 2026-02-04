using System;
using System.Collections.Generic;
using System.IO;
using Robust.Shared.Log;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.TextToSpeech;

public sealed class PcmShortStream : IDisposable
{
    private const int SampleRate = 24000;
    private static readonly ISawmill _sawmill = Logger.GetSawmill(nameof(PcmShortStream));

    private readonly Queue<short[]> _sampleQueue = new();
    private short[]? _currentBatch;
    private int _currentPos;
    private bool _completed;
    private bool _disposed;

    public bool IsCompleted => _completed && _sampleQueue.Count == 0 && _currentBatch == null;

    public bool HasData => _currentBatch != null || _sampleQueue.Count > 0;

    public long Length { get; set; }

    public void WriteCompressedChunk(byte[] compressedData)
    {
        if (_disposed || _completed)
            return;

        if (compressedData.Length < 4)
        {
            _sawmill.Warning($"Chunk too small: {compressedData.Length} bytes");
            return;
        }

        Length += compressedData.Length;

        var magic = $"{compressedData[0]:X2} {compressedData[1]:X2} {compressedData[2]:X2} {compressedData[3]:X2}";
        var isZstd = compressedData[0] == 0x28 && compressedData[1] == 0xB5 &&
                     compressedData[2] == 0x2F && compressedData[3] == 0xFD;

        if (!isZstd)
        {
            _sawmill.Error($"Expected ZStd but got magic: {magic}");
            return;
        }

        try
        {
            using var input = new MemoryStream(compressedData);
            using var zstd = new ZStdDecompressStream(input);
            using var decompressed = new MemoryStream();
            zstd.CopyTo(decompressed);

            var pcmBytes = decompressed.ToArray();
            if (pcmBytes.Length < 2)
                return;

            var sampleCount = pcmBytes.Length / 2;
            var samples = new short[sampleCount];

            // Delta decoding
            short acc = 0;
            for (var i = 0; i < sampleCount; i++)
            {
                var delta = (short)(pcmBytes[i * 2] | (pcmBytes[i * 2 + 1] << 8));
                acc += delta;
                samples[i] = acc;
            }

            _sampleQueue.Enqueue(samples);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"ZStd decompress failed: {ex.Message}, magic: {magic}, size: {compressedData.Length}");
        }
    }

    public void Complete() => _completed = true;

    public int ReadSamples(short[] buffer, int offset, int count)
    {
        var totalRead = 0;

        while (count > 0)
        {
            if (_currentBatch == null || _currentPos >= _currentBatch.Length)
            {
                if (_sampleQueue.Count == 0)
                {
                    _currentBatch = null;
                    break;
                }

                _currentBatch = _sampleQueue.Dequeue();
                _currentPos = 0;
            }

            var available = _currentBatch.Length - _currentPos;
            var toCopy = Math.Min(available, count);

            Array.Copy(_currentBatch, _currentPos, buffer, offset, toCopy);

            _currentPos += toCopy;
            offset += toCopy;
            count -= toCopy;
            totalRead += toCopy;

            if (_currentPos >= _currentBatch.Length)
                _currentBatch = null;
        }

        return totalRead;
    }

    public short[] ReadAllAvailable(float prependSilenceSeconds = 0f)
    {
        var resultList = new List<short>();

        if (prependSilenceSeconds > 0f)
        {
            var silenceSamples = (int)(prependSilenceSeconds * SampleRate);

            for (var i = 0; i < silenceSamples; i++)
                resultList.Add(0);
        }

        var buffer = new short[4096];
        int read;

        while ((read = ReadSamples(buffer, 0, buffer.Length)) > 0)
        {
            for (var i = 0; i < read; i++)
                resultList.Add(buffer[i]);
        }

        return [.. resultList];
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _completed = true;
        _sampleQueue.Clear();
        _currentBatch = null;
    }
}
