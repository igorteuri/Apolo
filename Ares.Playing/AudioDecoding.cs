/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Decodes a whole music file (used to identify music through its fingerprint).
*/
using System;
using System.Collections.Generic;
using Ares.Playing.Audio;

namespace Ares.Playing
{
    public static class AudioDecoding
    {
        /// <summary>
        /// Decodes a file to mono, 16 bit samples with the given sample rate.
        /// Returns null if the file can't be decoded.
        /// </summary>
        public static Int16[] ExtractMono(String file, int sampleRate, out double seconds)
        {
            seconds = 0;
            IAudioSource source = null;
            try
            {
                source = AudioSources.OpenFile(file);
                if (source == null)
                    return null;
                IStereoStream stereo = new SourceStream(source, true);
                ResampleStream resampler = new ResampleStream(stereo, (double)source.SampleRate / sampleRate);
                List<Int16> data = new List<Int16>();
                float[] buffer = new float[4096 * 2];
                while (true)
                {
                    int frames = resampler.Read(buffer, 0, 4096);
                    if (frames <= 0)
                        break;
                    for (int i = 0; i < frames; i++)
                    {
                        float v = buffer[i * 2];
                        if (v > 1f) v = 1f; else if (v < -1f) v = -1f;
                        data.Add((Int16)(v * 32767f));
                    }
                }
                seconds = (double)data.Count / sampleRate;
                return data.ToArray();
            }
            catch (OutOfMemoryException)
            {
                GC.Collect();
                return null;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                if (source != null)
                {
                    try { source.Dispose(); } catch (Exception) { }
                }
            }
        }
    }
}
