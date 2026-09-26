using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LangPlayer
{
    /// <summary>夜晚模式：WinForms 沒有內建深色主題，逐一幫控制項上色；標題列與捲軸用 Windows 的深色樣式。</summary>
    static class Theme
    {
        public static readonly Color Back = Color.FromArgb(30, 30, 30);
        public static readonly Color Panel = Color.FromArgb(40, 40, 42);
        public static readonly Color Ctl = Color.FromArgb(52, 52, 56);
        public static readonly Color Hover = Color.FromArgb(66, 66, 72);
        public static readonly Color Border = Color.FromArgb(80, 80, 86);
        public static readonly Color Text = Color.FromArgb(230, 230, 230);
        public static readonly Color Dim = Color.FromArgb(150, 150, 150);
        public static readonly Color Accent = Color.FromArgb(58, 123, 213);

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);

        public static void Apply(Form form)
        {
            form.BackColor = Back;
            form.ForeColor = Text;
            if (form.IsHandleCreated) DarkTitleBar(form); else form.HandleCreated += (s, e) => DarkTitleBar(form);
            ApplyChildren(form);
        }

        static void DarkTitleBar(Form form)
        {
            int on = 1;
            // 20＝DWMWA_USE_IMMERSIVE_DARK_MODE（Win11／Win10 20H1+）；舊版是 19
            if (DwmSetWindowAttribute(form.Handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(form.Handle, 19, ref on, 4);
        }

        static void DarkScrollbars(Control c)
        {
            if (c.IsHandleCreated) SetWindowTheme(c.Handle, "DarkMode_Explorer", null);
            else c.HandleCreated += (s, e) => SetWindowTheme(c.Handle, "DarkMode_Explorer", null);
        }

        static void ApplyChildren(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                bool dim = c.ForeColor == SystemColors.GrayText;
                if (c is Button)
                {
                    var b = (Button)c;
                    b.FlatStyle = FlatStyle.Flat;
                    b.FlatAppearance.BorderColor = Border;
                    b.FlatAppearance.MouseOverBackColor = Hover;
                    b.FlatAppearance.MouseDownBackColor = Accent;
                    b.BackColor = Ctl;
                    b.ForeColor = Text;
                    b.UseVisualStyleBackColor = false;
                }
                else if (c is ListBox)
                {
                    var l = (ListBox)c;
                    l.BackColor = Panel;
                    l.ForeColor = Text;
                    l.BorderStyle = BorderStyle.FixedSingle;
                    DarkScrollbars(l);
                }
                else if (c is ComboBox)
                {
                    var cb = (ComboBox)c;
                    cb.FlatStyle = FlatStyle.Flat;
                    cb.BackColor = Ctl;
                    cb.ForeColor = Text;
                    // 下拉選單在視覺樣式下不理 BackColor，自己畫
                    cb.DrawMode = DrawMode.OwnerDrawFixed;
                    cb.DrawItem += (s, e) =>
                    {
                        if (e.Index < 0) return;
                        bool sel = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
                        using (var bg = new SolidBrush(sel ? Accent : Ctl)) e.Graphics.FillRectangle(bg, e.Bounds);
                        TextRenderer.DrawText(e.Graphics, cb.Items[e.Index].ToString(), cb.Font, e.Bounds, Text,
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                    };
                }
                else if (c is NumericUpDown || c is TextBox)
                {
                    c.BackColor = Ctl;
                    c.ForeColor = Text;
                    if (c is TextBox) ((TextBox)c).BorderStyle = BorderStyle.FixedSingle;
                    else ((NumericUpDown)c).BorderStyle = BorderStyle.FixedSingle;
                }
                else if (c is TrackBar)
                {
                    c.BackColor = Back;
                }
                else
                {
                    c.BackColor = Back;
                    c.ForeColor = dim ? Dim : Text;
                }
                if (c.Controls.Count > 0) ApplyChildren(c);
            }
        }
    }
}
