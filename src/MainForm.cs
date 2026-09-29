using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Media;   // WPF MediaPlayer：用 Windows 內建的媒體引擎，變速時會保持音高

namespace LangPlayer
{
    class MainForm : Form
    {
        static readonly string[] AudioExt = { ".mp3", ".m4a", ".wav", ".wma", ".aac", ".flac" };
        const double MinSpeed = 0.5, MaxSpeed = 2.0;

        readonly Settings settings = Settings.Load();
        readonly MediaPlayer player = new MediaPlayer();
        readonly List<string> playlist = new List<string>();
        int current = -1;
        bool playing, seeking;

        Label nowLabel, timeLabel, speedLabel, hintLabel;
        ClickTrackBar seekBar, volumeBar;
        Button playButton, backButton, fwdButton, slowButton;
        double speedBeforeSlow = 1.0;   // 按 0.5x 之前的速度，再按一次回到這裡
        ComboBox loopBox;
        NumericUpDown skipBox;
        ListBox listBox;
        readonly Timer timer = new Timer { Interval = 200 };

        public MainForm()
        {
            Text = "語言聽力播放器";
            // 以 96 DPI 為基準依螢幕縮放（搭配 app.manifest 的 dpiAware），高解析度螢幕才不會糊或擠
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new System.Drawing.Font("Microsoft JhengHei UI", 10.5f);
            ClientSize = new Size(620, 560);
            MinimumSize = new Size(560, 460);
            StartPosition = FormStartPosition.CenterScreen;
            AllowDrop = true;
            BuildUi();

            player.MediaOpened += (s, e) => { ApplySpeed(); UpdateTime(); };
            player.MediaEnded += (s, e) => OnEnded();
            player.MediaFailed += (s, e) =>
            {
                playing = false;
                RefreshLabels();
                // 0xC00D11BA：Windows 找不到可用的音訊輸出（耳機／喇叭沒接或被停用）
                string msg = (uint)e.ErrorException.HResult == 0xC00D11BA
                    ? "找不到可以出聲的裝置：請確認喇叭或耳機有接上、Windows 的音效輸出沒有被停用。"
                    : "無法播放：" + e.ErrorException.Message;
                MessageBox.Show(this, msg, Text);
            };
            player.Volume = settings.Volume / 100.0;

            timer.Tick += (s, e) => UpdateTime();
            timer.Start();

            DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            DragDrop += (s, e) => AddPaths((string[])e.Data.GetData(DataFormats.FileDrop), true);
            FormClosing += (s, e) => { SaveState(); settings.Save(); UnregisterGlobal(); player.Close(); };
            Load += (s, e) => RestoreWindow();   // 等 DPI 縮放做完再套，不然大小會被再放大一次
            Shown += (s, e) => RegisterGlobal();
            RestorePlaylist();
            RefreshLabels();
            UpdateTime();
        }

        // ---------- 關掉再開：視窗與清單維持上次的樣子 ----------

        void RestoreWindow()
        {
            var r = settings.Window;
            if (r.Width <= 0 || r.Height <= 0) return;
            // 上次的位置得還看得到（例如拔掉外接螢幕後就不行），否則維持置中
            bool visible = Screen.AllScreens.Any(sc =>
            {
                var i = Rectangle.Intersect(sc.WorkingArea, r);
                return i.Width >= 100 && i.Height >= 50;
            });
            if (!visible) return;
            StartPosition = FormStartPosition.Manual;
            Bounds = r;
            if (settings.Maximized) WindowState = FormWindowState.Maximized;
        }

        /// <summary>載回上次的清單（找不到的檔略過），選回上次那首、停在開頭，按開始才播。</summary>
        void RestorePlaylist()
        {
            playlist.AddRange(Settings.LoadPlaylist().Where(f => File.Exists(f) && IsAudio(f)));
            current = playlist.FindIndex(f => string.Equals(f, settings.LastTrack, StringComparison.OrdinalIgnoreCase));
            if (current < 0 && playlist.Count > 0) current = 0;
            RefreshList();
            if (current >= 0) listBox.SelectedIndex = current;
        }

        void SaveState()
        {
            settings.Maximized = WindowState == FormWindowState.Maximized;
            settings.Window = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            settings.LastTrack = current >= 0 ? playlist[current] : "";
            Settings.SavePlaylist(playlist);
        }

        // ---------- 介面 ----------

        void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12) };
            Controls.Add(root);

            var top = Flow();
            top.Controls.Add(Btn("開啟檔案…", (s, e) => OpenFiles()));
            top.Controls.Add(Btn("開啟資料夾…", (s, e) => OpenFolder()));
            top.Controls.Add(Btn("清空清單", (s, e) => { Stop(); playlist.Clear(); current = -1; RefreshList(); RefreshLabels(); }));
            top.Controls.Add(Btn("快速鍵…", (s, e) => EditHotkeys()));
            root.Controls.Add(top);

            nowLabel = new Label { AutoSize = true, Font = new System.Drawing.Font(Font.FontFamily, 12f, FontStyle.Bold), Margin = new Padding(3, 10, 3, 2), MaximumSize = new Size(580, 0) };
            root.Controls.Add(nowLabel);

            var ctrl = Flow();
            ctrl.Controls.Add(Btn("⏮ 上一首", (s, e) => Do(PlayerAction.Prev)));
            backButton = Btn("⏪ 後退", (s, e) => Do(PlayerAction.Back));
            ctrl.Controls.Add(backButton);
            playButton = Btn("▶ 開始", (s, e) => Do(PlayerAction.PlayPause));
            playButton.Font = new System.Drawing.Font(Font.FontFamily, 12f, FontStyle.Bold);
            playButton.MinimumSize = new Size(110, 40);
            ctrl.Controls.Add(playButton);
            fwdButton = Btn("快進 ⏩", (s, e) => Do(PlayerAction.Forward));
            ctrl.Controls.Add(fwdButton);
            ctrl.Controls.Add(Btn("下一首 ⏭", (s, e) => Do(PlayerAction.Next)));
            root.Controls.Add(ctrl);

            var speed = Flow();
            speed.Controls.Add(Lbl("速度"));
            speed.Controls.Add(Btn("－ 慢", (s, e) => Do(PlayerAction.Slower)));
            speedLabel = new Label { AutoSize = true, Font = new System.Drawing.Font("Consolas", 13f, FontStyle.Bold), Margin = new Padding(8, 6, 8, 3) };
            speed.Controls.Add(speedLabel);
            speed.Controls.Add(Btn("快 ＋", (s, e) => Do(PlayerAction.Faster)));
            speed.Controls.Add(Btn("1.0x", (s, e) => Do(PlayerAction.SpeedReset)));
            slowButton = Btn("0.5x", (s, e) => Do(PlayerAction.SlowToggle));
            speed.Controls.Add(slowButton);
            root.Controls.Add(speed);

            var opts = Flow();
            opts.Controls.Add(Lbl("循環"));
            loopBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
            loopBox.Items.AddRange(new object[] { "不循環", "單曲循環", "清單循環" });
            loopBox.SelectedIndex = (int)settings.Loop;
            loopBox.SelectedIndexChanged += (s, e) => { settings.Loop = (LoopMode)loopBox.SelectedIndex; RefreshLabels(); };
            opts.Controls.Add(loopBox);
            opts.Controls.Add(Lbl("　跳幾秒"));
            skipBox = new NumericUpDown { Minimum = 1, Maximum = 120, Value = settings.SkipSeconds, Width = 60 };
            skipBox.ValueChanged += (s, e) => { settings.SkipSeconds = (int)skipBox.Value; RefreshLabels(); };
            opts.Controls.Add(skipBox);
            opts.Controls.Add(Lbl("　音量"));
            volumeBar = new ClickTrackBar { Minimum = 0, Maximum = 100, Value = settings.Volume, Width = 130, TickStyle = TickStyle.None, TabStop = false };
            volumeBar.ValueChanged += (s, e) => { settings.Volume = volumeBar.Value; player.Volume = settings.Volume / 100.0; };
            opts.Controls.Add(volumeBar);
            root.Controls.Add(opts);

            listBox = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, Margin = new Padding(3, 8, 3, 3) };
            listBox.DoubleClick += (s, e) => { if (listBox.SelectedIndex >= 0) PlayIndex(listBox.SelectedIndex); };
            root.Controls.Add(listBox);
            root.RowStyles.Clear();
            for (int i = 0; i < root.Controls.Count; i++)
                root.RowStyles.Add(new RowStyle(root.Controls[i] == listBox ? SizeType.Percent : SizeType.AutoSize, 100));

            hintLabel = new Label { AutoSize = true, MaximumSize = new Size(590, 0), Margin = new Padding(3, 6, 3, 3), ForeColor = SystemColors.GrayText };
            root.Controls.Add(hintLabel);
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // 播放進度放最下面：時間在左、拉桿佔滿
            var seekRow = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
            seekRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            seekRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            timeLabel = new Label { AutoSize = true, Font = new System.Drawing.Font("Consolas", 12f), Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 8, 3) };
            seekBar = new ClickTrackBar { Dock = DockStyle.Fill, TickStyle = TickStyle.None, Maximum = 1000, Height = 32, TabStop = false };
            seekBar.MouseDown += (s, e) => seeking = true;
            seekBar.Jumped += (s, e) => SeekToBar();   // 點哪跳哪：按下就先跳過去
            seekBar.MouseUp += (s, e) => { SeekToBar(); seeking = false; };
            seekRow.Controls.Add(timeLabel, 0, 0);
            seekRow.Controls.Add(seekBar, 1, 0);
            root.Controls.Add(seekRow);
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // 按鈕不要吃鍵盤焦點，不然 Space 會變成「按下目前焦點的按鈕」
            foreach (Control c in AllControls(root)) if (c is Button) ((Button)c).TabStop = false;
            Theme.Apply(this);
            playButton.BackColor = Theme.Accent;
            playButton.ForeColor = System.Drawing.Color.White;
        }

        static IEnumerable<Control> AllControls(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                yield return c;
                foreach (var cc in AllControls(c)) yield return cc;
            }
        }

        static FlowLayoutPanel Flow()
        {
            return new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true, Margin = new Padding(0, 4, 0, 0) };
        }

        static Button Btn(string text, EventHandler click)
        {
            var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(0, 36) };
            b.Click += click;
            return b;
        }

        static Label Lbl(string text)
        {
            return new Label { Text = text, AutoSize = true, Margin = new Padding(3, 9, 3, 3) };
        }

        void RefreshLabels()
        {
            playButton.Text = playing ? "⏸ 暫停" : "▶ 開始";
            backButton.Text = "⏪ 後退 " + settings.SkipSeconds + " 秒";
            fwdButton.Text = "快進 " + settings.SkipSeconds + " 秒 ⏩";
            speedLabel.Text = settings.Speed.ToString("0.0") + "x";
            slowButton.Text = settings.Speed == 0.5
                ? "還原 " + (speedBeforeSlow == 0.5 ? 1.0 : speedBeforeSlow).ToString("0.0") + "x"
                : "0.5x";
            if (loopBox.SelectedIndex != (int)settings.Loop) loopBox.SelectedIndex = (int)settings.Loop;
            nowLabel.Text = current >= 0 ? Path.GetFileNameWithoutExtension(playlist[current]) : "（把 mp3 拖進來，或按「開啟檔案」）";
            var parts = new List<string>();
            foreach (PlayerAction a in Enum.GetValues(typeof(PlayerAction)))
                if (settings.Hotkeys[a] != Keys.None) parts.Add(Settings.Names[a] + "：" + Settings.KeyText(settings.Hotkeys[a]));
            hintLabel.Text = "快速鍵" + (settings.GlobalHotkeys ? "（有修飾鍵的是全域）" : "") + "　" + string.Join("　", parts);
        }

        void RefreshList()
        {
            listBox.BeginUpdate();
            listBox.Items.Clear();
            for (int i = 0; i < playlist.Count; i++)
                listBox.Items.Add((i == current ? "▶ " : "　 ") + Path.GetFileName(playlist[i]));
            listBox.EndUpdate();
        }

        // ---------- 清單 ----------

        void OpenFiles()
        {
            using (var dlg = new OpenFileDialog { Multiselect = true, Filter = "音訊檔|*.mp3;*.m4a;*.wav;*.wma;*.aac;*.flac|所有檔案|*.*" })
            {
                if (Directory.Exists(settings.LastFolder)) dlg.InitialDirectory = settings.LastFolder;
                if (dlg.ShowDialog(this) == DialogResult.OK) AddPaths(dlg.FileNames, true);
            }
        }

        void OpenFolder()
        {
            using (var dlg = new FolderBrowserDialog { Description = "選一個放 mp3 的資料夾（含子資料夾）" })
            {
                if (Directory.Exists(settings.LastFolder)) dlg.SelectedPath = settings.LastFolder;
                if (dlg.ShowDialog(this) == DialogResult.OK) AddPaths(new[] { dlg.SelectedPath }, true);
            }
        }

        void AddPaths(IEnumerable<string> paths, bool playFirstNew)
        {
            var files = new List<string>();
            foreach (var p in paths)
            {
                if (Directory.Exists(p))
                {
                    files.AddRange(Directory.GetFiles(p, "*.*", SearchOption.AllDirectories)
                        .Where(IsAudio).OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase));
                    settings.LastFolder = p;
                }
                else if (File.Exists(p) && IsAudio(p))
                {
                    files.Add(p);
                    settings.LastFolder = Path.GetDirectoryName(p);
                }
            }
            if (files.Count == 0) return;
            int first = playlist.Count;
            playlist.AddRange(files);
            RefreshList();
            if (playFirstNew && (!playing || current < 0)) PlayIndex(first);
        }

        static bool IsAudio(string f)
        {
            return AudioExt.Contains(Path.GetExtension(f).ToLowerInvariant());
        }

        // ---------- 播放 ----------

        void PlayIndex(int i)
        {
            if (i < 0 || i >= playlist.Count) return;
            current = i;
            player.Open(new Uri(playlist[i]));
            player.Play();
            playing = true;
            ApplySpeed();
            RefreshList();
            RefreshLabels();
        }

        void Stop()
        {
            player.Stop();
            playing = false;
        }

        void ApplySpeed()
        {
            player.SpeedRatio = settings.Speed;
        }

        void OnEnded()
        {
            switch (settings.Loop)
            {
                case LoopMode.One:
                    player.Position = TimeSpan.Zero;
                    player.Play();
                    ApplySpeed();
                    return;
                case LoopMode.All:
                    PlayIndex((current + 1) % playlist.Count);
                    return;
                default:
                    if (current + 1 < playlist.Count) { PlayIndex(current + 1); return; }
                    playing = false;
                    player.Stop();
                    RefreshLabels();
                    return;
            }
        }

        void Do(PlayerAction a)
        {
            switch (a)
            {
                case PlayerAction.PlayPause:
                    if (current < 0) { if (playlist.Count > 0) PlayIndex(0); else OpenFiles(); return; }
                    if (player.Source == null) { PlayIndex(current); return; }   // 剛開啟、選回上次那首但還沒載入
                    if (playing) { player.Pause(); playing = false; }
                    else { player.Play(); ApplySpeed(); playing = true; }
                    break;
                case PlayerAction.Back: Skip(-settings.SkipSeconds); break;
                case PlayerAction.Forward: Skip(settings.SkipSeconds); break;
                case PlayerAction.Slower: SetSpeed(settings.Speed - 0.1); break;
                case PlayerAction.Faster: SetSpeed(settings.Speed + 0.1); break;
                case PlayerAction.SpeedReset: SetSpeed(1.0); break;
                case PlayerAction.SlowToggle:
                    if (settings.Speed == 0.5) SetSpeed(speedBeforeSlow == 0.5 ? 1.0 : speedBeforeSlow);
                    else { speedBeforeSlow = settings.Speed; SetSpeed(0.5); }
                    break;
                case PlayerAction.Prev:
                    // 播超過 3 秒按上一首＝回到這首開頭（跟一般播放器一樣）
                    if (player.Position.TotalSeconds > 3 || current <= 0) player.Position = TimeSpan.Zero;
                    else PlayIndex(current - 1);
                    break;
                case PlayerAction.Next:
                    if (current + 1 < playlist.Count) PlayIndex(current + 1);
                    else if (settings.Loop == LoopMode.All && playlist.Count > 0) PlayIndex(0);
                    break;
                case PlayerAction.CycleLoop:
                    settings.Loop = (LoopMode)(((int)settings.Loop + 1) % 3);
                    break;
            }
            RefreshLabels();
            UpdateTime();
        }

        void Skip(int seconds)
        {
            if (current < 0 || player.Source == null) return;
            var p = player.Position + TimeSpan.FromSeconds(seconds);
            if (p < TimeSpan.Zero) p = TimeSpan.Zero;
            if (player.NaturalDuration.HasTimeSpan && p > player.NaturalDuration.TimeSpan)
                p = player.NaturalDuration.TimeSpan - TimeSpan.FromMilliseconds(200);
            player.Position = p;
        }

        void SetSpeed(double v)
        {
            settings.Speed = Math.Round(Math.Max(MinSpeed, Math.Min(MaxSpeed, v)), 1);
            ApplySpeed();
        }

        void SeekToBar()
        {
            if (current < 0 || !player.NaturalDuration.HasTimeSpan) return;
            player.Position = TimeSpan.FromMilliseconds(player.NaturalDuration.TimeSpan.TotalMilliseconds * seekBar.Value / seekBar.Maximum);
        }

        void UpdateTime()
        {
            if (current < 0 || !player.NaturalDuration.HasTimeSpan)
            {
                timeLabel.Text = "00:00 / 00:00";
                return;
            }
            var pos = player.Position;
            var dur = player.NaturalDuration.TimeSpan;
            timeLabel.Text = Fmt(pos) + " / " + Fmt(dur);
            if (!seeking && dur.TotalMilliseconds > 0)
                seekBar.Value = Math.Min(seekBar.Maximum, (int)(seekBar.Maximum * pos.TotalMilliseconds / dur.TotalMilliseconds));
        }

        static string Fmt(TimeSpan t)
        {
            return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"mm\:ss");
        }

        // ---------- 快速鍵 ----------

        /// <summary>視窗內的快速鍵：在控制項處理之前攔下來（不然 ← → 會去動拉桿）。</summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!(ActiveControl is NumericUpDown) || (keyData & (Keys.Control | Keys.Alt)) != 0)
            {
                foreach (var kv in settings.Hotkeys)
                {
                    if (kv.Value != Keys.None && kv.Value == keyData) { Do(kv.Key); return true; }
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void EditHotkeys()
        {
            using (var dlg = new HotkeyDialog(settings.Hotkeys, settings.GlobalHotkeys))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Result == null) return;
                UnregisterGlobal();
                settings.Hotkeys = dlg.Result;
                settings.GlobalHotkeys = dlg.Global;
                settings.Save();
                RegisterGlobal();
                RefreshLabels();
            }
        }

        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        const int WM_HOTKEY = 0x0312;
        const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_NOREPEAT = 0x4000;
        readonly List<int> registered = new List<int>();

        /// <summary>全域快速鍵：只登記有修飾鍵的組合，單鍵留給視窗內。</summary>
        void RegisterGlobal()
        {
            if (!settings.GlobalHotkeys) return;
            var failed = new List<string>();
            foreach (var kv in settings.Hotkeys)
            {
                Keys k = kv.Value;
                if ((k & (Keys.Control | Keys.Alt | Keys.Shift)) == 0) continue;
                uint mods = MOD_NOREPEAT;
                if ((k & Keys.Control) != 0) mods |= MOD_CONTROL;
                if ((k & Keys.Alt) != 0) mods |= MOD_ALT;
                if ((k & Keys.Shift) != 0) mods |= MOD_SHIFT;
                int id = 1 + (int)kv.Key;
                if (RegisterHotKey(Handle, id, mods, (uint)(k & Keys.KeyCode))) registered.Add(id);
                else failed.Add(Settings.KeyText(k));
            }
            if (failed.Count > 0)
                MessageBox.Show(this, "這些全域快速鍵被別的程式占用了，只在本視窗有效：\n" + string.Join("、", failed), Text);
        }

        void UnregisterGlobal()
        {
            foreach (int id in registered) UnregisterHotKey(Handle, id);
            registered.Clear();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id >= 1 && id <= Enum.GetValues(typeof(PlayerAction)).Length) Do((PlayerAction)(id - 1));
            }
            base.WndProc(ref m);
        }
    }
}
