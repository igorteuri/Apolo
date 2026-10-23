using Avalonia.Media;

namespace Apolo.Desktop
{
    /// <summary>The masks of the line icons used by the window (painted with any color through OpacityMask).</summary>
    public static class Glyphs
    {
        public static readonly IBrush Previous = IconCache.Mask("skip-back");
        public static readonly IBrush Next = IconCache.Mask("skip-forward");
        public static readonly IBrush Stop = IconCache.Mask("square");
        public static readonly IBrush Repeat = IconCache.Mask("repeat");
        public static readonly IBrush Pause = IconCache.Mask("pause");
        public static readonly IBrush Play = IconCache.Mask("play");
        public static readonly IBrush Music = IconCache.Mask("music");
        public static readonly IBrush Effect = IconCache.Mask("zap");
    }
}
