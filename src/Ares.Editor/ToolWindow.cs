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
using System.Drawing;
using System.Windows.Forms;
using Ares.CommonGUI;

namespace Ares.Editor
{
    class ToolWindow : WeifenLuo.WinFormsUI.Docking.DockContent
    {
        /// <summary>Only the alerts window can be closed; all other panels are always shown (like in the Player).</summary>
        public virtual bool Closable
        {
            get { return false; }
        }

        public ToolWindow()
        {
            HideOnClose = true;
            // no floating windows: the panels stay inside the Editor
            DockAreas = WeifenLuo.WinFormsUI.Docking.DockAreas.DockLeft | WeifenLuo.WinFormsUI.Docking.DockAreas.DockRight
                | WeifenLuo.WinFormsUI.Docking.DockAreas.DockTop | WeifenLuo.WinFormsUI.Docking.DockAreas.DockBottom;
            if (!Closable)
                CloseButtonVisible = false;
            // a thin border around the panel, like the panels of the Player
            Padding = new Padding(1);
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            ThemePalette p = ThemeManager.Current;
            Color line = p != null && !p.IsClassic ? p.Border : SystemColors.ControlDark;
            using (Pen pen = new Pen(line))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }
}
