using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Ares.Player
{
    // Stream Deck: a small server which only answers on this computer (localhost) and is protected by a key.
    // The Apolo plugin of the Stream Deck software talks to it (play / pause, volume, modes, elements...).
    public partial class Player
    {
        private RemoteServer m_Deck;
        private const int DeckFirstPort = 8767;

        private void InitDeck()
        {
            try
            {
                m_Deck = new RemoteServer();
                m_Deck.Execute = RemoteExecute;
                m_Deck.Project = BuildProjectJson;
                if (Ares.CommonGUI.Preferences.GetBool("DeckEnabled", true))
                    StartDeck();
                ToolStripMenuItem item = new ToolStripMenuItem(T("Stream Deck...", "Stream Deck..."));
                item.Name = "deckMenuItem";
                item.Click += (s, e) => ShowDeckDialog();
                extrasToolStripMenuItem.DropDownItems.Insert(3, item);
                FormClosed += (s, e) =>
                {
                    try { m_Deck.Stop(); } catch (Exception) { }
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private string DeckKey()
        {
            string key = Ares.CommonGUI.Preferences.Get("DeckKey", "");
            if (key.Length < 8)
            {
                key = RandomPassword() + RandomPassword();
                Ares.CommonGUI.Preferences.Set("DeckKey", key);
            }
            return key;
        }

        private bool StartDeck()
        {
            return m_Deck.Start(DeckFirstPort, DeckKey());
        }

        private class FolderInfo
        {
            public int Index;      // index of the mode in the project
            public int Parent;     // index of the parent folder, -1 for the top level
            public int Depth;
            public string Path;    // "Parent > Child"
            public string Title;
        }

        // all folders in the order of the tree (parents before their subfolders)
        private List<FolderInfo> FolderRows()
        {
            List<FolderInfo> rows = new List<FolderInfo>();
            if (m_Project == null)
                return rows;
            HashSet<int> seen = new HashSet<int>();
            Action<ModeNode, int, string, int> walk = null;
            walk = (node, parent, parentPath, depth) =>
            {
                int index = ModeIndexByTitle(node.Title);
                string path = parentPath.Length == 0 ? node.Title : parentPath + " \u203A " + node.Title;
                int next = parent;
                if (index >= 0 && seen.Add(index))
                {
                    rows.Add(new FolderInfo { Index = index, Parent = parent, Depth = depth, Path = path, Title = node.Title });
                    next = index;
                }
                foreach (ModeNode child in node.Children)
                    walk(child, next, index >= 0 ? path : parentPath, depth + (index >= 0 ? 1 : 0));
            };
            if (m_ModeTree != null)
            {
                foreach (ModeNode top in m_ModeTree.Root.Children)
                    walk(top, -1, "", 0);
            }
            // folders which are not in the tree (should not happen) go to the top level
            IList<Ares.Data.IMode> modes = m_Project.GetModes();
            for (int i = 0; i < modes.Count; i++)
            {
                if (seen.Add(i))
                    rows.Add(new FolderInfo { Index = i, Parent = -1, Depth = 0, Path = modes[i].Title, Title = modes[i].Title });
            }
            return rows;
        }

        // modes and their visible elements, for the element / mode pickers of the plugin
        private string BuildProjectJson()
        {
            string result = "[]";
            try
            {
                Invoke(new MethodInvoker(() =>
                {
                    StringBuilder json = new StringBuilder("[");
                    if (m_Project != null)
                    {
                        bool firstMode = true;
                        IList<Ares.Data.IMode> projectModes = m_Project.GetModes();
                        foreach (FolderInfo folder in FolderRows())
                        {
                            Ares.Data.IMode mode = projectModes[folder.Index];
                            if (!firstMode)
                                json.Append(",");
                            firstMode = false;
                            json.Append("{\"title\":").Append(JsonString(mode.Title));
                            json.Append(",\"path\":").Append(JsonString(folder.Path)).Append(",\"depth\":").Append(folder.Depth);
                            json.Append(",\"elements\":[");
                            bool first = true;
                            foreach (Ares.Data.IModeElement me in mode.GetElements())
                            {
                                if (!me.IsVisibleInPlayer)
                                    continue;
                                if (!first)
                                    json.Append(",");
                                first = false;
                                json.Append("{\"id\":").Append(me.Id).Append(",\"title\":").Append(JsonString(me.Title)).Append("}");
                            }
                            json.Append("]}");
                        }
                    }
                    json.Append("]");
                    result = json.ToString();
                }));
            }
            catch (Exception)
            {
            }
            return result;
        }

        private static string DeckPluginFile
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StreamDeck", "Apolo.streamDeckPlugin"); }
        }

        private void ShowDeckDialog()
        {
            if (m_Deck == null)
                return;
            using (Form form = new Form())
            {
                form.Text = "Stream Deck";
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ShowInTaskbar = false;
                form.ClientSize = new Size(640, 470);
                form.Icon = Ares.CommonGUI.ApoloBrand.GetIcon(32, Ares.CommonGUI.ApoloState.Playing, 0);

                Label intro = new Label();
                intro.SetBounds(16, 12, 608, 64);
                intro.Text = T(
                    "Controle o Apolo com botões e dials do Stream Deck: tocar/pausar, parar, anterior/próxima, repetir, volumes, trocar de pasta e tocar playlists. O Apolo abre uma porta que só responde neste computador e só aceita a chave abaixo.",
                    "Control Apolo with Stream Deck keys and dials: play/pause, stop, previous/next, repeat, volumes, switching folders and playing playlists. Apolo opens a port which only answers on this computer and only accepts the key below.");

                CheckBox enabled = new CheckBox();
                enabled.SetBounds(16, 86, 400, 26);
                enabled.Text = T("Ativar o acesso do Stream Deck", "Enable Stream Deck access");
                enabled.Checked = m_Deck.Running;
                Label status = new Label();
                status.SetBounds(16, 114, 608, 24);

                Label portLabel = new Label();
                portLabel.SetBounds(16, 148, 80, 26);
                portLabel.Text = T("Porta:", "Port:");
                portLabel.TextAlign = ContentAlignment.MiddleLeft;
                TextBox port = new TextBox();
                port.SetBounds(100, 148, 100, 26);
                port.ReadOnly = true;
                Label keyLabel = new Label();
                keyLabel.SetBounds(16, 184, 80, 26);
                keyLabel.Text = T("Chave:", "Key:");
                keyLabel.TextAlign = ContentAlignment.MiddleLeft;
                TextBox key = new TextBox();
                key.SetBounds(100, 184, 300, 26);
                key.ReadOnly = true;
                key.UseSystemPasswordChar = true;
                key.Text = DeckKey();
                CheckBox show = new CheckBox();
                show.SetBounds(410, 184, 90, 26);
                show.Text = T("Mostrar", "Show");
                show.CheckedChanged += (s, e) => key.UseSystemPasswordChar = !show.Checked;
                Button copy = new Button();
                copy.SetBounds(504, 182, 120, 30);
                copy.Text = T("Copiar chave", "Copy key");
                copy.Click += (s, e) =>
                {
                    try { Clipboard.SetText(key.Text); } catch (Exception) { }
                };
                Button renew = new Button();
                renew.SetBounds(100, 222, 200, 30);
                renew.Text = T("Gerar nova chave", "New key");

                Label steps = new Label();
                steps.SetBounds(16, 268, 608, 120);
                steps.ForeColor = Color.Gray;
                steps.Text = T(
                    "Como usar:\r\n1. Clique em \"Instalar o plugin\" (o programa do Stream Deck abre e pede confirmação).\r\n2. No Stream Deck, procure o grupo \"Apolo\" e arraste as ações para os botões.\r\n3. Em qualquer ação, em \"Conexão com o Apolo\", cole a porta e a chave acima e clique em Salvar. Vale para todas as ações.",
                    "How to use:\r\n1. Click \"Install the plugin\" (the Stream Deck software opens and asks to confirm).\r\n2. In Stream Deck find the \"Apolo\" group and drag the actions onto keys.\r\n3. In any action, under \"Connection to Apolo\", paste the port and the key above and click Save. It applies to all actions.");

                Button install = new Button();
                install.SetBounds(16, 398, 200, 38);
                install.Text = T("Instalar o plugin", "Install the plugin");
                install.Click += (s, e) =>
                {
                    string file = DeckPluginFile;
                    if (!File.Exists(file))
                    {
                        MessageBox.Show(form, T("O arquivo do plugin não está na pasta do Apolo (StreamDeck\\Apolo.streamDeckPlugin). Ele vem no pacote gerado pelo build.yml atualizado.",
                            "The plugin file is not in the Apolo folder (StreamDeck\\Apolo.streamDeckPlugin). It comes in the package made by the updated build.yml."),
                            StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    try { System.Diagnostics.Process.Start(file); }
                    catch (Exception ex)
                    {
                        MessageBox.Show(form, T("Não consegui abrir o plugin. O programa do Stream Deck está instalado?\n\n", "Could not open the plugin. Is the Stream Deck software installed?\n\n") + ex.Message,
                            StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                };
                Button close = new Button();
                close.Text = T("Fechar", "Close");
                close.DialogResult = DialogResult.OK;
                close.SetBounds(640 - 16 - 100, 470 - 12 - 34, 100, 34);

                Action refresh = () =>
                {
                    port.Text = m_Deck.Running ? m_Deck.Port.ToString() : "";
                    status.Text = m_Deck.Running ? T("Ativo e aguardando o plugin.", "Active and waiting for the plugin.")
                        : (enabled.Checked ? T("Não consegui iniciar: ", "Could not start: ") + m_Deck.LastError : T("Desativado.", "Off."));
                    renew.Enabled = true;
                };
                enabled.CheckedChanged += (s, e) =>
                {
                    Ares.CommonGUI.Preferences.SetBool("DeckEnabled", enabled.Checked);
                    if (enabled.Checked)
                        StartDeck();
                    else
                        m_Deck.Stop();
                    refresh();
                };
                renew.Click += (s, e) =>
                {
                    Ares.CommonGUI.Preferences.Set("DeckKey", "");
                    key.Text = DeckKey();
                    if (m_Deck.Running)
                        StartDeck();
                    refresh();
                    MessageBox.Show(form, T("Nova chave criada. Atualize a chave no plugin do Stream Deck.", "New key created. Update the key in the Stream Deck plugin."),
                        StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                };

                form.Controls.AddRange(new Control[] { intro, enabled, status, portLabel, port, keyLabel, key, show, copy, renew, steps, install, close });
                form.AcceptButton = close;
                refresh();
                Ares.CommonGUI.ThemeManager.Apply(form);
                form.ShowDialog(this);
                key.Text = "";
            }
        }
    }
}
