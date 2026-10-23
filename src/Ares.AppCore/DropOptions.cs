using System;

namespace Ares.CommonGUI
{
    /// <summary>How the files of a folder are put together in one element.</summary>
    public enum PlaylistKind
    {
        /// <summary>Music: random order. Sounds: one random sound.</summary>
        Random = 0,
        /// <summary>Music and sounds: in the order of the files.</summary>
        Sequential = 1,
        /// <summary>Sounds only: all files at the same time.</summary>
        Parallel = 2
    }

    /// <summary>Choices made when folders / files are dropped into Apolo (same options as the Editor offers for new elements).</summary>
    public class DropOptions
    {
        /// <summary>true = sound effects ("Sounds"), false = music.</summary>
        public bool Sounds;
        /// <summary>true = all files of a folder in one element (playlist / random sound), false = one element (button) per file.</summary>
        public bool OneElement;
        /// <summary>Kind of playlist when OneElement is set.</summary>
        public PlaylistKind Kind = PlaylistKind.Random;
        /// <summary>Music lists: repeat forever.</summary>
        public bool Repeat = true;
        /// <summary>Volume of the files, in percent.</summary>
        public int Volume = 100;
        /// <summary>Fade in / fade out of each file, in milliseconds.</summary>
        public int FadeInMs;
        public int FadeOutMs;
        /// <summary>Music: smooth transition from one track to the next.</summary>
        public bool CrossFade;
        /// <summary>Where the dropped audio goes (only asked when the drop has more than one sensible answer).</summary>
        public DropLayout Layout = DropLayout.Tree;
    }
}
