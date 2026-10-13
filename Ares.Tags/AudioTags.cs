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
        public String copyright = String.Empty;
        public String comment = String.Empty;
        public uint year;
        public uint track;
        /// <summary>Length in seconds</summary>
        public double duration;
    }

    public static class AudioTags
    {
        /// <summary>
        /// Reads the tags of a file. Returns null if the file can't be read.
        /// </summary>
        /// <summary>
        /// Writes title, artist and album (empty values leave the tag as it is). Returns false if the file can't be changed.
        /// </summary>
        public static bool Write(String path, String title, String artist, String album)
        {
            if (String.IsNullOrEmpty(path))
                return false;
            try
            {
                using (TagLib.File file = TagLib.File.Create(path))
                {
                    TagLib.Tag tag = file.Tag;
                    if (tag == null)
                        return false;
                    if (!String.IsNullOrEmpty(title))
                        tag.Title = title;
                    if (!String.IsNullOrEmpty(artist))
                        tag.Performers = new String[] { artist };
                    if (!String.IsNullOrEmpty(album))
                        tag.Album = album;
                    file.Save();
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Sets title, artist and album exactly as given (empty clears the tag). Used to undo a change.</summary>
        public static bool WriteExact(String path, String title, String artist, String album)
        {
            if (String.IsNullOrEmpty(path))
                return false;
            try
            {
                using (TagLib.File file = TagLib.File.Create(path))
                {
                    TagLib.Tag tag = file.Tag;
                    if (tag == null)
                        return false;
                    tag.Title = String.IsNullOrEmpty(title) ? null : title;
                    tag.Performers = String.IsNullOrEmpty(artist) ? new String[0] : new String[] { artist };
                    tag.Album = String.IsNullOrEmpty(album) ? null : album;
                    file.Save();
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

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
                        info.copyright = tag.Copyright ?? String.Empty;
                        info.comment = tag.Comment ?? String.Empty;
                        info.year = tag.Year;
                        info.track = tag.Track;
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
