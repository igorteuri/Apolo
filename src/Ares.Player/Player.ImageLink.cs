using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Windows.Forms;
using Ares.Data;

namespace Ares.Player
{
    // Picture of a button from a link of the internet: the pasted text may be messy, the program finds the link.
    public partial class Player
    {
        private const int ImageLinkMaxBytes = 20 * 1024 * 1024;

        private static byte[] DownloadBytes(string url, out string contentType)
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch (Exception) { }
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout = 8000;
            request.ReadWriteTimeout = 8000;
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Apolo";
            request.Accept = "image/*,text/html;q=0.8,*/*;q=0.5";
            request.AllowAutoRedirect = true;
            try { request.Proxy = WebRequest.GetSystemWebProxy(); } catch (Exception) { }
            using (WebResponse response = request.GetResponse())
            {
                contentType = response.ContentType ?? "";
                if (response.ContentLength > ImageLinkMaxBytes)
                    throw new InvalidDataException("too big");
                using (Stream stream = response.GetResponseStream())
                using (MemoryStream buffer = new MemoryStream())
                {
                    byte[] chunk = new byte[16384];
                    int read;
                    while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
                    {
                        buffer.Write(chunk, 0, read);
                        if (buffer.Length > ImageLinkMaxBytes)
                            throw new InvalidDataException("too big");
                    }
                    return buffer.ToArray();
                }
            }
        }

        /// <summary>Tries the addresses found in the text until one gives a picture. Returns null when it worked, otherwise what went wrong.</summary>
        private static string FetchPictureFromText(string text, out byte[] picture)
        {
            picture = null;
            List<string> candidates = ImageUrlParser.Candidates(text);
            if (candidates.Count == 0)
                return T("Não encontrei nenhum link no texto. Cole o endereço da imagem (começa com http ou www).",
                    "I found no link in the text. Paste the address of the picture (it starts with http or www).");
            string problem = null;
            HashSet<string> tried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int attempts = 0;
            foreach (string candidate in candidates)
            {
                List<string> variants = new List<string> { candidate };
                if (!ImageUrlParser.HasImageExtension(candidate))
                {
                    // no ".png" at the end: the site may answer with a page; then the extensions are tried
                    foreach (string ext in ImageUrlParser.GuessExtensions)
                    {
                        int q = candidate.IndexOfAny(new[] { '?', '#' });
                        if (q < 0)
                            variants.Add(candidate + ext);
                    }
                }
                foreach (string url in variants)
                {
                    if (!tried.Add(url) || attempts >= 8)
                        continue;
                    attempts++;
                    try
                    {
                        string contentType;
                        byte[] data = DownloadBytes(url, out contentType);
                        string kind = ImageBytes.Detect(data);
                        if (kind == "html")
                        {
                            // a page, not a picture: the picture which the page announces
                            string page = System.Text.Encoding.UTF8.GetString(data);
                            string inner = ImageUrlParser.FindPageImage(page, url);
                            if (inner != null && tried.Add(inner))
                            {
                                string innerType;
                                byte[] innerData = DownloadBytes(inner, out innerType);
                                string innerKind = ImageBytes.Detect(innerData);
                                if (innerKind != null && innerKind != "html" && innerKind != "svg")
                                {
                                    picture = innerData;
                                    return null;
                                }
                            }
                            problem = T("O link abre uma página da internet, não uma imagem. Abra a imagem, clique com o botão direito nela e escolha \"Copiar endereço da imagem\".",
                                "The link opens a web page, not a picture. Open the picture, right-click it and choose \"Copy image address\".");
                        }
                        else if (kind == "svg")
                        {
                            problem = T("Essa imagem é SVG, um formato que o Apolo ainda não lê. Procure uma versão PNG, JPG ou WebP.",
                                "That picture is SVG, which Apolo cannot read yet. Look for a PNG, JPG or WebP version.");
                        }
                        else if (kind != null)
                        {
                            picture = data;
                            return null;
                        }
                    }
                    catch (InvalidDataException)
                    {
                        problem = T("A imagem é grande demais (mais de 20 MB).", "The picture is too big (more than 20 MB).");
                    }
                    catch (WebException ex)
                    {
                        HttpWebResponse response = ex.Response as HttpWebResponse;
                        if (response != null && (int)response.StatusCode == 403)
                            problem = T("O site não deixa baixar essa imagem (acesso negado). Baixe a imagem no computador e use \"Escolher imagem\".",
                                "The site does not allow downloading that picture (access denied). Save the picture on your computer and use \"Choose picture\".");
                        else if (problem == null)
                            problem = T("Não consegui abrir o link. Confira se ele está certo e se há internet.",
                                "I could not open the link. Check that it is right and that you are online.");
                    }
                    catch (Exception)
                    {
                        if (problem == null)
                            problem = T("Não consegui baixar a imagem desse link.", "I could not download the picture of that link.");
                    }
                }
            }
            return problem ?? T("Não consegui baixar uma imagem desse link.", "I could not download a picture from that link.");
        }

        /// <summary>Right click on a button > picture from a link.</summary>
        private void ChooseButtonImageFromLink()
        {
            IMode mode; int index;
            IModeElement me = MenuElement(out mode, out index);
            if (me == null || string.IsNullOrEmpty(m_CurrentProjectPath))
                return;
            string text = ImageLinkDialog.Ask(this, me.Title);
            if (text == null)
                return;
            byte[] picture;
            string problem;
            Cursor old = Cursor;
            Cursor = Cursors.WaitCursor;
            try { problem = FetchPictureFromText(text, out picture); }
            finally { Cursor = old; }
            if (problem != null)
            {
                MessageBox.Show(this, problem, StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            byte[] readable = Ares.CommonGUI.ImageDecoder.ToReadable(picture);
            if (readable == null)
            {
                MessageBox.Show(this, T("Não consegui ler essa imagem (formato com problema).", "I could not read that picture (damaged format)."),
                    StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string temp = Path.Combine(Path.GetTempPath(), "apolo-link-" + Guid.NewGuid().ToString("N") + ".img");
            try
            {
                File.WriteAllBytes(temp, readable);
                string spec = Ares.CommonGUI.ButtonImageStore.ImportImage(m_CurrentProjectPath, temp);
                Ares.CommonGUI.ButtonImageStore.Set(m_CurrentProjectPath, mode.Title, me.Title, spec);
                UpdateElementsPanel();
            }
            catch (Exception)
            {
                MessageBox.Show(this, T("Não consegui usar essa imagem.", "I could not use that picture."),
                    StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                try { File.Delete(temp); } catch (Exception) { }
            }
        }
    }

    /// <summary>Asks for the link; shows at once which address was understood from what is pasted.</summary>
    internal class ImageLinkDialog : Form
    {
        private readonly TextBox m_Text = new TextBox();
        private readonly Label m_Found = new Label();
        private readonly Button m_Ok = new Button();

        public static string Ask(IWin32Window owner, string buttonTitle)
        {
            using (ImageLinkDialog dlg = new ImageLinkDialog(buttonTitle))
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK)
                    return null;
                return dlg.m_Text.Text;
            }
        }

        private ImageLinkDialog(string buttonTitle)
        {
            Text = Loc.T("Imagem por link", "Picture from a link");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(520, 250);

            Label intro = new Label();
            intro.SetBounds(12, 10, 496, 36);
            intro.Text = Loc.T("Cole aqui o link da imagem para o botão \"" + buttonTitle + "\". Pode colar o texto todo: o Apolo acha o link sozinho, "
                + "mesmo sem \".png\" no final, com espaços sobrando ou no meio de um texto.",
                "Paste the link of the picture for the button \"" + buttonTitle + "\" here. You can paste the whole text: Apolo finds the link by itself, "
                + "even without \".png\" at the end, with extra spaces or inside a text.");
            Controls.Add(intro);

            m_Text.Multiline = true;
            m_Text.ScrollBars = ScrollBars.Vertical;
            m_Text.AcceptsReturn = true;
            m_Text.SetBounds(12, 52, 496, 100);
            m_Text.TextChanged += (s, e) => ShowFound();
            Controls.Add(m_Text);

            m_Found.SetBounds(12, 160, 496, 44);
            Controls.Add(m_Found);

            m_Ok.Text = Loc.T("Usar imagem", "Use picture");
            m_Ok.DialogResult = DialogResult.OK;
            m_Ok.SetBounds(ClientSize.Width - 12 - 110 - 8 - 90, 212, 110, 28);
            Controls.Add(m_Ok);
            Button cancel = new Button();
            cancel.Text = Loc.T("Cancelar", "Cancel");
            cancel.DialogResult = DialogResult.Cancel;
            cancel.SetBounds(ClientSize.Width - 12 - 90, 212, 90, 28);
            Controls.Add(cancel);
            AcceptButton = null;
            CancelButton = cancel;

            // what is on the clipboard is already a link: it starts filled in
            try
            {
                if (Clipboard.ContainsText())
                {
                    string clip = Clipboard.GetText();
                    if (ImageUrlParser.Candidates(clip).Count > 0)
                        m_Text.Text = clip;
                }
            }
            catch (Exception)
            {
            }
            ShowFound();
            Shown += (s, e) => { m_Text.Focus(); m_Text.SelectAll(); };
        }

        private void ShowFound()
        {
            List<string> found = ImageUrlParser.Candidates(m_Text.Text);
            m_Ok.Enabled = found.Count > 0;
            if (m_Text.Text.Trim().Length == 0)
                m_Found.Text = "";
            else if (found.Count == 0)
                m_Found.Text = Loc.T("Ainda não achei um link nesse texto.", "I do not see a link in that text yet.");
            else
                m_Found.Text = Loc.T("Link entendido: ", "Link understood: ") + found[0]
                    + (found.Count > 1 ? Loc.T("  (e mais " + (found.Count - 1) + " possibilidade(s) se esse falhar)", "  (and " + (found.Count - 1) + " other possibility(ies) if it fails)") : "");
        }
    }
}
