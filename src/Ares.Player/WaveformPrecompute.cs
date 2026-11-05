/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Prepares the waveforms of many files ahead of time (by hand from the Tools menu, or by itself when the program has been
 quiet for a while), so that they are in the cache when the music is used. No window code: the analysis and the question
 "should I stop now?" are given by the caller, so that it can be tested automatically.
*/
using System;
using System.Collections.Generic;

namespace Ares.Player
{
    public sealed class WaveformPrecompute
    {
        public sealed class Result
        {
            public int Total;      // files looked at
            public int Analyzed;   // files analyzed now
            public int Cached;     // files which already had their waveform
            public int Failed;     // files which could not be read
            public bool Stopped;   // stopped before the end
        }

        private readonly WaveformCache m_Cache;
        private readonly Func<string, byte[]> m_Analyze;

        public WaveformPrecompute(WaveformCache cache, Func<string, byte[]> analyze)
        {
            m_Cache = cache;
            m_Analyze = analyze;
        }

        /// <summary>True when nothing was heard for at least idleMs (tick counts, safe over the wrap-around of Environment.TickCount).</summary>
        public static bool IsIdle(int nowTick, int lastSoundTick, int idleMs)
        {
            return unchecked(nowTick - lastSoundTick) >= idleMs;
        }

        /// <summary>
        /// Goes through the files; the ones without a waveform in the cache are analyzed one by one (shouldStop is asked before each
        /// file). progress gets (files done, files in all). Files that are repeated or empty names are ignored.
        /// </summary>
        public Result Run(IEnumerable<string> paths, Func<bool> shouldStop, Action<int, int> progress)
        {
            Result result = new Result();
            List<string> list = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (paths != null)
            {
                foreach (string p in paths)
                {
                    if (!string.IsNullOrEmpty(p) && seen.Add(p))
                        list.Add(p);
                }
            }
            result.Total = list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                if (shouldStop != null && shouldStop())
                {
                    result.Stopped = true;
                    break;
                }
                string path = list[i];
                if (m_Cache.Get(path) != null)
                {
                    result.Cached++;
                }
                else
                {
                    byte[] wave = null;
                    try
                    {
                        wave = m_Analyze(path);
                    }
                    catch (Exception ex)
                    {
                        Ares.Settings.ErrorLog.Note(ex);
                    }
                    if (wave == null || wave.Length == 0)
                    {
                        result.Failed++;
                    }
                    else
                    {
                        m_Cache.Put(path, wave);
                        result.Analyzed++;
                    }
                }
                if (progress != null)
                    progress(i + 1, list.Count);
            }
            return result;
        }
    }
}
