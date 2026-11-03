/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The rough "shape of the sound" of a music file, for the seek bar: the loudness of about 300 equal slices of the file,
 one byte each (0 = silence, 255 = the loudest slice). Reads the file with the same decoders as the player.
*/
using System;

namespace Ares.Playing.Audio
{
    public static class WaveformAnalyzer
    {
        public const int DefaultPoints = 300;

        /// <summary>The loudness of each slice of a file, or null if the file can't be read or has no known length (web radio).</summary>
        public static byte[] Analyze(string path, int points = DefaultPoints)
        {
            IAudioSource source = null;
            try
            {
                source = AudioSources.OpenFile(path);
                return source == null ? null : Summarize(source, points);
            }
            catch (Exception ex)
            {
                Ares.Settings.ErrorLog.Note(ex);
                return null;
            }
            finally
            {
                if (source != null)
                    source.Dispose();
            }
        }

        internal static byte[] Summarize(IAudioSource source, int points)
        {
            if (points < 1 || source.Length <= 0 || source.Channels < 1 || source.SampleRate < 1)
                return null;
            int channels = source.Channels;
            double totalFrames = source.Length * source.SampleRate;
            double framesPerSlice = totalFrames / points;
            if (framesPerSlice < 1)
                return null;
            double[] sum = new double[points];
            long[] count = new long[points];
            float[] buffer = new float[4096 * channels];
            long frame = 0;
            int emptyReads = 0;
            while (true)
            {
                int read = source.Read(buffer, 0, buffer.Length);
                if (read < 0)
                    return null; // no data (network)
                if (read == 0)
                    break;
                int frames = read / channels;
                for (int i = 0; i < frames; i++, frame++)
                {
                    int slice = (int)(frame / framesPerSlice);
                    if (slice >= points)
                        slice = points - 1;
                    double mono = 0;
                    for (int c = 0; c < channels; c++)
                        mono += buffer[i * channels + c];
                    mono /= channels;
                    sum[slice] += mono * mono;
                    count[slice]++;
                }
                if (frames == 0 && ++emptyReads > 100)
                    break;
            }
            return Scale(sum, count);
        }

        // root mean square of each slice, in relation to the loudest slice; the square root makes the quiet parts visible too
        private static byte[] Scale(double[] sum, long[] count)
        {
            double[] level = new double[sum.Length];
            double max = 0;
            for (int i = 0; i < sum.Length; i++)
            {
                level[i] = count[i] > 0 ? Math.Sqrt(sum[i] / count[i]) : 0;
                if (level[i] > max)
                    max = level[i];
            }
            byte[] result = new byte[sum.Length];
            if (max <= 0)
                return result;
            for (int i = 0; i < result.Length; i++)
                result[i] = (byte)Math.Round(255 * Math.Sqrt(level[i] / max));
            return result;
        }
    }
}
