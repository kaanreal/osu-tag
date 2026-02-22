using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Osutag.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;

using Osutag.Services;

namespace Osutag.Views
{
    public partial class EditMetadataWindow : Window
    {
        public EditMetadataWindow()
        {
            InitializeComponent();
            this.DataContextChanged += EditMetadataWindow_DataContextChanged;
        }

        private void EditMetadataWindow_DataContextChanged(object? sender, EventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (ViewModel == null) return;

            if (e.PropertyName == nameof(EditMetadataViewModel.PlaybackRate) ||
                e.PropertyName == nameof(EditMetadataViewModel.MaintainPitch))
            {
                AudioService.Instance.UpdatePlaybackState((float)ViewModel.PlaybackRate, ViewModel.MaintainPitch);
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                this.BeginMoveDrag(e);
            }
        }

        private EditMetadataViewModel? ViewModel => DataContext as EditMetadataViewModel;

        private void PreviewRate_Click(object? sender, RoutedEventArgs e)
        {
            if (ViewModel == null || string.IsNullOrEmpty(ViewModel.SongPath)) return;

            // Stop current playback
            AudioService.Instance.Stop();

            // Play with rate and pitch
            int previewTime = ViewModel.OriginalItem.MapGroup?.PreviewTime ?? 0;
            if (previewTime <= 0) previewTime = 45000; // Default to 45s if unknown

            AudioService.Instance.PlayPreview(ViewModel.SongPath, previewTime, null, (float)ViewModel.PlaybackRate, ViewModel.MaintainPitch);
        }

        private void ResetPitch_Click(object? sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
                ViewModel.PitchSemitones = 0;
        }

        private async void Browse_Click(object? sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Cover Image",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    FilePickerFileTypes.ImageAll
                }
            });

            if (files.Count > 0)
            {
                ViewModel.ActiveCoverPath = files[0].Path.LocalPath;
            }
        }

        private async void Crop_Click(object? sender, RoutedEventArgs e)
        {
            if (ViewModel == null || string.IsNullOrEmpty(ViewModel.ActiveCoverPath)) return;

            var cropWin = new CropWindow(ViewModel.ActiveCoverPath);
            await cropWin.ShowDialog(this);

            if (cropWin.IsConfirmed)
            {
                // Generate a temp path for the cropped image
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var cacheDir = Path.Combine(appData, "osu!tag", "crops");
                if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);

                var fileName = $"crop_{Guid.NewGuid()}.jpg";
                var outputPath = Path.Combine(cacheDir, fileName);

                // Process Crop
                // Visual crop was roughly 400x400 output usually, but let's stick to high res
                // The logical crop is returned in original image pixels.
                // We want to save a high-quality square.

                var processor = new Services.ImageProcessor();
                processor.ProcessCoverWithCrop(
                    ViewModel.ActiveCoverPath,
                    outputPath,
                    cropWin.CropX,
                    cropWin.CropY,
                    cropWin.CropSize,
                    600, 600); // Save as 600x600 square

                ViewModel.ActiveCoverPath = outputPath;
            }
        }

        private void Save_Click(object? sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var item = ViewModel.OriginalItem;
            item.OverrideTitle = string.IsNullOrWhiteSpace(ViewModel.OverrideTitle) ? null : ViewModel.OverrideTitle;
            item.OverrideArtist = string.IsNullOrWhiteSpace(ViewModel.OverrideArtist) ? null : ViewModel.OverrideArtist;
            item.PlaybackRate = (float)ViewModel.PlaybackRate;
            item.PitchSemitones = (float)ViewModel.PitchSemitones;
            item.MaintainPitch = ViewModel.MaintainPitch;

            // If cover path changed from original, set it
            if (!string.IsNullOrEmpty(ViewModel.ActiveCoverPath))
            {
                item.OverrideCoverPath = ViewModel.ActiveCoverPath;
            }

            // --- Persistence ---
            // Push back to the source object so it survives RefreshSelectedItems()
            if (item.SourceDifficulty != null)
            {
                item.SourceDifficulty.OverrideTitle = item.OverrideTitle;
                item.SourceDifficulty.OverrideArtist = item.OverrideArtist;
                item.SourceDifficulty.OverrideRate = item.PlaybackRate;
                item.SourceDifficulty.OverridePitch = item.PitchSemitones;
                item.SourceDifficulty.OverrideMaintainPitch = item.MaintainPitch;
                item.SourceDifficulty.OverrideCoverPath = item.OverrideCoverPath;
            }
            else if (item.MapGroup != null)
            {
                item.MapGroup.OverrideTitle = item.OverrideTitle;
                item.MapGroup.OverrideArtist = item.OverrideArtist;
                item.MapGroup.OverrideRate = item.PlaybackRate;
                item.MapGroup.OverridePitch = item.PitchSemitones;
                item.MapGroup.OverrideMaintainPitch = item.MaintainPitch;
                item.MapGroup.OverrideCoverPath = item.OverrideCoverPath;
            }

            // Stop preview on save
            AudioService.Instance.Stop();

            Close();
        }

        private void Cancel_Click(object? sender, RoutedEventArgs e)
        {
            // Stop preview on cancel
            AudioService.Instance.Stop();
            Close();
        }
    }
}
