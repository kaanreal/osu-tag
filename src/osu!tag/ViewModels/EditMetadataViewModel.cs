using System;
using System.IO;

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
            set => SetProperty(ref _playbackRate, value);
        }

        public bool MaintainPitch
        {
            get => _maintainPitch;
            set
            {
                if (SetProperty(ref _maintainPitch, value))
                {
                    OnPropertyChanged(nameof(IsPitchShiftEnabled));
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
            set => SetProperty(ref _pitchSemitones, value);
        }

        public bool IsPitchEnabled
        {
            get => _isPitchEnabled;
            set => SetProperty(ref _isPitchEnabled, value);
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
            
            _originalTitle = diff?.Title ?? group?.Title ?? "";
            _originalArtist = diff?.Artist ?? group?.Artist ?? "";
            
            // Load existing overrides
            _overrideTitle = item.OverrideTitle ?? "";
            _overrideArtist = item.OverrideArtist ?? "";
            _activeCoverPath = item.OverrideCoverPath ?? diff?.CoverPath ?? group?.CoverPath ?? "";
            _playbackRate = item.PlaybackRate;
            _pitchSemitones = item.PitchSemitones;
            _maintainPitch = item.MaintainPitch;
            _isPitchEnabled = Math.Abs(item.PitchSemitones) > 0.01;
        }
    }
}
