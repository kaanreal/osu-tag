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

            _originalTitle = diff?.Title ?? group?.Title ?? "";
            _originalArtist = diff?.Artist ?? group?.Artist ?? "";
            
            // Load existing overrides
            _overrideTitle = item.OverrideTitle ?? "";
            _overrideArtist = item.OverrideArtist ?? "";
            _activeCoverPath = item.OverrideCoverPath ?? diff?.CoverPath ?? group?.CoverPath ?? "";
        }
    }
}
