using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Ares.Data;

namespace Ares.Player
{
    // Audio dropped on a playlist button, or on the track list, can be added to that playlist.
    public partial class Player
    {
        // id of the playlist button which receives a drop at the screen point (the open track list counts as one); -1 = none
        private int PlaylistAtScreen(Point screen)
        {
            if (m_Project == null)
                return -1;
            if (m_TrackElementId >= 0 && TrackContainer() != null && m_TrackList.Visible)
            {
                Rectangle list = m_TrackList.RectangleToScreen(m_TrackList.ClientRectangle);
                if (list.Contains(screen))
                    return m_TrackElementId;
            }
            ElementButton button = ButtonAtScreen(screen);
            if (button != null)
            {
                int id = (int)button.Tag;
                IModeElement me = FindTrackModeElement(id);
                if (me != null && me.StartElement is IGeneralElementContainer)
                    return id;
            }
            return -1;
        }

        // asks whether the dropped audio goes into the existing playlist; "No" goes on with the normal drop
        private void OfferAddToPlaylist(string[] paths, int elementId, string fallbackFolder)
        {
            IMode mode;
            IModeElement me = FindTrackModeElement(elementId, out mode);
            if (me == null || mode == null)
            {
                ImportDroppedPaths(paths, fallbackFolder);
                return;
            }
            List<string> files = new List<string>();
            foreach (Ares.CommonGUI.DroppedGroup group in Ares.CommonGUI.LibraryHelper.Collect(paths))
                files.AddRange(group.Files);
            if (files.Count == 0)
            {
                ImportDroppedPaths(paths, fallbackFolder);
                return;
            }
            DialogResult answer = Ares.CommonGUI.MessageDialog.Show(this,
                T("Adicionar " + files.Count + " música(s) à playlist \"" + me.Title + "\"?",
                  "Add " + files.Count + " track(s) to the playlist \"" + me.Title + "\"?")
                + "\n\n" + T("Sim: coloca na playlist que já existe.\nNão: cria botões novos (com as opções de sempre).\nCancelar: não faz nada.",
                             "Yes: puts them in the existing playlist.\nNo: creates new buttons (with the usual options).\nCancel: does nothing."),
                StringResources.Ares, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel)
                return;
            if (answer == DialogResult.No)
            {
                ImportDroppedPaths(paths, fallbackFolder);
                return;
            }
            AddFilesToPlaylist(elementId, files);
        }

        private void AddFilesToPlaylist(int elementId, List<string> files)
        {
            IMode mode;
            IModeElement me = FindTrackModeElement(elementId, out mode);
            if (me == null || mode == null)
                return;
            if (!(me.StartElement is IGeneralElementContainer))
                return;
            string modeTitle = mode.Title;
            string title = me.Title;
            int modeIndex = new List<IMode>(m_Project.GetModes()).IndexOf(mode);
            int index = mode.GetElements().IndexOf(me);
            bool music = me.StartElement is ISequentialBackgroundMusicList || me.StartElement is IRandomBackgroundMusicList;
            SoundFileType type = music ? SoundFileType.Music : SoundFileType.SoundEffect;
            int added = 0;
            ApplyProjectEdit(() =>
            {
                IModeElement current = m_Project.GetModes()[modeIndex].GetElements()[index];
                IGeneralElementContainer container = current.StartElement as IGeneralElementContainer;
                if (container == null)
                    return;
                // what is in the playlist already is not added twice
                HashSet<string> present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (IFileElement old in container.GetFileElements())
                    present.Add(old.FilePath);
                foreach (string file in files)
                {
                    if (!present.Add(file))
                        continue;
                    container.AddGeneralElement(DataModule.ElementFactory.CreateFileElement(file, type));
                    added++;
                }
            }, modeTitle);
            ReloadTrackList(modeTitle, title, -1);
            ShowToast(new Ares.Players.Message(Ares.Players.MessageType.Info, added > 0
                ? T(added + " música(s) adicionada(s) à playlist \"" + title + "\".", added + " track(s) added to the playlist \"" + title + "\".")
                : T("Essas músicas já estão na playlist.", "Those tracks are already in the playlist.")));
        }
    }
}
