/*
 Copyright (c) 2015 [Joerg Ruedenauer]
 
 This file is part of Ares.

 Ares is free software; you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation; either version 2 of the License, or
 (at your option) any later version.

 Ares is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with Ares; if not, write to the Free Software
 Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
 */
using System;
using System.Collections.Generic;
using System.Text;
using Ares.Data;

namespace Ares.Players
{
    public class MusicInfo
    {
        public String LongTitle { get; private set; }
        public String ShortTitle { get; private set; }
        /// <summary>Title from the file tag (or the element title), artist and album from the tag (may be empty).</summary>
        public String Title { get; private set; }
        public String Artist { get; private set; }
        public String Album { get; private set; }

        private MusicInfo(String longTitle, String shortTitle)
            : this(longTitle, shortTitle, shortTitle, String.Empty, String.Empty)
        {
        }

        private MusicInfo(String longTitle, String shortTitle, String title, String artist, String album)
        {
            LongTitle = longTitle;
            ShortTitle = shortTitle;
            Title = title ?? String.Empty;
            Artist = artist ?? String.Empty;
            Album = album ?? String.Empty;
        }

        public static MusicInfo GetInfo(int musicElementId)
        {
            if (musicElementId != -1)
            {
                IElement musicElement = Ares.Data.DataModule.ElementRepository.GetElement(musicElementId);
                if (musicElement == null)
                {
                    return new MusicInfo(String.Empty, String.Empty);
                }
                IFileElement fileElement = musicElement as IFileElement;
                String shortTitle = musicElement.Title;
                String longTitle = musicElement.Title;
                String tagTitle = musicElement.Title;
                String tagArtist = String.Empty;
                String tagAlbum = String.Empty;
                if (fileElement != null)
                {
                    String path = Settings.Settings.Instance.MusicDirectory;
                    path = System.IO.Path.Combine(path, fileElement.FilePath);
                    Ares.Tags.AudioTagInfo tag = Ares.Tags.AudioTags.Read(path);
                    if (tag != null)
                    {
                        StringBuilder musicInfoBuilder = new StringBuilder();
                        musicInfoBuilder.Append(tag.artist);
                        if (musicInfoBuilder.Length > 0)
                            musicInfoBuilder.Append(" - ");
                        musicInfoBuilder.Append(tag.album);
                        if (musicInfoBuilder.Length > 0)
                            musicInfoBuilder.Append(" - ");
                        musicInfoBuilder.Append(tag.title);
                        longTitle = musicInfoBuilder.ToString();
                        tagArtist = tag.artist;
                        tagAlbum = tag.album;
                        if (!String.IsNullOrEmpty(tag.title))
                            tagTitle = tag.title;
                    }
                }
                return new MusicInfo(longTitle, shortTitle, tagTitle, tagArtist, tagAlbum);
            }
            else
            {
                return new MusicInfo(String.Empty, String.Empty);
            }
        }
    }
}
