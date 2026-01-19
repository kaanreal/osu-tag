using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Osutag.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;

namespace Osutag.Views
{
    public partial class EditMetadataWindow : Window
    {
        public EditMetadataWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private EditMetadataViewModel? ViewModel => DataContext as EditMetadataViewModel;

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
             
             // If cover path changed from original, set it
             // Simple check: if active path is different from what would be default
             if (!string.IsNullOrEmpty(ViewModel.ActiveCoverPath))
             {
                 item.OverrideCoverPath = ViewModel.ActiveCoverPath;
             }

             Close();
        }

        private void Cancel_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
