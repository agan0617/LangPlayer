using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LangPlayer
{
    /// <summary>
    /// 點哪就跳到哪的拉桿。原生 TrackBar 點在軌道上只會移一個 LargeChange；
    /// 這裡在滑鼠按下時先把值設到游標位置，再交給原生處理，拇指剛好在游標下，就能接著拖。
    /// </summary>
    class ClickTrackBar : TrackBar
    {
        const int WM_LBUTTONDOWN = 0x0201;
        const int TBM_GETTHUMBRECT = 0x0400 + 25;
        const int TBM_GETCHANNELRECT = 0x0400 + 26;

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref RECT lParam);

        /// <summary>使用者點下去跳位置時觸發（播放進度用它來真的 seek）。</summary>
        public event EventHandler Jumped;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_LBUTTONDOWN && Orientation == Orientation.Horizontal)
            {
                int x = (short)(m.LParam.ToInt32() & 0xFFFF);
                RECT channel = new RECT(), thumb = new RECT();
                SendMessage(Handle, TBM_GETCHANNELRECT, IntPtr.Zero, ref channel);
                SendMessage(Handle, TBM_GETTHUMBRECT, IntPtr.Zero, ref thumb);
                bool onThumb = x >= thumb.Left && x <= thumb.Right;
                if (!onThumb)
                {
                    int half = (thumb.Right - thumb.Left) / 2;
                    int left = channel.Left + half, right = channel.Right - half;
                    if (right > left)
                    {
                        double ratio = Math.Max(0, Math.Min(1, (double)(x - left) / (right - left)));
                        Value = Minimum + (int)Math.Round(ratio * (Maximum - Minimum));
                        if (Jumped != null) Jumped(this, EventArgs.Empty);
                    }
                }
            }
            base.WndProc(ref m);
        }
    }
}
