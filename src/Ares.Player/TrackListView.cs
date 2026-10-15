/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The list of the tracks of a playlist, with columns (like the list of Soundpad).
*/
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Ares.Player
{
    internal class TrackListView : ListView
    {
        public TrackListView()
        {
            View = System.Windows.Forms.View.Details;
            FullRowSelect = true;
            HideSelection = false;
            MultiSelect = false;
            AllowColumnReorder = true;
            LabelEdit = false;
            AllowDrop = true;
            DoubleBuffered = true;
            ShowItemToolTips = true;
        }

        public int SelectedIndex
        {
            get { return SelectedIndices.Count > 0 ? SelectedIndices[0] : -1; }
            set
            {
                if (value < 0 || value >= Items.Count)
                {
                    SelectedIndices.Clear();
                    return;
                }
                ListViewItem item = Items[value];
                item.Selected = true;
                item.Focused = true;
                item.EnsureVisible();
            }
        }

        public int IndexFromPoint(Point p)
        {
            ListViewHitTestInfo info = HitTest(p);
            return info != null && info.Item != null ? info.Item.Index : -1;
        }
    }
}
