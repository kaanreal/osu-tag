using System;
using NAudio.Wave;
using SoundTouch;

namespace Osutag.Services
{
    /// <summary>
    /// NAudio SampleProvider that uses SoundTouch.Net for time-stretching (Tempo Shift).
    /// Keeps pitch constant while changing speed.
    /// </summary>
    public class SoundTouchSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly SoundTouchProcessor _soundTouch;
        private readonly float[] _sourceBuffer;
        private readonly float[] _soundTouchOutputBuffer;
        private bool _endOfStream = false;

        public WaveFormat WaveFormat => _source.WaveFormat;

        private float _tempo = 1.0f;
        public float Tempo
        {
            get => _tempo;
            set
            {
                if (_tempo != value)
                {
                    _tempo = value;
                    _soundTouch.Tempo = value;
                }
            }
        }

        public SoundTouchSampleProvider(ISampleProvider source)
        {
            _source = source;
            _soundTouch = new SoundTouchProcessor();
            
            // Configure SoundTouch
            _soundTouch.SampleRate = source.WaveFormat.SampleRate;
            _soundTouch.Channels = source.WaveFormat.Channels;
            _soundTouch.Tempo = 1.0f;
            _soundTouch.Pitch = 1.0f;
            _soundTouch.Rate = 1.0f;

            // Buffers
            _sourceBuffer = new float[source.WaveFormat.SampleRate * source.WaveFormat.Channels]; // 1s buffer
            _soundTouchOutputBuffer = new float[source.WaveFormat.SampleRate * source.WaveFormat.Channels]; 
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = 0;

            // Loop until we have enough output samples or source is dry
            while (samplesRead < count)
            {
                // Try to read from SoundTouch first
                // ReceiveSamples takes a span/array destination
                // Check API: ReceiveSamples(Span<float> outBuffer, int maxSamples)
                int outputNeeded = count - samplesRead;
                int available = _soundTouch.ReceiveSamples(buffer.AsSpan(offset + samplesRead, outputNeeded), outputNeeded / WaveFormat.Channels);
                
                if (available > 0)
                {
                    samplesRead += available * WaveFormat.Channels;
                }
                else
                {
                    // No output available, feed more input
                    if (_endOfStream) break;

                    int sourceRead = _source.Read(_sourceBuffer, 0, _sourceBuffer.Length);
                    if (sourceRead == 0)
                    {
                        _endOfStream = true;
                        _soundTouch.Flush(); // Tell SoundTouch input is done
                    }
                    else
                    {
                        // PutSamples(ReadOnlySpan<float> samples, int numSamples)
                        _soundTouch.PutSamples(_sourceBuffer.AsSpan(0, sourceRead), sourceRead / WaveFormat.Channels);
                    }
                }
            }
            
            // Fill remainder with zeros if stream ended
            if (samplesRead < count)
            {
                Array.Clear(buffer, offset + samplesRead, count - samplesRead);
                samplesRead = count; // Return full count usually to avoid stopping early? Or return actual?
                // NAudio expects full count mostly, unless end of stream
                return samplesRead; 
            }

            return samplesRead;
        }
    }
}
