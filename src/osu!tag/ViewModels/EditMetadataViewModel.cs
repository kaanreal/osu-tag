using System;

namespace Osutag.ViewModels
{
    public class EditMetadataViewModel : ObservableObject
    {
        private string _originalTitle = "";
        private string _originalArtist = "";
        private string _overrideTitle = "";
        private string _overrideArtist = "";
        private string _activeCoverPath = "";
        private double _playbackRate = 1.0;
        private double _pitchSemitones = 0.0;
        private bool _maintainPitch = true;
        private bool _isPitchEnabled = false;
        private string _cutError = "";
        private bool _isTrimEnabled;
        private double _audioDurationSeconds = 300;
        private double _cutStartSecondsValue;
        private double _cutEndSecondsValue;

        public string OriginalTitle
        {
            get => _originalTitle;
            set => SetProperty(ref _originalTitle, value);
        }

        public string OriginalArtist
        {
            get => _originalArtist;
            set => SetProperty(ref _originalArtist, value);
        }

        public string OverrideTitle
        {
            get => _overrideTitle;
            set => SetProperty(ref _overrideTitle, value);
        }

        public string OverrideArtist
        {
            get => _overrideArtist;
            set => SetProperty(ref _overrideArtist, value);
        }

        public string ActiveCoverPath
        {
            get => _activeCoverPath;
            set => SetProperty(ref _activeCoverPath, value);
        }

        public SelectedItemInfo OriginalItem { get; }
        public string SongPath { get; }

        public double PlaybackRate
        {
            get => _playbackRate;
            set
            {
                if (SetProperty(ref _playbackRate, value))
                {
                    if (!_maintainPitch)
                    {
                        // Coupled: Update semitones to match rate
                        // semitones = 12 * log2(rate)
                        _pitchSemitones = 12.0 * Math.Log(_playbackRate) / Math.Log(2.0);
                        OnPropertyChanged(nameof(PitchSemitones));
                    }
                }
            }
        }

        public bool MaintainPitch
        {
            get => _maintainPitch;
            set
            {
                if (SetProperty(ref _maintainPitch, value))
                {
                    OnPropertyChanged(nameof(IsPitchShiftEnabled));
                    if (!_maintainPitch)
                    {
                        // Force sync when enabled
                        _pitchSemitones = 12.0 * Math.Log(_playbackRate) / Math.Log(2.0);
                        OnPropertyChanged(nameof(PitchSemitones));
                    }
                }
            }
        }

        public bool IsPitchShiftEnabled
        {
            get => !_maintainPitch;
            set => MaintainPitch = !value;
        }

        public double PitchSemitones
        {
            get => _pitchSemitones;
            set
            {
                if (SetProperty(ref _pitchSemitones, value))
                {
                    // If we MANUALLY change semitones, we keep coupling logic consistent
                    // But usually user uses the rate slider now.
                }
            }
        }

        public bool IsPitchEnabled
        {
            get => _isPitchEnabled;
            set => SetProperty(ref _isPitchEnabled, value);
        }

        public bool IsTrimEnabled
        {
            get => _isTrimEnabled;
            set => SetProperty(ref _isTrimEnabled, value);
        }

        public double AudioDurationSeconds
        {
            get => _audioDurationSeconds;
            private set
            {
                if (SetProperty(ref _audioDurationSeconds, Math.Max(1, value)))
                    OnPropertyChanged(nameof(AudioDurationLabel));
            }
        }

        public double CutStartSecondsValue
        {
            get => _cutStartSecondsValue;
            set
            {
                var clamped = Math.Clamp(value, 0, AudioDurationSeconds);
                if (SetProperty(ref _cutStartSecondsValue, clamped))
                {
                    OnPropertyChanged(nameof(CutStartLabel));
                    OnPropertyChanged(nameof(SelectedRangeLabel));
                }
            }
        }

        public double CutEndSecondsValue
        {
            get => _cutEndSecondsValue;
            set
            {
                var clamped = Math.Clamp(value, 0, AudioDurationSeconds);
                if (SetProperty(ref _cutEndSecondsValue, clamped))
                {
                    OnPropertyChanged(nameof(CutEndLabel));
                    OnPropertyChanged(nameof(SelectedRangeLabel));
                }
            }
        }

        public string AudioDurationLabel => FormatTime(AudioDurationSeconds);
        public string CutStartLabel => FormatTime(CutStartSecondsValue);
        public string CutEndLabel => FormatTime(CutEndSecondsValue);
        public string SelectedRangeLabel => FormatTime(Math.Max(0, CutEndSecondsValue - CutStartSecondsValue));

        public string CutError
        {
            get => _cutError;
            private set
            {
                if (SetProperty(ref _cutError, value))
                    OnPropertyChanged(nameof(HasCutError));
            }
        }

        public bool HasCutError => !string.IsNullOrEmpty(CutError);

        /// <summary>
        /// Validates the optional visual trim range.
        /// </summary>
        public bool TryGetCutRange(out float? startSeconds, out float? endSeconds)
        {
            startSeconds = null;
            endSeconds = null;

            if (!IsTrimEnabled)
            {
                CutError = "";
                return true;
            }

            var start = (float)CutStartSecondsValue;
            var end = (float)CutEndSecondsValue;
            if (end <= start)
            {
                CutError = "End time must be greater than the start time.";
                return false;
            }

            startSeconds = start;
            endSeconds = end;
            CutError = "";
            return true;
        }

        private static string FormatTime(double seconds)
        {
            var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return time.TotalHours >= 1
                ? time.ToString(@"h\:mm\:ss")
                : time.ToString(@"m\:ss");
        }

        public EditMetadataViewModel(SelectedItemInfo item)
        {
            OriginalItem = item;
            
            var group = item.MapGroup;
            DifficultyItem? diff = null;
            if (group != null && item.SubDisplayName != null)
            {
                foreach(var d in group.Difficulties)
                {
                    if (d.DifficultyName == item.SubDisplayName)
                    {
                        diff = d;
                        break;
                    }
                }
            }

            SongPath = item.AudioFile?.Mp3Path ?? diff?.Difficulty?.Mp3Path ?? group?.Difficulties[0].Difficulty.Mp3Path ?? "";

            if (!string.IsNullOrEmpty(SongPath))
            {
                try
                {
                    using var audioFile = TagLib.File.Create(SongPath);
                    AudioDurationSeconds = audioFile.Properties.Duration.TotalSeconds;
                }
                catch
                {
                    // Keep a usable visual range if metadata probing is unavailable.
                }
            }
            
            _originalTitle = diff?.Title ?? group?.Title ?? "";
            _originalArtist = diff?.Artist ?? group?.Artist ?? "";
            
            // Load existing overrides
            _overrideTitle = item.OverrideTitle ?? "";
            _overrideArtist = item.OverrideArtist ?? "";
            _activeCoverPath = item.OverrideCoverPath ?? diff?.CoverPath ?? group?.CoverPath ?? "";
            _playbackRate = item.PlaybackRate;
            _pitchSemitones = item.PitchSemitones;
            _maintainPitch = item.MaintainPitch;
            _isPitchEnabled = Math.Abs(_pitchSemitones) > 0.01;
            _isTrimEnabled = item.CutStartSeconds.HasValue || item.CutEndSeconds.HasValue;
            _cutStartSecondsValue = Math.Clamp(item.CutStartSeconds ?? 0, 0, AudioDurationSeconds);
            _cutEndSecondsValue = Math.Clamp(item.CutEndSeconds ?? (float)AudioDurationSeconds, 0, AudioDurationSeconds);
        }
    }
}
