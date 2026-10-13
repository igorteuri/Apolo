/*
 Copyright (c) 2026 [Ares fork]

 This file is part of Ares.

 Ares is free software; you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation; either version 2 of the License, or
 (at your option) any later version.

 Ares is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 GNU General Public License for more details.
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Ares.Playing;

namespace Ares.Player
{
    /// <summary>
    /// Shows one small progress bar with a stop button for each sound effect which is currently playing.
    /// </summary>
    public class SoundProgressPanel : FlowLayoutPanel
    {
        public event EventHandler<SoundEventArgs> StopRequested;
        public event EventHandler<SoundEventArgs> PauseRequested;
        public event EventHandler<SoundSeekEventArgs> SeekRequested;
        public event EventHandler<SoundRepeatEventArgs> RepeatRequested;

        public class SoundRepeatEventArgs : SoundEventArgs
        {
            public SoundRepeatEventArgs(int handle, bool repeat) : base(handle) { Repeat = repeat; }
            public bool Repeat { get; private set; }
        }

        public class SoundEventArgs : EventArgs
        {
            public SoundEventArgs(int handle) { Handle = handle; }
            public int Handle { get; private set; }
        }

        public class SoundSeekEventArgs : SoundEventArgs
        {
            public SoundSeekEventArgs(int handle, double seconds) : base(handle) { Seconds = seconds; }
            public double Seconds { get; private set; }
        }

        private class Row : Panel
        {
            public int SoundHandle;
            public Label TitleLabel;
            public MusicProgressBar Bar;
            public Button StopButton;
            public Button PauseButton;
            public CheckBox RepeatBox;
            public bool Updating;

            public Row(int handle)
            {
                SoundHandle = handle;
                Margin = new Padding(0, 0, 0, 4);
                TitleLabel = new Label();
                TitleLabel.AutoEllipsis = true;
                TitleLabel.UseMnemonic = false;
                TitleLabel.AutoSize = false;
                Bar = new MusicProgressBar();
                Bar.Compact = true;
                StopButton = new Button();
                StopButton.Text = "■"; // black square
                StopButton.TabStop = false;
                PauseButton = new Button();
                PauseButton.Text = "\u275A\u275A"; // two bars
                PauseButton.TabStop = false;
                RepeatBox = new CheckBox();
                RepeatBox.Appearance = Appearance.Button;
                RepeatBox.TextAlign = ContentAlignment.MiddleCenter;
                RepeatBox.Text = "\u21BB";
                RepeatBox.TabStop = false;
                Controls.Add(TitleLabel);
                Controls.Add(Bar);
                Controls.Add(StopButton);
                Controls.Add(PauseButton);
                Controls.Add(RepeatBox);
                Height = 50;
                DoLayoutRow();
            }

            public void DoLayoutRow()
            {
                int buttonSize = 28;
                int titleHeight = Math.Max(16, TitleLabel.Font.Height + 2);
                int barWidth = Math.Max(20, Width - 3 * buttonSize - 14);
                TitleLabel.SetBounds(2, 0, barWidth, titleHeight);
                Bar.SetBounds(0, titleHeight, barWidth, Math.Max(30, Bar.Font.Height * 2 + 4));
                StopButton.SetBounds(Width - buttonSize, titleHeight + 2, buttonSize, buttonSize);
                RepeatBox.SetBounds(Width - 2 * buttonSize - 4, titleHeight + 2, buttonSize, buttonSize);
                PauseButton.SetBounds(Width - 3 * buttonSize - 8, titleHeight + 2, buttonSize, buttonSize);
                Height = titleHeight + Bar.Height + 2;
            }

            protected override void OnResize(EventArgs eventargs)
            {
                base.OnResize(eventargs);
                if (TitleLabel != null && StopButton != null && Bar != null && RepeatBox != null && PauseButton != null)
                    DoLayoutRow();
            }
        }

        private readonly Dictionary<int, Row> m_Rows = new Dictionary<int, Row>();

        public SoundProgressPanel()
        {
            FlowDirection = FlowDirection.TopDown;
            WrapContents = false;
            AutoScroll = true;
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            UpdateRowWidths();
        }

        private void UpdateRowWidths()
        {
            int width = ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2;
            if (width < 60)
                width = 60;
            foreach (Row row in m_Rows.Values)
            {
                if (row.Width != width)
                    row.Width = width;
            }
        }

        /// <summary>
        /// Brings the displayed bars in line with the sounds which are currently playing.
        /// </summary>
        public void UpdateSounds(IList<PlayingFileInfo> sounds)
        {
            HashSet<int> alive = new HashSet<int>(sounds.Select(s => s.Handle));
            bool changed = false;

            foreach (int handle in m_Rows.Keys.Where(h => !alive.Contains(h)).ToList())
            {
                Row row = m_Rows[handle];
                m_Rows.Remove(handle);
                Controls.Remove(row);
                row.Dispose();
                changed = true;
            }

            SuspendLayout();
            foreach (PlayingFileInfo sound in sounds)
            {
                Row row;
                if (!m_Rows.TryGetValue(sound.Handle, out row))
                {
                    row = CreateRow(sound.Handle);
                    m_Rows.Add(sound.Handle, row);
                    Controls.Add(row);
                    changed = true;
                }
                if (row.TitleLabel.Text != sound.Title)
                    row.TitleLabel.Text = sound.Title;
                row.Bar.SetProgress(sound.Position, sound.Length);
                string pauseText = sound.Paused ? "\u25B6" : "\u275A\u275A";
                if (row.PauseButton.Text != pauseText)
                    row.PauseButton.Text = pauseText;
                if (row.RepeatBox.Checked != sound.Repeat)
                {
                    row.Updating = true;
                    row.RepeatBox.Checked = sound.Repeat;
                    row.Updating = false;
                }
            }
            if (changed)
                UpdateRowWidths();
            ResumeLayout(changed);
        }

        public void ClearSounds()
        {
            if (m_Rows.Count == 0)
                return;
            UpdateSounds(new List<PlayingFileInfo>());
        }

        private Row CreateRow(int handle)
        {
            Row row = new Row(handle);
            row.StopButton.Click += (sender, e) =>
            {
                EventHandler<SoundEventArgs> handler = StopRequested;
                if (handler != null)
                    handler(this, new SoundEventArgs(handle));
            };
            row.PauseButton.Click += (sender, e) =>
            {
                EventHandler<SoundEventArgs> handler = PauseRequested;
                if (handler != null)
                    handler(this, new SoundEventArgs(handle));
            };
            row.RepeatBox.CheckedChanged += (sender, e) =>
            {
                if (row.Updating)
                    return;
                EventHandler<SoundRepeatEventArgs> handler = RepeatRequested;
                if (handler != null)
                    handler(this, new SoundRepeatEventArgs(handle, row.RepeatBox.Checked));
            };
            row.Bar.SeekRequested += (sender, e) =>
            {
                EventHandler<SoundSeekEventArgs> handler = SeekRequested;
                if (handler != null)
                    handler(this, new SoundSeekEventArgs(handle, e.Seconds));
            };
            row.Width = Math.Max(60, ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2);
            return row;
        }
    }
}
