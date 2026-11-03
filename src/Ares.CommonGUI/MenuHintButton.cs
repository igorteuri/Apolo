using System.Windows.Forms;

namespace Ares.CommonGUI
{
    /// <summary>A toolbar button with a small arrow beside the icon: it tells that a right click offers more options.</summary>
    public class MenuHintButton : ToolStripButton
    {
        public MenuHintButton()
        {
            Padding = new Padding(0, 0, 9, 0);
        }
    }
}
