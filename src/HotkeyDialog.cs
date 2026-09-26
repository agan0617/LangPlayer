using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace LangPlayer
{
    /// <summary>快速鍵設定：點一格再按下想要的組合鍵。</summary>
    class HotkeyDialog : Form
    {
        public Dictionary<PlayerAction, Keys> Result;
        public bool Global;

        readonly Dictionary<PlayerAction, TextBox> boxes = new Dictionary<PlayerAction, TextBox>();
        readonly Dictionary<PlayerAction, Keys> keys;
        readonly CheckBox globalBox;

        public HotkeyDialog(Dictionary<PlayerAction, Keys> current, bool global)
        {
            keys = new Dictionary<PlayerAction, Keys>(current);
            Text = "設定快速鍵";
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft JhengHei UI", 10f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);

            // 不要 Dock：讓表格自己決定寬度，視窗再跟著表格 AutoSize
            var table = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Location = new Point(12, 12) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var hint = new Label
            {
                Text = "點右邊的格子，再按下想要的按鍵（可加 Ctrl／Alt／Shift）。按 Delete 清除。",
                AutoSize = true, MaximumSize = new Size(460, 0), Margin = new Padding(3, 3, 3, 10)
            };
            table.Controls.Add(hint, 0, 0);
            table.SetColumnSpan(hint, 3);

            int row = 1;
            foreach (PlayerAction a in Enum.GetValues(typeof(PlayerAction)))
            {
                var label = new Label { Text = Settings.Names[a], AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 12, 3) };
                var box = new TextBox { ReadOnly = true, Width = 190, Text = Settings.KeyText(keys[a]), Tag = a, BackColor = SystemColors.Window };
                box.KeyDown += OnBoxKeyDown;
                box.PreviewKeyDown += (s, e) => e.IsInputKey = true;   // 讓方向鍵、Tab 也送得進來
                boxes[a] = box;
                table.Controls.Add(label, 0, row);
                table.Controls.Add(box, 1, row);
                row++;
            }

            globalBox = new CheckBox
            {
                Text = "全域快速鍵（切到其他視窗時也有效）",
                Checked = global, AutoSize = true, Margin = new Padding(3, 12, 3, 3)
            };
            table.Controls.Add(globalBox, 0, row);
            table.SetColumnSpan(globalBox, 3);
            row++;
            var globalHint = new Label
            {
                Text = "全域只套用有 Ctrl／Alt／Shift 的組合鍵（像 Ctrl+Alt+Space）；單一按鍵（Space、←）若也設成全域，別的程式就打不了字，所以只在本視窗有效。",
                AutoSize = true, MaximumSize = new Size(460, 0), ForeColor = SystemColors.GrayText
            };
            table.Controls.Add(globalHint, 0, row);
            table.SetColumnSpan(globalHint, 3);
            row++;

            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0) };
            var ok = new Button { Text = "確定", DialogResult = DialogResult.OK, AutoSize = true };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true };
            var reset = new Button { Text = "恢復預設", AutoSize = true };
            reset.Click += (s, e) =>
            {
                foreach (var kv in Settings.Defaults()) { keys[kv.Key] = kv.Value; boxes[kv.Key].Text = Settings.KeyText(kv.Value); }
            };
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(reset);
            table.Controls.Add(buttons, 0, row);
            table.SetColumnSpan(buttons, 3);

            Controls.Add(table);
            AcceptButton = null;   // Enter 可以被設成快速鍵，不要讓它按下確定
            CancelButton = null;
            ok.Click += (s, e) => { Result = keys; Global = globalBox.Checked; };
            Theme.Apply(this);
        }

        void OnBoxKeyDown(object sender, KeyEventArgs e)
        {
            var box = (TextBox)sender;
            var a = (PlayerAction)box.Tag;
            e.SuppressKeyPress = true;
            e.Handled = true;
            Keys code = e.KeyCode;
            if (code == Keys.ControlKey || code == Keys.ShiftKey || code == Keys.Menu) return;   // 只按了修飾鍵，繼續等
            Keys k = (code == Keys.Delete && e.Modifiers == Keys.None) ? Keys.None : (code | e.Modifiers);
            // 同一組按鍵不能給兩個動作：先從別的動作拿掉
            if (k != Keys.None)
            {
                foreach (PlayerAction other in Enum.GetValues(typeof(PlayerAction)))
                {
                    if (other != a && keys[other] == k) { keys[other] = Keys.None; boxes[other].Text = Settings.KeyText(Keys.None); }
                }
            }
            keys[a] = k;
            box.Text = Settings.KeyText(k);
        }
    }
}
