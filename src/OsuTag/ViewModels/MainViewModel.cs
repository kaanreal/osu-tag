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
using OsuTag.Models;
using OsuTag.Services;
using Avalonia.Threading;

namespace OsuTag.ViewModels
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
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public required string DifficultyName { get; set; }
        public required OsuMapDifficulty Difficulty { get; set; }
        
        // Display properties for Overlay
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? CoverPath { get; set; }
    }

    public class MapItemGroup : ObservableObject
    {
        private bool _isExpanded = false;
        private bool _isSelected = false;

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

        public required string Artist { get; set; }
        public required string Title { get; set; }
        public string? CoverPath { get; set; }
        public required string Creator { get; set; }
        public string? Source { get; set; }
        public string? Tags { get; set; }
        public string? PreviewMp3Path { get; set; }
        public int PreviewTime { get; set; }
        public ObservableCollection<DifficultyItem> Difficulties { get; } = new();
        public ObservableCollection<AudioFileItem> UniqueAudioFiles { get; } = new();

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
        private const int ITEMS_PER_PAGE = 50;
        private bool _canLoadMore = false;
        private CancellationTokenSource? _searchDebounceToken;
        private const int SEARCH_DEBOUNCE_MS = 300;
        private bool _isSearching = false;
        private Dictionary<string, int> _playCountCache = new();
        private ObservableCollection<object> _selectedItems = new();
        private bool _isSelectionPanelExpanded = false;
        private bool _isLoadingMore = false;
        private bool _isOverlayOpen = false;
        private MapItemGroup? _overlayMapGroup;

        public bool IsOverlayOpen
        {
            get => _isOverlayOpen;
            set => SetProperty(ref _isOverlayOpen, value);
        }

        public MapItemGroup? OverlayMapGroup
        {
            get => _overlayMapGroup;
            set => SetProperty(ref _overlayMapGroup, value);
        }

        public ObservableCollection<object> SelectedItems
        {
            get => _selectedItems;
            set => SetProperty(ref _selectedItems, value);
        }

        public int SelectedCount => _selectedItems.Count;

        public bool IsSelectionPanelExpanded
        {
            get => _isSelectionPanelExpanded;
            set => SetProperty(ref _isSelectionPanelExpanded, value);
        }

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

        public bool CanLoadMore
        {
            get => _canLoadMore;
            set
            {
                if (SetProperty(ref _canLoadMore, value))
                {
                    ((RelayCommand)LoadMoreCommand).RaiseCanExecuteChanged();
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
                }
            }
        }

        public bool IsSearching
        {
            get => _isSearching;
            set => SetProperty(ref _isSearching, value);
        }

        public bool IsLoadingMore
        {
            get => _isLoadingMore;
            set => SetProperty(ref _isLoadingMore, value);
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
            set => SetProperty(ref _progressPercentage, value);
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
                        result = result.OrderByDescending(map => GetPlayCount(map, playCounts)).ToList();
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
            await LoadCompanellaPlayCounts();
            FilterMaps();
        }

        private void FilterMaps()
        {
            // Sync version for initial load
            _ = FilterMapsAsync();
        }

        public async void LoadMoreItems()
        {
            if (_displayedCount >= _filteredMapGroups.Count || IsLoadingMore)
            {
                CanLoadMore = false;
                return;
            }

            IsLoadingMore = true;
            
            // Brief delay for smooth transition
            await Task.Delay(400);

            // Get items to add
            var count = Math.Min(ITEMS_PER_PAGE, _filteredMapGroups.Count - _displayedCount);
            var allItems = _filteredMapGroups.Take(_displayedCount + count).ToList();

            // Replace entire collection (single UI update instead of many)
            MapGroups = new ObservableCollection<MapItemGroup>(allItems);

            _displayedCount += count;
            IsLoadingMore = false;
            CanLoadMore = _displayedCount < _filteredMapGroups.Count;
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

            // Auto-load saved path if enabled - load from cache then smart scan for new
            if (SettingsService.Settings.RememberSongsPath &&
                !string.IsNullOrEmpty(SettingsService.Settings.LastSongsPath) &&
                Directory.Exists(SettingsService.Settings.LastSongsPath))
            {
                _ = LoadFromCacheAndSmartScan(SettingsService.Settings.LastSongsPath);
            }
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
            RefreshSelectedItems();
        }

        private async void OpenSettings()
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

        private string GetCacheFilePath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var cacheDir = Path.Combine(appData, "osu!tag");
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);
            return Path.Combine(cacheDir, "mapcache.json");
        }

        private class CachedMapData
        {
            public string Artist { get; set; } = "";
            public string Title { get; set; } = "";
            public string Creator { get; set; } = "";
            public string? Source { get; set; }
            public string? Tags { get; set; }
            public string? CoverPath { get; set; }
            public string? PreviewMp3Path { get; set; }
            public int PreviewTime { get; set; }
            public List<CachedDifficulty> Difficulties { get; set; } = new();
        }

        private class CachedDifficulty
        {
            public string DifficultyName { get; set; } = "";
            public string Mp3Path { get; set; } = "";
            public string OsuFilePath { get; set; } = "";
            public string? Rate { get; set; }
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
                    Difficulties = g.Difficulties.Select(d => new CachedDifficulty
                    {
                        DifficultyName = d.DifficultyName,
                        Mp3Path = d.Difficulty.Mp3Path,
                        OsuFilePath = d.Difficulty.OsuFilePath,
                        Rate = d.Difficulty.Rate
                    }).ToList()
                }).ToList();

                var json = JsonSerializer.Serialize(cacheData);
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
                var cacheData = JsonSerializer.Deserialize<List<CachedMapData>>(json);

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
                        PreviewTime = cached.PreviewTime
                    };

                    foreach (var diff in cached.Difficulties)
                    {
                        if (!File.Exists(diff.OsuFilePath))
                            continue;

                        mapGroup.Difficulties.Add(new DifficultyItem
                        {
                            DifficultyName = diff.DifficultyName,
                            Difficulty = new OsuMapDifficulty
                            {
                                DifficultyName = diff.DifficultyName,
                                Mp3Path = diff.Mp3Path,
                                OsuFilePath = diff.OsuFilePath,
                                Rate = diff.Rate
                            },
                            // IsSelected = false by default to prevent "select all" behavior for stacks
                            Title = cached.Title, 
                            Artist = cached.Artist, 
                            CoverPath = cached.CoverPath
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
            ErrorMessage = "";

            // Save path if remember is enabled
            if (SettingsService.Settings.RememberSongsPath)
            {
                SettingsService.Settings.LastSongsPath = path;
                SettingsService.Save();
            }

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
                        FilterMaps();
                    }
                    IsScanning = false;
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
                    FilterMaps();
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
                            PreviewTime = firstMap.PreviewTime
                        };

                        // Add all difficulties for this map
                        foreach (var map in group)
                        {
                            foreach (var diff in map.Difficulties)
                            {
                                mapGroup.Difficulties.Add(new DifficultyItem
                                {
                                    DifficultyName = diff.DifficultyName,
                                    Difficulty = diff,
                                    // IsSelected = false by default
                                    Title = diff.Title ?? map.Title,
                                    Artist = diff.Artist ?? map.Artist,
                                    CoverPath = diff.CoverPath ?? map.CoverPath
                                });
                            }
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
                                PreviewTime = firstMap.PreviewTime
                            });
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
                FilterMaps();

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
            IsSelectionPanelExpanded = false;
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
                    // For multi-audio maps/stacks, add selected difficulties
                    // Previously we used UniqueAudioFiles, but now we allow selecting individual difficulties from the overlay
                    foreach (var diff in group.Difficulties.Where(d => d.IsSelected))
                    {
                        newSelection.Add(new SelectedItemInfo
                        {
                            MapGroup = group,
                            AudioFile = null, // Or create a dummy one if needed, but we used DiffName mainly
                            DisplayName = $"{group.Artist} - {group.Title}",
                            SubDisplayName = diff.DifficultyName // This is now the Version string (e.g. "Song Name")
                        });
                    }
                }
                else if (group.IsSelected)
                {
                    // For simple maps, add the group
                    newSelection.Add(new SelectedItemInfo
                    {
                        MapGroup = group,
                        AudioFile = null,
                        DisplayName = $"{group.Artist} - {group.Title}",
                        SubDisplayName = null
                    });
                }
            }

            SelectedItems = new ObservableCollection<object>(newSelection);
            OnPropertyChanged(nameof(SelectedCount));
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
                ProgressMessage = $"{i + 1}/{itemsToConvert.Count}: {group.Artist} - {group.Title}";

                try
                {
                    string safeTitle = string.Concat(
                        $"{group.Artist} - {group.Title}"
                            .Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_')
                    ).Trim();

                    string mapOutputDir = Path.Combine(config.OutputDir, safeTitle);
                    Directory.CreateDirectory(mapOutputDir);

                    string? coverOutput = null;
                    if (ProcessCovers && !string.IsNullOrEmpty(group.CoverPath) && File.Exists(group.CoverPath))
                    {
                        coverOutput = Path.Combine(mapOutputDir, "cover.jpg");
                        imageProcessor.ProcessCover(group.CoverPath, coverOutput, 3000, 3000);
                    }

                    string mp3Output = Path.Combine(mapOutputDir, $"{safeTitle}.mp3");
                    File.Copy(diff.Mp3Path, mp3Output, overwrite: true);

                    var osuMap = new OsuMap
                    {
                        Artist = group.Artist,
                        Title = group.Title,
                        Creator = group.Creator,
                        CoverPath = group.CoverPath,
                        Difficulties = new List<OsuMapDifficulty> { diff },
                        PreviewTime = group.PreviewTime
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
                // Only override titles if the group actually has multiple different audio files (i.e. is a stack)
                // AND has variance in metadata (Artist, Title, or Cover) to distinguish "Compilations" from "Rate Packs".
                if (group.IsStack && group.HasMultipleDifferentAudios)
                {
                    // Heuristic: Check for metadata variance
                    bool hasDifferentArtists = group.Difficulties.Select(d => d.Artist).Distinct().Count() > 1;
                    bool hasDifferentTitles = group.Difficulties.Select(d => d.Difficulty.Title).Distinct().Count() > 1; // Use raw diff title
                    bool hasDifferentCovers = group.Difficulties.Select(d => d.CoverPath).Distinct().Count() > 1;

                    // If it looks like a Compilation Pack (varied songs), use the Version/DifficultyName as the Title
                    if (hasDifferentArtists || hasDifferentTitles || hasDifferentCovers)
                    {
                         foreach (var diff in group.Difficulties)
                        {
                            // Override Display Title with the Version/DifficultyName
                            if (!string.IsNullOrEmpty(diff.DifficultyName))
                            {
                                diff.Title = diff.DifficultyName;
                            }
                        }
                    }
                    // Else: It's likely a Rate Pack (Same Song, Different Audios/Rates), so keep the Main Title.
                }
            }
            catch (Exception ex)
            {
                // Safely ignore metadata finalization errors to prevent crashes
                System.Diagnostics.Debug.WriteLine($"Error finalizing metadata: {ex.Message}");
            }
        }


    }
}
