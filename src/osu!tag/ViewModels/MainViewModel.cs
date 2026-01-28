using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Text.Json;
using System.Text.RegularExpressions;
using Osutag.Models;
using Osutag.Services;
using Avalonia.Threading;

namespace Osutag.ViewModels
{
    public class AudioFileItem : ObservableObject
    {
        private bool _isSelected = false;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public required string Mp3Path { get; set; }
        public required string DisplayName { get; set; }
        public int PreviewTime { get; set; }
    }

    public class DifficultyItem : ObservableObject
    {
        private bool _isSelected = false;
        private string? _coverPath;
        private Avalonia.Media.Imaging.Bitmap? _coverBitmap;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private bool _isLoadingCover = false;

        public required string DifficultyName { get; set; }
        public required OsuMapDifficulty Difficulty { get; set; }
        
        // Display properties for Overlay
        public string? Title { get; set; }
        public string? Artist { get; set; }

        public string? CoverPath
        {
            get => _coverPath;
            set
            {
                if (SetProperty(ref _coverPath, value))
                {
                    // Reset bitmap so next access triggers load
                    _coverBitmap = null;
                    OnPropertyChanged(nameof(CoverBitmap));
                    _isLoadingCover = false;
                }
            }
        }

        /// <summary>
        /// Cached bitmap loaded asynchronously. Bind to this instead of using PathToBitmapConverter.
        /// </summary>
        public Avalonia.Media.Imaging.Bitmap? CoverBitmap
        {
            get
            {
                if (_coverBitmap == null && !_isLoadingCover && !string.IsNullOrEmpty(_coverPath))
                {
                    LoadCoverAsync();
                }
                return _coverBitmap;
            }
            private set => SetProperty(ref _coverBitmap, value);
        }

        private async void LoadCoverAsync()
        {
            var currentPath = _coverPath;
            if (string.IsNullOrEmpty(currentPath))
            {
                return;
            }

            _isLoadingCover = true;

            try
            {
                var bitmap = await Services.ImageCacheService.Instance.GetImageAsync(currentPath);
                
                // Must dispatch to UI thread
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (_coverPath == currentPath)
                    {
                        CoverBitmap = bitmap;
                    }
                });
            }
            finally
            {
                _isLoadingCover = false;
            }
        }

        // Randomization for Shuffle Animation
        private static readonly Random _rng = new();
        public double RandomAngle { get; } = (_rng.NextDouble() * 16.0) - 8.0; // -8 to 8 degrees
        public double RandomOffsetX { get; } = (_rng.NextDouble() * 30.0) - 15.0; // -15 to 15 px
        public double RandomOffsetY { get; } = (_rng.NextDouble() * 30.0) - 15.0; // -15 to 15 px
    }

    public class MapItemGroup : ObservableObject
    {
        private bool _isExpanded = false;
        private bool _isSelected = false;
        private string? _coverPath;
        private Avalonia.Media.Imaging.Bitmap? _coverBitmap;

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public void SetIsSelectedWithoutPropagation(bool value)
        {
            if (SetProperty(ref _isSelected, value, nameof(IsSelected)))
            {
                // Do not propagate to children
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    foreach (var diff in Difficulties)
                    {
                        diff.IsSelected = value;
                    }
                }
            }
        }

        private bool _isLoadingCover = false;

        public required string Artist { get; set; }
        public required string Title { get; set; }

        public string? CoverPath
        {
            get => _coverPath;
            set
            {
                if (SetProperty(ref _coverPath, value))
                {
                    // Reset bitmap so next access triggers load
                    _coverBitmap = null;
                    OnPropertyChanged(nameof(CoverBitmap));
                    _isLoadingCover = false;
                }
            }
        }

        /// <summary>
        /// Cached bitmap loaded asynchronously. Bind to this instead of using PathToBitmapConverter.
        /// </summary>
        public Avalonia.Media.Imaging.Bitmap? CoverBitmap
        {
            get
            {
                if (_coverBitmap == null && !_isLoadingCover && !string.IsNullOrEmpty(_coverPath))
                {
                    LoadCoverAsync();
                }
                return _coverBitmap;
            }
            private set => SetProperty(ref _coverBitmap, value);
        }

        private async void LoadCoverAsync()
        {
            var currentPath = _coverPath;
            if (string.IsNullOrEmpty(currentPath))
            {
                return;
            }

            _isLoadingCover = true;

            try
            {
                var bitmap = await Services.ImageCacheService.Instance.GetImageAsync(currentPath);
                
                // Must dispatch to UI thread
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (_coverPath == currentPath)
                    {
                        CoverBitmap = bitmap;
                    }
                });
            }
            finally
            {
                _isLoadingCover = false;
            }
        }
        public required string Creator { get; set; }
        public string? Source { get; set; }
        public string? Tags { get; set; }
        public string? PreviewMp3Path { get; set; }
        public int PreviewTime { get; set; }
        public int BeatmapSetId { get; set; } = -1;
        public string? DirectoryPath { get; set; }
        public ICommand? OpenBeatmapUrlCommand { get; set; }
        public ICommand? OpenDirectoryCommand { get; set; }
        public ICommand? ExportBackgroundCommand { get; set; }
        public ObservableCollection<DifficultyItem> Difficulties { get; } = new();
        public ObservableCollection<AudioFileItem> UniqueAudioFiles { get; } = new();
        
        private bool _isOnSpotify;
        public bool IsOnSpotify
        {
            get => _isOnSpotify;
            set => SetProperty(ref _isOnSpotify, value);
        }

        private string? _spotifyUrl;
        public string? SpotifyUrl
        {
            get => _spotifyUrl;
            set => SetProperty(ref _spotifyUrl, value);
        }

        public ICommand? OpenSpotifyUrlCommand { get; set; }

        // Randomization for Shuffle Animation
        private static readonly Random _rng = new();
        public double RandomAngle { get; } = (_rng.NextDouble() * 20.0) - 10.0; // -10 to 10 degrees
        public double RandomOffsetX { get; } = (_rng.NextDouble() * 40.0) - 20.0; // -20 to 20 px
        public double RandomOffsetY { get; } = (_rng.NextDouble() * 40.0) - 20.0; // -20 to 20 px

        public bool HasMultipleDifferentAudios
        {
            get
            {
                var uniqueMp3s = Difficulties.Select(d => d.Difficulty.Mp3Path).Distinct().Count();
                return uniqueMp3s > 1;
            }
        }

        public int UniqueAudioCount
        {
            get
            {
                return Difficulties.Select(d => d.Difficulty.Mp3Path).Distinct().Count();
            }
        }

        public bool HasMultipleDifferentRates
        {
            get
            {
                var uniqueRates = Difficulties
                    .Select(d => d.Difficulty.Rate ?? "1.0x")
                    .Distinct()
                    .Count();
                return uniqueRates > 1;
            }
        }

        public int UniqueRateCount
        {
            get
            {
                return Difficulties
                    .Select(d => d.Difficulty.Rate ?? "1.0x")
                    .Distinct()
                    .Count();
            }
        }

        public bool IsStack => UniqueAudioFiles.Count > 1; // Only stack if multiple UNIQUE audio files exist
    }

    public class ConversionResult
    {
        public required string Title { get; set; }
        public required string Message { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Represents a selected item in the selection panel.
    /// </summary>
    public class SelectedItemInfo
    {
        public MapItemGroup? MapGroup { get; set; }
        public AudioFileItem? AudioFile { get; set; }
        public required string DisplayName { get; set; }
        public string? SubDisplayName { get; set; }
        public string? OverrideTitle { get; set; }
        public string? OverrideArtist { get; set; }
        public string? OverrideCoverPath { get; set; }
        public float PlaybackRate { get; set; } = 1.0f;
        public float PitchSemitones { get; set; } = 0.0f;
        public bool MaintainPitch { get; set; } = true;

    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public event EventHandler? CanExecuteChanged;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public class MainViewModel : ObservableObject
    {
        private string _selectedPath = "";
        private bool _isFolderSelectionVisible = false;
        private string _outputPath = "";
        private string _pathStatusMessage = "";
        private string _errorMessage = "";
        private bool _processCovers = true;
        private bool _createBackups = false;
        private bool _isProcessing = false;
        private bool _mapsLoaded = false;
        private bool _isScanning = false;
        private int _scanProgress = 0;
        private string _scanStatusMessage = "";
        private int _progressPercentage = 0;
        private string _progressMessage = "";
        private MapItemGroup? _selectedMapGroup = null;
        private string _searchQuery = "";
        private ObservableCollection<MapItemGroup> _allMapGroups = new();
        private ObservableCollection<MapItemGroup> _mapGroups = new();
        private List<MapItemGroup> _filteredMapGroups = new();
        public List<MapItemGroup> FilteredMaps => _filteredMapGroups;
        private int _displayedCount = 0;
        public int DisplayedCount => _displayedCount;
        private const int ITEMS_PER_PAGE = 50;
        private bool _canLoadMore = false;
        private CancellationTokenSource? _searchDebounceToken;
        private const int SEARCH_DEBOUNCE_MS = 300;
        private bool _isSearching = false;
        private Dictionary<string, int> _playCountCache = new();
        private ObservableCollection<object> _selectedItems = new();
        private bool _isLoadingMore = false;
        private bool _isOverlayOpen = false;
        private MapItemGroup? _overlayMapGroup;
        private string _githubStars = "0";

        public string GithubStars
        {
            get => _githubStars;
            set => SetProperty(ref _githubStars, value);
        }

        public bool IsOverlayOpen
        {
            get => _isOverlayOpen;
            set => SetProperty(ref _isOverlayOpen, value);
        }

        public bool IsLoadingMore
        {
            get => _isLoadingMore;
            set
            {
                if (SetProperty(ref _isLoadingMore, value))
                {
                    OnPropertyChanged(nameof(ShowMainOverlay));
                    OnPropertyChanged(nameof(CurrentLoadingTitle));
                    OnPropertyChanged(nameof(ShowLoadMore));

                }
            }
        }

        public MapItemGroup? OverlayMapGroup
        {
            get => _overlayMapGroup;
            set => SetProperty(ref _overlayMapGroup, value);
        }

        public ObservableCollection<object> SelectedItems
        {
            get => _selectedItems;
            set 
            {
                if (SetProperty(ref _selectedItems, value))
                {
                    OnPropertyChanged(nameof(SelectedCount));
                    OnPropertyChanged(nameof(HasSelectedMaps));
                }
            }
        }

        public int SelectedCount => _selectedItems.Count;
        public bool HasSelectedMaps => SelectedCount > 0;

        private SelectedItemInfo? _lastSelectedItem;
        public SelectedItemInfo? LastSelectedItem
        {
            get => _lastSelectedItem;
            set => SetProperty(ref _lastSelectedItem, value);
        }

        private bool _isBottomBarExpanded;
        public bool IsBottomBarExpanded
        {
            get => _isBottomBarExpanded;
            set => SetProperty(ref _isBottomBarExpanded, value);
        }

        public string SpotifyClientId
        {
            get => SettingsService.Settings.SpotifyClientId;
            set
            {
                if (SettingsService.Settings.SpotifyClientId != value)
                {
                    SettingsService.Settings.SpotifyClientId = value;
                    OnPropertyChanged(nameof(SpotifyClientId));
                    SettingsService.Save();
                }
            }
        }

        public string SpotifyClientSecret
        {
            get => SettingsService.Settings.SpotifyClientSecret;
            set
            {
                if (SettingsService.Settings.SpotifyClientSecret != value)
                {
                    SettingsService.Settings.SpotifyClientSecret = value;
                    OnPropertyChanged(nameof(SpotifyClientSecret));
                    SettingsService.Save();
                }
            }
        }

        public string SearchHints => "Search by: Artist, Title, Creator, Difficulty, Tags, or Source";

        public bool ShowLoadMore => CanLoadMore && !IsScanning && !IsLoadingMore && !IsSearching && IsInitialLoadDone;

        public bool CanLoadMore
        {
            get => _canLoadMore;
            set
            {
                if (SetProperty(ref _canLoadMore, value))
                {
                    ((RelayCommand)LoadMoreCommand).RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(ShowLoadMore));
                }
            }
        }

        public bool IsFolderSelectionVisible
        {
            get => _isFolderSelectionVisible;
            set 
            {
                if (SetProperty(ref _isFolderSelectionVisible, value))
                {
                    OnPropertyChanged(nameof(ShowMainOverlay));
                }
            }
        }

        private bool _isInitialLoadDone;
        public bool IsInitialLoadDone
        {
            get => _isInitialLoadDone;
            set 
            {
                if (SetProperty(ref _isInitialLoadDone, value))
                {
                    OnPropertyChanged(nameof(ShowLoadMore));
                }
            }
        }

        private bool _isStartingUp = true;
        public bool IsStartingUp
        {
            get => _isStartingUp;
            set 
            {
                if (SetProperty(ref _isStartingUp, value))
                {
                    OnPropertyChanged(nameof(ShowMainOverlay));
                }
            }
        }


        public bool ShowMainOverlay => IsStartingUp || IsFolderSelectionVisible || IsScanning || IsLoadingMore || (IsProcessing && !IsBottomBarExpanded);

        public string CurrentLoadingTitle {
            get {
                if (IsStartingUp) return "Starting Up...";
                if (IsFolderSelectionVisible) return "Welcome to osu!tag";
                if (IsScanning) return "Scanning Songs...";
                if (IsLoadingMore) return "Loading Library...";
                if (IsProcessing) return "Processing Maps...";
                return "Please Wait...";
            }
        }

        public bool IsProgressBarIndeterminate => (IsScanning && ProgressPercentage == 0);

        public bool IsWindows => PlatformService.IsWindows;
        public string AppVersion => "v" + Osutag.Services.AppVersion.Current;
        public bool IsCompanellaSupported => PlatformService.IsWindows;
        
        // Update Properties
        private bool _isUpdateAvailable;
        public bool IsUpdateAvailable
        {
            get => _isUpdateAvailable;
            set => SetProperty(ref _isUpdateAvailable, value);
        }

        private string _newUpdateVersion = "";
        public string NewUpdateVersion
        {
            get => _newUpdateVersion;
            set => SetProperty(ref _newUpdateVersion, value);
        }

        public ICommand OpenUpdateWindowCommand { get; }
        
        private string _companellaStatus = "Scanning...";
        public string CompanellaStatus
        {
            get => _companellaStatus;
            set => SetProperty(ref _companellaStatus, value);
        }

        private string _selectedTheme = SettingsService.Settings.ThemeColor;
        public string SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (SetProperty(ref _selectedTheme, value))
                {
                    SettingsService.Settings.ThemeColor = value;
                    SettingsService.Save();
                    // Theme application logic will go in App.axaml.cs or via dynamic resource
                }
            }
        }

        public bool IsScanning
        {
            get => _isScanning;
            set
            {
                if (SetProperty(ref _isScanning, value))
                {
                    ((RelayCommand)RescanCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)StartConversionCommand).RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(ShowMainOverlay));
                    OnPropertyChanged(nameof(CurrentLoadingTitle));
                    OnPropertyChanged(nameof(IsProgressBarIndeterminate));
                    OnPropertyChanged(nameof(ShowLoadMore));

                }
            }
        }

        public bool IsSearching
        {
            get => _isSearching;
            set 
            {
                if (SetProperty(ref _isSearching, value))
                {
                    OnPropertyChanged(nameof(ShowLoadMore));
                }
            }
        }

        public double PreviewVolume
        {
            get => SettingsService.Settings.PreviewVolume;
            set
            {
                if (SettingsService.Settings.PreviewVolume != value)
                {
                    SettingsService.Settings.PreviewVolume = value;
                    OnPropertyChanged(nameof(PreviewVolume));
                    SettingsService.Save();
                }
            }
        }

        public int ScanProgress
        {
            get => _scanProgress;
            set => SetProperty(ref _scanProgress, value);
        }

        public string ScanStatusMessage
        {
            get => _scanStatusMessage;
            set => SetProperty(ref _scanStatusMessage, value);
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    DebouncedSearch();
                }
            }
        }

        private async void DebouncedSearch()
        {
            _searchDebounceToken?.Cancel();
            _searchDebounceToken = new CancellationTokenSource();
            var token = _searchDebounceToken.Token;

            // Immediately show loading and clear - set new empty collection (single UI update)
            IsSearching = true;
            MapGroups = new ObservableCollection<MapItemGroup>();
            _displayedCount = 0;

            try
            {
                await Task.Delay(SEARCH_DEBOUNCE_MS, token);
                if (!token.IsCancellationRequested)
                {
                    await FilterMapsAsync(token);
                }
            }
            catch (TaskCanceledException)
            {
                // Search was cancelled by new input - keep showing loading
            }
        }

        public string SelectedPath
        {
            get => _selectedPath;
            set
            {
                if (SetProperty(ref _selectedPath, value))
                {
                    ((RelayCommand)RescanCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string OutputPath
        {
            get => _outputPath;
            set => SetProperty(ref _outputPath, value);
        }

        public string PathStatusMessage
        {
            get => _pathStatusMessage;
            set => SetProperty(ref _pathStatusMessage, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public bool ProcessCovers
        {
            get => _processCovers;
            set => SetProperty(ref _processCovers, value);
        }

        public bool CreateBackups
        {
            get => _createBackups;
            set => SetProperty(ref _createBackups, value);
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    ((RelayCommand)StartConversionCommand).RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(ShowMainOverlay));
                    OnPropertyChanged(nameof(CurrentLoadingTitle));
                }
            }
        }

        public bool MapsLoaded
        {
            get => _mapsLoaded;
            set => SetProperty(ref _mapsLoaded, value);
        }

        public int ProgressPercentage
        {
            get => _progressPercentage;
            set 
            {
                if (SetProperty(ref _progressPercentage, value))
                {
                    OnPropertyChanged(nameof(IsProgressBarIndeterminate));
                }
            }
        }

        public string ProgressMessage
        {
            get => _progressMessage;
            set => SetProperty(ref _progressMessage, value);
        }

        public MapItemGroup? SelectedMapGroup
        {
            get => _selectedMapGroup;
            set => SetProperty(ref _selectedMapGroup, value);
        }

        public ObservableCollection<MapItemGroup> MapGroups
        {
            get => _mapGroups;
            set => SetProperty(ref _mapGroups, value);
        }

        private async Task FilterMapsAsync(CancellationToken token = default)
        {
            try
            {
                // Run filtering on background thread
                var query = _searchQuery;
                var sortByMostPlayed = SettingsService.Settings.SortByMostPlayed;
                var playCounts = _playCountCache;

                var filteredList = await Task.Run(() =>
                {
                    List<MapItemGroup> result;

                    if (string.IsNullOrWhiteSpace(query))
                    {
                        result = _allMapGroups.ToList();
                    }
                    else
                    {
                        result = _allMapGroups.Where(map =>
                        {
                            // Use StringComparison for faster case-insensitive search
                            if (map.Artist.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                map.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                                return true;

                            if (map.Creator.Contains(query, StringComparison.OrdinalIgnoreCase))
                                return true;

                            if (map.Difficulties.Any(d => d.DifficultyName.Contains(query, StringComparison.OrdinalIgnoreCase)))
                                return true;

                            if (!string.IsNullOrEmpty(map.Tags) && map.Tags.Contains(query, StringComparison.OrdinalIgnoreCase))
                                return true;

                            if (!string.IsNullOrEmpty(map.Source) && map.Source.Contains(query, StringComparison.OrdinalIgnoreCase))
                                return true;

                            return false;
                        }).ToList();
                    }

                    // Sort by most played if enabled
                    if (sortByMostPlayed && playCounts.Count > 0)
                    {
                        result = result
                            .OrderByDescending(map => GetPlayCount(map, playCounts) > 0)
                            .ThenByDescending(map => GetPlayCount(map, playCounts))
                            .ToList();
                    }

                    return result;
                }, token);

                if (token.IsCancellationRequested) return;

                _filteredMapGroups = filteredList;
                OnPropertyChanged(nameof(FilteredMaps));

                // Get initial items to display
                var initialItems = _filteredMapGroups.Take(ITEMS_PER_PAGE).ToList();

                // Set new collection in one go (single UI update instead of 50)
                MapGroups = new ObservableCollection<MapItemGroup>(initialItems);

                _displayedCount = initialItems.Count;
                OnPropertyChanged(nameof(DisplayedCount));
                CanLoadMore = _displayedCount < _filteredMapGroups.Count;
            }
            finally
            {
                IsSearching = false;
            }
        }

        private static int GetPlayCount(MapItemGroup map, Dictionary<string, int> playCounts)
        {
            if (playCounts.Count == 0)
                return 0;

            // Get folder name from any difficulty's mp3 path
            var mp3Path = map.Difficulties.FirstOrDefault()?.Difficulty.Mp3Path;
            if (!string.IsNullOrEmpty(mp3Path))
            {
                var folder = Path.GetDirectoryName(mp3Path);
                if (!string.IsNullOrEmpty(folder))
                {
                    var folderName = Path.GetFileName(folder);
                    if (!string.IsNullOrEmpty(folderName) && playCounts.TryGetValue(folderName, out int count))
                        return count;
                }
            }

            // Fallback: try Artist - Title match
            var key = $"{map.Artist} - {map.Title}";
            if (playCounts.TryGetValue(key, out int artistTitleCount))
                return artistTitleCount;

            return 0;
        }

        private async Task LoadCompanellaPlayCounts()
        {
            if (!SettingsService.Settings.SortByMostPlayed)
                return;

            var companellaPath = SettingsService.Settings.CompanellaPath;

            // Auto-detect Companella path if not set
            if (string.IsNullOrEmpty(companellaPath))
            {
                companellaPath = PlatformService.GetDefaultCompanellaPath();
                // Save the auto-detected path for future use
                SettingsService.Settings.CompanellaPath = companellaPath;
                SettingsService.Save();
            }

            if (string.IsNullOrEmpty(companellaPath))
                return;

            try
            {
                _playCountCache = await Task.Run(() =>
                {
                    var service = new CompanellaService(companellaPath);
                    if (service.IsAvailable())
                    {
                        return service.GetPlayCounts();
                    }
                    return new Dictionary<string, int>();
                });
            }
            catch
            {
                _playCountCache = new Dictionary<string, int>();
            }
        }

        /// <summary>
        /// Reloads Companella play counts and re-sorts the map list.
        /// Call this after settings change to apply new sorting.
        /// </summary>
        public async Task RefreshCompanellaSorting()
        {
            try
            {
                // Force UI clear to prevent "one stack" layout glitch
                // This ensures ItemsRepeater resets its state before the new sorted list arrives
                IsSearching = true; // Shows loading state if bound
                MapGroups = new ObservableCollection<MapItemGroup>(); 
                await Task.Delay(1); // Yield to UI thread to allow layout update
                
                await LoadCompanellaPlayCounts();
                await FilterMapsAsync();
            }
            catch (Exception)
            {
                // Silent failure
            }
            finally
            {
                IsSearching = false;
            }
        }


        public async void LoadMoreItems()
        {
            if (_displayedCount >= _filteredMapGroups.Count || IsLoadingMore)
            {
                CanLoadMore = false;
                return;
            }

            IsLoadingMore = true;
            
            try
            {
                // Brief delay for smooth transition
                await Task.Delay(400);

                // Get items to add
                var count = Math.Min(ITEMS_PER_PAGE, _filteredMapGroups.Count - _displayedCount);
                var allItems = _filteredMapGroups.Take(_displayedCount + count).ToList();

                // Replace entire collection (single UI update instead of many)
                MapGroups = new ObservableCollection<MapItemGroup>(allItems);

                _displayedCount += count;
                OnPropertyChanged(nameof(DisplayedCount));
                CanLoadMore = _displayedCount < _filteredMapGroups.Count;
            }
            finally
            {
                IsLoadingMore = false;
            }
        }

        private void RemoveItem(SelectedItemInfo? info)
        {
            if (info == null) return;

            // Find the corresponding MapGroup or DifficultyItem and deselect it
            if (info.MapGroup != null)
            {
                // Unset Group Selection
                if (info.SubDisplayName == null) // It's a whole group
                {
                     if (info.MapGroup.IsSelected)
                         ToggleMapSelection(info.MapGroup);
                }
                else // It's a specific difficulty
                {
                    var diff = info.MapGroup.Difficulties.FirstOrDefault(d => 
                        d.DifficultyName == info.SubDisplayName); // Or check ID if we had one
                    
                    if (diff != null && diff.IsSelected)
                    {
                        // Logic from SelectDifficulty: "diff.IsSelected = !diff.IsSelected"
                        // We just want to set it to false.
                        diff.IsSelected = false;
                        
                        // Update group partial state
                        var anySelected = info.MapGroup.Difficulties.Any(d => d.IsSelected);
                        info.MapGroup.SetIsSelectedWithoutPropagation(anySelected);

                        RefreshSelectedItems();
                    }
                }
            }
        }
        
        private async void EditItem(SelectedItemInfo? info)
        {
            if (info == null) return;
            
            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
            if (topLevel != null)
            {
                var vm = new EditMetadataViewModel(info);
                var win = new Views.EditMetadataWindow
                {
                    DataContext = vm
                };
                await win.ShowDialog(topLevel);
                
                // Refresh list if needed (to update display name if title changed?)
                // Since DisplayName is set on selection, if override changes, 
                // we might want to update DisplayName to reflect 'OverrideTitle'.
                // Ideally SelectedItemInfo.DisplayName should be property with notification, or we update it here.
                
                if (!string.IsNullOrEmpty(info.OverrideTitle))
                {
                    info.DisplayName = info.OverrideTitle;
                    // Force refresh of the collection to update UI?
                    // ObservableCollection doesn't detect property changes inside items unless they implement INotifyPropertyChanged
                    // SelectedItemInfo doesn't implement INPC currently.
                    
                    // Hack: Toggle property or replace item
                    var index = SelectedItems.IndexOf(info);
                    if (index >= 0)
                    {
                        SelectedItems[index] = info; // Re-set to trigger binding update
                    }
                }
            }
        }

        public ObservableCollection<ConversionResult> ConversionResults { get; } = new();

        public ICommand BrowseCommand { get; }
        public ICommand UseDefaultPathCommand { get; }
        public ICommand RescanCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand DeselectAllCommand { get; }
        public ICommand StartConversionCommand { get; }
        public ICommand SettingsCommand { get; }
        public ICommand ToggleBottomBarCommand { get; }
        public ICommand LoadMoreCommand { get; }
        public ICommand ToggleMapSelectionCommand { get; }
        public ICommand BrowseOutputPathCommand { get; }
        public ICommand CloseOverlayCommand { get; }
        public ICommand SelectDifficultyCommand { get; }
        public ICommand RemoveItemCommand { get; }
        public ICommand OpenGithubCommand { get; }
        public ICommand EditItemCommand { get; }
        // Duplicates removed
        public ICommand ClearCacheCommand { get; }
        public ICommand OpenBeatmapUrlCommand { get; }
        public ICommand OpenDirectoryCommand { get; }
        public ICommand ExportBackgroundCommand { get; }
        public ICommand OpenSupporterUrlCommand { get; }
        public ICommand OpenSpotifyUrlCommand { get; }

        public MainViewModel()
        {
            // Load settings
            _outputPath = string.IsNullOrEmpty(SettingsService.Settings.LastUsedPath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
                : SettingsService.Settings.LastUsedPath;
            _processCovers = SettingsService.Settings.ProcessCovers;
            _createBackups = SettingsService.Settings.CreateBackups;

            BrowseCommand = new RelayCommand(_ => Browse());
            BrowseOutputPathCommand = new RelayCommand(_ => BrowseOutputPath());
            UseDefaultPathCommand = new RelayCommand(_ => UseDefaultPath());
            RescanCommand = new RelayCommand(_ => Rescan(), _ => !IsScanning && !string.IsNullOrEmpty(SelectedPath) && Directory.Exists(SelectedPath));
            SelectAllCommand = new RelayCommand(_ => SelectAll());
            DeselectAllCommand = new RelayCommand(_ => DeselectAll());
            StartConversionCommand = new RelayCommand(_ => StartConversion(), _ => !IsProcessing && HasAnySelection());
            SettingsCommand = new RelayCommand(_ => OpenSettings());
            LoadMoreCommand = new RelayCommand(_ => LoadMoreItems(), _ => CanLoadMore);
            ToggleMapSelectionCommand = new RelayCommand(param => ToggleMapSelection(param as MapItemGroup));
            ToggleBottomBarCommand = new RelayCommand(_ => IsBottomBarExpanded = !IsBottomBarExpanded);
            CloseOverlayCommand = new RelayCommand(_ => CloseOverlay());
            SelectDifficultyCommand = new RelayCommand(param => SelectDifficulty(param as DifficultyItem));
            RemoveItemCommand = new RelayCommand(param => RemoveItem(param as SelectedItemInfo));
            EditItemCommand = new RelayCommand(param => EditItem(param as SelectedItemInfo));
            RemoveItemCommand = new RelayCommand(param => RemoveItem(param as SelectedItemInfo));
            EditItemCommand = new RelayCommand(param => EditItem(param as SelectedItemInfo));
            ClearCacheCommand = new RelayCommand(_ => ClearCache());
            OpenSupporterUrlCommand = new RelayCommand(_ => OpenSupporterUrl());
            OpenBeatmapUrlCommand = new RelayCommand(param => OpenBeatmapUrl(param as MapItemGroup));
            OpenDirectoryCommand = new RelayCommand(param => OpenDirectory(param as MapItemGroup));
            ExportBackgroundCommand = new RelayCommand(param => ExportBackground(param as MapItemGroup));
            OpenUpdateWindowCommand = new RelayCommand(_ => OpenUpdateWindow());
            OpenSpotifyUrlCommand = new RelayCommand(OpenSpotifyUrl);
            OpenGithubCommand = new RelayCommand(_ => OpenGithub());

        }

        private void OpenSpotifyUrl(object? parameter)
        {
            string? url = null;
            if (parameter is MapItemGroup group) url = group.SpotifyUrl;
            else if (parameter is string s) url = s;

            if (!string.IsNullOrEmpty(url))
            {
                PlatformService.OpenUrl(url);
            }
        }

        private async Task FetchSpotifyStatusForAllAsync()
        {
            // Only run if credentials are set
            if (string.IsNullOrEmpty(SettingsService.Settings.SpotifyClientId) || 
                string.IsNullOrEmpty(SettingsService.Settings.SpotifyClientSecret))
                return;

            var groupsToProcess = _allMapGroups.Where(g => !g.IsOnSpotify).ToList();
            if (!groupsToProcess.Any()) return;

            // Process in batches to avoid overwhelming the API
            const int batchSize = 5;
            for (int i = 0; i < groupsToProcess.Count; i += batchSize)
            {
                var batch = groupsToProcess.Skip(i).Take(batchSize);
                var tasks = batch.Select(async group =>
                {
                    var (isOnSpotify, url) = await SpotifyService.Instance.SearchTrackAsync(group.Artist, group.Title);
                    if (isOnSpotify)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            group.IsOnSpotify = true;
                            group.SpotifyUrl = url;
                            
                            // Also update difficulties
                            foreach (var diff in group.Difficulties)
                            {
                                diff.Difficulty.IsOnSpotify = true;
                                diff.Difficulty.SpotifyUrl = url;
                            }
                        });
                    }
                });

                await Task.WhenAll(tasks);
                // Save cache periodically
                if (i % 20 == 0) SaveMapCache();
                
                await Task.Delay(100); // Brief delay between batches
            }

            SaveMapCache();
        }

        // InitializeAsync will be called from View OnLoaded

        public async Task InitializeAsync()
        {
            // Give the UI thread a moment to start the entrance animation smoothly
            await Task.Delay(50);
            
            await Task.CompletedTask;
            // Auto-scan for Companella on Windows


            if (IsCompanellaSupported)
            {
                _ = AutoDiscoverCompanellaAsync();
            }

            // Check for updates on startup
            _ = CheckUpdatesOnStartup();

            // Auto-load saved path if enabled - load from cache then smart scan for new
            if (SettingsService.Settings.RememberSongsPath &&
                !string.IsNullOrEmpty(SettingsService.Settings.LastSongsPath) &&
                Directory.Exists(SettingsService.Settings.LastSongsPath))
            {
                _ = LoadFromCacheAndSmartScan(SettingsService.Settings.LastSongsPath);
            }
            else
            {
                IsFolderSelectionVisible = true;
            }

            IsStartingUp = false;
            
            // Fetch GitHub stars after startup
            _ = FetchGithubStarsAsync();
        }




        // ... existing methods ...

        private async void BrowseOutputPath()
        {
            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (topLevel != null)
            {
                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
                {
                    Title = "Select Output Directory",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    OutputPath = folders[0].Path.LocalPath;
                    // Save to settings
                    SettingsService.Settings.LastUsedPath = OutputPath;
                    SettingsService.Save();
                }
            }
        }


        private void CloseOverlay()
        {
            IsOverlayOpen = false;
            OverlayMapGroup = null;
        }

        private void SelectDifficulty(DifficultyItem? diff)
        {
            if (diff == null || OverlayMapGroup == null) return;

            // Logic: Selecting a single diff from a stack
            // We want to add this specific difficulty to the selection
            // If the group was already selected (maybe another diff), we keep it selected but update LastSelectedItem?
            // Or does selecting one diff imply the group is "partially" selected?
            // For now, let's treat the Group as Selected if at least one diff is selected.
            
            diff.IsSelected = !diff.IsSelected; // Toggle
            
            // Play audio preview if selecting (and it's a valid audio file)
            if (diff.IsSelected && !string.IsNullOrEmpty(diff.Difficulty.Mp3Path))
            {
                AudioService.Instance.PlayPreview(diff.Difficulty.Mp3Path, diff.Difficulty.PreviewTime);
            }
            
            // Update group selection state based on children
            // Use the new method to avoid triggering the "Select All" logic down to children again
            var anySelected = OverlayMapGroup.Difficulties.Any(d => d.IsSelected);
            OverlayMapGroup.SetIsSelectedWithoutPropagation(anySelected);

            if (diff.IsSelected)
            {
                LastSelectedItem = new SelectedItemInfo 
                { 
                    MapGroup = OverlayMapGroup, 
                    DisplayName = $"{OverlayMapGroup.Artist} - {OverlayMapGroup.Title}",
                    SubDisplayName = diff.DifficultyName 
                };
                IsBottomBarExpanded = false;
            }
            
            RefreshSelectedItems();
            
            // Optional: Close overlay after selection? User might want to select multiple.
            // "select individually for conversion" implies multiple choice.
            // Let's keep overlay open.
        }

        private void ToggleMapSelection(MapItemGroup? group)
        {
            if (group == null) return;

            // If it's a stack (multiple difficulties), open the overlay
            if (group.IsStack) // Use the new property
            {
                OverlayMapGroup = group;
                IsOverlayOpen = true;
                return;
            }

            // Standard single-map behavior
            if (group.IsSelected)
            {
                // Deselect group and all sub-items
                group.IsSelected = false;
                foreach (var diff in group.Difficulties)
                {
                    diff.IsSelected = false;
                }
                
                RefreshSelectedItems();
                return;
            }

            // Select
            group.IsSelected = true;
            LastSelectedItem = new SelectedItemInfo 
            { 
                MapGroup = group, 
                DisplayName = $"{group.Artist} - {group.Title}" 
            };
            IsBottomBarExpanded = false;
            RefreshSelectedItems();
        }

        private async void OpenSettings()
        {
            try
            {
                var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
                if (topLevel != null)
                {
                    var dialog = new Views.SettingsWindow();
                    await dialog.ShowDialog(topLevel);

                    // Reload settings
                    _processCovers = SettingsService.Settings.ProcessCovers;
                    _createBackups = SettingsService.Settings.CreateBackups;
                    OnPropertyChanged(nameof(ProcessCovers));
                    OnPropertyChanged(nameof(CreateBackups));
                    
                    await RefreshCompanellaSorting();
                }
            }
            catch (Exception)
            {
            }
        }

        private async void Browse()
        {
            // Use Avalonia's file dialog
            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (topLevel != null)
            {
                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
                {
                    Title = "Select the osu! Songs folder",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    var path = folders[0].Path.LocalPath;
                    _ = SetPathAsync(path, useSmartScan: false);
                }
            }
        }

        private void UseDefaultPath()
        {
            string defaultPath = PlatformService.GetDefaultOsuSongsPath();
            _ = SetPathAsync(defaultPath, useSmartScan: false);
        }

        private void Rescan()
        {
            CanLoadMore = false;
            OnPropertyChanged(nameof(ShowLoadMore));

            if (!string.IsNullOrEmpty(SelectedPath) && Directory.Exists(SelectedPath))
            {
                // Clear cache to force full rescan
                ClearCache();
                _ = SetPathAsync(SelectedPath, useSmartScan: false);
            }
        }


        private void ClearCache()
        {
            try
            {
                var cachePath = GetCacheFilePath();
                if (File.Exists(cachePath))
                    File.Delete(cachePath);
                SettingsService.Settings.ScannedFolders = "";
                SettingsService.Save();
            }
            catch { }
        }

        // Returns mapping of folder name -> last scanned ticks (UTC). Backwards-compatible with older format which stored only names (ticks=0).
        private Dictionary<string, long> GetScannedFoldersInfo()
        {
            var scannedStr = SettingsService.Settings.ScannedFolders ?? "";
            var dict = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(scannedStr))
                return dict;

            foreach (var token in scannedStr.Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                // New format: "folderName=ticks". Old format may be just the folder name.
                var parts = token.Split('=');
                if (parts.Length == 2 && long.TryParse(parts[1], out long ticks))
                {
                    dict[parts[0]] = ticks;
                }
                else
                {
                    dict[token] = 0L; // legacy entry
                }
            }

            return dict;
        }

        // Backwards-compatible helper that returns just the set of scanned folder names
        private HashSet<string> GetScannedFolders()
        {
            return new HashSet<string>(GetScannedFoldersInfo().Keys, StringComparer.OrdinalIgnoreCase);
        }

        private void SaveScannedFoldersInfo(Dictionary<string, long> info)
        {
            SettingsService.Settings.ScannedFolders = string.Join("|", info.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            SettingsService.Save();
        }

        // Backwards-compatible save
        private void SaveScannedFolders(HashSet<string> folders)
        {
            var dict = folders.ToDictionary(f => f, f => 0L);
            SaveScannedFoldersInfo(dict);
        }

        private async Task FetchGithubStarsAsync()
        {
            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("osu-tag");
                var response = await client.GetAsync("https://api.github.com/repos/kaanreal/osu-tag");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("stargazers_count", out var stars))
                    {
                        GithubStars = stars.GetInt32().ToString();
                    }
                }
            }
            catch { /* Silent fail */ }
        }

        private void OpenGithub()
        {
            PlatformService.OpenUrl("https://github.com/kaanreal/osu-tag");
        }

        private string GetCacheFilePath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var cacheDir = Path.Combine(appData, "osu!tag");
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);
            return Path.Combine(cacheDir, "mapcache.json");
        }

        internal class CachedMapData
        {
            public string Artist { get; set; } = "";
            public string Title { get; set; } = "";
            public string Creator { get; set; } = "";
            public string? Source { get; set; }
            public string? Tags { get; set; }
            public string? CoverPath { get; set; }
            public string? PreviewMp3Path { get; set; }
            public int PreviewTime { get; set; }
            public int BeatmapSetId { get; set; } = -1;
            public string? DirectoryPath { get; set; }
            public bool IsOnSpotify { get; set; }
            public string? SpotifyUrl { get; set; }
            public List<CachedDifficulty> Difficulties { get; set; } = new();
        }

        internal class CachedDifficulty
        {
            public string DifficultyName { get; set; } = "";
            public string Mp3Path { get; set; } = "";
            public string OsuFilePath { get; set; } = "";
            public string? Rate { get; set; }
            public string? Artist { get; set; }
            public string? Title { get; set; }
            public string? CoverPath { get; set; }
            public int PreviewTime { get; set; } = -1;
            public bool IsOnSpotify { get; set; }
            public string? SpotifyUrl { get; set; }
        }

        private void SaveMapCache()
        {
            try
            {
                var cacheData = _allMapGroups.Select(g => new CachedMapData
                {
                    Artist = g.Artist,
                    Title = g.Title,
                    Creator = g.Creator,
                    Source = g.Source,
                    Tags = g.Tags,
                    CoverPath = g.CoverPath,
                    PreviewMp3Path = g.PreviewMp3Path,
                    PreviewTime = g.PreviewTime,
                    BeatmapSetId = g.BeatmapSetId,
                    DirectoryPath = g.DirectoryPath,
                    IsOnSpotify = g.IsOnSpotify,
                    SpotifyUrl = g.SpotifyUrl,
                    Difficulties = g.Difficulties.Select(d => new CachedDifficulty
                    {
                        DifficultyName = d.DifficultyName,
                        Mp3Path = d.Difficulty.Mp3Path,
                        OsuFilePath = d.Difficulty.OsuFilePath,
                        Rate = d.Difficulty.Rate,
                        Artist = d.Artist,
                        Title = d.Title,
                        CoverPath = d.CoverPath,
                        PreviewTime = d.Difficulty.PreviewTime,
                        IsOnSpotify = d.Difficulty.IsOnSpotify,
                        SpotifyUrl = d.Difficulty.SpotifyUrl
                    }).ToList()
                }).ToList();

                var json = JsonSerializer.Serialize(cacheData, AppJsonContext.Default.ListCachedMapData);
                File.WriteAllText(GetCacheFilePath(), json);
            }
            catch { /* Ignore cache save errors */ }
        }

        private List<MapItemGroup> LoadMapCache()
        {
            var groups = new List<MapItemGroup>();
            try
            {
                var cachePath = GetCacheFilePath();
                if (!File.Exists(cachePath))
                    return groups;

                using var fs = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
                using var reader = new StreamReader(fs, System.Text.Encoding.UTF8);
                var json = reader.ReadToEnd();
                var cacheData = JsonSerializer.Deserialize(json, AppJsonContext.Default.ListCachedMapData);

                if (cacheData == null)
                    return groups;

                foreach (var cached in cacheData)
                {
                    // Verify at least one difficulty file still exists
                    if (!cached.Difficulties.Any(d => File.Exists(d.OsuFilePath)))
                        continue;

                    var mapGroup = new MapItemGroup
                    {
                        Artist = cached.Artist,
                        Title = cached.Title,
                        Creator = cached.Creator,
                        Source = cached.Source,
                        Tags = cached.Tags,
                        CoverPath = cached.CoverPath,
                        PreviewMp3Path = cached.PreviewMp3Path,
                        PreviewTime = cached.PreviewTime,
                        BeatmapSetId = cached.BeatmapSetId,
                        DirectoryPath = cached.DirectoryPath,
                        IsOnSpotify = cached.IsOnSpotify,
                        SpotifyUrl = cached.SpotifyUrl,
                        OpenBeatmapUrlCommand = OpenBeatmapUrlCommand,
                        OpenDirectoryCommand = OpenDirectoryCommand,
                        ExportBackgroundCommand = ExportBackgroundCommand,
                        OpenSpotifyUrlCommand = OpenSpotifyUrlCommand
                    };
                    
                    // Smart Check: Check if all difficulties share the same metadata
                    var distinctTitles = cached.Difficulties.Select(d => d.Title).Where(t => !string.IsNullOrEmpty(t)).Distinct().ToList();
                    var distinctArtists = cached.Difficulties.Select(d => d.Artist).Where(a => !string.IsNullOrEmpty(a)).Distinct().ToList();
                    var distinctMp3s = cached.Difficulties.Select(d => d.Mp3Path).Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
                    
                    // Logic:
                    // 1. If Metadata varies -> Mixed Pack (Compilation)
                    // 2. If Metadata is constant BUT Audio varies AND Artist is "Various Artists" -> Mixed Pack (Lazy Compilation)
                    // (We check for "Various Artists" to avoid flagging "Rate Packs" like Quadraphinix as Mixed Packs, since they have multiple MP3s but same Artist)
                    
                    bool metadataVaries = distinctTitles.Count > 1 || distinctArtists.Count > 1;
                    bool isVariousArtistsPack = distinctMp3s.Count > 1 && distinctArtists.Any(a => a?.Equals("Various Artists", StringComparison.OrdinalIgnoreCase) == true);
                    
                    bool isMixedPack = metadataVaries || isVariousArtistsPack;

                    if (isMixedPack)
                    {
                        // Only overwrite Group Title if the titles themselves aren't consistent
                        // (e.g. A real compilation with different songs).
                        // If it's a "Chordjack Pack" where every map is named "Chordjack Pack", keep that title.
                        if (distinctTitles.Count > 1)
                        {
                            if (!string.IsNullOrEmpty(cached.DirectoryPath))
                            {
                                 var dirName = Path.GetFileName(cached.DirectoryPath);
                                 // Clean up leading ID if present (e.g. "12345 Artist - Title")
                                 var cleanName = Regex.Replace(dirName, @"^\d+\s+", "");
                                 
                                 // User wants to remove the Artist from the title string (Format: "Artist - Title")
                                 // Split by " - " and take the rest
                                 var parts = cleanName.Split(new[] { " - " }, 2, StringSplitOptions.None);
                                 if (parts.Length > 1)
                                 {
                                     cleanName = parts[1];
                                 }

                                 mapGroup.Title = cleanName;
                                 mapGroup.Artist = "Various Artists"; 
                            }
                        }
                    }

                    foreach (var d in cached.Difficulties)
                    {
                        if (!File.Exists(d.OsuFilePath))
                            continue;

                        var diff = new OsuMapDifficulty
                        {
                            DifficultyName = d.DifficultyName,
                            Mp3Path = d.Mp3Path,
                            OsuFilePath = d.OsuFilePath,
                            Rate = d.Rate,
                            Artist = d.Artist,
                            Title = d.Title,
                            CoverPath = d.CoverPath,
                            PreviewTime = d.PreviewTime,
                            IsOnSpotify = d.IsOnSpotify,
                            SpotifyUrl = d.SpotifyUrl
                        };

                        // FOR CHILDREN: If it's a mixed pack, the "Title" metadata is often generic (e.g. "Pack Name")
                        // The user wants to see the "Version" (Song Name) instead.
                        var displayTitle = isMixedPack ? d.DifficultyName : (d.Title ?? cached.Title);

                        mapGroup.Difficulties.Add(new DifficultyItem
                        {
                            DifficultyName = d.DifficultyName,
                            Difficulty = diff,
                            // IsSelected = false by default to prevent "select all" behavior for stacks
                            Title = displayTitle, 
                            Artist = d.Artist ?? cached.Artist, 
                            CoverPath = d.CoverPath ?? cached.CoverPath
                        });
                    }

                    // Create unique audio files list
                    var uniqueMp3s = mapGroup.Difficulties
                        .Select(d => d.Difficulty.Mp3Path)
                        .Distinct()
                        .ToList();

                    foreach (var mp3Path in uniqueMp3s)
                    {
                        var fileName = Path.GetFileName(mp3Path);
                        mapGroup.UniqueAudioFiles.Add(new AudioFileItem
                        {
                            Mp3Path = mp3Path,
                            DisplayName = fileName,
                            PreviewTime = cached.PreviewTime
                        });
                    }

                if (mapGroup.Difficulties.Count > 0)
                    {
                        FinalizeMapGroupMetadata(mapGroup);
                        groups.Add(mapGroup);
                    }
                }
            }
            catch { /* Ignore cache load errors */ }

            return groups;
        }

        private async Task LoadFromCacheAndSmartScan(string path)
        {
            SelectedPath = path;
            ErrorMessage = "";
            MapGroups.Clear();
            _allMapGroups.Clear();
            _filteredMapGroups.Clear();
            MapsLoaded = false;
            IsScanning = true;
            ScanProgress = 0;
            PathStatusMessage = "Loading cached maps...";

            // Yield once to let UI update state (loading bar text) before heavy I/O
            await Task.Yield();

            // Load from cache first
            var cachedGroups = await Task.Run(() => LoadMapCache());

            if (cachedGroups.Count > 0)
            {
                foreach (var group in cachedGroups)
                {
                    _allMapGroups.Add(group);
                }
                PathStatusMessage = $"Loaded {cachedGroups.Count} cached map sets, checking for new maps...";
            }

            // Now do a smart scan for new maps only
            await SetPathAsync(path, useSmartScan: true);
        }

        private async Task SetPathAsync(string path, bool useSmartScan = false)
        {
            SelectedPath = path;
            IsFolderSelectionVisible = false;
            ErrorMessage = "";

            // Save path if remember is enabled
            if (SettingsService.Settings.RememberSongsPath)
            {
                SettingsService.Settings.LastSongsPath = path;
                SettingsService.Save();
            }

            // Initialize Audio Engine in background (parallel to map loading)
            _ = Task.Run(() => AudioService.Instance.Initialize());

            // Only clear if not using smart scan, or if smart scan is disabled in settings
            bool smartScanEnabled = useSmartScan && SettingsService.Settings.SmartScan;

            if (!smartScanEnabled)
            {
                MapGroups.Clear();
                _allMapGroups.Clear();
                _filteredMapGroups.Clear();
            }

            MapsLoaded = false;
            IsScanning = true;
            ScanProgress = 0;
            PathStatusMessage = "Initializing...";

            if (!Directory.Exists(path))
            {
                PathStatusMessage = "⚠ Path does not exist";
                ErrorMessage = "The specified path could not be found.";
                IsScanning = false;
                return;
            }

            try
            {
                // Get folder count first for progress
                var allFolders = Directory.GetDirectories(path);

                // For smart scan, filter out already scanned folders using saved timestamps
                var scannedInfo = smartScanEnabled ? GetScannedFoldersInfo() : new Dictionary<string, long>();
                
                // If smart scan is enabled but we have no maps loaded (e.g. cache failed or first run),
                // we must force a scan of all folders to populate the list.
                // Clearing scannedInfo forces the filter below to treat all folders as new.
                if (smartScanEnabled && _allMapGroups.Count == 0)
                {
                    scannedInfo.Clear();
                }

                var existingFolderNames = _allMapGroups
                    .SelectMany(g => g.Difficulties)
                    .Select(d => Path.GetDirectoryName(d.Difficulty.Mp3Path))
                    .Where(p => p != null)
                    .Select(p => Path.GetFileName(p!))
                    .Distinct()
                    .ToHashSet();

                // Only re-scan folders that are not known or where .osu files are newer than the recorded ticks
                var foldersToScan = smartScanEnabled
                    ? allFolders.Where(f =>
                    {
                        var name = Path.GetFileName(f);
                        if (!scannedInfo.TryGetValue(name, out long savedTicks))
                            return true; // never scanned before

                        try
                        {
                            var osuFiles = Directory.GetFiles(f, "*.osu");
                            if (osuFiles.Length == 0) return true;

                            long maxTicks = 0;
                            foreach (var osu in osuFiles)
                            {
                                long t = File.GetLastWriteTimeUtc(osu).Ticks;
                                if (t > maxTicks) maxTicks = t;
                            }

                            return maxTicks > savedTicks;
                        }
                        catch
                        {
                            // If anything goes wrong checking timestamps, be conservative and re-scan
                            return true;
                        }
                    }).ToArray()
                    : allFolders;

                int totalFolders = foldersToScan.Length;
                var maps = new List<OsuMap>();
                var newScannedFolders = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

                if (totalFolders == 0 && smartScanEnabled)
                {
                    ScanStatusMessage = "No new folders to scan";
                    PathStatusMessage = $"✓ No new maps - {_allMapGroups.Count} map sets loaded";
                    MapsLoaded = _allMapGroups.Count > 0;
                    if (MapsLoaded)
                    {
                        await LoadCompanellaPlayCounts();
                        await FilterMapsAsync();
                        _ = Task.Run(() => FetchSpotifyStatusForAllAsync());
                    }
                    IsScanning = false;
                    await Task.Delay(200); // Allow UI to layout
                    IsInitialLoadDone = true;
                    return;
                }

                ScanStatusMessage = smartScanEnabled
                    ? $"Smart scanning {totalFolders} new/changed folders..."
                    : $"Scanning {totalFolders} folders...";

                var scannedFoldersDict = new System.Collections.Concurrent.ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase);

                var scanner = new Services.OsuMapScanner();
                var mapsBag = new System.Collections.Concurrent.ConcurrentBag<OsuMap>();
                int processed = 0;
                var lastUpdate = DateTime.MinValue;
                var updateInterval = TimeSpan.FromMilliseconds(100);

                // Limit degree of parallelism to reduce heavy simultaneous I/O
                int parallelism = Math.Max(Services.OsuMapScanner.MinParallelism, Environment.ProcessorCount / Services.OsuMapScanner.ParallelismDivider);

                await Parallel.ForEachAsync(foldersToScan, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, async (folder, ct) =>
                {
                    try
                    {
                        var folderMaps = await scanner.ScanSingleFolderAsync(folder);
                        foreach (var map in folderMaps)
                        {
                            mapsBag.Add(map);
                        }

                        // Record folder as scanned (store latest .osu write timestamp)
                        var name = Path.GetFileName(folder);
                        try
                        {
                            var osuFiles = Directory.GetFiles(folder, "*.osu");
                            long maxTicks = 0;
                            foreach (var osu in osuFiles)
                            {
                                long t = File.GetLastWriteTimeUtc(osu).Ticks;
                                if (t > maxTicks) maxTicks = t;
                            }

                            if (maxTicks == 0)
                                maxTicks = DateTime.UtcNow.Ticks;

                            scannedFoldersDict[name] = maxTicks;
                        }
                        catch
                        {
                            scannedFoldersDict[name] = DateTime.UtcNow.Ticks;
                        }
                    }
                    catch { /* Ignore per-folder errors while scanning to continue other folders */ }

                    var currentProcessed = System.Threading.Interlocked.Increment(ref processed);

                    // Throttle UI updates to every 100ms
                    var now = DateTime.UtcNow;
                    if (now - lastUpdate > updateInterval || currentProcessed == totalFolders)
                    {
                        lastUpdate = now;
                        int progress = (int)((currentProcessed / (double)totalFolders) * 100);
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            ProgressPercentage = progress;
                            ScanProgress = progress;
                            ScanStatusMessage = $"Scanning... {currentProcessed}/{totalFolders} folders ({mapsBag.Count} maps found)";
                        });
                    }
                });

                maps.AddRange(mapsBag);

                // Merge concurrent dict into outer scope collection
                foreach (var kvp in scannedFoldersDict)
                {
                    newScannedFolders[kvp.Key] = kvp.Value;
                }

                // Save scanned folders to cache for smart scan
                if (smartScanEnabled || SettingsService.Settings.SmartScan)
                {
                    foreach (var kvp in newScannedFolders)
                    {
                        scannedInfo[kvp.Key] = kvp.Value;
                    }
                    SaveScannedFoldersInfo(scannedInfo);
                }

                if (maps.Count == 0 && _allMapGroups.Count == 0)
                {
                    PathStatusMessage = "⚠ No maps found";
                    ErrorMessage = "No beatmaps were found in the specified path.";
                    IsScanning = false;
                    return;
                }

                if (maps.Count == 0 && _allMapGroups.Count > 0)
                {
                    // Smart scan found no new maps but we have existing ones
                    PathStatusMessage = $"✓ No new maps - {_allMapGroups.Count} map sets loaded";
                    await LoadCompanellaPlayCounts();
                    await FilterMapsAsync();
                    MapsLoaded = true;
                    IsScanning = false;
                    return;
                }

                ScanStatusMessage = "Processing map groups...";

                // Group maps and create MapItemGroups on background thread
                var mapGroups = await Task.Run(() =>
                {
                    var groups = new List<MapItemGroup>();
                    // Group by Directory Path to keep mappacks together even if metadata differs
                    var groupedMaps = maps.GroupBy(m => 
                    {
                        try
                        {
                            var diff = m.Difficulties.FirstOrDefault();
                            // Fallback to Artist-Title if path is invalid or unavailable
                            if (diff != null && !string.IsNullOrEmpty(diff.OsuFilePath))
                            {
                                var dir = Path.GetDirectoryName(diff.OsuFilePath);
                                if (!string.IsNullOrEmpty(dir)) return dir;
                            }
                        }
                        catch 
                        {
                            // Fallback on error
                        }
                        return $"{m.Artist} - {m.Title}";
                    }).ToList();

                    foreach (var group in groupedMaps)
                    {
                        var firstMap = group.First();
                        var mapGroup = new MapItemGroup
                        {
                            Artist = firstMap.Artist,
                            Title = firstMap.Title,
                            Creator = firstMap.Creator,
                            Source = firstMap.Source,
                            Tags = firstMap.Tags,
                            CoverPath = firstMap.CoverPath,
                            PreviewMp3Path = firstMap.Difficulties.FirstOrDefault()?.Mp3Path,
                            PreviewTime = firstMap.PreviewTime,
                            BeatmapSetId = firstMap.BeatmapSetId,
                            DirectoryPath = Path.GetDirectoryName(firstMap.Difficulties.FirstOrDefault()?.OsuFilePath),
                            OpenBeatmapUrlCommand = OpenBeatmapUrlCommand,
                            OpenDirectoryCommand = OpenDirectoryCommand,
                            ExportBackgroundCommand = ExportBackgroundCommand
                        };

                        // Smart Check (Fresh Scan): Check if all difficulties share metadata
                        var allDiffsInGroup = group.SelectMany(g => g.Difficulties).ToList();
                        var distinctTitles2 = allDiffsInGroup.Select(d => d.Title).Where(t => !string.IsNullOrEmpty(t)).Distinct().ToList();
                        var distinctArtists2 = allDiffsInGroup.Select(d => d.Artist).Where(a => !string.IsNullOrEmpty(a)).Distinct().ToList();
                        var distinctMp3s2 = allDiffsInGroup.Select(d => d.Mp3Path).Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
                        
                        bool metadataVaries2 = distinctTitles2.Count > 1 || distinctArtists2.Count > 1;
                        bool isVariousArtistsPack2 = distinctMp3s2.Count > 1 && distinctArtists2.Any(a => a?.Equals("Various Artists", StringComparison.OrdinalIgnoreCase) == true);

                        bool isMixedPack2 = metadataVaries2 || isVariousArtistsPack2;

                        if (isMixedPack2)
                        {
                            if (distinctTitles2.Count > 1)
                            {
                                if (!string.IsNullOrEmpty(mapGroup.DirectoryPath))
                                {
                                     var dirName = Path.GetFileName(mapGroup.DirectoryPath);
                                     var cleanName = Regex.Replace(dirName, @"^\d+\s+", "");
                                     
                                     // Strip Artist ("Artist - Title")
                                     var parts = cleanName.Split(new[] { " - " }, 2, StringSplitOptions.None);
                                     if (parts.Length > 1)
                                     {
                                         cleanName = parts[1];
                                     }

                                     mapGroup.Title = cleanName;
                                     mapGroup.Artist = "Various Artists";
                                }
                            }
                        }

                        // Add all difficulties for this map
                        foreach (var map in group)
                        {
                            foreach (var diff in map.Difficulties)
                            {
                                 var displayTitle = isMixedPack2 ? diff.DifficultyName : map.Title;

                                 mapGroup.Difficulties.Add(new DifficultyItem
                                 {
                                     DifficultyName = diff.DifficultyName,
                                     Difficulty = diff,
                                     Title = displayTitle,
                                     Artist = map.Artist,
                                     CoverPath = map.CoverPath
                                 });
                            }
                            
                            // Also add unique MP3s to the separate tracking set
                            foreach (var diff in map.Difficulties)
                            {
                                if (!string.IsNullOrEmpty(diff.Mp3Path) && !mapGroup.UniqueAudioFiles.Any(a => a.Mp3Path == diff.Mp3Path))
                                {
                                    mapGroup.UniqueAudioFiles.Add(new AudioFileItem 
                                    { 
                                        Mp3Path = diff.Mp3Path,
                                        DisplayName = isMixedPack2 ? diff.DifficultyName : Path.GetFileName(diff.Mp3Path), 
                                        PreviewTime = diff.PreviewTime
                                    });
                                }
                            }
                        }

                        groups.Add(mapGroup);
                    }
                    // Finalize metadata for all groups (e.g. override titles for stacks)
                    foreach (var group in groups)
                    {
                        FinalizeMapGroupMetadata(group);
                    }

                    return groups;
                });

                ScanStatusMessage = "Loading maps...";

                // Add all to backing collection (no UI update yet)
                foreach (var group in mapGroups)
                {
                    _allMapGroups.Add(group);
                }

                // Load Companella play counts if enabled
                await LoadCompanellaPlayCounts();

                // Apply filter - this only loads first 50 items to UI
                await FilterMapsAsync();
                
                _ = Task.Run(() => FetchSpotifyStatusForAllAsync());

                if (smartScanEnabled && maps.Count > 0)
                {
                    PathStatusMessage = $"✓ Added {maps.Count} new maps - {_allMapGroups.Count} total map sets";
                }
                else
                {
                    PathStatusMessage = $"✓ {maps.Count} maps found in {_allMapGroups.Count} map sets";
                }
                MapsLoaded = true;

                // Save cache for next startup
                if (SettingsService.Settings.RememberSongsPath)
                {
                    await Task.Run(() => SaveMapCache());
                }
                
                await Task.Delay(300); // Allow initial cards to render
                IsInitialLoadDone = true;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error scanning: {ex.Message}";
                PathStatusMessage = "⚠ Scan failed";
            }
            finally
            {
                IsScanning = false;
                ScanProgress = 100;
                ScanStatusMessage = "";
                ((RelayCommand)RescanCommand).RaiseCanExecuteChanged();
            }
        }

        private void SelectAll()
        {
            foreach (var group in MapGroups)
            {
                if (group.IsStack)
                {
                    // For stacks (multi-audio): mark card as selected and select ALL difficulties
                    // This matches "Select All" behavior for the user to convert the whole pack if desired
                    group.IsSelected = true;
                    foreach (var diff in group.Difficulties)
                    {
                        diff.IsSelected = true;
                    }
                }
                else if (group.HasMultipleDifferentRates)
                {
                    // For multi-rate maps: mark card as selected and select only the base rate (1.0x) difficulty
                    group.IsSelected = true;
                    var baseRateDiff = group.Difficulties.FirstOrDefault(d =>
                        string.IsNullOrEmpty(d.Difficulty.Rate) || d.Difficulty.Rate == "1.0x");
                    if (baseRateDiff != null)
                    {
                        baseRateDiff.IsSelected = true;
                    }
                }
                else
                {
                    // For normal maps: mark card as selected
                    group.IsSelected = true;
                }
            }
            RefreshSelectedItems();
        }

        private void DeselectAll()
        {
            foreach (var group in MapGroups)
            {
                group.IsSelected = false;
                // Also deselect all difficulties
                foreach (var diff in group.Difficulties)
                {
                    diff.IsSelected = false;
                }
            }
            RefreshSelectedItems();
            IsBottomBarExpanded = false;
        }

        /// <summary>
        /// Refreshes the list of selected items for the selection panel.
        /// </summary>
        public void RefreshSelectedItems()
        {
            var newSelection = new List<object>();

            foreach (var group in _allMapGroups)
            {
                if (group.IsStack)
                {
                    foreach (var diff in group.Difficulties.Where(d => d.IsSelected))
                    {
                        var info = new SelectedItemInfo
                        {
                            MapGroup = group,
                            AudioFile = null,
                            DisplayName = $"{group.Artist} - {group.Title}",
                            SubDisplayName = diff.DifficultyName,
                            PlaybackRate = ParseRateMultiplier(diff.Difficulty.Rate)
                        };
                        newSelection.Add(info);
                    }
                }
                else if (group.IsSelected)
                {
                    // For single maps, try to find a selected difficulty or at least one with a rate
                    var selectedDiff = group.Difficulties.FirstOrDefault(d => d.IsSelected) ?? group.Difficulties.FirstOrDefault();
                    
                    newSelection.Add(new SelectedItemInfo
                    {
                        MapGroup = group,
                        AudioFile = null,
                        DisplayName = $"{group.Artist} - {group.Title}",
                        SubDisplayName = null,
                        PlaybackRate = selectedDiff != null ? ParseRateMultiplier(selectedDiff.Difficulty.Rate) : 1.0f
                    });
                }
            }

            SelectedItems = new ObservableCollection<object>(newSelection);
            
            // Manage LastSelectedItem to prevent stale/incorrect collapsed state details
            if (SelectedCount == 0)
            {
                LastSelectedItem = null;
                IsBottomBarExpanded = false;
            }
            else if (LastSelectedItem == null || !newSelection.Any(x => x is SelectedItemInfo info && info.MapGroup == LastSelectedItem.MapGroup && info.SubDisplayName == LastSelectedItem.SubDisplayName))
            {
                // If current LastSelectedItem is gone or null, pick the actual last one from current selection
                var last = newSelection.LastOrDefault() as SelectedItemInfo;
                if (last != null) LastSelectedItem = last;
            }

            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(HasSelectedMaps));
            ((RelayCommand)StartConversionCommand).RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Removes an item from selection.
        /// </summary>
        public void RemoveFromSelection(SelectedItemInfo item)
        {
            if (item.MapGroup != null && item.MapGroup.IsStack)
            {
                // For stacks, find the difficulty by name (SubDisplayName) and deselect it
                var diff = item.MapGroup.Difficulties.FirstOrDefault(d => d.DifficultyName == item.SubDisplayName);
                if (diff != null)
                {
                    diff.IsSelected = false;
                }
                
                // Update group selection state
                bool anySelected = item.MapGroup.Difficulties.Any(d => d.IsSelected);
                item.MapGroup.SetIsSelectedWithoutPropagation(anySelected);
            }
            else if (item.MapGroup != null)
            {
                item.MapGroup.IsSelected = false;
            }
            RefreshSelectedItems();
        }

        private float ParseRateMultiplier(string? rateStr)
        {
            if (string.IsNullOrEmpty(rateStr)) return 1.0f;
            var clean = rateStr.Replace("x", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (float.TryParse(clean, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float result))
                return result;
            return 1.0f;
        }

        /// <summary>
        /// Clears all selections.
        /// </summary>
        public void ClearSelection()
        {
            DeselectAll();
        }

        /// <summary>
        /// Checks if any map or audio file is selected for conversion.
        /// </summary>
        private bool HasAnySelection()
        {
            return _allMapGroups.Any(g =>
                // Simple maps: check if group is selected
                (g.IsSelected && !g.IsStack) ||
                // Stacks: check if any difficulty is selected
                (g.IsStack && g.Difficulties.Any(d => d.IsSelected))
            );
        }

        private async void StartConversion()
        {
            // Check if any maps are selected (either simple selection or expanded with audio files selected)
            bool hasSelection = _allMapGroups.Any(g =>
                // Simple maps: check if group is selected
                (g.IsSelected && !g.IsStack) ||
                // Stacks: check if any difficulty is selected
                (g.IsStack && g.Difficulties.Any(d => d.IsSelected))
            );

            if (!Directory.Exists(SelectedPath) || !hasSelection)
            {
                ErrorMessage = "Please select at least one map or audio file to convert!";
                return;
            }

            IsProcessing = true;
            ProgressPercentage = 0;
            ConversionResults.Clear();
            ErrorMessage = "";

            try
            {
                await Task.Run(() => RunConversion());
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private void RunConversion()
        {
            var config = new Config(OutputPath);
            var imageProcessor = new Services.ImageProcessor();
            var mp3Tagger = new Services.Mp3Tagger();

            // Build list of items to convert
            var itemsToConvert = new List<(MapItemGroup Group, OsuMapDifficulty Diff, string DiffName)>();

            foreach (var group in _allMapGroups)
            {
                if (group.IsStack)
                {
                    // For stacks (multi-audio), convert individually selected difficulties from the overlay
                    // We iterate difficulties directly because user selects them specifically
                    foreach (var diffItem in group.Difficulties.Where(d => d.IsSelected))
                    {
                        itemsToConvert.Add((group, diffItem.Difficulty, diffItem.DifficultyName));
                    }
                }
                else if (group.IsSelected)
                {
                    // For simple selection, convert the first difficulty (standard logic)
                    var firstDiff = group.Difficulties.FirstOrDefault();
                    if (firstDiff != null)
                    {
                        itemsToConvert.Add((group, firstDiff.Difficulty, firstDiff.DifficultyName));
                    }
                }
            }

            if (itemsToConvert.Count == 0)
            {
                AddResult("No items", "No maps or audio files were selected for conversion.");
                return;
            }

            Directory.CreateDirectory(config.OutputDir);


            for (int i = 0; i < itemsToConvert.Count; i++)
            {
                var (group, diff, diffName) = itemsToConvert[i];

                ProgressPercentage = (int)((i + 1.0) / itemsToConvert.Count * 100);
                
                // Determine effective metadata (Separated from UI Display Title)
                // Heuristic: If it's a compilation pack (different songs), use Version as Title. 
                // If it's a rate pack (same song), use the song Title.
                
                string effArtist = !string.IsNullOrEmpty(diff.Artist) ? diff.Artist : group.Artist;
                string diffTitle = !string.IsNullOrEmpty(diff.Title) ? diff.Title : group.Title; // The raw title from .osu (often the song name)
                
                // Detection: Is this specific difficulty part of a "Mixed Pack"?
                // Match the logic from ScanMapsAsync exactly:
                var distinctTitles = group.Difficulties.Select(d => d.Difficulty.Title).Where(t => !string.IsNullOrEmpty(t)).Distinct().ToList();
                var distinctArtists = group.Difficulties.Select(d => d.Artist).Where(a => !string.IsNullOrEmpty(a)).Distinct().ToList();
                var distinctMp3s = group.Difficulties.Select(d => d.Difficulty.Mp3Path).Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();

                bool metadataVaries = distinctTitles.Count > 1 || distinctArtists.Count > 1;
                bool isVariousArtistsPack = distinctMp3s.Count > 1 && distinctArtists.Any(a => a != null && a.Equals("Various Artists", StringComparison.OrdinalIgnoreCase));
                
                bool isMixedPack = metadataVaries || isVariousArtistsPack;
                
                string effTitle = diffTitle; 
                if (isMixedPack && !string.IsNullOrEmpty(diffName))
                {
                    // For mixed packs, use the Version (diffName) as the Title
                    effTitle = diffName;
                }

                string? effCoverPath = !string.IsNullOrEmpty(diff.CoverPath) && File.Exists(diff.CoverPath) 
                    ? diff.CoverPath 
                    : group.CoverPath;
                int effPreviewTime = diff.PreviewTime > 0 ? diff.PreviewTime : group.PreviewTime;

                // CHECK FOR MANUAL OVERRIDES (From SelectedItemInfo)
                var selectedInfo = SelectedItems.Cast<SelectedItemInfo>().FirstOrDefault(info => 
                    info.MapGroup == group && 
                    (string.IsNullOrEmpty(info.SubDisplayName) || info.SubDisplayName == diffName));

                if (selectedInfo != null)
                {
                     if (!string.IsNullOrEmpty(selectedInfo.OverrideTitle)) effTitle = selectedInfo.OverrideTitle;
                     if (!string.IsNullOrEmpty(selectedInfo.OverrideArtist)) effArtist = selectedInfo.OverrideArtist;
                     if (!string.IsNullOrEmpty(selectedInfo.OverrideCoverPath)) effCoverPath = selectedInfo.OverrideCoverPath;
                }

                ProgressMessage = $"{i + 1}/{itemsToConvert.Count}: {effArtist} - {effTitle}";

                try
                {
                    string safeTitle = string.Concat(
                        $"{effArtist} - {effTitle}"
                            .Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_')
                    ).Trim();

                    string mapOutputDir = Path.Combine(config.OutputDir, safeTitle);
                    Directory.CreateDirectory(mapOutputDir);

                    string? coverOutput = null;
                    if (ProcessCovers && !string.IsNullOrEmpty(effCoverPath) && File.Exists(effCoverPath))
                    {
                        coverOutput = Path.Combine(mapOutputDir, "cover.jpg");
                        
                        // Heuristic: If cover path contains "crops" (our manual crop folder), assume it's already perfect 1:1
                        // Just copy it or process lightly (don't upscale to 3000 if it's small)
                        bool isManualCrop = effCoverPath.Contains("crops") && effCoverPath.Contains("osu!tag");

                        if (isManualCrop)
                        {
                            // Just copy the file to the output
                            File.Copy(effCoverPath, coverOutput, true);
                        }
                        else
                        {
                            // Standard processing (Crop center + Resize to 3000x3000)
                            imageProcessor.ProcessCover(effCoverPath, coverOutput, 3000, 3000);
                        }
                    }

                    string mp3Output = Path.Combine(mapOutputDir, $"{safeTitle}.mp3");
                    File.Copy(diff.Mp3Path, mp3Output, overwrite: true);

                    var osuMap = new OsuMap
                    {
                        Artist = effArtist,
                        Title = effTitle,
                        Creator = group.Creator,
                        CoverPath = effCoverPath,
                        Difficulties = new List<OsuMapDifficulty> { diff },
                        PreviewTime = effPreviewTime
                    };

                    mp3Tagger.TagMp3(mp3Output, osuMap, coverOutput);

                    // Update Discord RPC
                    DiscordRpcService.UpdateStatus("converting", i + 1);

                    AddResult("✓", $"{safeTitle}");
                }
                catch (Exception ex)
                {
                    AddResult("✗", $"{group.Artist} - {group.Title}: {ex.Message}");
                }
            }

            // Conversion complete - update RPC based on current selection state
            if (SelectedCount > 0)
            {
                DiscordRpcService.UpdateStatus("selected", SelectedCount);
            }
            else
            {
                DiscordRpcService.UpdateStatus("completed", itemsToConvert.Count);
            }

            ProgressMessage = "Conversion Complete!";
            ProgressPercentage = 100;

            AddResult("Done!", $"All maps saved to: {config.OutputDir}");

            // Telemetry: send conversion count
            try
            {
                _ = TelemetryService.TrackTotalConversions(itemsToConvert.Count);
            }
            catch
            {
                // ignore telemetry errors
            }
        }

        private void AddResult(string title, string message)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                ConversionResults.Add(new ConversionResult { Title = title, Message = message });
            });
        }

        private void FinalizeMapGroupMetadata(MapItemGroup group)
        {
            try
            {
                // Refined Logic (User Request):
                // 1. It must be a "Stack" (Multiple UNIQUE audio files = Compilations/Mappacks).
                //    Single-audio mapsets (just diffs or rates) should keep their song title.
                // 2. Titles must be identical across all difficulties (e.g. "Favorites Compilation").
                //    If titles differ (e.g. "Mappack 1" containing "Song A", "Song B"), we keep original titles.

                if (!group.IsStack) return; // Skip if not a multi-audio stack

                // Aligning logic with RunConversion (MP3 Tagging):
                // We swap Title -> Version (DifficultyName) if:
                // A) It is a "Compilation" (Different Titles) -> e.g. Mappack where Version holds real name.
                // B) It is "Multi-Artist" (Different Artists) -> e.g. Favorites Comp where Version holds real name.
                //
                // We DO NOT swap if it is a "Rate Pack" (Same Title, Same Artist) -> Keep Title "My Song".

                bool distinctTitles = group.Difficulties.Select(d => d.Title).Distinct().Count() > 1;
                bool distinctArtists = group.Difficulties.Select(d => d.Artist).Distinct().Count() > 1;

                if (distinctTitles || distinctArtists)
                {
                    // Swap to Version (DifficultyName)
                    foreach (var diff in group.Difficulties)
                    {
                        if (!string.IsNullOrEmpty(diff.DifficultyName))
                        {
                            diff.Title = diff.DifficultyName;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Safely ignore metadata finalization errors
                System.Diagnostics.Debug.WriteLine($"Error finalizing metadata: {ex.Message}");
            }
        }


        private async Task AutoDiscoverCompanellaAsync()
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string companellaPath = Path.Combine(localAppData, "Companella");
                
                if (Directory.Exists(companellaPath))
                {
                    CompanellaStatus = $"Found Companella data at {companellaPath}";
                    SettingsService.Settings.CompanellaPath = companellaPath;
                    SettingsService.Save();
                    await LoadCompanellaPlayCounts();
                }
                else
                {
                    CompanellaStatus = "Companella data not found. Please select path manually.";
                }
            }
            catch (Exception ex)
            {
                CompanellaStatus = $"Error scanning for Companella: {ex.Message}";
            }
        }
        private void OpenSupporterUrl()
        {
            var url = "https://osu.ppy.sh/store/products/supporter-tag?target=Kxxn";
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch { /* Ignore */ }
        }

        private void OpenBeatmapUrl(MapItemGroup? group)
        {
            if (group == null) return;
            
            string url;
            if (group.BeatmapSetId > 0)
            {
                url = $"https://osu.ppy.sh/s/{group.BeatmapSetId}";
            }
            else
            {
                // Fallback: Search by Artist and Title
                var query = System.Uri.EscapeDataString($"{group.Artist} {group.Title}");
                url = $"https://osu.ppy.sh/beatmapsets?q={query}";
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch { /* Ignore open errors */ }
        }

        private void OpenDirectory(MapItemGroup? group)
        {
            if (group == null) return;
            
            // Try explicit path, fallback to finding via difficulties
            var path = group.DirectoryPath;
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                 var diff = group.Difficulties.FirstOrDefault();
                 if (diff != null && !string.IsNullOrEmpty(diff.Difficulty.OsuFilePath))
                 {
                     path = Path.GetDirectoryName(diff.Difficulty.OsuFilePath);
                 }
            }

            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                try
                {
                    if (IsWindows)
                    {
                        System.Diagnostics.Process.Start("explorer.exe", path);
                    }
                    else
                    {
                        // Mac/Linux
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
                    }
                }
                catch { /* Ignore */ }
            }
        }

        private async void ExportBackground(MapItemGroup? group)
        {
            if (group == null || string.IsNullOrEmpty(group.CoverPath) || !File.Exists(group.CoverPath)) return;

            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
            if (topLevel == null) return;

            var fileName = Path.GetFileName(group.CoverPath);
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Save Background",
                SuggestedFileName = fileName,
                DefaultExtension = Path.GetExtension(fileName).TrimStart('.'),
                FileTypeChoices = new[] { new Avalonia.Platform.Storage.FilePickerFileType("Images") { Patterns = new[] { "*.jpg", "*.png", "*.jpeg" } } }
            });

            if (file != null)
            {
                try
                {
                    var localPath = file.Path.LocalPath;
                    File.Copy(group.CoverPath, localPath, true);
                }
                catch { /* Ignore save errors */ }
            }
        }
        
        private async Task CheckUpdatesOnStartup()
        {
            try
            {
                var updateInfo = await Task.Run(() => UpdateService.Instance.CheckForUpdatesAsync());
                
                // Update UI on main thread
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (updateInfo != null && updateInfo.IsNewer)
                    {
                        IsUpdateAvailable = true;
                        NewUpdateVersion = "New Update Available: " + updateInfo.Version;
                    }
                    else
                    {
                        IsUpdateAvailable = false;
                        NewUpdateVersion = "";
                    }
                });
            }
            catch (Exception)
            {
            }
        }

        private async void OpenUpdateWindow()
        {
             // Use cached result if valid, else re-check or use what we have
             // For now just re-check to ensure we get the latest info object
             var updateInfo = await UpdateService.Instance.CheckForUpdatesAsync();
             if (updateInfo != null)
             {
                 // Even if not strictly newer, allow opening if manually triggered? 
                 // But this is usually triggered by "New Update" badge, so it implies newer.
                 
                 var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
                 if (topLevel != null)
                 {
                     var updateWin = new Views.UpdateWindow(updateInfo);
                     await updateWin.ShowDialog(topLevel);
                 }
             }
        }
    }
}
