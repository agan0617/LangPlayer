using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace LangPlayer
{
    /// <summary>可自訂快速鍵的動作。順序就是設定畫面上的順序。</summary>
    enum PlayerAction { PlayPause, Back, Forward, Slower, Faster, SpeedReset, Prev, Next, CycleLoop }

    enum LoopMode { None, One, All }

    /// <summary>設定存在 exe 旁的 settings.ini（key=value），解壓到哪都能帶著走。</summary>
    class Settings
    {
        public Dictionary<PlayerAction, Keys> Hotkeys = new Dictionary<PlayerAction, Keys>();
        public bool GlobalHotkeys;
        public int SkipSeconds = 5;
        public double Speed = 1.0;
        public LoopMode Loop = LoopMode.None;
        public int Volume = 80;
        public string LastFolder = "";

        public static readonly Dictionary<PlayerAction, string> Names = new Dictionary<PlayerAction, string>
        {
            { PlayerAction.PlayPause, "開始／暫停" },
            { PlayerAction.Back, "後退 N 秒" },
            { PlayerAction.Forward, "快進 N 秒" },
            { PlayerAction.Slower, "放慢 0.1 倍" },
            { PlayerAction.Faster, "加快 0.1 倍" },
            { PlayerAction.SpeedReset, "速度回到 1.0" },
            { PlayerAction.Prev, "上一首" },
            { PlayerAction.Next, "下一首" },
            { PlayerAction.CycleLoop, "切換循環模式" },
        };

        public static Dictionary<PlayerAction, Keys> Defaults()
        {
            return new Dictionary<PlayerAction, Keys>
            {
                { PlayerAction.PlayPause, Keys.Space },
                { PlayerAction.Back, Keys.Left },
                { PlayerAction.Forward, Keys.Right },
                { PlayerAction.Slower, Keys.OemOpenBrackets },
                { PlayerAction.Faster, Keys.OemCloseBrackets },
                { PlayerAction.SpeedReset, Keys.Back },
                { PlayerAction.Prev, Keys.PageUp },
                { PlayerAction.Next, Keys.PageDown },
                { PlayerAction.CycleLoop, Keys.L },
            };
        }

        static string FilePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini"); }
        }

        public static Settings Load()
        {
            var s = new Settings();
            s.Hotkeys = Defaults();
            try
            {
                if (!File.Exists(FilePath)) return s;
                foreach (var line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                    PlayerAction a;
                    if (k.StartsWith("key.") && Enum.TryParse(k.Substring(4), out a))
                        s.Hotkeys[a] = (Keys)int.Parse(v);
                    else if (k == "global") s.GlobalHotkeys = v == "1";
                    else if (k == "skip") s.SkipSeconds = Math.Max(1, int.Parse(v));
                    else if (k == "speed") s.Speed = double.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
                    else if (k == "loop") s.Loop = (LoopMode)int.Parse(v);
                    else if (k == "volume") s.Volume = int.Parse(v);
                    else if (k == "folder") s.LastFolder = v;
                }
            }
            catch { /* 壞掉就用預設值 */ }
            return s;
        }

        public void Save()
        {
            var sb = new StringBuilder();
            foreach (var kv in Hotkeys) sb.AppendLine("key." + kv.Key + "=" + (int)kv.Value);
            sb.AppendLine("global=" + (GlobalHotkeys ? "1" : "0"));
            sb.AppendLine("skip=" + SkipSeconds);
            sb.AppendLine("speed=" + Speed.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            sb.AppendLine("loop=" + (int)Loop);
            sb.AppendLine("volume=" + Volume);
            sb.AppendLine("folder=" + LastFolder);
            try { File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8); }
            catch { /* 放在唯讀位置就算了，下次用預設 */ }
        }

        public static string KeyText(Keys k)
        {
            if (k == Keys.None) return "（未設定）";
            var parts = new List<string>();
            if ((k & Keys.Control) != 0) parts.Add("Ctrl");
            if ((k & Keys.Alt) != 0) parts.Add("Alt");
            if ((k & Keys.Shift) != 0) parts.Add("Shift");
            Keys code = k & Keys.KeyCode;
            string name;
            switch (code)
            {
                case Keys.Space: name = "Space"; break;
                case Keys.Left: name = "←"; break;
                case Keys.Right: name = "→"; break;
                case Keys.Up: name = "↑"; break;
                case Keys.Down: name = "↓"; break;
                case Keys.OemOpenBrackets: name = "["; break;
                case Keys.OemCloseBrackets: name = "]"; break;
                case Keys.Oemcomma: name = ","; break;
                case Keys.OemPeriod: name = "."; break;
                case Keys.OemMinus: name = "-"; break;
                case Keys.Oemplus: name = "="; break;
                case Keys.Back: name = "Backspace"; break;
                case Keys.PageUp: name = "PageUp"; break;
                case Keys.PageDown: name = "PageDown"; break;
                default:
                    name = code.ToString();
                    if (name.Length == 2 && name[0] == 'D' && char.IsDigit(name[1])) name = name.Substring(1);
                    break;
            }
            parts.Add(name);
            return string.Join(" + ", parts);
        }
    }
}
