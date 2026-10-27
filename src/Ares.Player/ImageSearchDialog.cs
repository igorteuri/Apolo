/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Search of free pictures on the internet (for buttons and folders); shared by the Player and the Editor.
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Ares.Player
{
    internal abstract class MediaForm : Form
    {
        protected static string T(string pt, string en)
        {
            return Loc.T(pt, en);
        }

        protected void Ui(Action action)
        {
            if (IsDisposed || !IsHandleCreated)
                return;
            try { BeginInvoke(action); } catch (InvalidOperationException swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
        }

        protected MediaForm(string title)
        {
            Text = title;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(920, 640);
            MinimumSize = new Size(760, 480);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            try { Icon = Ares.CommonGUI.ApoloBrand.GetIcon(32, Ares.CommonGUI.ApoloState.Playing, 0); } catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
        }

        protected static void Place(Control c, int x, int y, int w, int h, AnchorStyles anchor)
        {
            c.SetBounds(x, y, w, h);
            c.Anchor = anchor;
        }

        protected const AnchorStyles TopLR = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        protected const AnchorStyles BottomL = AnchorStyles.Bottom | AnchorStyles.Left;
        protected const AnchorStyles BottomR = AnchorStyles.Bottom | AnchorStyles.Right;
        protected const AnchorStyles BottomLR = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        protected const AnchorStyles All4 = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
    }

    // ---- pictures for the buttons ----

    internal class ImageSearchDialog : MediaForm
    {
        private readonly TextBox m_Query = new TextBox();
        private readonly CheckBox m_Commercial = new CheckBox();
        private readonly Button m_Search = new Button();
        private readonly ListView m_List = new ListView();
        private readonly ImageList m_Images = new ImageList();
        private readonly Label m_Credit = new Label();
        private readonly Button m_Use = new Button();
        private readonly Button m_Cancel = new Button();
        private int m_Generation;

        public string TempFile;
        public MediaHit Chosen;

        public ImageSearchDialog(string title)
            : base(T("Sugerir imagens para o botão", "Suggest pictures for the button"))
        {
            Label l = new Label();
            l.Text = T("Buscar:", "Search:");
            Place(l, 12, 16, 60, 20, AnchorStyles.Top | AnchorStyles.Left);
            Controls.Add(l);
            Place(m_Query, 76, 12, 560, 24, TopLR);
            m_Query.Text = title;
            m_Query.KeyDown += (s, e) =>
            {
                if (e.KeyCode == System.Windows.Forms.Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    RunSearch();
                }
            };
            Controls.Add(m_Query);
            Place(m_Search, 646, 10, 120, 28, AnchorStyles.Top | AnchorStyles.Right);
            m_Search.Text = T("Buscar", "Search");
            m_Search.Click += (s, e) => RunSearch();
            Controls.Add(m_Search);
            Place(m_Commercial, 12, 42, 890, 22, TopLR);
            m_Commercial.Text = T("Só imagens que podem ser usadas em lives e vídeos monetizados (uso comercial)", "Only pictures that may be used in streams and monetized videos (commercial use)");
            m_Commercial.Checked = true;
            Controls.Add(m_Commercial);

            m_Images.ImageSize = new Size(160, 120);
            m_Images.ColorDepth = ColorDepth.Depth32Bit;
            m_List.View = View.LargeIcon;
            m_List.LargeImageList = m_Images;
            m_List.MultiSelect = false;
            m_List.HideSelection = false;
            Place(m_List, 12, 70, 896, 456, All4);
            m_List.SelectedIndexChanged += (s, e) => ShowCredit();
            m_List.DoubleClick += (s, e) => UseSelected();
            Controls.Add(m_List);

            Place(m_Credit, 12, 532, 896, 40, BottomLR);
            m_Credit.Text = T("Dica: se o título estiver em português e vierem poucas imagens, tente em inglês (ex.: \"forest\").",
                "Tip: if few pictures come up, try other words.");
            Controls.Add(m_Credit);

            Place(m_Use, 12, 596, 170, 30, BottomL);
            m_Use.Text = T("Usar esta imagem", "Use this picture");
            m_Use.Enabled = false;
            m_Use.Click += (s, e) => UseSelected();
            Controls.Add(m_Use);
            Place(m_Cancel, 808, 596, 100, 30, BottomR);
            m_Cancel.Text = T("Cancelar", "Cancel");
            m_Cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(m_Cancel);
            CancelButton = m_Cancel;

            Shown += (s, e) => RunSearch();
        }

        private void ShowCredit()
        {
            MediaHit h = SelectedHit();
            m_Use.Enabled = h != null;
            if (h == null)
                return;
            m_Credit.Text = h.Credit + "  [" + h.Source + "]";
        }

        private MediaHit SelectedHit()
        {
            if (m_List.SelectedItems.Count == 0)
                return null;
            return m_List.SelectedItems[0].Tag as MediaHit;
        }

        private static Bitmap Fit(byte[] data, int w, int h)
        {
            using (MemoryStream ms = new MemoryStream(data))
            using (Image img = Image.FromStream(ms))
            {
                Bitmap bmp = new Bitmap(w, h);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    float scale = Math.Min((float)w / img.Width, (float)h / img.Height);
                    int dw = Math.Max(1, (int)(img.Width * scale));
                    int dh = Math.Max(1, (int)(img.Height * scale));
                    g.DrawImage(img, (w - dw) / 2, (h - dh) / 2, dw, dh);
                }
                return bmp;
            }
        }

        private void RunSearch()
        {
            string query = m_Query.Text.Trim();
            if (query.Length == 0)
                return;
            int gen = ++m_Generation;
            bool commercial = m_Commercial.Checked;
            m_List.Items.Clear();
            m_Images.Images.Clear();
            m_Use.Enabled = false;
            m_Credit.Text = T("Buscando...", "Searching...");
            m_Search.Enabled = false;
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                string problems;
                List<MediaHit> hits = OpenMedia.SearchImages(query, commercial, out problems);
                int shown = 0;
                foreach (MediaHit h in hits)
                {
                    if (gen != m_Generation)
                        return;
                    Bitmap bmp = null;
                    try { bmp = Fit(OpenMedia.GetBytes(h.ThumbUrl), 160, 120); }
                    catch (Exception swallowed) { Ares.Settings.ErrorLog.Note(swallowed); }
                    if (bmp == null)
                        continue;
                    shown++;
                    MediaHit hit = h;
                    Bitmap b = bmp;
                    Ui(() =>
                    {
                        if (gen != m_Generation)
                        {
                            b.Dispose();
                            return;
                        }
                        m_Images.Images.Add(b);
                        b.Dispose();
                        ListViewItem item = new ListViewItem(hit.Title.Length > 22 ? hit.Title.Substring(0, 21) + "…" : hit.Title);
                        item.ImageIndex = m_Images.Images.Count - 1;
                        item.Tag = hit;
                        item.ToolTipText = hit.Credit;
                        m_List.Items.Add(item);
                    });
                }
                Ui(() =>
                {
                    if (gen != m_Generation)
                        return;
                    m_Search.Enabled = true;
                    if (shown == 0)
                        m_Credit.Text = problems.Length > 0
                            ? T("Não consegui buscar: ", "Could not search: ") + problems
                            : T("Nenhuma imagem encontrada. Tente outras palavras.", "No picture found. Try other words.");
                    else
                        m_Credit.Text = shown + T(" imagens. Clique numa para ver o autor e a licença; duplo clique para usar.",
                            " pictures. Click one to see the author and licence; double click to use it.");
                });
            });
        }

        private void UseSelected()
        {
            MediaHit h = SelectedHit();
            if (h == null)
                return;
            Cursor old = Cursor;
            Cursor = Cursors.WaitCursor;
            try
            {
                string temp = Path.Combine(Path.GetTempPath(), "apolo-img-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".img");
                OpenMedia.DownloadFile(h.DownloadUrl, temp);
                TempFile = temp;
                Chosen = h;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Apolo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                Cursor = old;
            }
        }
    }
}
