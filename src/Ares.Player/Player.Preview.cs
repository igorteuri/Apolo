using System;
using System.Windows.Forms;

namespace Ares.Player
{
    // For looking at the windows during development (tools/preview.ps1): APOLO_PREVIEW=<name> opens that window by itself
    // a moment after the program starts. Without the variable nothing happens.
    public partial class Player
    {
        private void InitPreview()
        {
            string which = Environment.GetEnvironmentVariable("APOLO_PREVIEW");
            if (string.IsNullOrEmpty(which))
                return;
            // ticks once the program runs (the splash screen and the fade in of the window are over by then)
            Timer timer = new Timer { Interval = 5000 };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                try { OpenPreview(which.Trim().ToLowerInvariant()); }
                catch (Exception ex) { Trace(ex.ToString()); }
            };
            timer.Start();
        }

        private static void Trace(string text)
        {
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(Ares.Settings.AppFolders.Root, "preview.log"), DateTime.Now.ToString("HH:mm:ss.fff ") + text + Environment.NewLine); }
            catch (Exception) { }
        }

        private void OpenPreview(string which)
        {
            switch (which)
            {
                case "packs": ShowPacksDialog(); break;
                case "switch": ShowProjectSwitch(); break;
                case "credits": ShowProjectCredits(); break;
                case "prompt": NewEmptyMode(false); break;
                case "tour": ShowGuidedTour(false); break;
                case "export": { ExportKind kind; AskExportKind(out kind); } break;
                case "webcap":
                    // a web page in a tab (APOLO_PREVIEW_URL), to check the recording of its sound in audio-capture.log
                    ShowWebTab(m_WebWindow.AddTabForPreview(Environment.GetEnvironmentVariable("APOLO_PREVIEW_URL") ?? "https://www.youtube.com/"));
                    break;
                case "bank": BankPickerDialog.Pick(this, Bank, "drag"); break;
                case "missing":
                    {
                        var entries = new System.Collections.Generic.List<MissingEntry>();
                        foreach (string name in new[] { "Taverna à noite.mp3", "Chuva forte.ogg", "Batalha épica 🐉.flac" })
                        {
                            MissingEntry entry = new MissingEntry { FileName = name, FullPath = @"D:\Músicas antigas\RPG\" + name };
                            entry.Places.Add("Taverna / Ambiente");
                            entries.Add(entry);
                        }
                        using (MissingFilesDialog dlg = new MissingFilesDialog(entries))
                        {
                            dlg.ShowDialog(this);
                        }
                    }
                    break;
            }
        }
    }
}
