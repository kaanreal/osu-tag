using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows;
using System.Windows.Forms;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Text.Json;
using OsuTag.Models;

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
    }

    public class ConversionResult
    {
        public required string Title { get; set; }
        public required string Message { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);
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
        private List<MapItemGroup> _filteredMapGroups = new();
        private int _displayedCount = 0;
        private const int ITEMS_PER_PAGE = 100;
        private bool _canLoadMore = false;
        private CancellationTokenSource? _searchDebounceToken;
        private const int SEARCH_DEBOUNCE_MS = 150;
        
        public bool CanLoadMore
        {
            get => _canLoadMore;
            set => SetProperty(ref _canLoadMore, value);
        }
        
        public bool IsScanning
        {
            get => _isScanning;
            set => SetProperty(ref _isScanning, value);
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
            
            try
            {
                await Task.Delay(SEARCH_DEBOUNCE_MS, token);
                if (!token.IsCancellationRequested)
                {
                    FilterMaps();
                }
            }
            catch (TaskCanceledException) { }
        }

        public string SelectedPath
        {
            get => _selectedPath;
            set => SetProperty(ref _selectedPath, value);
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
            set => SetProperty(ref _isProcessing, value);
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

        public ObservableCollection<MapItemGroup> MapGroups { get; } = new();
        
        private void FilterMaps()
        {
            // Build filtered list on background thread for large collections
            if (string.IsNullOrWhiteSpace(_searchQuery))
            {
                _filteredMapGroups = _allMapGroups.ToList();
            }
            else
            {
                var query = _searchQuery;
                _filteredMapGroups = _allMapGroups.Where(map =>
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
            
            // Reset and show first page - batch update
            _displayedCount = 0;
            var initialItems = _filteredMapGroups.Take(ITEMS_PER_PAGE).ToList();
            
            MapGroups.Clear();
            foreach (var item in initialItems)
            {
                MapGroups.Add(item);
            }
            _displayedCount = initialItems.Count;
            CanLoadMore = _displayedCount < _filteredMapGroups.Count;
        }
        
        public void LoadMoreItems()
        {
            if (_displayedCount >= _filteredMapGroups.Count)
            {
                CanLoadMore = false;
                return;
            }
            
            // Get items to add
            var count = Math.Min(ITEMS_PER_PAGE, _filteredMapGroups.Count - _displayedCount);
            var itemsToAdd = _filteredMapGroups.GetRange(_displayedCount, count);
            
            // Batch add items
            foreach (var item in itemsToAdd)
            {
                MapGroups.Add(item);
            }
            
            _displayedCount += count;
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

        public MainViewModel()
        {
            // Load settings
            _outputPath = string.IsNullOrEmpty(Properties.Settings.Default.LastUsedPath) 
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
                : Properties.Settings.Default.LastUsedPath;
            _processCovers = Properties.Settings.Default.ProcessCovers;
            _createBackups = Properties.Settings.Default.CreateBackups;
            
            BrowseCommand = new RelayCommand(_ => Browse());
            UseDefaultPathCommand = new RelayCommand(_ => UseDefaultPath());
            RescanCommand = new RelayCommand(_ => Rescan(), _ => MapsLoaded && !IsScanning);
            SelectAllCommand = new RelayCommand(_ => SelectAll());
            DeselectAllCommand = new RelayCommand(_ => DeselectAll());
            StartConversionCommand = new RelayCommand(_ => StartConversion(), _ => !IsProcessing && MapGroups.Any(g => g.Difficulties.Any(d => d.IsSelected)));
            SettingsCommand = new RelayCommand(_ => OpenSettings());
            
            // Auto-load saved path if enabled - load from cache then smart scan for new
            if (Properties.Settings.Default.RememberSongsPath && 
                !string.IsNullOrEmpty(Properties.Settings.Default.LastSongsPath) &&
                Directory.Exists(Properties.Settings.Default.LastSongsPath))
            {
                _ = LoadFromCacheAndSmartScan(Properties.Settings.Default.LastSongsPath);
            }
        }

        private void Browse()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select the osu! Songs folder";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _ = SetPathAsync(dialog.SelectedPath, useSmartScan: false);
                }
            }
        }

        private void UseDefaultPath()
        {
            string defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "osu!",
                "Songs"
            );
            _ = SetPathAsync(defaultPath, useSmartScan: false);
        }
        
        private void Rescan()
        {
            if (!string.IsNullOrEmpty(SelectedPath) && Directory.Exists(SelectedPath))
            {
                _ = SetPathAsync(SelectedPath, useSmartScan: true);
            }
        }

        private HashSet<string> GetScannedFolders()
        {
            var scannedStr = Properties.Settings.Default.ScannedFolders ?? "";
            if (string.IsNullOrEmpty(scannedStr))
                return new HashSet<string>();
            return new HashSet<string>(scannedStr.Split('|', StringSplitOptions.RemoveEmptyEntries));
        }
        
        private void SaveScannedFolders(HashSet<string> folders)
        {
            Properties.Settings.Default.ScannedFolders = string.Join("|", folders);
            Properties.Settings.Default.Save();
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
                    
                var json = File.ReadAllText(cachePath);
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
                            IsSelected = true
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
                        groups.Add(mapGroup);
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
            ScanStatusMessage = "Loading cached maps...";
            
            // Load from cache first
            var cachedGroups = await Task.Run(() => LoadMapCache());
            
            if (cachedGroups.Count > 0)
            {
                foreach (var group in cachedGroups)
                {
                    _allMapGroups.Add(group);
                }
                ScanStatusMessage = $"Loaded {cachedGroups.Count} cached map sets, checking for new maps...";
            }
            
            // Now do a smart scan for new maps only
            await SetPathAsync(path, useSmartScan: true);
        }

        private async Task SetPathAsync(string path, bool useSmartScan = false)
        {
            SelectedPath = path;
            ErrorMessage = "";
            
            // Save path if remember is enabled
            if (Properties.Settings.Default.RememberSongsPath)
            {
                Properties.Settings.Default.LastSongsPath = path;
                Properties.Settings.Default.Save();
            }
            
            // Only clear if not using smart scan, or if smart scan is disabled in settings
            bool smartScanEnabled = useSmartScan && Properties.Settings.Default.SmartScan;
            
            if (!smartScanEnabled)
            {
                MapGroups.Clear();
                _allMapGroups.Clear();
                _filteredMapGroups.Clear();
            }
            
            MapsLoaded = false;
            IsScanning = true;
            ScanProgress = 0;
            ScanStatusMessage = "Initializing...";

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
                
                // For smart scan, filter out already scanned folders
                var scannedFolders = smartScanEnabled ? GetScannedFolders() : new HashSet<string>();
                var existingFolderNames = _allMapGroups
                    .SelectMany(g => g.Difficulties)
                    .Select(d => Path.GetDirectoryName(d.Difficulty.Mp3Path))
                    .Where(p => p != null)
                    .Select(p => Path.GetFileName(p!))
                    .Distinct()
                    .ToHashSet();
                
                var foldersToScan = smartScanEnabled 
                    ? allFolders.Where(f => !scannedFolders.Contains(Path.GetFileName(f))).ToArray()
                    : allFolders;
                
                int totalFolders = foldersToScan.Length;
                var maps = new List<OsuMap>();
                var newScannedFolders = new HashSet<string>();

                if (totalFolders == 0 && smartScanEnabled)
                {
                    ScanStatusMessage = "No new folders to scan";
                    PathStatusMessage = $"✓ No new maps - {_allMapGroups.Count} map sets loaded";
                    MapsLoaded = _allMapGroups.Count > 0;
                    if (MapsLoaded) FilterMaps();
                    IsScanning = false;
                    return;
                }

                ScanStatusMessage = smartScanEnabled 
                    ? $"Smart scanning {totalFolders} new folders..."
                    : $"Scanning {totalFolders} folders...";

                await Task.Run(() =>
                {
                    var scanner = new Services.OsuMapScanner();
                    var mapsBag = new System.Collections.Concurrent.ConcurrentBag<OsuMap>();
                    var scannedFoldersBag = new System.Collections.Concurrent.ConcurrentBag<string>();
                    int processed = 0;
                    var lastUpdate = DateTime.MinValue;
                    var updateInterval = TimeSpan.FromMilliseconds(100);
                    
                    Parallel.ForEach(foldersToScan, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, folder =>
                    {
                        try
                        {
                            var map = scanner.ScanSingleFolder(folder);
                            if (map != null)
                            {
                                mapsBag.Add(map);
                            }
                            scannedFoldersBag.Add(Path.GetFileName(folder));
                        }
                        catch { }
                        
                        var currentProcessed = System.Threading.Interlocked.Increment(ref processed);
                        
                        // Throttle UI updates to every 100ms
                        var now = DateTime.UtcNow;
                        if (now - lastUpdate > updateInterval || currentProcessed == totalFolders)
                        {
                            lastUpdate = now;
                            int progress = (int)((currentProcessed / (double)totalFolders) * 100);
                            
                            System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                            {
                                ScanProgress = progress;
                                ScanStatusMessage = $"Scanning... {currentProcessed}/{totalFolders} folders ({mapsBag.Count} maps found)";
                            });
                        }
                    });
                    
                    maps.AddRange(mapsBag);
                    foreach (var folder in scannedFoldersBag)
                    {
                        newScannedFolders.Add(folder);
                    }
                });
                
                // Save scanned folders to cache for smart scan
                if (smartScanEnabled || Properties.Settings.Default.SmartScan)
                {
                    foreach (var folder in newScannedFolders)
                    {
                        scannedFolders.Add(folder);
                    }
                    SaveScannedFolders(scannedFolders);
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
                    var groupedMaps = maps.GroupBy(m => $"{m.Artist} - {m.Title}").ToList();

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
                                    IsSelected = true
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
                    return groups;
                });

                ScanStatusMessage = "Loading maps...";

                // Add all to backing collection (no UI update yet)
                foreach (var group in mapGroups)
                {
                    _allMapGroups.Add(group);
                }

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
                if (Properties.Settings.Default.RememberSongsPath)
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
            }
        }

        private void SelectAll()
        {
            foreach (var group in _allMapGroups)
            {
                group.IsSelected = true;
            }
        }

        private void DeselectAll()
        {
            foreach (var group in _allMapGroups)
            {
                group.IsSelected = false;
            }
        }

        private async void StartConversion()
        {
            // Check if any maps are selected (either simple selection or expanded with audio files selected)
            bool hasSelection = _allMapGroups.Any(g => 
                (g.IsSelected && !g.HasMultipleDifferentAudios) || 
                (g.HasMultipleDifferentAudios && g.UniqueAudioFiles.Any(a => a.IsSelected))
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
                if (group.HasMultipleDifferentAudios)
                {
                    // For expanded maps, only convert selected audio files
                    foreach (var audioFile in group.UniqueAudioFiles.Where(a => a.IsSelected))
                    {
                        // Find all difficulties that use this audio file
                        var diffsForThisAudio = group.Difficulties
                            .Where(d => d.Difficulty.Mp3Path == audioFile.Mp3Path)
                            .ToList();
                        
                        if (diffsForThisAudio.Any())
                        {
                            // Use the first difficulty for this audio file
                            var diff = diffsForThisAudio.First();
                            itemsToConvert.Add((group, diff.Difficulty, diff.DifficultyName));
                        }
                    }
                }
                else if (group.IsSelected)
                {
                    // For simple selection, convert the first difficulty
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

                    AddResult("✓", $"{safeTitle}");
                }
                catch (Exception ex)
                {
                    AddResult("✗", $"{group.Artist} - {group.Title}: {ex.Message}");
                }
            }

            AddResult("Done!", $"All maps saved to: {config.OutputDir}");
        }

        private void AddResult(string title, string message)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                ConversionResults.Add(new ConversionResult { Title = title, Message = message });
            });
        }

        private void OpenSettings()
        {
            // Settings are handled in MainWindow.xaml.cs
            // After dialog closes, reload settings
        }    }
}