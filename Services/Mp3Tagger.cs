using System;
using System.IO;
using System.Collections.Generic;
using OsuTag.Models;
using TagLib;
using IOFile = System.IO.File;

namespace OsuTag.Services
{
    public class Mp3Tagger
    {
        public void TagMp3(string mp3Path, OsuMap metadata, string? coverPath = null)
        {
            try
            {
                var file = TagLib.File.Create(mp3Path);
                var tag = file.Tag;

                tag.Title = metadata.Title;
                tag.Performers = new[] { metadata.Artist };
                tag.Album = metadata.Source ?? "osu! Beatmap";
                tag.Comment = metadata.Tags ?? "";

                // Embed cover if available
                if (!string.IsNullOrEmpty(coverPath) && IOFile.Exists(coverPath))
                {
                    try
                    {
                        byte[] coverData = IOFile.ReadAllBytes(coverPath);
                        var picture = new Picture(new ByteVector(coverData))
                        {
                            Type = PictureType.FrontCover,
                            Description = "Cover",
                            MimeType = "image/jpeg"
                        };
                        var pictures = new List<IPicture>(tag.Pictures) { picture };
                        tag.Pictures = pictures.ToArray();
                    }
                    catch
                    {
                        // Cover embedding is optional
                    }
                }

                file.Save();
            }
            catch
            {
                // Tagging failure is non-fatal for the conversion process
            }
        }
    }
}
