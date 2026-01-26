using System;
using NAudio.Wave;

namespace Osutag.Services
{
    /// <summary>
    /// A simple Varispeed Sample Provider for NAudio.
    /// Changes playback speed by resampling (which also changes pitch).
    /// </summary>
    public class VarispeedSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly int _channels;
        private readonly int _sourceSampleRate;
        private float _playbackRate = 1.0f;

        public float PlaybackRate
        {
            get => _playbackRate;
            set
            {
                if (_playbackRate != value)
                {
                    _playbackRate = value;
                }
            }
        }

        public WaveFormat WaveFormat => _source.WaveFormat;

        public VarispeedSampleProvider(ISampleProvider source)
        {
            _source = source;
            _channels = source.WaveFormat.Channels;
            _sourceSampleRate = source.WaveFormat.SampleRate;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            if (_playbackRate == 0) return 0;

            if (_playbackRate == 1.0f)
            {
                return _source.Read(buffer, offset, count);
            }

            // Simple Linear Interpolation Resampling for Varispeed
            // To slow down (rate < 1), we need FEWER source samples per output sample (wait, no. playing slower means we consume source slower)
            // Output samples = count
            // Source samples needed = count * rate
            
            int sourceSamplesNeeded = (int)(count * _playbackRate) + _channels * 2; // +buffer for interpolation
            var sourceBuffer = new float[sourceSamplesNeeded];
            
            int sourceRead = _source.Read(sourceBuffer, 0, sourceSamplesNeeded);
            if (sourceRead == 0) return 0;

            int outputSamplesGenerated = 0;
            float sourcePosition = 0;

            while (outputSamplesGenerated < count && sourcePosition + _channels < sourceRead)
            {
                int intSourcePosition = (int)sourcePosition;
                // Align to channel block
                intSourcePosition -= intSourcePosition % _channels; 
                
                if (intSourcePosition + _channels >= sourceRead) break;

                for (int ch = 0; ch < _channels; ch++)
                {
                    buffer[offset + outputSamplesGenerated + ch] = sourceBuffer[intSourcePosition + ch];
                    // Simply skipping/repeating samples (Nearest Neighbor) is cheaper and often sufficient for "preview"
                    // but linear interpolation is better. Let's do nearest neighbor first for stability & speed.
                }

                outputSamplesGenerated += _channels;
                sourcePosition += _playbackRate * _channels;
            }

            return outputSamplesGenerated;
        }
    }
}
