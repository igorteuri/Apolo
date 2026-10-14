/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The panel at the top of the Editor: play / pause / stop of the selected playlist with the progress bars of
 what is playing (the same controls as in the Player) and the look the button of the playlist will have in the Player.
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Ares.Player;
using Ares.Playing;

namespace Ares.Editor
{
    /// <summary>What is selected in the project explorer (a folder or the button of a playlist).</summary>
    class SelectionInfo
    {
        public ProjectExplorer Source;
        public string ProjectPath;
        public string Mode;
        public string Element;      // "" for a folder
        public string Title;
        public string TypeName;
        public bool IsFolder;
    }

    class PlaybackPanel : ToolWindow
    {
        private readonly Label m_Title = new Label();
        private readonly Button m_Play = new Button();
        private readonly Button m_Pause = new Button();
        private readonly Button m_Stop = new Button();
        private readonly SoundProgressPanel m_Running = new SoundProgressPanel();
        private readonly Label m_LookTitle = new Label();
        private readonly Panel m_PreviewHost = new Panel();
        private readonly FlowLayoutPanel m_LookButtons = new FlowLayoutPanel();
        private readonly Timer m_Timer = new Timer();
        private ElementButton m_Preview;
        private SelectionInfo m_Info;

        public PlaybackPanel()
        {
            Text = ProjectExplorer.Tx("Reprodução e aparência", "Playback and look");
            ClientSize = new Size(900, 170);

            // ---- left: playback ----
            Panel left = new Panel();
            left.Dock = DockStyle.Fill;
            Panel right = new Panel();
            right.Dock = DockStyle.Right;
            right.Width = 460;

            m_Title.Dock = DockStyle.Top;
            m_Title.Height = 26;
            m_Title.TextAlign = ContentAlignment.MiddleLeft;
            m_Title.Padding = new Padding(6, 0, 0, 0);
            m_Title.Font = new Font(Font, FontStyle.Bold);
            m_Title.Text = ProjectExplorer.Tx("Selecione uma playlist", "Select a playlist");

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Top;
            buttons.Height = 36;
            buttons.Padding = new Padding(4, 2, 0, 0);
            SetupButton(m_Play, "▶  " + ProjectExplorer.Tx("Tocar", "Play"), 90);
            SetupButton(m_Pause, "❚❚  " + ProjectExplorer.Tx("Pausar", "Pause"), 100);
            SetupButton(m_Stop, "■  " + ProjectExplorer.Tx("Parar", "Stop"), 90);
            buttons.Controls.Add(m_Play);
            buttons.Controls.Add(m_Pause);
            buttons.Controls.Add(m_Stop);
            m_Play.Click += (s, e) => { if (m_Info != null && m_Info.Source != null) m_Info.Source.PlaySelected(); };
            m_Stop.Click += (s, e) => { Ares.Editor.Actions.Playing.Instance.StopAll(); };
            m_Pause.Click += (s, e) => TogglePauseAll();

            m_Running.Dock = DockStyle.Fill;
            m_Running.AutoScroll = true;
            m_Running.StopRequested += (s, e) => PlayingModule.StopRunningFile(e.Handle);
            m_Running.PauseRequested += (s, e) => PlayingModule.ToggleFilePause(e.Handle);
            m_Running.SeekRequested += (s, e) => PlayingModule.SeekFile(e.Handle, e.Seconds);

            left.Controls.Add(m_Running);
            left.Controls.Add(buttons);
            left.Controls.Add(m_Title);

            // ---- right: how the button looks in the Player ----
            m_LookTitle.Dock = DockStyle.Top;
            m_LookTitle.Height = 26;
            m_LookTitle.TextAlign = ContentAlignment.MiddleLeft;
            m_LookTitle.Padding = new Padding(6, 0, 0, 0);
            m_LookTitle.Font = new Font(Font, FontStyle.Bold);
            m_LookTitle.Text = ProjectExplorer.Tx("Aparência no Player", "Look in the Player");

            m_PreviewHost.Dock = DockStyle.Left;
            m_PreviewHost.Width = 180;
            m_PreviewHost.Padding = new Padding(6);

            m_LookButtons.Dock = DockStyle.Fill;
            m_LookButtons.Padding = new Padding(2);
            AddLookButton(ProjectExplorer.Tx("Emoji", "Emoji"), s => s.LooksChooseEmoji());
            AddLookButton(ProjectExplorer.Tx("Emoji automático", "Auto emoji"), s => s.LooksAutoEmoji());
            AddLookButton(ProjectExplorer.Tx("Cor", "Color"), s => s.LooksChooseColor());
            AddLookButton(ProjectExplorer.Tx("Ícone", "Icon"), s => s.LooksChooseIcon());
            AddLookButton(ProjectExplorer.Tx("Imagem", "Picture"), s => s.LooksChoosePicture());
            AddLookButton(ProjectExplorer.Tx("Imagens da internet", "Internet pictures"), s => s.LooksSuggestImages());
            AddLookButton(ProjectExplorer.Tx("Remover imagem", "Remove picture"), s => s.LooksRemoveImage());

            right.Controls.Add(m_LookButtons);
            right.Controls.Add(m_PreviewHost);
            right.Controls.Add(m_LookTitle);

            Controls.Add(left);
            Controls.Add(right);
            // docking order: the "fill" panel must be added first to be laid out last
            left.BringToFront();

            m_Timer.Interval = 250;
            m_Timer.Tick += (s, e) => RefreshRunning();
            m_Timer.Start();
            FormClosed += (s, e) => m_Timer.Stop();
        }

        protected override string GetPersistString()
        {
            return "PlaybackPanel";
        }

        private static void SetupButton(Button b, string text, int width)
        {
            b.Text = text;
            b.Size = new Size(width, 28);
            b.Margin = new Padding(2);
        }

        private void AddLookButton(string text, Action<ProjectExplorer> action)
        {
            Button b = new Button();
            b.Text = text;
            b.AutoSize = true;
            b.Margin = new Padding(3);
            b.Padding = new Padding(4, 0, 4, 0);
            b.Click += (s, e) =>
            {
                if (m_Info != null && m_Info.Source != null)
                    action(m_Info.Source);
            };
            m_LookButtons.Controls.Add(b);
        }

        private void TogglePauseAll()
        {
            List<PlayingFileInfo> files = PlayingModule.GetRunningFiles();
            bool anyPlaying = false;
            foreach (PlayingFileInfo f in files)
            {
                if (!f.Paused)
                    anyPlaying = true;
            }
            foreach (PlayingFileInfo f in files)
            {
                // pause everything which plays; when everything is paused, continue everything
                if (f.Paused != anyPlaying)
                    continue;
                PlayingModule.ToggleFilePause(f.Handle);
            }
        }

        private void RefreshRunning()
        {
            try
            {
                List<PlayingFileInfo> files = PlayingModule.GetRunningFiles();
                m_Running.UpdateSounds(files);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>Called by the project explorer when another folder or playlist is selected (null: nothing useful).</summary>
        public void SetSelection(SelectionInfo info)
        {
            m_Info = info;
            if (info == null)
            {
                m_Title.Text = ProjectExplorer.Tx("Selecione uma playlist", "Select a playlist");
                m_LookTitle.Text = ProjectExplorer.Tx("Aparência no Player", "Look in the Player");
                SetPreview(null);
                return;
            }
            m_Title.Text = info.IsFolder
                ? ProjectExplorer.Tx("Pasta: ", "Folder: ") + info.Title
                : info.Mode + "  ›  " + info.Title + (info.TypeName.Length > 0 ? "   (" + info.TypeName + ")" : "");
            m_LookTitle.Text = ProjectExplorer.Tx("Aparência no Player", "Look in the Player")
                + (string.IsNullOrEmpty(info.ProjectPath) ? "  -  " + ProjectExplorer.Tx("salve o projeto primeiro", "save the project first") : "");
            SetPreview(info);
        }

        private void SetPreview(SelectionInfo info)
        {
            if (m_Preview != null)
            {
                m_PreviewHost.Controls.Remove(m_Preview);
                m_Preview.Dispose();
                m_Preview = null;
            }
            if (info == null)
                return;
            ElementButton button = new ElementButton();
            button.Font = Font;
            button.Text = info.Title;
            button.Enabled = true;
            int kind = 0;
            Bitmap picture = null;
            string spec = null;
            if (!string.IsNullOrEmpty(info.ProjectPath))
            {
                spec = Ares.CommonGUI.ButtonImageStore.Get(info.ProjectPath, info.Mode, info.Element);
                if (spec != null && (spec.StartsWith("img:") || spec.StartsWith("lib:")))
                {
                    picture = Ares.CommonGUI.ButtonImageStore.LoadImage(info.ProjectPath, spec);
                    if (picture != null)
                        kind = 2;
                }
                else if (spec != null && spec.StartsWith("lucide:"))
                {
                    kind = 1;
                }
                button.TintColor = Ares.CommonGUI.ButtonImageStore.GetColor(info.ProjectPath, info.Mode, info.IsFolder ? null : info.Element);
            }
            if (kind == 2)
            {
                button.Picture = picture;
                if (spec.IndexOf("emoji-", StringComparison.OrdinalIgnoreCase) >= 0)
                    button.PictureContain = true;
            }
            else if (kind == 1)
            {
                button.IconName = spec.Substring(7);
            }
            Size size = ElementButton.ComputeCardSize(new List<string> { info.Title }, new List<int> { kind }, Font);
            button.SetBounds(6, 6, size.Width, size.Height);
            // only a preview: it does not play anything
            button.SwallowClick = false;
            button.Click += (s, e) => { button.Checked = false; };
            m_PreviewHost.Controls.Add(button);
            m_Preview = button;
        }

        public void RefreshLook()
        {
            SetSelection(m_Info);
        }
    }
}
