/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Finds out which song a file is (acoustic fingerprint -> AcoustID -> MusicBrainz): title, artist, album, year, label.
 Used by the music renamer and the copyright check of the Player.
*/
using System;

namespace Ares.TagsImport
{
    public class MusicLookupResult
    {
        public bool Identified;
        public String Title = String.Empty;
        public String Artist = String.Empty;
        public String Album = String.Empty;
        public String Year = String.Empty;
        public String Label = String.Empty;
        public String AcoustId = String.Empty;
        public String MusicBrainzId = String.Empty;
        /// <summary>What went wrong (empty if the lookup worked, even when the song was not found).</summary>
        public String Error = String.Empty;
    }

    public static class MusicLookup
    {
        /// <summary>Identifies a file on the internet. Takes a few seconds (the services allow one request per second).</summary>
        public static MusicLookupResult Identify(String path, bool withLabel)
        {
            MusicLookupResult result = new MusicLookupResult();
            try
            {
                double seconds;
                Int16[] pcm = PcmExtractor.ExtractPcm(path, out seconds);
                if (pcm == null || pcm.Length == 0)
                {
                    result.Error = "audio";
                    return result;
                }
                String code = ChromaPrint.CalculateChromaprintCode(pcm);
                if (String.IsNullOrEmpty(code))
                {
                    result.Error = "fingerprint";
                    return result;
                }
                using (AcoustAPI acoust = new AcoustAPI())
                {
                    AcoustIdInfo info = acoust.IdentifySong(code, seconds);
                    if (info == null || String.IsNullOrEmpty(info.MusicBrainzId))
                        return result;
                    result.AcoustId = info.AcoustId ?? String.Empty;
                    result.MusicBrainzId = info.MusicBrainzId;
                }
                using (MusicBrainzApi mb = new MusicBrainzApi())
                {
                    String title, artist, album, year, label;
                    if (mb.RetrieveDetails(result.MusicBrainzId, out title, out artist, out album, out year, out label))
                    {
                        result.Title = title;
                        result.Artist = artist;
                        result.Album = album;
                        result.Year = year;
                        result.Label = label;
                        result.Identified = !String.IsNullOrEmpty(title);
                    }
                }
            }
            catch (DllNotFoundException)
            {
                result.Error = "dll";
            }
            catch (BadImageFormatException)
            {
                result.Error = "dll";
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }
            return result;
        }
    }
}
