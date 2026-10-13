/*
 Copyright (c) 2015 [Joerg Ruedenauer]
 
 This file is part of Ares.

 Ares is free software; you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation; either version 2 of the License, or
 (at your option) any later version.

 Ares is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with Ares; if not, write to the Free Software
 Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using Ares.Players;
using Ares.CommonGUI;

namespace Ares.Player
{
	partial class MessagesForm : Ares.CommonGUI.InvokableForm
    {
        private ListBox m_List;
        private Label m_Header;
        private Button m_CopyButton;
        private Button m_MissingButton;
        private Panel m_TopBar;
        private Panel m_BottomBar;

        /// <summary>Raised when the user wants to look for missing audio files.</summary>
        public event EventHandler FindMissingRequested;

        private static string T(string pt, string en)
        {
            return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt" ? pt : en;
        }

        public MessagesForm()
        {
			InitializeComponent();
            BuildModernUi();
            Messages.Instance.MessageReceived += new MessageReceivedHandler(MessageReceived);
            filterBox.SelectedIndex = Ares.Settings.Settings.Instance.MessageFilterLevel;
        }

        // the old grid is replaced by a card list in the colors of the current theme
        private void BuildModernUi()
        {
            Controls.Clear();
            Text = T("Mensagens", "Messages");
            ClientSize = new Size(600, 460);
            Icon = Ares.CommonGUI.ApoloBrand.GetIcon(32, Ares.CommonGUI.ApoloState.Playing, 0);
            MinimumSize = new Size(420, 300);
            ShowInTaskbar = false;

            m_TopBar = new Panel { Dock = DockStyle.Top, Height = 46 };
            m_Header = new Label { AutoSize = true, Location = new Point(12, 14), Text = T("Mostrar:", "Show:") };
            label1.Visible = false;
            filterBox.SetBounds(m_Header.Right + 56, 10, 190, 28);
            m_TopBar.Controls.Add(m_Header);
            m_TopBar.Controls.Add(filterBox);
            m_TopBar.Resize += (s, e) => { };

            m_BottomBar = new Panel { Dock = DockStyle.Bottom, Height = 52 };
            clearButton.Text = T("Limpar", "Clear");
            clearButton.SetBounds(12, 10, 90, 32);
            m_CopyButton = new Button { Text = T("Copiar", "Copy"), Bounds = new Rectangle(110, 10, 90, 32) };
            m_CopyButton.Click += (s, e) => CopyMessages();
            m_MissingButton = new Button { Text = T("Arquivos ausentes...", "Missing files..."), Bounds = new Rectangle(208, 10, 190, 32) };
            m_MissingButton.Click += (s, e) =>
            {
                EventHandler handler = FindMissingRequested;
                if (handler != null)
                    handler(this, EventArgs.Empty);
            };
            m_BottomBar.Controls.Add(clearButton);
            m_BottomBar.Controls.Add(m_CopyButton);
            m_BottomBar.Controls.Add(m_MissingButton);

            m_List = new ListBox();
            m_List.Dock = DockStyle.Fill;
            m_List.BorderStyle = BorderStyle.None;
            m_List.DrawMode = DrawMode.OwnerDrawVariable;
            m_List.IntegralHeight = false;
            m_List.MeasureItem += List_MeasureItem;
            m_List.DrawItem += List_DrawItem;
            m_List.SelectionMode = SelectionMode.MultiExtended;
            m_List.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == System.Windows.Forms.Keys.C)
                    CopyMessages();
            };
            m_List.SizeChanged += (s, e) => RefillGrid();

            Controls.Add(m_List);
            Controls.Add(m_BottomBar);
            Controls.Add(m_TopBar);
            m_List.BringToFront();
        }

        private void RefillGrid()
        {
            if (m_List == null)
                return;
            m_List.BeginUpdate();
            m_List.Items.Clear();
            foreach (Ares.Players.Message m in Messages.Instance.GetAllMessages())
            {
                AddMessage(m);
            }
            m_List.EndUpdate();
            ScrollToEnd();
        }

        private void ScrollToEnd()
        {
            if (m_List.Items.Count > 0)
                m_List.TopIndex = m_List.Items.Count - 1;
        }

        private void MessageReceived(Ares.Players.Message m)
        {
            if (IsDisposed)
                return;
			if (this.IsInvokeRequired())
            {
                BeginInvoke(new MethodInvoker(() => { AddMessage(m); ScrollToEnd(); }));
            }
            else
            {
                AddMessage(m);
                ScrollToEnd();
            }
        }

        private void AddMessage(Ares.Players.Message m)
        {
            if (m.Type >= FilterLevel)
            {
                m_List.Items.Add(m);
            }
        }

        private void CopyMessages()
        {
            try
            {
                IEnumerable<object> source = m_List.SelectedItems.Count > 0 ? m_List.SelectedItems.Cast<object>() : m_List.Items.Cast<object>();
                string text = string.Join(Environment.NewLine, source.Select(o => ((Ares.Players.Message)o).Text));
                if (text.Length > 0)
                    Clipboard.SetText(text);
            }
            catch (Exception)
            {
            }
        }

        // ---- drawing ----

        private const int BarWidth = 5;

        private static Color SeverityColor(MessageType type, ThemePalette p, bool themed)
        {
            switch (type)
            {
                case MessageType.Error:
                    return Color.FromArgb(222, 84, 84);
                case MessageType.Warning:
                    return Color.FromArgb(232, 168, 48);
                case MessageType.Info:
                    return themed ? p.Accent : SystemColors.Highlight;
                default:
                    return themed ? p.TextDim : SystemColors.GrayText;
            }
        }

        private static string SeverityName(MessageType type)
        {
            switch (type)
            {
                case MessageType.Error:
                    return T("ERRO", "ERROR");
                case MessageType.Warning:
                    return T("AVISO", "WARNING");
                case MessageType.Info:
                    return T("INFO", "INFO");
                default:
                    return T("DEPURAÇÃO", "DEBUG");
            }
        }

        private const TextFormatFlags TextFlags = TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.Left;

        private int TextWidth
        {
            get { return Math.Max(80, m_List.ClientSize.Width - BarWidth - 36); }
        }

        private void List_MeasureItem(object sender, MeasureItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= m_List.Items.Count)
                return;
            Ares.Players.Message m = (Ares.Players.Message)m_List.Items[e.Index];
            int small = TextRenderer.MeasureText("Ag", Font).Height;
            int body = TextRenderer.MeasureText(m.Text ?? "", Font, new Size(TextWidth, int.MaxValue), TextFlags).Height;
            e.ItemHeight = small + body + 22;
        }

        private void List_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= m_List.Items.Count)
                return;
            Ares.Players.Message m = (Ares.Players.Message)m_List.Items[e.Index];
            ThemePalette p = ThemeManager.Current;
            bool themed = p != null && !p.IsClassic;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color window = themed ? p.Window : SystemColors.Window;
            Color surface = themed ? p.Surface : SystemColors.ControlLight;
            Color text = themed ? p.Text : SystemColors.ControlText;
            Color dim = themed ? p.TextDim : SystemColors.GrayText;
            Color sev = SeverityColor(m.Type, p, themed);

            e.Graphics.FillRectangle(new SolidBrush(window), e.Bounds);
            Rectangle card = new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 3, e.Bounds.Width - 16, e.Bounds.Height - 6);
            using (SolidBrush b = new SolidBrush(selected ? (themed ? p.Selection : SystemColors.Highlight) : surface))
                e.Graphics.FillRectangle(b, card);
            using (SolidBrush b = new SolidBrush(sev))
                e.Graphics.FillRectangle(b, new Rectangle(card.X, card.Y, BarWidth, card.Height));
            int left = card.X + BarWidth + 10;
            int small = TextRenderer.MeasureText("Ag", Font).Height;
            using (Font bold = new Font(Font, FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, SeverityName(m.Type), bold, new Point(left, card.Y + 6), sev, TextFormatFlags.NoPadding);
            Rectangle textRect = new Rectangle(left, card.Y + 8 + small, TextWidth, card.Height - small - 12);
            TextRenderer.DrawText(e.Graphics, m.Text ?? "", Font, textRect, selected && !themed ? SystemColors.HighlightText : text, TextFlags);
        }

        public MessageType FilterLevel { get; set; }

        private void filterBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            FilterLevel = (MessageType)filterBox.SelectedIndex;
            Ares.Settings.Settings.Instance.MessageFilterLevel = filterBox.SelectedIndex;
            RefillGrid();
        }

        private void clearButton_Click(object sender, EventArgs e)
        {
            m_List.Items.Clear();
            Messages.Instance.Clear();
        }
    }
}
