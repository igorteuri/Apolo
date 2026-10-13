/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Reads the tags (title, artist, album ...) of music files, with TagLib# (LGPL).
*/
using System;

namespace Ares.Tags
{
    /// <summary>The tags of an audio file.</summary>
    public class AudioTagInfo
    {
        public String title = String.Empty;
        public String artist = String.Empty;
        public String album = String.Empty;
        public String genre = String.Empty;
        public String mood = String.Empty;
        /// <summary>Length in seconds</summary>
        public double duration;
    }

    public static class AudioTags
    {
        /// <summary>
        /// Reads the tags of a file. Returns null if the file can't be read.
        /// </summary>
        public static AudioTagInfo Read(String path)
        {
            if (String.IsNullOrEmpty(path))
                return null;
            try
            {
                using (TagLib.File file = TagLib.File.Create(path))
                {
                    AudioTagInfo info = new AudioTagInfo();
                    TagLib.Tag tag = file.Tag;
                    if (tag != null)
                    {
                        info.title = tag.Title ?? String.Empty;
                        info.artist = tag.JoinedPerformers ?? String.Empty;
                        info.album = tag.Album ?? String.Empty;
                        info.genre = tag.JoinedGenres ?? String.Empty;
                    }
                    try
                    {
                        TagLib.Id3v2.Tag id3 = file.GetTag(TagLib.TagTypes.Id3v2, false) as TagLib.Id3v2.Tag;
                        if (id3 != null)
                            info.mood = id3.GetTextAsString("TMOO") ?? String.Empty;
                    }
                    catch (Exception)
                    {
                    }
                    if (file.Properties != null)
                        info.duration = file.Properties.Duration.TotalSeconds;
                    return info;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
