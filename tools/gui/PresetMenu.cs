// One preset per encoder (OPTI H.264, OPTI HEVC, OPTI AV1): the menu lists them flat, under their own name.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AuraFlashXR
{
    public static class PresetMenu
    {
        static readonly Color Bg = Color.FromArgb(0x14, 0x1C, 0x2B);

        /// The dropdown of a per-encoder submenu: no image/check margin (WinForms would paint them as a light strip next
        /// to the dark parent menu), dark background and the same dark renderer as the parent.
        public static ToolStripDropDownMenu CreateSubmenu()
        {
            return new ToolStripDropDownMenu
            {
                ShowImageMargin = false,
                ShowCheckMargin = false,
                BackColor = Bg,
                ForeColor = Ui.DarkText,
                Font = Ui.SemiBold,
                Renderer = new Ui.DarkMenuRenderer(),
                DropShadowEnabled = true
            };
        }

        /// A submenu entry (the encoder family) that opens the dark dropdown of CreateSubmenu().
        public static ToolStripMenuItem CreateFamily(string family, Color foreColor)
        {
            return new ToolStripMenuItem(family)
            {
                AutoSize = true,
                Padding = new Padding(14, 6, 18, 6),
                ForeColor = foreColor,
                Font = Ui.SemiBold,
                BackColor = Bg,
                DropDown = CreateSubmenu()
            };
        }

        /// A level entry (LOW/MID/HIGH/OPTI) of a family submenu.
        public static ToolStripMenuItem CreateLevel(string text, bool checkable, bool isChecked, Color foreColor, Action onClick)
        {
            var item = new ToolStripMenuItem(text)
            {
                AutoSize = true,
                Padding = new Padding(14, 6, 18, 6),
                ForeColor = foreColor,
                Font = Ui.SemiBold,
                Checked = isChecked,
                CheckOnClick = false,
                BackColor = Bg
            };
            item.Click += (s, e) => onClick();
            return item;
        }
    }
}
