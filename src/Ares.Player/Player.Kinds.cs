using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Ares.Data;

namespace Ares.Player
{
    // Adding one button (music playlist or sound effect) from the panel, and switching a button between music and sound effect.
    public partial class Player
    {
        private ContextMenuStrip m_AddMenu;

        private void ShowAddMenu(Control anchor, Point at)
        {
            if (m_AddMenu == null)
            {
                m_AddMenu = new ContextMenuStrip();
                m_AddMenu.Items.Add(T("Playlist de música...", "Music playlist..."), null, (s, e) => AddButtonOfKind(false));
                m_AddMenu.Items.Add(T("Efeito sonoro (SFX)...", "Sound effect (SFX)..."), null, (s, e) => AddButtonOfKind(true));
                m_AddMenu.Items.Add(new ToolStripSeparator());
                m_AddMenu.Items.Add(T("Vários arquivos (escolher como organizar)...", "Several files (choose how to organize)..."), null, (s, e) => AddFilesDialog());
                m_AddMenu.Items.Add(T("Pasta do computador (escolher como organizar)...", "Folder of the computer (choose how to organize)..."), null, (s, e) => AddFolderDialog());
            }
            m_AddMenu.Show(anchor, at);
        }

        /// <summary>Asks for the audio files and makes one button of them in the folder which is shown.</summary>
        private void AddButtonOfKind(bool sfx)
        {
            List<string> files;
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Multiselect = true;
                dlg.Title = sfx ? T("Escolher o arquivo do efeito sonoro", "Choose the file of the sound effect")
                    : T("Escolher as músicas da playlist", "Choose the tracks of the playlist");
                dlg.Filter = T("Arquivos de áudio", "Audio files")
                    + "|*.mp3;*.wav;*.ogg;*.opus;*.flac;*.m4a;*.aac;*.wma;*.aif;*.aiff;*.mp2;*.mp1";
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                files = dlg.FileNames.Where(Ares.CommonGUI.DropScan.IsAudioFile).ToList();
            }
            if (files.Count == 0)
                return;
            string suggestion = files.Count == 1 ? Path.GetFileNameWithoutExtension(files[0])
                : Path.GetFileName(Path.GetDirectoryName(files[0]));
            string name = Prompt(this, sfx ? T("Novo efeito sonoro", "New sound effect") : T("Nova playlist", "New playlist"),
                T("Nome do botão:", "Button name:"), suggestion);
            if (name == null)
                return;

            Ares.CommonGUI.DropOptions options = new Ares.CommonGUI.DropOptions
            {
                Sounds = sfx,
                // one sound effect is just the file; several are one button which picks one at random
                OneElement = !(sfx && files.Count == 1),
                Kind = Ares.CommonGUI.PlaylistKind.Random,
                Repeat = !sfx
            };
            Ares.CommonGUI.DroppedGroup group = new Ares.CommonGUI.DroppedGroup { Name = name, Files = files };
            List<IModeElement> elements = Ares.CommonGUI.LibraryHelper.BuildModeElements(group, files, options);
            foreach (IModeElement element in elements)
                element.Title = name;

            if (m_Project == null)
            {
                string libPath = LibraryProjectPath;
                EnsureProjectFile(libPath);
                OpenProject(libPath, false);
            }
            if (m_Project == null)
                return;
            string wanted = OpenFolderTitle() ?? T("Botões", "Buttons");
            ApplyProjectEdit(() =>
            {
                IMode mode = m_Project.GetModes().FirstOrDefault(m => m.Title == wanted);
                if (mode == null)
                    mode = m_Project.AddMode(wanted);
                foreach (IModeElement element in elements)
                    mode.AddElement(element);
            }, wanted);
            AutoBankAfterCreate(new[] { new KeyValuePair<string, string>(wanted, name) }, null);
        }

        private static IFileElement CopyFileAs(IFileElement file, SoundFileType type)
        {
            IFileElement copy = DataModule.ElementFactory.CreateFileElement(file.FilePath, type);
            copy.Effects.Volume = file.Effects.Volume;
            copy.Effects.FadeInTime = file.Effects.FadeInTime;
            copy.Effects.FadeOutTime = file.Effects.FadeOutTime;
            copy.Effects.CrossFading = type == SoundFileType.Music && file.Effects.CrossFading;
            return copy;
        }

        /// <summary>Switches a button between music and sound effect; several tracks as a sound effect play all together, so the user is asked.</summary>
        private void ToggleButtonKind(int elementId)
        {
            if (m_Project == null)
                return;
            IMode owner = OwnerModeOf(elementId);
            IModeElement me = owner != null ? owner.GetElements().FirstOrDefault(x => x.Id == elementId) : null;
            if (me == null)
                return;
            IElement start = me.StartElement;
            int kinds = KindsOf(start, 0);
            IFileElement single = start as IFileElement;
            List<IFileElement> files = null;
            IGeneralElementContainer container = start as IGeneralElementContainer;
            if (single != null)
            {
                files = new List<IFileElement> { single };
            }
            else if (container != null)
            {
                files = new List<IFileElement>();
                foreach (IContainerElement inner in container.GetGeneralElements())
                {
                    IFileElement file = inner.InnerElement as IFileElement;
                    if (file == null)
                    {
                        files = null;
                        break;
                    }
                    files.Add(file);
                }
            }
            if ((kinds != 1 && kinds != 2) || files == null || files.Count == 0)
            {
                MessageBox.Show(this, T("Este botão tem uma estrutura especial (por exemplo, mistura música e efeitos, ou tem vários níveis). "
                    + "Não dá para trocar o tipo automaticamente; use \"Propriedades da playlist\".",
                    "This button has a special structure (for example it mixes music and effects, or has several levels). "
                    + "Its kind cannot be switched automatically; use \"Playlist properties\"."),
                    StringResources.Ares, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool toSfx = kinds == 1;
            string title = me.Title;
            if (toSfx && files.Count > 1)
            {
                if (MessageBox.Show(this, T("A playlist \"" + title + "\" tem " + files.Count + " músicas.\n\n"
                        + "Como efeito sonoro, TODAS vão tocar ao mesmo tempo quando você clicar no botão. "
                        + "Isso serve para montar uma cena misturando vários sons, mas não é o normal para músicas.\n\n"
                        + "Tem certeza de que quer transformar em efeito sonoro?",
                        "The playlist \"" + title + "\" has " + files.Count + " tracks.\n\n"
                        + "As a sound effect, ALL of them play at the same time when you click the button. "
                        + "That is useful to build a scene by mixing several sounds, but it is not normal for music.\n\n"
                        + "Are you sure you want to make it a sound effect?"),
                        StringResources.Ares, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    return;
            }
            else if (!toSfx && files.Count > 1 && container is IElementContainer<IParallelElement>)
            {
                if (MessageBox.Show(this, T("Este efeito sonoro toca " + files.Count + " sons juntos.\n\n"
                        + "Como música, os arquivos passam a tocar um de cada vez (em ordem sorteada).\n\nContinuar?",
                        "This sound effect plays " + files.Count + " sounds together.\n\n"
                        + "As music, the files play one at a time (in random order).\n\nContinue?"),
                        StringResources.Ares, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
            }

            int ownerIndex = ModeIndexByTitle(owner.Title);
            ApplyProjectEdit(() =>
            {
                IMode mode = m_Project.GetModes()[ownerIndex];
                IModeElement target = mode.GetElements().FirstOrDefault(x => x.Id == elementId);
                if (target == null)
                    return;
                IElement old = target.StartElement;
                SoundFileType type = toSfx ? SoundFileType.SoundEffect : SoundFileType.Music;
                IFileElement oldFile = old as IFileElement;
                if (oldFile != null)
                {
                    oldFile.SoundFileType = type;
                    if (toSfx)
                        oldFile.Effects.CrossFading = false;
                    return;
                }
                IGeneralElementContainer oldContainer = (IGeneralElementContainer)old;
                List<IFileElement> copies = new List<IFileElement>();
                foreach (IContainerElement inner in oldContainer.GetGeneralElements())
                    copies.Add(CopyFileAs((IFileElement)inner.InnerElement, type));
                IElement made;
                if (copies.Count == 1)
                {
                    made = copies[0];
                }
                else if (toSfx)
                {
                    IElementContainer<IParallelElement> together = DataModule.ElementFactory.CreateParallelContainer(title);
                    foreach (IFileElement copy in copies)
                        together.AddElement(copy);
                    made = together;
                }
                else if (old is ISequentialContainer)
                {
                    ISequentialBackgroundMusicList list = DataModule.ElementFactory.CreateSequentialBackgroundMusicList(title);
                    foreach (IFileElement copy in copies)
                        list.AddElement(copy);
                    list.RepeatCount = -1;
                    made = list;
                }
                else
                {
                    IRandomBackgroundMusicList list = DataModule.ElementFactory.CreateRandomBackgroundMusicList(title);
                    foreach (IFileElement copy in copies)
                        list.AddElement(copy);
                    list.RepeatCount = -1;
                    made = list;
                }
                target.StartElement = made;
                DataModule.ElementRepository.DeleteElement(old.Id);
            }, null);
        }
    }
}
