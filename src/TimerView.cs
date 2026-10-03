using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// 왼쪽 타이머 카드: 단계 표시, 원형 진행바, 시간, 사이클 점, 조작 버튼, 오늘 기록, 세션 종료
class TimerView : Surface
{
    public string Phase = "focus", TimeText = "25:00", SubText = "", FocusText = "0분", RestText = "0분";
    public double Progress = 1;
    public bool Running;
    public int DotCount = 4, DotsDone, DotNow = -1;
    public event Action<string> Command;

    string hover = "";
    readonly List<KeyValuePair<string, RectangleF>> hits = new List<KeyValuePair<string, RectangleF>>();
    float s;

    public TimerView()
    {
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
    }

    public void Set(string time, string sub, double progress, bool running, string focus, string rest, int dots, int done, int now)
    {
        if (time == TimeText && sub == SubText && progress == Progress && running == Running && focus == FocusText &&
            rest == RestText && dots == DotCount && done == DotsDone && now == DotNow) return;
        TimeText = time; SubText = sub; Progress = progress; Running = running; FocusText = focus; RestText = rest;
        DotCount = dots; DotsDone = done; DotNow = now;
        Invalidate();
    }

    // ---------- 배치 ----------
    RectangleF Pills() { return new RectangleF((Width - 336 * s) / 2, 24 * s, 336 * s, 44 * s); }
    RectangleF ThemeBtn() { return Circle(Width - 24 * s - 17 * s, 46 * s, 34 * s); }
    RectangleF Ring() { return new RectangleF(Width / 2f - 135 * s, 90 * s, 270 * s, 270 * s); }
    RectangleF Circle(float cx, float cy, float d) { return new RectangleF(cx - d / 2, cy - d / 2, d, d); }
    RectangleF Reset() { return Circle(Width / 2f - 96 * s, 446 * s, 52 * s); }
    RectangleF Play() { return Circle(Width / 2f, 446 * s, 80 * s); }
    RectangleF Skip() { return Circle(Width / 2f + 96 * s, 446 * s, 52 * s); }
    RectangleF LiveBox(int i)
    {
        float w = (Width - 56 * s - 12 * s) / 2;
        return new RectangleF(28 * s + i * (w + 12 * s), 506 * s, w, 66 * s);
    }
    RectangleF End() { return new RectangleF(28 * s, 588 * s, Width - 56 * s, 46 * s); }

    void BuildHits()
    {
        s = Width / 500f;
        hits.Clear();
        hits.Add(new KeyValuePair<string, RectangleF>("theme", ThemeBtn()));
        hits.Add(new KeyValuePair<string, RectangleF>("reset", Reset()));
        hits.Add(new KeyValuePair<string, RectangleF>("play", Play()));
        hits.Add(new KeyValuePair<string, RectangleF>("skip", Skip()));
        hits.Add(new KeyValuePair<string, RectangleF>("end", End()));
    }

    string HitId(int x, int y)
    {
        BuildHits();
        foreach (KeyValuePair<string, RectangleF> h in hits)
            if (h.Value.Contains(x, y)) return h.Key;
        return "";
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        string id = HitId(e.X, e.Y);
        if (id != hover) { hover = id; Cursor = id == "" ? Cursors.Default : Cursors.Hand; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { hover = ""; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        Focus();
        string id = HitId(e.X, e.Y);
        if (id != "" && Command != null) Command(id);
        base.OnMouseClick(e);
    }

    // ---------- 그리기 ----------
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Gfx.Quality(g);
        g.Clear(Theme.Bg);
        BuildHits();

        RectangleF card = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (SolidBrush b = new SolidBrush(Theme.Card)) Gfx.FillRound(g, b, card, 26 * s);
        using (Pen p = new Pen(Theme.Line)) Gfx.DrawRound(g, p, card, 26 * s);

        DrawPills(g);
        DrawThemeButton(g);
        DrawRing(g);
        DrawDots(g);
        DrawControls(g);
        DrawLive(g);
        DrawEnd(g);
    }

    void DrawPills(Graphics g)
    {
        RectangleF box = Pills();
        using (SolidBrush b = new SolidBrush(Theme.Card2)) Gfx.FillRound(g, b, box, 22 * s);
        string[] ids = { "focus", "short", "long" }, names = { "집중", "짧은 휴식", "긴 휴식" };
        float w = (box.Width - 10 * s) / 3;
        using (Font f = Theme.Fnt(14 * s, true))
            for (int i = 0; i < 3; i++)
            {
                RectangleF r = new RectangleF(box.X + 5 * s + i * w, box.Y + 5 * s, w, box.Height - 10 * s);
                bool on = Phase == ids[i];
                if (on) using (LinearGradientBrush b = Gfx.AccentBrush(r)) Gfx.FillRound(g, b, r, 17 * s);
                Gfx.Label(g, names[i], f, on ? Color.White : Theme.Muted, r, StringAlignment.Center, StringAlignment.Center);
            }
    }

    // 다크일 땐 해(라이트로 전환), 라이트일 땐 달(다크로 전환) 아이콘
    void DrawThemeButton(Graphics g)
    {
        RectangleF r = ThemeBtn();
        CircleButton(g, r, hover == "theme");
        float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
        if (Theme.Dark)
        {
            using (SolidBrush b = new SolidBrush(Theme.Text)) g.FillEllipse(b, cx - 4.5f * s, cy - 4.5f * s, 9 * s, 9 * s);
            using (Pen p = new Pen(Theme.Text, 1.8f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                for (int i = 0; i < 8; i++)
                {
                    double a = Math.PI * i / 4;
                    float c = (float)Math.Cos(a), sn = (float)Math.Sin(a);
                    g.DrawLine(p, cx + c * 7.5f * s, cy + sn * 7.5f * s, cx + c * 10 * s, cy + sn * 10 * s);
                }
        }
        else
        {
            float d = 17 * s;
            using (GraphicsPath moon = new GraphicsPath())
            {
                moon.AddEllipse(cx - d / 2, cy - d / 2, d, d);
                using (GraphicsPath cut = new GraphicsPath())
                {
                    cut.AddEllipse(cx - d / 2 + 6 * s, cy - d / 2 - 4 * s, d, d);
                    using (Region reg = new Region(moon))
                    {
                        reg.Exclude(cut);
                        using (SolidBrush b = new SolidBrush(Theme.Text)) g.FillRegion(b, reg);
                    }
                }
            }
        }
    }

    void DrawRing(Graphics g)
    {
        RectangleF ring = Ring();
        float stroke = 12 * s;
        RectangleF arc = RectangleF.Inflate(ring, -stroke / 2, -stroke / 2);
        using (Pen p = new Pen(Theme.Track, stroke)) g.DrawEllipse(p, arc);
        float sweep = (float)(360 * Math.Max(0, Math.Min(1, Progress)));
        if (sweep > 0.4f)
            using (LinearGradientBrush b = Gfx.AccentBrush(ring))
            using (Pen p = new Pen(b, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawArc(p, arc, -90, Math.Min(sweep, 359.9f));

        float cx = ring.X + ring.Width / 2, cy = ring.Y + ring.Height / 2;
        DrawTime(g, TimeText, cx, cy - 8 * s, 58 * s);
        using (Font f = Theme.Fnt(13 * s, false))
            Gfx.Label(g, SubText, f, Theme.Muted, new RectangleF(ring.X, cy + 38 * s, ring.Width, 24 * s), StringAlignment.Center, StringAlignment.Center);
    }

    // 숫자 폭이 달라 시간이 흔들려 보이지 않도록 칸을 고정해 그린다
    void DrawTime(Graphics g, string txt, float cx, float cy, float px)
    {
        using (Font f = Theme.Fnt(px, true))
        using (StringFormat sf = new StringFormat(StringFormatFlags.NoWrap))
        {
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;
            float dw = px * 0.6f;   // 숫자 칸 폭
            float cw = dw * 0.5f;   // ':' 칸 폭
            float total = 0;
            foreach (char c in txt) total += c == ':' ? cw : dw;
            float x = cx - total / 2, h = px * 1.5f;
            foreach (char c in txt)
            {
                float w = c == ':' ? cw : dw;
                Gfx.DrawText(g, c.ToString(), f, Theme.Text, new RectangleF(x - dw, cy - h / 2, w + 2 * dw, h), sf);
                x += w;
            }
        }
    }

    void DrawDots(Graphics g)
    {
        int n = Math.Min(DotCount, 12);
        float d = 10 * s, gap = 9 * s, total = n * d + (n - 1) * gap;
        float x = (Width - total) / 2, y = 386 * s;
        for (int i = 0; i < n; i++)
        {
            RectangleF r = new RectangleF(x + i * (d + gap), y, d, d);
            if (i < DotsDone) using (SolidBrush b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, r);
            else if (i == DotNow)
            {
                using (SolidBrush b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, r);
                using (Pen p = new Pen(Color.FromArgb(90, Theme.Accent), 2 * s)) g.DrawEllipse(p, RectangleF.Inflate(r, 3 * s, 3 * s));
            }
            else using (SolidBrush b = new SolidBrush(Theme.Track)) g.FillEllipse(b, r);
        }
    }

    void DrawControls(Graphics g)
    {
        // 초기화
        RectangleF r = Reset();
        CircleButton(g, r, hover == "reset");
        using (Pen p = new Pen(Theme.Text, 2.2f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, rad = 8.5f * s;
            g.DrawArc(p, cx - rad, cy - rad, rad * 2, rad * 2, -45, 270);
            double a = Math.PI * 225 / 180;
            PointF tip = new PointF(cx + (float)Math.Cos(a) * rad, cy + (float)Math.Sin(a) * rad);
            PointF t = new PointF(-(float)Math.Sin(a), (float)Math.Cos(a));
            PointF n = new PointF(-t.Y, t.X);
            using (SolidBrush b = new SolidBrush(Theme.Text))
                g.FillPolygon(b, new[] {
                    new PointF(tip.X + t.X * 5 * s, tip.Y + t.Y * 5 * s),
                    new PointF(tip.X + n.X * 4.5f * s, tip.Y + n.Y * 4.5f * s),
                    new PointF(tip.X - n.X * 4.5f * s, tip.Y - n.Y * 4.5f * s) });
        }

        // 건너뛰기
        r = Skip();
        CircleButton(g, r, hover == "skip");
        using (SolidBrush b = new SolidBrush(Theme.Text))
        {
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            g.FillPolygon(b, new[] { new PointF(cx - 7 * s, cy - 8 * s), new PointF(cx + 5 * s, cy), new PointF(cx - 7 * s, cy + 8 * s) });
            g.FillRectangle(b, cx + 6 * s, cy - 8 * s, 2.5f * s, 16 * s);
        }

        // 시작 / 일시정지
        r = Play();
        using (LinearGradientBrush b = Gfx.AccentBrush(r)) g.FillEllipse(b, r);
        if (hover == "play") using (SolidBrush w = new SolidBrush(Color.FromArgb(30, Color.White))) g.FillEllipse(w, r);
        using (SolidBrush w = new SolidBrush(Color.White))
        {
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            if (Running)
            {
                Gfx.FillRound(g, w, new RectangleF(cx - 10 * s, cy - 11 * s, 7 * s, 22 * s), 2 * s);
                Gfx.FillRound(g, w, new RectangleF(cx + 3 * s, cy - 11 * s, 7 * s, 22 * s), 2 * s);
            }
            else
                g.FillPolygon(w, new[] { new PointF(cx - 8 * s, cy - 13 * s), new PointF(cx + 14 * s, cy), new PointF(cx - 8 * s, cy + 13 * s) });
        }
    }

    void CircleButton(Graphics g, RectangleF r, bool hot)
    {
        using (SolidBrush b = new SolidBrush(hot ? Theme.Mix(Theme.Card2, Theme.Accent, 0.16) : Theme.Card2)) g.FillEllipse(b, r);
        using (Pen p = new Pen(Theme.Line)) g.DrawEllipse(p, r);
    }

    void DrawLive(Graphics g)
    {
        string[] label = { "오늘 집중", "오늘 휴식" }, val = { FocusText, RestText };
        using (Font fl = Theme.Fnt(12 * s, false))
        using (Font fv = Theme.Fnt(18 * s, true))
            for (int i = 0; i < 2; i++)
            {
                RectangleF r = LiveBox(i);
                using (SolidBrush b = new SolidBrush(Theme.Card2)) Gfx.FillRound(g, b, r, 16 * s);
                Gfx.Label(g, label[i], fl, Theme.Muted, new RectangleF(r.X + 16 * s, r.Y + 9 * s, r.Width - 24 * s, 20 * s), StringAlignment.Near, StringAlignment.Center);
                Gfx.Label(g, val[i], fv, Theme.Text, new RectangleF(r.X + 16 * s, r.Y + 30 * s, r.Width - 24 * s, 28 * s), StringAlignment.Near, StringAlignment.Center);
            }
    }

    void DrawEnd(Graphics g)
    {
        RectangleF r = End();
        bool hot = hover == "end";
        if (hot) using (SolidBrush b = new SolidBrush(Theme.Card2)) Gfx.FillRound(g, b, r, 15 * s);
        using (Pen p = new Pen(hot ? Theme.Accent : Theme.Line)) Gfx.DrawRound(g, p, r, 15 * s);
        using (Font f = Theme.Fnt(14.5f * s, true))
            Gfx.Label(g, "공부 세션 종료", f, Theme.Text, r, StringAlignment.Center, StringAlignment.Center);
    }
}
