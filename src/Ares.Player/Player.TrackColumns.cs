/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Track list with columns: #, title, artist, album, duration, file. Click on a column header to sort (a second click
 reverses, a third click goes back to the order of the playlist), drag the headers to rearrange the columns, drag the rows
 to reorder a sequential playlist. The data comes from the tags of the files (read in the background).
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Ares.Data;

namespace Ares.Player
{
    public partial class Player
    {
        private const int ColNumber = 0;
        private const int ColKind = 1;
        private const int ColTitle = 2;
        private const int ColArtist = 3;
        private const int ColAlbum = 4;
        private const int ColDuration = 5;
        private const int ColPlays = 6;
        private const int ColRights = 7;
        private const int ColFile = 8;

        private static readonly Dictionary<string, Ares.Tags.AudioTagInfo> s_TagCache =
            new Dictionary<string, Ares.Tags.AudioTagInfo>(StringComparer.OrdinalIgnoreCase);

        private int m_TrackSortCol = -1;
        private bool m_TrackSortAsc = true;
        private int m_TrackMetaGeneration;
        private ToolStripMenuItem m_TrackSaveOrderItem;
        private ToolStripMenuItem m_TrackMostPlayedItem;
        private ToolStripMenuItem m_TrackLeastPlayedItem;
        private ListViewItem m_TrackDropItem;

        private static string[] TrackColumnNames()
        {
            return new string[]
            {
                "#",
                T("Tipo", "Kind"),
                T("Título", "Title"),
                T("Artista", "Artist"),
                T("Álbum", "Album"),
                T("Duração", "Duration"),
                T("Tocadas", "Plays"),
                T("Direitos", "Rights"),
                T("Arquivo", "File")
            };
        }

        private void InitTrackColumns()
        {
            string[] names = TrackColumnNames();
            int[] widths = { 44, 46, 250, 150, 150, 70, 70, 110, 240 };
            for (int i = 0; i < names.Length; i++)
            {
                ColumnHeader h = new ColumnHeader();
                h.Text = names[i];
                // never narrower than the header with its sort arrow ("Duraç...", "Tocad..." were cut)
                int needed = TextRenderer.MeasureText(names[i] + "  ▲", m_TrackList.Font).Width + 26;
                h.Width = Math.Max(widths[i], needed);
                if (i == ColNumber || i == ColDuration || i == ColPlays)
                    h.TextAlign = HorizontalAlignment.Right;
                else if (i == ColKind)
                    h.TextAlign = HorizontalAlignment.Center;
                m_TrackList.Columns.Add(h);
            }
            m_TrackList.ColumnClick += (s, e) => SortTracksBy(e.Column);
            m_TrackList.ItemDrag += (s, e) => StartTrackDrag(e.Item as ListViewItem);
            m_TrackList.DragEnter += (s, e) => e.Effect = e.Data.GetDataPresent(typeof(ListViewItem)) ? DragDropEffects.Move
                : (e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None);
            m_TrackList.DragOver += (s, e) => TrackDragOver(e);
            m_TrackList.DragLeave += (s, e) => ClearTrackDropMark();
            m_TrackList.DragDrop += (s, e) => TrackDragDrop(e);
            UpdateTrackHeaders();
        }

        private void UpdateTrackHeaders()
        {
            string[] names = TrackColumnNames();
            for (int i = 0; i < names.Length && i < m_TrackList.Columns.Count; i++)
            {
                string text = names[i];
                if (i == m_TrackSortCol)
                    text += m_TrackSortAsc ? "  ▲" : "  ▼";
                if (m_TrackList.Columns[i].Text != text)
                    m_TrackList.Columns[i].Text = text;
            }
        }

        private void SortTracksBy(int column)
        {
            if (m_TrackSortCol != column)
            {
                m_TrackSortCol = column;
                m_TrackSortAsc = true;
            }
            else if (m_TrackSortAsc)
            {
                m_TrackSortAsc = false;
            }
            else
            {
                m_TrackSortCol = -1; // back to the order of the playlist
                m_TrackSortAsc = true;
            }
            UpdateTrackHeaders();
            FillTrackList();
        }

        private static string FormatDuration(double seconds)
        {
            return TrackText.FormatDuration(seconds);
        }

        private IEnumerable<TrackRow> SortedTrackRows(IEnumerable<TrackRow> rows)
        {
            if (m_TrackSortCol < 0)
                return rows;
            bool asc = m_TrackSortAsc;
            switch (m_TrackSortCol)
            {
                case ColNumber:
                    return asc ? rows.OrderBy(r => r.Original) : rows.OrderByDescending(r => r.Original);
                case ColTitle:
                    return asc ? rows.OrderBy(r => r.File.Title ?? "", StringComparer.CurrentCultureIgnoreCase)
                               : rows.OrderByDescending(r => r.File.Title ?? "", StringComparer.CurrentCultureIgnoreCase);
                case ColArtist:
                    return asc ? rows.OrderBy(r => r.Artist ?? "", StringComparer.CurrentCultureIgnoreCase)
                               : rows.OrderByDescending(r => r.Artist ?? "", StringComparer.CurrentCultureIgnoreCase);
                case ColAlbum:
                    return asc ? rows.OrderBy(r => r.Album ?? "", StringComparer.CurrentCultureIgnoreCase)
                               : rows.OrderByDescending(r => r.Album ?? "", StringComparer.CurrentCultureIgnoreCase);
                case ColDuration:
                    return asc ? rows.OrderBy(r => r.Seconds) : rows.OrderByDescending(r => r.Seconds);
                case ColPlays:
                    return asc ? rows.OrderBy(r => r.Plays) : rows.OrderByDescending(r => r.Plays);
                case ColKind:
                    return asc ? rows.OrderBy(r => (int)r.File.SoundFileType) : rows.OrderByDescending(r => (int)r.File.SoundFileType);
                case ColRights:
                    return asc ? rows.OrderBy(r => RightsCheck.Rank(r.Rights)) : rows.OrderByDescending(r => RightsCheck.Rank(r.Rights));
                default:
                    return asc ? rows.OrderBy(r => r.File.FilePath ?? "", StringComparer.CurrentCultureIgnoreCase)
                               : rows.OrderByDescending(r => r.File.FilePath ?? "", StringComparer.CurrentCultureIgnoreCase);
            }
        }

        // the color of the mark of the rights: green / yellow / red must be readable on the background of the theme (also the light ones)
        private System.Drawing.Color RightsColor(char code)
        {
            Ares.CommonGUI.ThemePalette p = Ares.CommonGUI.ThemeManager.Current;
            bool themed = p != null && !p.IsClassic;
            System.Drawing.Color back = themed ? p.Surface : System.Drawing.SystemColors.Window;
            System.Drawing.Color text = themed ? p.Text : System.Drawing.SystemColors.WindowText;
            return Ares.CommonGUI.ThemeManager.EnsureContrast(RightsCheck.ColorOf(code, text), back, 4.5);
        }

        private void RefreshRightsColors()
        {
            try
            {
                m_TrackList.BeginUpdate();
                foreach (ListViewItem item in m_TrackList.Items)
                {
                    TrackRow row = item.Tag as TrackRow;
                    if (row != null)
                        item.SubItems[ColRights].ForeColor = RightsColor(row.Rights);
                }
                m_TrackList.EndUpdate();
                m_TrackList.Invalidate();
            }
            catch (Exception)
            {
            }
        }

        private ListViewItem MakeTrackItem(TrackRow row)
        {
            ListViewItem item = new ListViewItem(row.Number.ToString());
            item.UseItemStyleForSubItems = false;
            item.SubItems.Add(row.File.SoundFileType == SoundFileType.Music ? "\u266B" : "\u26A1");
            item.SubItems.Add(row.File.Title ?? "");
            item.SubItems.Add(row.Artist ?? "");
            item.SubItems.Add(row.Album ?? "");
            item.SubItems.Add(FormatDuration(row.Seconds));
            item.SubItems.Add(row.Plays.ToString());
            ListViewItem.ListViewSubItem rightsCell = item.SubItems.Add(RightsCheck.Label(row.Rights));
            rightsCell.ForeColor = RightsColor(row.Rights);
            item.ToolTipText = row.RightsWhy.Length > 0 ? row.RightsWhy + "  (" + T("indicativo, não garantia", "indication, not a guarantee") + ")" : "";
            item.SubItems.Add(row.File.FilePath ?? "");
            item.Tag = row;
            return item;
        }

        // ---- tags in the background ----

        private void ApplyCachedTags(TrackRow row)
        {
            Ares.Tags.AudioTagInfo info;
            string full = FullPathOf(row.File);
            lock (s_TagCache)
            {
                if (!s_TagCache.TryGetValue(full, out info))
                    return;
            }
            row.Loaded = true;
            if (row.Rights == '\0')
            {
                char qc;
                string qw;
                RightsCheck.Quick(info, full, out qc, out qw);
                row.Rights = qc;
                row.RightsWhy = qw;
            }
            row.Artist = info.artist;
            row.Album = info.album;
            row.Seconds = info.duration;
        }

        private void StartTrackMetaLoading()
        {
            int generation = ++m_TrackMetaGeneration;
            List<KeyValuePair<TrackRow, string>> todo = new List<KeyValuePair<TrackRow, string>>();
            foreach (TrackRow row in m_TrackRows)
            {
                if (!row.Loaded)
                    todo.Add(new KeyValuePair<TrackRow, string>(row, FullPathOf(row.File)));
            }
            if (todo.Count == 0)
                return;
            ThreadPool.QueueUserWorkItem(state =>
            {
                int done = 0;
                foreach (KeyValuePair<TrackRow, string> pair in todo)
                {
                    if (generation != m_TrackMetaGeneration)
                        return;
                    Ares.Tags.AudioTagInfo info = null;
                    try
                    {
                        if (System.IO.File.Exists(pair.Value))
                            info = Ares.Tags.AudioTags.Read(pair.Value);
                    }
                    catch (Exception)
                    {
                    }
                    if (info == null)
                        info = new Ares.Tags.AudioTagInfo();
                    lock (s_TagCache)
                        s_TagCache[pair.Value] = info;
                    done++;
                    if (done % 12 == 0 || done == todo.Count)
                    {
                        try
                        {
                            BeginInvoke((Action)(() => TrackMetaArrived(generation)));
                        }
                        catch (Exception)
                        {
                            return;
                        }
                    }
                }
            });
        }

        // some tags were read: fills the cells (and sorts again when the sorted column changed)
        private void TrackMetaArrived(int generation)
        {
            if (generation != m_TrackMetaGeneration || m_TrackList == null)
                return;
            foreach (TrackRow row in m_TrackRows)
            {
                if (!row.Loaded)
                    ApplyCachedTags(row);
            }
            if (m_TrackSortCol == ColArtist || m_TrackSortCol == ColAlbum || m_TrackSortCol == ColDuration || m_TrackSortCol == ColRights)
            {
                FillTrackList(false);
                return;
            }
            m_TrackList.BeginUpdate();
            foreach (ListViewItem item in m_TrackList.Items)
            {
                TrackRow row = item.Tag as TrackRow;
                if (row == null || !row.Loaded)
                    continue;
                item.SubItems[ColArtist].Text = row.Artist ?? "";
                item.SubItems[ColAlbum].Text = row.Album ?? "";
                item.SubItems[ColDuration].Text = FormatDuration(row.Seconds);
                item.SubItems[ColRights].Text = RightsCheck.Label(row.Rights);
                item.SubItems[ColRights].ForeColor = RightsColor(row.Rights);
                if (row.RightsWhy.Length > 0)
                    item.ToolTipText = row.RightsWhy;
            }
            m_TrackList.EndUpdate();
        }

        // a music title was counted: updates the cell (the list is sorted again the next time it is filled)
        private void RefreshTrackPlays(int fileId, int plays)
        {
            if (m_TrackList == null)
                return;
            foreach (TrackRow row in m_TrackRows)
            {
                if (row.File.Id != fileId)
                    continue;
                row.Plays = plays;
                // when sorted by plays the list is NOT sorted again right now: otherwise the title which started would
                // jump down the list and the sequence to play would change under the user's hands
                foreach (ListViewItem item in m_TrackList.Items)
                {
                    if (ReferenceEquals(item.Tag, row))
                        item.SubItems[ColPlays].Text = plays.ToString();
                }
                return;
            }
        }

        // sorts a sequential playlist by the number of plays and saves that order
        private void OrderByPlays(bool leastFirst)
        {
            if (!(TrackContainer() is ISequentialContainer) || m_TrackSearch.Text.Trim().Length > 0)
                return;
            m_TrackSortCol = ColPlays;
            m_TrackSortAsc = leastFirst;
            UpdateTrackHeaders();
            FillTrackList(false);
            SaveTrackOrder();
        }

        // result of an earlier check on the internet (kept in a file)
        private void InitRights(TrackRow row)
        {
            char code;
            string why;
            if (RightsCheck.TryGetChecked(FullPathOf(row.File), out code, out why))
            {
                row.Rights = code;
                row.RightsWhy = why;
            }
        }

        // checks the files of the list on the internet (some seconds each); the traffic light is filled in as the answers come
        private void CheckRightsOnline()
        {
            List<KeyValuePair<TrackRow, string>> todo = new List<KeyValuePair<TrackRow, string>>();
            foreach (TrackRow row in m_TrackRows)
            {
                char c;
                string w;
                string path = FullPathOf(row.File);
                if (!RightsCheck.TryGetChecked(path, out c, out w))
                    todo.Add(new KeyValuePair<TrackRow, string>(row, path));
            }
            if (todo.Count == 0)
            {
                ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Info,
                    T("Todas as músicas desta lista já foram verificadas.", "All the tracks of this list were checked already.")));
                return;
            }
            ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Info,
                T("Verificando " + todo.Count + " música(s) na internet. Leva alguns segundos por música; o resultado é só um indicativo.",
                  "Checking " + todo.Count + " track(s) on the internet. It takes some seconds per track; the result is only an indication.")));
            ThreadPool.QueueUserWorkItem(state =>
            {
                foreach (KeyValuePair<TrackRow, string> pair in todo)
                {
                    char code;
                    string why;
                    try
                    {
                        Ares.Tags.AudioTagInfo tag;
                        lock (s_TagCache)
                            s_TagCache.TryGetValue(pair.Value, out tag);
                        RightsCheck.Full(pair.Value, tag, out code, out why);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    if (code == '\0')
                        continue;
                    TrackRow captured = pair.Key;
                    char capturedCode = code;
                    string capturedWhy = why;
                    try
                    {
                        BeginInvoke((Action)(() =>
                        {
                            captured.Rights = capturedCode;
                            captured.RightsWhy = capturedWhy;
                            foreach (ListViewItem item in m_TrackList.Items)
                            {
                                if (ReferenceEquals(item.Tag, captured))
                                {
                                    item.SubItems[ColRights].Text = RightsCheck.Label(capturedCode);
                                    item.SubItems[ColRights].ForeColor = RightsColor(capturedCode);
                                    item.ToolTipText = capturedWhy;
                                }
                            }
                        }));
                    }
                    catch (Exception)
                    {
                        return;
                    }
                }
                try
                {
                    BeginInvoke((Action)(() => ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Info,
                        T("Verificação de direitos autorais concluída.", "Copyright check finished.")))));
                }
                catch (Exception)
                {
                }
            });
        }

        // ---- reorder by dragging ----

        private bool CanReorderTracks()
        {
            return TrackContainer() is ISequentialContainer && m_TrackSortCol < 0 && m_TrackSearch.Text.Trim().Length == 0;
        }

        private void StartTrackDrag(ListViewItem item)
        {
            if (item == null)
                return;
            if (!CanReorderTracks())
            {
                string why = !(TrackContainer() is ISequentialContainer)
                    ? T("A ordem só importa em \"Música — em sequência\". Mude o tipo da playlist para reorganizar.",
                        "The order only matters in a sequential list. Change the playlist type to reorder.")
                    : T("Para arrastar, tire a ordenação (clique no título da coluna até a seta sumir) e limpe a busca.",
                        "To drag, remove the sorting (click the column title until the arrow is gone) and clear the search.");
                ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Info, why));
                return;
            }
            m_TrackList.DoDragDrop(item, DragDropEffects.Move);
        }

        private void ClearTrackDropMark()
        {
            if (m_TrackDropItem != null)
            {
                m_TrackDropItem.BackColor = m_TrackList.BackColor;
                m_TrackDropItem = null;
            }
        }

        private void TrackDragOver(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(ListViewItem)))
            {
                // audio files from outside go into the playlist (handled by the library drop)
                e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
                return;
            }
            e.Effect = DragDropEffects.Move;
            Point p = m_TrackList.PointToClient(new Point(e.X, e.Y));
            int idx = m_TrackList.IndexFromPoint(p);
            ListViewItem over = idx >= 0 ? m_TrackList.Items[idx] : null;
            if (over != m_TrackDropItem)
            {
                ClearTrackDropMark();
                m_TrackDropItem = over;
                if (over != null)
                    over.BackColor = ControlPaint.LightLight(SystemColors.Highlight);
            }
        }

        private void TrackDragDrop(DragEventArgs e)
        {
            ClearTrackDropMark();
            ListViewItem dragged = e.Data.GetData(typeof(ListViewItem)) as ListViewItem;
            Point p = m_TrackList.PointToClient(new Point(e.X, e.Y));
            int idx = m_TrackList.IndexFromPoint(p);
            if (dragged == null || idx < 0 || !CanReorderTracks())
                return;
            TrackRow src = dragged.Tag as TrackRow;
            TrackRow dst = m_TrackList.Items[idx].Tag as TrackRow;
            ISequentialContainer seq = TrackContainer() as ISequentialContainer;
            if (src == null || dst == null || seq == null || src.Index < 0 || dst.Index < 0 || src.Index == dst.Index)
                return;
            int fromIndex = src.Index;
            int delta = dst.Index - src.Index;
            string modeTitle = m_TrackModeTitle;
            string elementTitle = m_TrackElementTitle;
            int target = dst.Index;
            // after the mouse button is released, so that the list is not rebuilt in the middle of the drag
            RunAfterDrag(() =>
            {
                ApplyProjectEdit(() => seq.MoveElements(fromIndex, fromIndex, delta), modeTitle);
                ReloadTrackList(modeTitle, elementTitle, target);
            });
        }

        // writes the sorted order into the (sequential) playlist
        private void SaveTrackOrder()
        {
            ISequentialContainer seq = TrackContainer() as ISequentialContainer;
            if (seq == null || m_TrackSortCol < 0 || m_TrackSearch.Text.Trim().Length > 0)
                return;
            List<TrackRow> desired = new List<TrackRow>(m_TrackShown);
            foreach (TrackRow row in desired)
            {
                if (row.Index < 0)
                    return;
            }
            string modeTitle = m_TrackModeTitle;
            string elementTitle = m_TrackElementTitle;
            ApplyProjectEdit(() =>
            {
                List<int> order = new List<int>();
                for (int i = 0; i < desired.Count; i++)
                    order.Add(i);
                for (int pos = 0; pos < desired.Count; pos++)
                {
                    int want = desired[pos].Index;
                    int cur = order.IndexOf(want);
                    if (cur >= 0 && cur != pos)
                    {
                        seq.MoveElements(cur, cur, pos - cur);
                        order.RemoveAt(cur);
                        order.Insert(pos, want);
                    }
                }
            }, modeTitle);
            m_TrackSortCol = -1;
            m_TrackSortAsc = true;
            UpdateTrackHeaders();
            ReloadTrackList(modeTitle, elementTitle, -1);
            ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Info, T("Ordem gravada na playlist.", "Order saved in the playlist.")));
        }

        // ---- edit title / artist / album ----

        private void EditSelectedTrackInfo()
        {
            int idx = m_TrackList.SelectedIndex;
            if (idx < 0 || idx >= m_TrackShown.Count)
                return;
            TrackRow row = m_TrackShown[idx];
            string full = FullPathOf(row.File);
            string oldTitle = row.File.Title ?? "";
            string artist = row.Artist ?? "";
            string album = row.Album ?? "";
            using (Form dlg = new Form())
            {
                dlg.Text = T("Editar informações da música", "Edit track information");
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.ShowInTaskbar = false;
                dlg.ClientSize = new Size(440, 190);
                TextBox tTitle = AddTrackField(dlg, T("Título (no Apolo)", "Title (in Apolo)"), oldTitle, 14);
                TextBox tArtist = AddTrackField(dlg, T("Artista (nas tags do arquivo)", "Artist (file tags)"), artist, 58);
                TextBox tAlbum = AddTrackField(dlg, T("Álbum (nas tags do arquivo)", "Album (file tags)"), album, 102);
                Button ok = new Button();
                ok.Text = "OK";
                ok.DialogResult = DialogResult.OK;
                ok.SetBounds(dlg.ClientSize.Width - 180, 148, 80, 28);
                Button cancel = new Button();
                cancel.Text = T("Cancelar", "Cancel");
                cancel.DialogResult = DialogResult.Cancel;
                cancel.SetBounds(dlg.ClientSize.Width - 94, 148, 80, 28);
                dlg.Controls.Add(ok);
                dlg.Controls.Add(cancel);
                dlg.AcceptButton = ok;
                dlg.CancelButton = cancel;
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                string newTitle = tTitle.Text.Trim();
                bool tagsChanged = tArtist.Text.Trim() != artist || tAlbum.Text.Trim() != album;
                if (tagsChanged)
                {
                    if (!Ares.Tags.AudioTags.Write(full, "", tArtist.Text.Trim(), tAlbum.Text.Trim()))
                    {
                        MessageBox.Show(this, T("Não foi possível gravar as tags do arquivo (ele está tocando, em uso ou é somente leitura?).",
                            "The tags of the file could not be written (is it playing, in use or read-only?)."),
                            StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        lock (s_TagCache)
                            s_TagCache.Remove(full);
                        row.Loaded = false;
                    }
                }
                if (newTitle.Length > 0 && newTitle != oldTitle)
                {
                    IFileElement file = row.File;
                    string modeTitle = m_TrackModeTitle;
                    string elementTitle = m_TrackElementTitle;
                    ApplyProjectEdit(() => { file.Title = newTitle; }, modeTitle);
                    ReloadTrackList(modeTitle, elementTitle, -1);
                }
                else
                {
                    FillTrackList();
                }
            }
        }

        private static TextBox AddTrackField(Form dlg, string label, string value, int top)
        {
            Label l = new Label();
            l.Text = label;
            l.AutoSize = true;
            l.Location = new Point(14, top);
            TextBox t = new TextBox();
            t.Text = value;
            t.SetBounds(14, top + 18, dlg.ClientSize.Width - 28, 22);
            dlg.Controls.Add(l);
            dlg.Controls.Add(t);
            return t;
        }
    }
}
