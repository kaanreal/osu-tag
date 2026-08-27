using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Osutag.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;

using Osutag.Services;

namespace Osutag.Views
{
    public partial class EditMetadataWindow : Window
    {
        private readonly DispatcherTimer _trimPreviewTimer;
        private Slider? _activeTrimSlider;
        private Slider? _trimStartSlider;
        private Slider? _trimEndSlider;
        private bool _trimSliderPressed;

        public EditMetadataWindow()
        {
            InitializeComponent();
            _trimPreviewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
            _trimPreviewTimer.Tick += TrimPreviewTimer_Tick;
            // Slider thumbs handle pointer presses themselves. Listen to the
            // routed events even when the thumb marks them handled so trim
            // cues still start reliably during a drag.
            _trimStartSlider = this.FindControl<Slider>("TrimStartSlider");
            _trimEndSlider = this.FindControl<Slider>("TrimEndSlider");
            if (_trimStartSlider != null)
            {
                _trimStartSlider.AddHandler(InputElement.PointerPressedEvent, TrimSlider_PointerPressed, RoutingStrategies.Bubble, true);
                _trimStartSlider.AddHandler(InputElement.PointerReleasedEvent, TrimSlider_PointerReleased, RoutingStrategies.Bubble, true);
            }
            if (_trimEndSlider != null)
            {
                _trimEndSlider.AddHandler(InputElement.PointerPressedEvent, TrimSlider_PointerPressed, RoutingStrategies.Bubble, true);
                _trimEndSlider.AddHandler(InputElement.PointerReleasedEvent, TrimSlider_PointerReleased, RoutingStrategies.Bubble, true);
            }
            this.DataContextChanged += EditMetadataWindow_DataContextChanged;
            this.Closed += (_, _) => StopTrimPreview();
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
            if (e.Handled)
                return;

            // The window doubles as a custom title bar. Do not steal pointer
            // presses from controls that need to receive a drag/click.
            if (e.Source is Slider or Button or TextBox or ToggleSwitch or ToggleButton)
                return;

            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                this.BeginMoveDrag(e);
            }
        }

        private EditMetadataViewModel? ViewModel => DataContext as EditMetadataViewModel;

        private void PreviewRate_Click(object? sender, RoutedEventArgs e)
        {
            if (ViewModel == null || string.IsNullOrEmpty(ViewModel.SongPath)) return;

            if (!ViewModel.TryGetCutRange(out var cutStart, out var cutEnd)) return;

            // Stop current playback
            AudioService.Instance.Stop();

            // Play with rate and pitch
            int previewTime = ViewModel.OriginalItem.MapGroup?.PreviewTime ?? 0;
            if (previewTime <= 0) previewTime = 45000; // Default to 45s if unknown

            int? previewDuration = null;
            if (cutStart.HasValue || cutEnd.HasValue)
            {
                previewTime = ToMilliseconds(cutStart ?? 0f);
                if (cutEnd.HasValue)
                    previewDuration = Math.Max(1, ToMilliseconds(cutEnd.Value - (cutStart ?? 0f)));
            }

            AudioService.Instance.PlayPreview(ViewModel.SongPath, previewTime, previewDuration, (float)ViewModel.PlaybackRate, ViewModel.MaintainPitch);
        }

        private static int ToMilliseconds(float seconds)
        {
            return (int)Math.Clamp(Math.Round(seconds * 1000f), 0, int.MaxValue);
        }

        private void TrimSlider_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Slider slider || ViewModel?.IsTrimEnabled != true)
                return;

            _activeTrimSlider = slider;
            _trimSliderPressed = true;
            e.Handled = true;
            ScheduleTrimPreview();
        }

        private void TrimSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            if (sender is not Slider slider || (!_trimSliderPressed && !slider.IsFocused))
                return;

            _activeTrimSlider = slider;
            ScheduleTrimPreview();
        }

        private void TrimSlider_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (sender is not Slider slider)
                return;

            _activeTrimSlider = slider;
            _trimSliderPressed = false;
            e.Handled = true;
            ScheduleTrimPreview();
        }

        private void ScheduleTrimPreview()
        {
            _trimPreviewTimer.Stop();
            _trimPreviewTimer.Start();
        }

        private void TrimPreviewTimer_Tick(object? sender, EventArgs e)
        {
            _trimPreviewTimer.Stop();
            PreviewTrimSnippet();
        }

        private void PreviewTrimSnippet()
        {
            if (ViewModel == null || !ViewModel.IsTrimEnabled || string.IsNullOrEmpty(ViewModel.SongPath))
                return;

            if (!ViewModel.TryGetCutRange(out var cutStart, out var cutEnd) || !cutStart.HasValue || !cutEnd.HasValue)
                return;

            const float cuePlaybackSeconds = 2.25f;
            var start = cutStart.Value;
            var end = cutEnd.Value;
            var rate = Math.Max(0.01f, (float)ViewModel.PlaybackRate);
            // AudioService interprets duration as source time and scales it by
            // the rate. Scale the source cue too, so even a 0.25x preview stays
            // a short, usable listen instead of lasting four times as long.
            var length = Math.Min(cuePlaybackSeconds * rate, end - start);
            if (length <= 0)
                return;

            // When moving the out point, listen immediately before the handle;
            // when moving the in point, listen immediately after it.
            var cueStart = ReferenceEquals(_activeTrimSlider, _trimEndSlider)
                ? Math.Max(start, end - length)
                : start;

            AudioService.Instance.PlayPreview(
                ViewModel.SongPath,
                ToMilliseconds(cueStart),
                ToMilliseconds(length),
                rate,
                ViewModel.MaintainPitch);
        }

        private void StopTrimPreview()
        {
            _trimPreviewTimer.Stop();
            _trimSliderPressed = false;
            _activeTrimSlider = null;
            AudioService.Instance.Stop();
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

            if (!ViewModel.TryGetCutRange(out var cutStart, out var cutEnd)) return;

            var item = ViewModel.OriginalItem;
            item.OverrideTitle = string.IsNullOrWhiteSpace(ViewModel.OverrideTitle) ? null : ViewModel.OverrideTitle;
            item.OverrideArtist = string.IsNullOrWhiteSpace(ViewModel.OverrideArtist) ? null : ViewModel.OverrideArtist;
            item.PlaybackRate = (float)ViewModel.PlaybackRate;
            item.PitchSemitones = (float)ViewModel.PitchSemitones;
            item.MaintainPitch = ViewModel.MaintainPitch;
            item.CutStartSeconds = cutStart;
            item.CutEndSeconds = cutEnd;

            // If cover path changed from original, set it
            if (!string.IsNullOrEmpty(ViewModel.ActiveCoverPath))
            {
                item.OverrideCoverPath = ViewModel.ActiveCoverPath;
            }

            // --- Persistence ---
            // Push back to the source object so it survives RefreshSelectedItems()
            if (item.MapGroup?.IsStack == true && item.SourceDifficulty != null)
            {
                item.SourceDifficulty.OverrideTitle = item.OverrideTitle;
                item.SourceDifficulty.OverrideArtist = item.OverrideArtist;
                item.SourceDifficulty.OverrideRate = item.PlaybackRate;
                item.SourceDifficulty.OverridePitch = item.PitchSemitones;
                item.SourceDifficulty.OverrideMaintainPitch = item.MaintainPitch;
                item.SourceDifficulty.OverrideCutStartSeconds = item.CutStartSeconds;
                item.SourceDifficulty.OverrideCutEndSeconds = item.CutEndSeconds;
                item.SourceDifficulty.OverrideCoverPath = item.OverrideCoverPath;
            }
            else if (item.MapGroup != null)
            {
                item.MapGroup.OverrideTitle = item.OverrideTitle;
                item.MapGroup.OverrideArtist = item.OverrideArtist;
                item.MapGroup.OverrideRate = item.PlaybackRate;
                item.MapGroup.OverridePitch = item.PitchSemitones;
                item.MapGroup.OverrideMaintainPitch = item.MaintainPitch;
                item.MapGroup.OverrideCutStartSeconds = item.CutStartSeconds;
                item.MapGroup.OverrideCutEndSeconds = item.CutEndSeconds;
                item.MapGroup.OverrideCoverPath = item.OverrideCoverPath;
            }

            // Stop preview on save
            StopTrimPreview();

            Close();
        }

        private void Cancel_Click(object? sender, RoutedEventArgs e)
        {
            // Stop preview on cancel
            StopTrimPreview();
            Close();
        }
    }
}
