using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Ares.Player
{
    /// <summary>
    /// Keeps the rough waveform of music files (a few hundred bytes each) in a folder, so that a file is analyzed once.
    /// A file is recognized by its path, size and date: a changed file is analyzed again. No window code.
    /// </summary>
    public sealed class WaveformCache
    {
        private readonly string m_Folder;

        public WaveformCache(string folder)
        {
            m_Folder = folder;
        }

        private string FileFor(string musicPath)
        {
            FileInfo info = new FileInfo(musicPath);
            if (!info.Exists)
                return null;
            string key = musicPath.ToLowerInvariant() + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks;
            using (SHA1 sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
                return Path.Combine(m_Folder, BitConverter.ToString(hash).Replace("-", "") + ".wave");
            }
        }

        public byte[] Get(string musicPath)
        {
            try
            {
                string file = string.IsNullOrEmpty(musicPath) ? null : FileFor(musicPath);
                if (file == null || !File.Exists(file))
                    return null;
                byte[] data = File.ReadAllBytes(file);
                return data.Length > 0 ? data : null;
            }
            catch (Exception ex)
            {
                Ares.Settings.ErrorLog.Note(ex);
                return null;
            }
        }

        public void Put(string musicPath, byte[] peaks)
        {
            try
            {
                string file = string.IsNullOrEmpty(musicPath) || peaks == null || peaks.Length == 0 ? null : FileFor(musicPath);
                if (file == null)
                    return;
                Directory.CreateDirectory(m_Folder);
                string temp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllBytes(temp, peaks);
                if (File.Exists(file))
                    File.Delete(file);
                File.Move(temp, file);
            }
            catch (Exception ex)
            {
                Ares.Settings.ErrorLog.Note(ex);
            }
        }
    }
}
