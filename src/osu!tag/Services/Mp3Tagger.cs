using System;
using System.IO;
using System.Collections.Generic;
using Osutag.Models;
using TagLib;
using TagLib.Id3v2;
using IOFile = System.IO.File;

namespace Osutag.Services
{
    internal class Mp3Tagger
    {
        public void TagMp3(string mp3Path, OsuMap metadata, string? coverPath = null)
        {
            try
            {
                // Force ID3v2.3 for compatibility (Windows/macOS/Spotify don't read v2.4 reliably)
                TagLib.Id3v2.Tag.DefaultVersion = 3;
                TagLib.Id3v2.Tag.ForceDefaultVersion = true;

                using var file = TagLib.File.Create(mp3Path);

                // Force ID3v2 tag (supports album art)
                var id3v2 = (TagLib.Id3v2.Tag)file.GetTag(TagTypes.Id3v2, true);

                id3v2.Title = metadata.Title;
                id3v2.Performers = new[] { metadata.Artist };
                id3v2.Album = metadata.Source ?? "osu! Beatmap";
                id3v2.Comment = metadata.Tags ?? "";

                // Also set on combined tag for compatibility
                file.Tag.Title = metadata.Title;
                file.Tag.Performers = new[] { metadata.Artist };
                file.Tag.Album = metadata.Source ?? "osu! Beatmap";
                file.Tag.Comment = metadata.Tags ?? "";

                // Embed cover if available
                if (!string.IsNullOrEmpty(coverPath) && IOFile.Exists(coverPath))
                {
                    try
                    {
                        byte[] coverData = IOFile.ReadAllBytes(coverPath);
                        Console.Error.WriteLine($"[Mp3Tagger] Cover: {coverPath} ({coverData.Length} bytes)");

                        if (coverData.Length > 0)
                        {
                            string ext = Path.GetExtension(coverPath).ToLowerInvariant();
                            string mimeType = ext switch
                            {
                                ".png" => "image/png",
                                ".bmp" => "image/bmp",
                                _ => "image/jpeg"
                            };

                            var picture = new Picture(new ByteVector(coverData))
                            {
                                Type = PictureType.FrontCover,
                                Description = "Cover",
                                MimeType = mimeType
                            };

                            var pics = new IPicture[] { picture };
                            // Set on both ID3v2 and combined tag
                            id3v2.Pictures = pics;
                            file.Tag.Pictures = pics;

                            Console.Error.WriteLine($"[Mp3Tagger] Embedded {mimeType} cover ({coverData.Length} bytes) into {Path.GetFileName(mp3Path)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[Mp3Tagger] Cover embed FAILED for {mp3Path}: {ex.Message}");
                    }
                }
                else
                {
                    Console.Error.WriteLine($"[Mp3Tagger] No cover for {Path.GetFileName(mp3Path)} (coverPath={coverPath ?? "null"}, exists={(!string.IsNullOrEmpty(coverPath) && IOFile.Exists(coverPath))})");
                }

                file.Save();
                Console.Error.WriteLine($"[Mp3Tagger] Saved tags for {Path.GetFileName(mp3Path)}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Mp3Tagger] TAGGING FAILED for {mp3Path}: {ex}");
            }
        }
    }
}
