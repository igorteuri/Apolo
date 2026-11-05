/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Waveforms ahead of time: "Analyze the waveforms of the project" in the Tools menu (by hand), and an automatic scan when the
 program has been quiet for a while (nothing playing, no web tab playing). The automatic scan stops at once when sound
 starts. The work itself is in WaveformPrecompute (tested); the results go to the same cache as the seek bar and the chips.
*/
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Ares.Player
{
    public partial class Player
    {
        private const int WaveIdleMs = 150000;       // quiet for 2.5 minutes: the automatic scan may start
        private const int WaveResumeMs = 4000;       // sound heard in the last seconds: the scan stops
        private Timer m_WaveIdleTimer;
        private volatile bool m_WaveScanning;
        private volatile bool m_WaveScanCancel;
        private ToolStripMenuItem m_WaveAutoItem;
        private string m_WaveAutoDoneFor = "";       // project and number of files already covered by an automatic scan

        private void InitWaveScan()
        {
            ToolStripMenuItem now = new ToolStripMenuItem(T("Analisar as formas de onda do projeto", "Analyze the waveforms of the project"));
            now.ToolTipText = T("Prepara a forma de onda de todas as músicas e efeitos do projeto, para a barra de progresso aparecer na hora.",
                "Prepares the waveform of all the music and effects of the project, so that the progress bar shows it at once.");
            now.Click += (s, e) => StartWaveScan(true);
            m_WaveAutoItem = new ToolStripMenuItem(T("Analisar sozinho quando o Apolo estiver parado", "Analyze by itself when Apolo is idle"));
            m_WaveAutoItem.ToolTipText = T("Depois de alguns minutos sem tocar nada, analisa as formas de onda aos poucos; para assim que algo começa a tocar.",
                "After a few minutes with nothing playing, analyzes the waveforms little by little; stops as soon as something plays.");
            m_WaveAutoItem.CheckOnClick = true;
            m_WaveAutoItem.Checked = Ares.CommonGUI.Preferences.GetBool("WaveAutoScan", true);
            m_WaveAutoItem.CheckedChanged += (s, e) => Ares.CommonGUI.Preferences.SetBool("WaveAutoScan", m_WaveAutoItem.Checked);
            extrasToolStripMenuItem.DropDownItems.Add(now);
            extrasToolStripMenuItem.DropDownItems.Add(m_WaveAutoItem);

            m_WaveIdleTimer = new Timer();
            m_WaveIdleTimer.Interval = 20000;
            m_WaveIdleTimer.Tick += (s, e) => CheckWaveIdle();
            m_WaveIdleTimer.Start();
            FormClosing += (s, e) => m_WaveScanCancel = true;
        }

        private bool AnythingPlayingNow()
        {
            if (!WaveformPrecompute.IsIdle(Environment.TickCount, Ares.Playing.AudioLeveler.SoundTick, WaveResumeMs))
                return true;
            if (m_WebWindow != null)
            {
                foreach (WebTab tab in m_WebWindow.Tabs)
                {
                    if (tab.Playing)
                        return true;
                }
            }
            return false;
        }

        private void CheckWaveIdle()
        {
            if (IsDisposed || m_WaveScanning || m_Project == null || m_WaveAutoItem == null || !m_WaveAutoItem.Checked)
                return;
            if (!WaveformPrecompute.IsIdle(Environment.TickCount, Ares.Playing.AudioLeveler.SoundTick, WaveIdleMs) || AnythingPlayingNow())
                return;
            StartWaveScan(false);
        }

        // byHand: from the menu (runs to the end, tells the result); otherwise the automatic scan (quiet, stops when sound starts)
        private void StartWaveScan(bool byHand)
        {
            if (m_WaveScanning)
            {
                if (byHand)
                    ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Info, T("A análise já está em andamento.", "The analysis is already running.")));
                return;
            }
            List<string> files = AudioFilesOfProject(false);
            string marker = (m_Project != null ? m_Project.Title : "") + "|" + files.Count;
            if (!byHand && marker == m_WaveAutoDoneFor)
                return; // this project was already covered; it is looked at again when it changes
            if (files.Count == 0)
            {
                if (byHand)
                    ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Info, T("O projeto não tem arquivos de áudio para analisar.", "The project has no audio files to analyze.")));
                return;
            }
            if (m_WaveCache == null)
                m_WaveCache = new WaveformCache(System.IO.Path.Combine(Ares.Settings.AppFolders.Root, "waveforms"));
            WaveformCache cache = m_WaveCache;
            m_WaveScanning = true;
            m_WaveScanCancel = false;
            if (byHand)
                ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Info, T("Analisando " + files.Count + " arquivos em segundo plano...", "Analyzing " + files.Count + " files in the background...")));
            System.Threading.Tasks.Task.Run(() =>
            {
                WaveformPrecompute.Result result = null;
                try
                {
                    System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.BelowNormal;
                    WaveformPrecompute run = new WaveformPrecompute(cache, path =>
                    {
                        // the same gate as the seek bar: one analysis at a time
                        s_WaveGate.Wait();
                        try
                        {
                            return Ares.Playing.Audio.WaveformAnalyzer.Analyze(path);
                        }
                        finally
                        {
                            s_WaveGate.Release();
                        }
                    });
                    result = run.Run(files, () => m_WaveScanCancel || (!byHand && AnythingPlayingNow()), null);
                }
                catch (Exception ex)
                {
                    Ares.Settings.ErrorLog.Note(ex);
                }
                finally
                {
                    m_WaveScanning = false;
                }
                if (IsDisposed)
                    return;
                try
                {
                    BeginInvoke(new System.Windows.Forms.MethodInvoker(() =>
                    {
                        if (result != null && !result.Stopped)
                            m_WaveAutoDoneFor = marker;
                        if (!byHand || result == null)
                            return;
                        string text = result.Stopped
                            ? T("Análise interrompida: " + result.Analyzed + " novas formas de onda.", "Analysis stopped: " + result.Analyzed + " new waveforms.")
                            : T("Formas de onda prontas: " + result.Analyzed + " novas, " + result.Cached + " já existiam"
                                + (result.Failed > 0 ? ", " + result.Failed + " não puderam ser lidas." : "."),
                                "Waveforms ready: " + result.Analyzed + " new, " + result.Cached + " already there"
                                + (result.Failed > 0 ? ", " + result.Failed + " could not be read." : "."));
                        ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Info, text));
                    }));
                }
                catch (Exception swallowed)
                {
                    Ares.Settings.ErrorLog.Note(swallowed); // the window was closed
                }
            });
        }
    }
}
