using System;
using System.IO;
using System.Linq;

namespace Ares.CommonGUI
{
    /// <summary>The sentences of the question "what to do with what was dropped" (shared by the Windows and the Avalonia window).</summary>
    public static class DropTexts
    {
        public static bool IsPt
        {
            get { return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt"; }
        }

        private static string Tx(string pt, string en)
        {
            return IsPt ? pt : en;
        }

        private static string Q(string name)
        {
            return "\u201c" + name + "\u201d";
        }

        public static string Summary(DropScan scan)
        {
            bool pt = IsPt;
            int files = scan.FileCount;
            switch (scan.Shape)
            {
                case DropShape.OneFile:
                    return (pt ? "1 arquivo de áudio: " : "1 audio file: ") + Path.GetFileName(scan.LooseFiles[0]);
                case DropShape.Files:
                    return pt ? files + " arquivos de áudio." : files + " audio files.";
                case DropShape.Folder:
                    return pt ? "Pasta " + Q(scan.Folders[0].Name) + ": " + files + " música(s)."
                              : "Folder " + Q(scan.Folders[0].Name) + ": " + files + " track(s).";
                case DropShape.NestedFolder:
                    int subs = scan.Folders[0].Subfolders.Count;
                    return pt ? "A pasta " + Q(scan.Folders[0].Name) + " tem " + subs + " subpasta(s), com " + files + " música(s) no total."
                              : "The folder " + Q(scan.Folders[0].Name) + " has " + subs + " subfolder(s), with " + files + " track(s) in all.";
                default:
                    int loose = scan.LooseFiles.Count;
                    return pt ? scan.Folders.Count + " pasta(s)" + (loose > 0 ? " e " + loose + " arquivo(s) solto(s)" : "") + ": " + files + " música(s) no total."
                              : scan.Folders.Count + " folder(s)" + (loose > 0 ? " and " + loose + " loose file(s)" : "") + ": " + files + " track(s) in all.";
            }
        }

        public static string DestinationText(string destination)
        {
            if (destination == null)
                return Tx("Vai para pastas novas do projeto.", "It goes into new folders of the project.");
            return Tx("Destino: pasta ", "Destination: folder ") + Q(destination);
        }

        // the text of a choice: a short title and (after \u0001) an explanation which is shown below it
        public static string LayoutText(DropScan scan, DropLayout layout, string destination)
        {
            bool pt = IsPt;
            string single = scan.Folders.Count == 1 ? Q(scan.Folders[0].Name) : null;
            const string Sep = "\u0001";
            switch (layout)
            {
                case DropLayout.Here:
                    if (!scan.HasSubfolders)
                        return pt ? "Colocar na pasta " + Q(destination) : "Put it in the folder " + Q(destination);
                    AudioFolder first = scan.Folders.First(f => !f.IsLeaf);
                    string example = Q(first.Name + " - " + first.Subfolders[0].Name);
                    return pt ? "Tudo na pasta " + Q(destination) + Sep + "Um botão por pasta de músicas, com o nome da subpasta junto (ex.: " + example + ")."
                              : "Everything in the folder " + Q(destination) + Sep + "One button per folder of music, with the name of the subfolder (e.g. " + example + ").";
                case DropLayout.Tree:
                    if (scan.Folders.Count == 0)
                        return pt ? "Criar a pasta " + Q("Músicas soltas") : "Create the folder " + Q("Loose music");
                    if (!scan.HasSubfolders)
                        return single != null
                            ? (pt ? "Criar uma pasta nova " + single : "Create a new folder " + single)
                            : (pt ? "Criar uma pasta nova para cada pasta arrastada" : "Create a new folder for each dropped folder");
                    return pt ? "Criar as mesmas pastas do computador" + Sep + "Cada subpasta vira uma pasta do Apolo, com os seus botões."
                              : "Create the same folders as on the computer" + Sep + "Each subfolder becomes a folder of Apolo, with its buttons.";
                case DropLayout.Compact:
                    return single != null
                        ? (pt ? "Uma pasta só, com um botão por subpasta" + Sep + "Cria a pasta " + single + "; cada subpasta vira um botão dentro dela."
                              : "One folder with one button per subfolder" + Sep + "Creates the folder " + single + "; each subfolder becomes a button inside it.")
                        : (pt ? "Uma pasta para cada pasta arrastada" + Sep + "Cada subpasta vira um botão dentro da pasta dela."
                              : "A folder for each dropped folder" + Sep + "Each subfolder becomes a button inside its folder.");
                default:
                    return pt ? "Um botão só por pasta arrastada" + Sep + "Fica na pasta " + Q(destination) + ", com todas as músicas de dentro (inclusive subpastas)."
                              : "One button per dropped folder" + Sep + "It goes in the folder " + Q(destination) + ", with all the music inside (subfolders too).";
            }
        }
    }
}
