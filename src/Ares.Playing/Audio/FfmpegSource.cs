/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 A decoder which uses the program "ffmpeg" (if it is installed) for the formats the engine cannot read by itself:
 M4A / AAC, WMA, FLAC, AIFF, ALAC... On Windows the Media Foundation of the system does this; on Linux and macOS this is the
 way. ffmpeg is not part of Apolo: it is looked for beside the program, in the PATH and in the usual folders.
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Ares.Playing.Audio
{
    internal sealed class FfmpegSource : IAudioSource
    {
        private const int Rate = AudioEngine.SampleRate;

        private static string s_Exe;
        private static bool s_Looked;

        /// <summary>The ffmpeg program, or null if there is none.</summary>
        public static string Executable
        {
            get
            {
                if (!s_Looked)
                {
                    s_Looked = true;
                    s_Exe = Find();
                }
                return s_Exe;
            }
        }

        private static string Find()
        {
            string name = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
            List<string> folders = new List<string>();
            folders.Add(AppContext.BaseDirectory);
            folders.Add(Path.Combine(AppContext.BaseDirectory, "ffmpeg"));
            string path = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(path))
                folders.AddRange(path.Split(Path.PathSeparator));
            if (!OperatingSystem.IsWindows())
                folders.AddRange(new[] { "/usr/bin", "/usr/local/bin", "/opt/homebrew/bin", "/snap/bin", "/opt/local/bin" });
            foreach (string folder in folders)
            {
                try
                {
                    if (string.IsNullOrEmpty(folder))
                        continue;
                    string candidate = Path.Combine(folder.Trim('"'), name);
                    if (File.Exists(candidate))
                        return candidate;
                }
                catch (Exception)
                {
                }
            }
            return null;
        }

        private readonly string m_Path;
        private readonly double m_Length;
        private readonly bool m_Mono;
        private Process m_Process;
        private Stream m_Output;
        private long m_Frames;          // frames delivered since the (re)start
        private double m_Start;         // position (seconds) where the process was started
        private readonly byte[] m_Bytes = new byte[8192];

        private FfmpegSource(string path, double length, bool mono)
        {
            m_Path = path;
            m_Length = length;
            m_Mono = mono;
            Launch(0);
        }

        /// <summary>null if ffmpeg is missing or cannot read the file.</summary>
        public static FfmpegSource TryOpen(string path)
        {
            string exe = Executable;
            if (exe == null)
                return null;
            bool mono;
            double length = Probe(exe, path, out mono);
            if (length < 0)
                return null;
            return new FfmpegSource(path, length, mono);
        }

        // the duration in seconds (0 if unknown), -1 if the file cannot be read
        private static double Probe(string exe, string path, out bool mono)
        {
            mono = false;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe);
                psi.ArgumentList.Add("-hide_banner");
                psi.ArgumentList.Add("-i");
                psi.ArgumentList.Add(path);
                psi.RedirectStandardError = true;
                psi.RedirectStandardOutput = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                using (Process p = Process.Start(psi))
                {
                    string text = p.StandardError.ReadToEnd();
                    p.WaitForExit(10000);
                    if (text.IndexOf("Audio:", StringComparison.Ordinal) < 0)
                        return -1;
                    // a mono file is copied to both sides (ffmpeg's own mix lowers it by 3 dB)
                    Match audio = Regex.Match(text, @"Audio:[^\r\n]*");
                    mono = audio.Success && Regex.IsMatch(audio.Value, @",\s*mono\b");
                    Match m = Regex.Match(text, @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)");
                    if (!m.Success)
                        return 0;
                    return int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 3600.0
                        + int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) * 60.0
                        + double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                }
            }
            catch (Exception)
            {
                return -1;
            }
        }

        private void Launch(double seconds)
        {
            Stop();
            ProcessStartInfo psi = new ProcessStartInfo(Executable);
            psi.ArgumentList.Add("-v");
            psi.ArgumentList.Add("quiet");
            if (seconds > 0)
            {
                psi.ArgumentList.Add("-ss");
                psi.ArgumentList.Add(seconds.ToString("0.###", CultureInfo.InvariantCulture));
            }
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(m_Path);
            psi.ArgumentList.Add("-vn");
            if (m_Mono)
            {
                psi.ArgumentList.Add("-af");
                psi.ArgumentList.Add("pan=stereo|c0=c0|c1=c0");
            }
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("f32le");
            psi.ArgumentList.Add("-ac");
            psi.ArgumentList.Add("2");
            psi.ArgumentList.Add("-ar");
            psi.ArgumentList.Add(Rate.ToString(CultureInfo.InvariantCulture));
            psi.ArgumentList.Add("pipe:1");
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            m_Process = Process.Start(psi);
            // the messages of ffmpeg are not wanted, but the pipe must not fill up
            m_Process.ErrorDataReceived += (s, e) => { };
            m_Process.BeginErrorReadLine();
            m_Output = m_Process.StandardOutput.BaseStream;
            m_Start = seconds;
            m_Frames = 0;
        }

        private void Stop()
        {
            Process p = m_Process;
            m_Process = null;
            m_Output = null;
            if (p == null)
                return;
            try { if (!p.HasExited) p.Kill(); } catch (Exception) { }
            try { p.Dispose(); } catch (Exception) { }
        }

        public int Channels { get { return 2; } }
        public int SampleRate { get { return Rate; } }
        public double Length { get { return m_Length; } }
        public double Position { get { return m_Start + (double)m_Frames / Rate; } }
        public bool CanSeek { get { return m_Length > 0; } }

        public void Seek(double seconds)
        {
            Launch(Math.Max(0, Math.Min(seconds, Math.Max(0, m_Length - 0.01))));
        }

        public int Read(float[] buffer, int offset, int count)
        {
            Stream output = m_Output;
            if (output == null)
                return 0;
            int bytesWanted = Math.Min(count * 4, m_Bytes.Length);
            int got = 0;
            try
            {
                // a pipe may deliver less than asked: fill whole samples
                while (got < 4)
                {
                    int n = output.Read(m_Bytes, got, bytesWanted - got);
                    if (n <= 0)
                        break;
                    got += n;
                    if (got >= 4 && (got % 4 == 0))
                        break;
                }
            }
            catch (Exception)
            {
                return 0;
            }
            int samples = got / 4;
            if (samples == 0)
                return 0;
            Buffer.BlockCopy(m_Bytes, 0, buffer, offset * 4, samples * 4);
            m_Frames += samples / 2;
            return samples;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
