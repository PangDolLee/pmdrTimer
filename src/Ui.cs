using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

static class Theme
{
    public static Color Bg, Card, Card2, Line, Text, Muted, Track;
    public static bool Dark;
    public static Color Accent = Color.FromArgb(255, 107, 107);
    public static Color Accent2 = Color.FromArgb(255, 159, 107);

    static string family, phase = "focus";

    static Theme() { SetDark(false); }

    // 라이트(기본) / 다크 팔레트 전환
    public static void SetDark(bool dark)
    {
        Dark = dark;
        if (dark)
        {
            Bg = Color.FromArgb(13, 15, 20); Card = Color.FromArgb(22, 26, 35); Card2 = Color.FromArgb(32, 37, 50);
            Line = Color.FromArgb(46, 52, 68); Text = Color.FromArgb(236, 239, 246); Muted = Color.FromArgb(140, 149, 168);
            Track = Color.FromArgb(48, 54, 70);
        }
        else
        {
            Bg = Color.FromArgb(240, 242, 248); Card = Color.FromArgb(255, 255, 255); Card2 = Color.FromArgb(242, 244, 250);
            Line = Color.FromArgb(224, 228, 238); Text = Color.FromArgb(24, 28, 40); Muted = Color.FromArgb(110, 118, 138);
            Track = Color.FromArgb(226, 230, 240);
        }
    }

    // 단계별 강조색. 바뀌었으면 true
    public static bool SetPhase(string p)
    {
        if (p == phase) return false;
        phase = p;
        if (p == "short") { Accent = Color.FromArgb(47, 212, 160); Accent2 = Color.FromArgb(94, 224, 230); }
        else if (p == "long") { Accent = Color.FromArgb(124, 140, 255); Accent2 = Color.FromArgb(177, 140, 255); }
        else { Accent = Color.FromArgb(255, 107, 107); Accent2 = Color.FromArgb(255, 159, 107); }
        return true;
    }

    public static string Family
    {
        get
        {
            if (family == null)
            {
                string[] want = { "Pretendard Variable", "Pretendard", "Malgun Gothic", "Segoe UI", "NanumGothic", "Noto Sans CJK KR" };
                family = FontFamily.GenericSansSerif.Name;
                foreach (string w in want)
                {
                    bool found = false;
                    foreach (FontFamily ff in FontFamily.Families)
                        if (string.Equals(ff.Name, w, StringComparison.OrdinalIgnoreCase)) { found = true; break; }
                    if (found) { family = w; break; }
                }
            }
            return family;
        }
    }

    public static Font Fnt(float px, bool bold)
    {
        return FntStyle(px, bold ? FontStyle.Bold : FontStyle.Regular);
    }

    // 내장 Pretendard를 쓰고, 불러오지 못했을 때만 시스템 글꼴로 대체한다
    public static Font FntStyle(float px, FontStyle style)
    {
        px = Math.Max(1f, (float)Math.Round(px));   // 정수 픽셀 크기라야 획이 번지지 않는다
        if (FontLoader.Family != null) return new Font(FontLoader.Family, px, style, GraphicsUnit.Pixel);
        return new Font(Family, px, style, GraphicsUnit.Pixel);
    }

    public static Color Mix(Color a, Color b, double t)
    {
        return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }
}

static class Gfx
{
    public static GraphicsPath Round(RectangleF r, float rad)
    {
        GraphicsPath p = new GraphicsPath();
        float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
        if (d < 1f) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void FillRound(Graphics g, Brush b, RectangleF r, float rad)
    {
        using (GraphicsPath p = Round(r, rad)) g.FillPath(b, p);
    }

    public static void DrawRound(Graphics g, Pen pen, RectangleF r, float rad)
    {
        using (GraphicsPath p = Round(r, rad)) g.DrawPath(pen, p);
    }

    public static LinearGradientBrush AccentBrush(RectangleF r)
    {
        if (r.Width < 1) r.Width = 1;
        if (r.Height < 1) r.Height = 1;
        return new LinearGradientBrush(r, Theme.Accent, Theme.Accent2, 45f);
    }

    public static void Label(Graphics g, string s, Font f, Color c, RectangleF r, StringAlignment h, StringAlignment v)
    {
        // 글자가 픽셀 사이에 걸치면 흐려지므로 상자 위치를 정수 픽셀에 맞춘다
        r = new RectangleF((float)Math.Round(r.X), (float)Math.Round(r.Y), (float)Math.Round(r.Width), (float)Math.Round(r.Height));
        using (SolidBrush b = new SolidBrush(c))
        using (StringFormat sf = new StringFormat(StringFormatFlags.NoWrap))
        {
            sf.Alignment = h; sf.LineAlignment = v; sf.Trimming = StringTrimming.EllipsisCharacter;
            g.DrawString(s, f, b, r, sf);
        }
    }

    public static void Quality(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;
        // ClearType + 격자 맞춤: 불투명 배경 위에서 획이 가장 선명하게 그려진다.
        // (격자 맞춤 없는 AntiAlias는 작은 글자가 번져 보여 사용하지 않는다)
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    }
}

// 직접 그리는 컨트롤의 공통 베이스 (깜박임 없는 더블 버퍼)
class Surface : Control
{
    public Surface()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Theme.Card;
    }
}

class Card : Surface
{
    public float Radius = 22;

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Gfx.Quality(g);
        g.Clear(Theme.Bg);
        RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (SolidBrush b = new SolidBrush(Theme.Card)) Gfx.FillRound(g, b, r, Radius);
        using (Pen p = new Pen(Theme.Line)) Gfx.DrawRound(g, p, r, Radius);
    }
}

class RoundButton : Surface
{
    public bool Primary;
    bool hover, down;

    public RoundButton() { Cursor = Cursors.Hand; }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Gfx.Quality(g);
        g.Clear(BackColor);
        RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        float rad = Height * 0.3f;
        if (Primary)
        {
            using (LinearGradientBrush b = Gfx.AccentBrush(r)) Gfx.FillRound(g, b, r, rad);
            if (hover) using (SolidBrush w = new SolidBrush(Color.FromArgb(down ? 40 : 28, Color.White))) Gfx.FillRound(g, w, r, rad);
        }
        else
        {
            using (SolidBrush b = new SolidBrush(hover ? Theme.Mix(Theme.Card2, Theme.Accent, 0.14) : Theme.Card2)) Gfx.FillRound(g, b, r, rad);
            using (Pen p = new Pen(hover ? Theme.Accent : Theme.Line)) Gfx.DrawRound(g, p, r, rad);
        }
        using (Font f = Theme.Fnt(Height * 0.4f, true))
            Gfx.Label(g, Text, f, Primary ? Color.White : Theme.Text, new RectangleF(0, 0, Width, Height - 1), StringAlignment.Center, StringAlignment.Center);
    }
}

class Switch : Surface
{
    bool on, hover;
    public event EventHandler Changed;

    public Switch() { Cursor = Cursors.Hand; }

    public bool Checked
    {
        get { return on; }
        set { if (on != value) { on = value; Invalidate(); } }
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseClick(MouseEventArgs e)
    {
        on = !on; Invalidate();
        if (Changed != null) Changed(this, EventArgs.Empty);
        base.OnMouseClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Gfx.Quality(g);
        g.Clear(BackColor);
        RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        if (on) using (LinearGradientBrush b = Gfx.AccentBrush(r)) Gfx.FillRound(g, b, r, Height);
        else using (SolidBrush b = new SolidBrush(hover ? Theme.Mix(Theme.Track, Theme.Muted, 0.25) : Theme.Track)) Gfx.FillRound(g, b, r, Height);
        float d = Height - 6, x = on ? Width - 3 - d : 3;
        using (SolidBrush w = new SolidBrush(Color.White)) g.FillEllipse(w, x, 3, d, d);
    }
}

class Slider : Surface
{
    int val = 50;
    bool drag;
    public event EventHandler Changed;

    public Slider() { Cursor = Cursors.Hand; }

    public int Value
    {
        get { return val; }
        set { value = Math.Max(0, Math.Min(100, value)); if (val != value) { val = value; Invalidate(); } }
    }

    void Pick(int x)
    {
        float pad = Height / 2f;
        int v = (int)Math.Round((x - pad) / Math.Max(1f, Width - 2 * pad) * 100);
        v = Math.Max(0, Math.Min(100, v));
        if (v != val)
        {
            val = v; Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e) { drag = true; Pick(e.X); base.OnMouseDown(e); }
    protected override void OnMouseMove(MouseEventArgs e) { if (drag) Pick(e.X); base.OnMouseMove(e); }
    protected override void OnMouseUp(MouseEventArgs e) { drag = false; base.OnMouseUp(e); }
    protected override void OnMouseWheel(MouseEventArgs e) { Value = val + (e.Delta > 0 ? 5 : -5); if (Changed != null) Changed(this, EventArgs.Empty); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Gfx.Quality(g);
        g.Clear(BackColor);
        float pad = Height / 2f, th = Math.Max(4f, Height * 0.22f), cy = Height / 2f;
        float x = pad + (Width - 2 * pad) * val / 100f;
        RectangleF track = new RectangleF(pad, cy - th / 2, Width - 2 * pad, th);
        using (SolidBrush b = new SolidBrush(Theme.Track)) Gfx.FillRound(g, b, track, th);
        RectangleF fill = new RectangleF(pad, cy - th / 2, Math.Max(1f, x - pad), th);
        using (LinearGradientBrush b = Gfx.AccentBrush(new RectangleF(pad, 0, Math.Max(1f, Width - 2 * pad), Height))) Gfx.FillRound(g, b, fill, th);
        float d = Height * 0.62f;
        using (SolidBrush w = new SolidBrush(Color.White)) g.FillEllipse(w, x - d / 2, cy - d / 2, d, d);
        using (Pen p = new Pen(Theme.Dark ? Theme.Line : Theme.Muted, Math.Max(1f, Height / 26f))) g.DrawEllipse(p, x - d / 2, cy - d / 2, d, d);
    }
}

// [−] 25 분 [+] 형태의 숫자 입력. 숫자는 직접 타이핑하거나 버튼/휠/방향키로 바꾼다.
class Stepper : Surface
{
    public int Min = 1, Max = 99;
    public string Unit = "";
    public event EventHandler Changed;
    int val;
    bool lockEvent;
    TextBox tb;
    string hover = "";

    public Stepper()
    {
        tb = new TextBox();
        tb.BorderStyle = BorderStyle.None;
        tb.TextAlign = HorizontalAlignment.Center;
        tb.BackColor = Theme.Card2;
        tb.ForeColor = Theme.Text;
        tb.MaxLength = 3;
        tb.KeyDown += delegate (object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { Commit(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Up) { Value = val + 1; e.Handled = true; }
            else if (e.KeyCode == Keys.Down) { Value = val - 1; e.Handled = true; }
        };
        tb.Leave += delegate { Commit(); };
        tb.GotFocus += delegate { tb.SelectAll(); };
        Controls.Add(tb);
        Cursor = Cursors.Default;
    }

    public int Value
    {
        get { return val; }
        set
        {
            int v = Math.Max(Min, Math.Min(Max, value));
            tb.Text = v.ToString();
            if (v != val)
            {
                val = v;
                if (!lockEvent && Changed != null) Changed(this, EventArgs.Empty);
            }
        }
    }

    public void SetSilently(int v) { lockEvent = true; Value = v; lockEvent = false; }

    public void ApplyTheme() { tb.BackColor = Theme.Card2; tb.ForeColor = Theme.Text; }

    void Commit()
    {
        int v;
        Value = int.TryParse(tb.Text, out v) ? v : val;
    }

    RectangleF Minus() { float b = Height - 6; return new RectangleF(3, 3, b, b); }
    RectangleF Plus() { float b = Height - 6; return new RectangleF(Width - 3 - b, 3, b, b); }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        float s = Height / 32f;
        FontLoader.ApplyTo(tb, 14 * s, true);
        float mid = Width - 2 * Height;
        tb.Width = (int)(mid * 0.58f);
        tb.Left = (int)(Height + 2 * s);
        tb.Top = Math.Max(0, (Height - tb.Height) / 2);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        string h = Minus().Contains(e.X, e.Y) ? "-" : Plus().Contains(e.X, e.Y) ? "+" : "";
        if (h != hover) { hover = h; Cursor = h == "" ? Cursors.Default : Cursors.Hand; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { hover = ""; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Commit();
        if (Minus().Contains(e.X, e.Y)) Value = val - 1;
        else if (Plus().Contains(e.X, e.Y)) Value = val + 1;
        base.OnMouseDown(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e) { Value = val + (e.Delta > 0 ? 1 : -1); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Gfx.Quality(g);
        g.Clear(Theme.Card);
        float s = Height / 32f;
        RectangleF r = new RectangleF(0, 0, Width - 1, Height - 1);
        using (SolidBrush b = new SolidBrush(Theme.Card2)) Gfx.FillRound(g, b, r, 10 * s);
        foreach (string id in new[] { "-", "+" })
        {
            RectangleF bt = id == "-" ? Minus() : Plus();
            if (hover == id) using (SolidBrush b = new SolidBrush(Theme.Mix(Theme.Card2, Theme.Accent, 0.25))) Gfx.FillRound(g, b, bt, 8 * s);
            using (Pen p = new Pen(Theme.Text, 1.8f * s))
            {
                float cx = bt.X + bt.Width / 2, cy = bt.Y + bt.Height / 2, k = 5 * s;
                g.DrawLine(p, cx - k, cy, cx + k, cy);
                if (id == "+") g.DrawLine(p, cx, cy - k, cx, cy + k);
            }
        }
        float ux = tb.Right + 2 * s;
        using (Font f = Theme.Fnt(12 * s, false))
            Gfx.Label(g, Unit, f, Theme.Muted, new RectangleF(ux, 0, Width - Height - ux, Height), StringAlignment.Near, StringAlignment.Center);
    }
}

// 할 일 목록 (체크 · 삭제 · 스크롤)
class TaskList : Surface
{
    public List<TaskItem> Items = new List<TaskItem>();
    public event EventHandler Changed;
    float scroll;
    int hoverRow = -1, hoverPart;   // 1=체크, 2=삭제, 3=본문

    float S { get { return Width / 424f; } }
    float RowH { get { return 40 * S; } }
    float Gap { get { return 6 * S; } }
    float Total { get { return Items.Count * (RowH + Gap) - Gap; } }

    void Clamp() { scroll = Math.Max(0, Math.Min(scroll, Math.Max(0, Total - Height))); }

    public void ScrollToEnd() { scroll = float.MaxValue; Clamp(); Invalidate(); }

    void HitTest(int x, int y, out int row, out int part)
    {
        row = -1; part = 0;
        float yy = y + scroll;
        int i = (int)(yy / (RowH + Gap));
        if (yy < 0 || i < 0 || i >= Items.Count || yy - i * (RowH + Gap) > RowH) return;
        row = i;
        part = x < 44 * S ? 1 : x > Width - 44 * S ? 2 : 3;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int r, p; HitTest(e.X, e.Y, out r, out p);
        if (r != hoverRow || p != hoverPart)
        {
            hoverRow = r; hoverPart = p;
            Cursor = (p == 1 || p == 2) ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { hoverRow = -1; hoverPart = 0; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        scroll -= e.Delta / 120f * (RowH + Gap);
        Clamp(); Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        int r, p; HitTest(e.X, e.Y, out r, out p);
        if (r < 0) return;
        if (p == 1) Items[r].Done = !Items[r].Done;
        else if (p == 2) Items.RemoveAt(r);
        else return;
        Clamp(); hoverRow = -1;
        Invalidate();
        if (Changed != null) Changed(this, EventArgs.Empty);
    }

    protected override void OnResize(EventArgs e) { Clamp(); base.OnResize(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Gfx.Quality(g);
        g.Clear(BackColor);
        float s = S;
        if (Items.Count == 0)
        {
            using (Font f = Theme.Fnt(13 * s, false))
            using (SolidBrush b = new SolidBrush(Theme.Muted))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString("아직 할 일이 없어요.\n오늘의 목표를 적어 보세요.", f, b, new RectangleF(0, 0, Width, Height), sf);
            return;
        }
        using (Font f = Theme.Fnt(14 * s, false))
        using (Font fd = Theme.FntStyle(14 * s, FontStyle.Strikeout))
        {
            for (int i = 0; i < Items.Count; i++)
            {
                float y = i * (RowH + Gap) - scroll;
                if (y + RowH < 0 || y > Height) continue;
                TaskItem t = Items[i];
                RectangleF row = new RectangleF(0, y, Width - 1, RowH);
                using (SolidBrush b = new SolidBrush(Theme.Card2)) Gfx.FillRound(g, b, row, 12 * s);

                RectangleF c = new RectangleF(14 * s, y + (RowH - 22 * s) / 2, 22 * s, 22 * s);
                if (t.Done)
                {
                    using (LinearGradientBrush b = Gfx.AccentBrush(c)) g.FillEllipse(b, c);
                    using (Pen p = new Pen(Color.White, 2f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                        g.DrawLines(p, new[] { new PointF(c.X + 6 * s, c.Y + 11.5f * s), new PointF(c.X + 9.5f * s, c.Y + 15 * s), new PointF(c.X + 16 * s, c.Y + 7 * s) });
                }
                else
                {
                    bool hot = hoverRow == i && hoverPart == 1;
                    using (Pen p = new Pen(hot ? Theme.Accent : Theme.Muted, 2f * s)) g.DrawEllipse(p, c.X + s, c.Y + s, c.Width - 2 * s, c.Height - 2 * s);
                }

                RectangleF tx = new RectangleF(48 * s, y, Width - 48 * s - 44 * s, RowH);
                Gfx.Label(g, t.Text, t.Done ? fd : f, t.Done ? Theme.Muted : Theme.Text, tx, StringAlignment.Near, StringAlignment.Center);

                bool xh = hoverRow == i && hoverPart == 2;
                using (Pen p = new Pen(xh ? Theme.Accent : Theme.Muted, 1.8f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    float cx = Width - 24 * s, cy = y + RowH / 2, k = 4.5f * s;
                    g.DrawLine(p, cx - k, cy - k, cx + k, cy + k);
                    g.DrawLine(p, cx - k, cy + k, cx + k, cy - k);
                }
            }
        }
    }
}

class QuoteCard : Card
{
    string text = "", by = "";

    public void Set(string[] q)
    {
        text = q[0]; by = q[1];
        Invalidate();
    }

    // 글꼴에 의존하지 않고 여는 따옴표를 도형으로 그린다
    static void DrawQuoteMark(Graphics g, float cx, float top, float s)
    {
        s *= 0.72f;
        float r = 7.5f * s, cy = top + 10 * s;
        using (SolidBrush b = new SolidBrush(Theme.Accent))
            for (int i = 0; i < 2; i++)
            {
                float x = cx + (i == 0 ? -17 : 5) * s;
                g.FillEllipse(b, x, cy, 2 * r, 2 * r);
                using (GraphicsPath tail = new GraphicsPath())
                {
                    tail.AddBezier(x, cy + r, x, cy - 6 * s, x + 6 * s, cy - 14 * s, x + 12 * s, cy - 15 * s);
                    tail.AddBezier(x + 12 * s, cy - 15 * s, x + 8 * s, cy - 8 * s, x + 2 * r, cy - 2 * s, x + 2 * r, cy + r);
                    tail.CloseFigure();
                    g.FillPath(b, tail);
                }
            }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Height < 40) return;
        Graphics g = e.Graphics;
        float s = Width / 480f;
        float pad = 36 * s;
        RectangleF area = new RectangleF(pad, 0, Width - 2 * pad, Height);

        using (Font ft = Theme.Fnt(20 * s, true))
        using (Font fb = Theme.Fnt(13.5f * s, false))
        using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center })
        {
            SizeF mark = new SizeF(0, 50 * s);
            SizeF tsz = g.MeasureString(text, ft, (int)area.Width, sf);
            float total = mark.Height + tsz.Height + 18 * s + fb.GetHeight(g);
            float y = (Height - total) / 2;
            DrawQuoteMark(g, Width / 2f, y + 14 * s, s);
            y += mark.Height;
            using (SolidBrush b = new SolidBrush(Theme.Text))
                g.DrawString(text, ft, b, new RectangleF(area.X, y, area.Width, tsz.Height + 4), sf);
            y += tsz.Height + 18 * s;
            using (SolidBrush b = new SolidBrush(Theme.Muted))
                g.DrawString("— " + by, fb, b, new RectangleF(area.X, y, area.Width, fb.GetHeight(g) + 4), sf);
        }
    }
}

// 화면 위쪽에 잠깐 뜨는 알림
class Toast : Surface
{
    string msg = "";
    Timer hide = new Timer();

    public Toast()
    {
        Visible = false;
        hide.Interval = 4000;
        hide.Tick += delegate { hide.Stop(); Visible = false; };
    }

    public void ShowMessage(string m, int parentWidth)
    {
        msg = m;
        float s = parentWidth / 1048f;
        using (Font f = Theme.Fnt(15 * s, true))
        using (Graphics g = CreateGraphics())
        {
            SizeF sz = g.MeasureString(m, f);
            Size = new Size((int)(sz.Width + 48 * s), (int)(46 * s));
        }
        Left = (parentWidth - Width) / 2;
        Top = (int)(14 * s);
        Visible = true; BringToFront(); Invalidate();
        hide.Stop(); hide.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Gfx.Quality(g);
        g.Clear(Theme.Bg);
        RectangleF r = new RectangleF(0, 0, Width - 1, Height - 1);
        using (SolidBrush b = new SolidBrush(Theme.Text)) Gfx.FillRound(g, b, r, Height);
        using (Font f = Theme.Fnt(Height * 0.33f, true))
            Gfx.Label(g, msg, f, Theme.Bg, new RectangleF(0, 0, Width, Height), StringAlignment.Center, StringAlignment.Center);
    }
}
