using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

static class Program
{
    [DllImport("user32.dll")]
    static extern bool SetProcessDPIAware();

    [STAThread]
    static void Main()
    {
        try { SetProcessDPIAware(); } catch (Exception) { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}

static class Fmt
{
    public static string Dur(double sec)
    {
        int t = (int)Math.Round(sec), h = t / 3600, m = t % 3600 / 60, x = t % 60;
        if (h > 0) return h + "시간 " + m + "분";
        if (m > 0) return m + "분 " + x + "초";
        return x + "초";
    }

    public static string DurShort(double sec)
    {
        int m = (int)(sec / 60);
        return m >= 60 ? (m / 60) + "시간 " + (m % 60) + "분" : m + "분";
    }
}

// 플래너: 할 일 입력, 진행률, 목록
class PlannerCard : Card
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

    public TextBox Input = new TextBox();
    public RoundButton AddButton = new RoundButton();
    public TaskList List = new TaskList();
    public event Action<string> AddRequested;
    int done, total;

    public PlannerCard()
    {
        Input.BorderStyle = BorderStyle.None;
        Input.MaxLength = 100;
        Input.BackColor = Theme.Card2;
        Input.ForeColor = Theme.Text;
        Input.KeyDown += delegate (object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Submit(); }
        };
        Input.HandleCreated += delegate
        {
            try { SendMessage(Input.Handle, 0x1501, (IntPtr)1, "오늘 할 일을 입력하세요"); } catch (Exception) { }
        };
        AddButton.Text = "추가";
        AddButton.Primary = true;
        AddButton.Click += delegate { Submit(); };
        Controls.Add(Input);
        Controls.Add(AddButton);
        Controls.Add(List);
    }

    void Submit()
    {
        string t = Input.Text.Trim();
        if (t.Length == 0) return;
        Input.Text = "";
        if (AddRequested != null) AddRequested(t);
    }

    public void SetProgress(int d, int t) { done = d; total = t; Invalidate(); }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        float s = Width / 480f;
        AddButton.SetBounds((int)(Width - 28 * s - 76 * s), (int)(58 * s), (int)(76 * s), (int)(40 * s));
        Input.Font = Theme.Fnt(14 * s, false);
        Input.SetBounds((int)(28 * s + 14 * s), 0, (int)(Width - 56 * s - 84 * s - 28 * s), Input.Height);
        Input.Top = (int)(58 * s + (40 * s - Input.Height) / 2);
        List.SetBounds((int)(28 * s), (int)(130 * s), (int)(Width - 56 * s), Math.Max(0, (int)(Height - 130 * s - 18 * s)));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        float s = Width / 480f;
        using (Font f = Theme.Fnt(15 * s, true))
            Gfx.Label(g, "플래너", f, Theme.Text, new RectangleF(28 * s, 20 * s, 200 * s, 26 * s), StringAlignment.Near, StringAlignment.Center);
        if (total > 0)
            using (Font f = Theme.Fnt(13 * s, false))
                Gfx.Label(g, done + " / " + total + " 완료", f, Theme.Muted, new RectangleF(Width - 28 * s - 200 * s, 20 * s, 200 * s, 26 * s), StringAlignment.Far, StringAlignment.Center);

        RectangleF box = new RectangleF(28 * s, 58 * s, Width - 56 * s - 84 * s, 40 * s);
        using (SolidBrush b = new SolidBrush(Theme.Card2)) Gfx.FillRound(g, b, box, 13 * s);
        using (Pen p = new Pen(Theme.Line)) Gfx.DrawRound(g, p, box, 13 * s);

        RectangleF bar = new RectangleF(28 * s, 112 * s, Width - 56 * s, 6 * s);
        using (SolidBrush b = new SolidBrush(Theme.Track)) Gfx.FillRound(g, b, bar, 3 * s);
        if (total > 0 && done > 0)
        {
            RectangleF fill = new RectangleF(bar.X, bar.Y, bar.Width * done / total, bar.Height);
            using (LinearGradientBrush b = Gfx.AccentBrush(bar)) Gfx.FillRound(g, b, fill, 3 * s);
        }
    }
}

// 설정: 시간·사이클, 자동 시작, 알림음 on/off, 알림음 볼륨
class SettingsCard : Card
{
    public Stepper[] Steps = new Stepper[4];
    public Switch SwitchAuto = new Switch(), SwitchSound = new Switch();
    public Slider Volume = new Slider();
    public RoundButton TestFocus = new RoundButton(), TestRest = new RoundButton();

    static readonly string[] Labels = { "집중 시간", "짧은 휴식", "긴 휴식", "긴 휴식까지 사이클", "다음 단계 자동 시작", "종료 알림음", "알림음 볼륨" };
    static readonly string[] Units = { "분", "분", "분", "회" };
    static readonly int[] Mins = { 1, 1, 1, 1 }, Maxs = { 180, 60, 120, 12 };

    public SettingsCard()
    {
        for (int i = 0; i < 4; i++)
        {
            Steps[i] = new Stepper();
            Steps[i].Min = Mins[i]; Steps[i].Max = Maxs[i]; Steps[i].Unit = Units[i];
            Controls.Add(Steps[i]);
        }
        TestFocus.Text = "집중음"; TestRest.Text = "휴식음";
        Controls.Add(SwitchAuto); Controls.Add(SwitchSound); Controls.Add(Volume);
        Controls.Add(TestFocus); Controls.Add(TestRest);
        Volume.Changed += delegate { Invalidate(); };
    }

    float RowTop(int i, float s) { return (54 + i * 42) * s; }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (Steps[3] == null) return;   // 생성자에서 컨트롤을 채우는 중
        float s = Width / 480f;
        for (int i = 0; i < 4; i++)
            Steps[i].SetBounds((int)(Width - 24 * s - 150 * s), (int)(RowTop(i, s) + 5 * s), (int)(150 * s), (int)(32 * s));
        SwitchAuto.SetBounds((int)(Width - 24 * s - 46 * s), (int)(RowTop(4, s) + 8 * s), (int)(46 * s), (int)(26 * s));
        SwitchSound.SetBounds((int)(Width - 24 * s - 46 * s), (int)(RowTop(5, s) + 8 * s), (int)(46 * s), (int)(26 * s));
        Volume.SetBounds((int)(150 * s), (int)(RowTop(6, s) + 8 * s), (int)(122 * s), (int)(26 * s));
        TestFocus.SetBounds((int)(Width - 24 * s - 130 * s), (int)(RowTop(6, s) + 6 * s), (int)(62 * s), (int)(30 * s));
        TestRest.SetBounds((int)(Width - 24 * s - 62 * s), (int)(RowTop(6, s) + 6 * s), (int)(62 * s), (int)(30 * s));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        float s = Width / 480f;
        using (Font f = Theme.Fnt(15 * s, true))
            Gfx.Label(g, "설정", f, Theme.Text, new RectangleF(24 * s, 18 * s, 200 * s, 26 * s), StringAlignment.Near, StringAlignment.Center);
        using (Font f = Theme.Fnt(14 * s, false))
        using (Pen p = new Pen(Theme.Line))
            for (int i = 0; i < Labels.Length; i++)
            {
                float y = RowTop(i, s);
                Gfx.Label(g, Labels[i], f, Theme.Text, new RectangleF(24 * s, y, 200 * s, 42 * s), StringAlignment.Near, StringAlignment.Center);
                if (i > 0) g.DrawLine(p, 24 * s, y, Width - 24 * s, y);
            }
        using (Font f = Theme.Fnt(13 * s, true))
            Gfx.Label(g, Volume.Value + "%", f, Theme.Muted, new RectangleF(276 * s, RowTop(6, s), 44 * s, 42 * s), StringAlignment.Near, StringAlignment.Center);
    }
}

// 세션 종료 시 보여 주는 요약
class StatsDialog : Form
{
    readonly double focus, rest;
    readonly int cycles, done, total;
    readonly float K;

    public StatsDialog(double focusSec, double restSec, int cycleCount, int doneTasks, int totalTasks, float scale)
    {
        focus = focusSec; rest = restSec; cycles = cycleCount; done = doneTasks; total = totalTasks; K = scale;
        Text = "공부 세션 요약";
        BackColor = Theme.Bg;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size((int)(440 * K), (int)(400 * K));
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

        RoundButton keep = new RoundButton();
        keep.Text = "계속 공부하기";
        keep.SetBounds((int)(30 * K), (int)(332 * K), (int)(185 * K), (int)(46 * K));
        keep.Click += delegate { DialogResult = DialogResult.Cancel; };
        RoundButton fresh = new RoundButton();
        fresh.Text = "새 세션 시작";
        fresh.Primary = true;
        fresh.SetBounds((int)(225 * K), (int)(332 * K), (int)(185 * K), (int)(46 * K));
        fresh.Click += delegate { DialogResult = DialogResult.OK; };
        keep.BackColor = Theme.Bg; fresh.BackColor = Theme.Bg;
        Controls.Add(keep); Controls.Add(fresh);
        AcceptButton = null;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Gfx.Quality(g);
        g.Clear(Theme.Bg);
        float s = K;
        using (Font f = Theme.Fnt(22 * s, true))
            Gfx.Label(g, "수고하셨어요", f, Theme.Text, new RectangleF(30 * s, 26 * s, 380 * s, 34 * s), StringAlignment.Near, StringAlignment.Center);
        using (Font f = Theme.Fnt(13 * s, false))
            Gfx.Label(g, "이번 공부 세션의 기록입니다.", f, Theme.Muted, new RectangleF(30 * s, 62 * s, 380 * s, 22 * s), StringAlignment.Near, StringAlignment.Center);

        string[] cap = { "총 집중 시간", "총 휴식 시간" }, val = { Fmt.Dur(focus), Fmt.Dur(rest) };
        Color[] col = Theme.Dark
            ? new[] { Color.FromArgb(255, 138, 138), Color.FromArgb(79, 224, 180) }
            : new[] { Color.FromArgb(232, 84, 84), Color.FromArgb(22, 170, 128) };
        for (int i = 0; i < 2; i++)
        {
            RectangleF r = new RectangleF(30 * s + i * 195 * s, 104 * s, 185 * s, 80 * s);
            using (SolidBrush b = new SolidBrush(Theme.Card2)) Gfx.FillRound(g, b, r, 18 * s);
            using (Font f = Theme.Fnt(12 * s, false))
                Gfx.Label(g, cap[i], f, Theme.Muted, new RectangleF(r.X + 16 * s, r.Y + 12 * s, r.Width - 24 * s, 20 * s), StringAlignment.Near, StringAlignment.Center);
            using (Font f = Theme.Fnt(22 * s, true))
                Gfx.Label(g, val[i], f, col[i], new RectangleF(r.X + 16 * s, r.Y + 36 * s, r.Width - 24 * s, 34 * s), StringAlignment.Near, StringAlignment.Center);
        }

        double sum = focus + rest;
        if (sum <= 0) sum = 1;
        RectangleF bar = new RectangleF(30 * s, 204 * s, 380 * s, 10 * s);
        using (SolidBrush b = new SolidBrush(Theme.Track)) Gfx.FillRound(g, b, bar, 5 * s);
        float fw = (float)(bar.Width * focus / sum);
        using (GraphicsPath clip = Gfx.Round(bar, 5 * s))
        {
            Region old = g.Clip;
            g.SetClip(clip);
            using (SolidBrush b = new SolidBrush(col[0])) g.FillRectangle(b, bar.X, bar.Y, fw, bar.Height);
            using (SolidBrush b = new SolidBrush(col[1])) g.FillRectangle(b, bar.X + fw, bar.Y, bar.Width - fw, bar.Height);
            g.Clip = old;
        }

        string[] rowL = { "완료한 사이클", "완료한 할 일" }, rowV = { cycles + "회", done + " / " + total };
        for (int i = 0; i < 2; i++)
        {
            RectangleF r = new RectangleF(30 * s, 232 * s + i * 38 * s, 380 * s, 32 * s);
            using (Font f = Theme.Fnt(14 * s, false))
                Gfx.Label(g, rowL[i], f, Theme.Muted, r, StringAlignment.Near, StringAlignment.Center);
            using (Font f = Theme.Fnt(14 * s, true))
                Gfx.Label(g, rowV[i], f, Theme.Text, r, StringAlignment.Far, StringAlignment.Center);
        }
    }
}

class MainForm : Form
{
    readonly float K;
    readonly Config cfg = Store.LoadConfig();
    readonly List<TaskItem> tasks = Store.LoadTasks();

    TimerView tv = new TimerView();
    PlannerCard planner = new PlannerCard();
    Control slot = new Control();
    SettingsCard settings = new SettingsCard();
    QuoteCard quote = new QuoteCard();
    Toast toast = new Toast();
    Timer ticker = new Timer(), anim = new Timer();

    string phase = "focus";
    int cycle;
    bool running, wasFocusing;
    DateTime last = DateTime.UtcNow, lastSave = DateTime.UtcNow, lastAnim = DateTime.UtcNow;
    double remaining, statFocus, statRest, slotP, slotTarget;
    int statCycles;

    static string NameOf(string p) { return p == "focus" ? "집중" : p == "short" ? "짧은 휴식" : "긴 휴식"; }

    double Total(string p) { return (p == "focus" ? cfg.Focus : p == "short" ? cfg.Short : cfg.Long) * 60.0; }

    string NextPhase() { return phase != "focus" ? "focus" : (cycle + 1 >= cfg.Cycles ? "long" : "short"); }

    public MainForm()
    {
        float k = 1f;
        try { using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) k = Math.Max(1f, g.DpiX / 96f); } catch (Exception) { }
        K = k;

        Text = "포모도로 타이머";
        BackColor = Theme.Bg;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
        ClientSize = new Size((int)(1048 * K), (int)(688 * K));

        Theme.SetDark(cfg.Dark);
        BackColor = Theme.Bg;
        Store.LoadStat(out statFocus, out statRest, out statCycles);
        remaining = Total("focus");

        tv.SetBounds(P(24), P(24), P(500), P(640));
        planner.SetBounds(P(544), P(24), P(480), P(264));
        slot.SetBounds(P(544), P(304), P(480), P(360));
        slot.BackColor = Theme.Bg;
        slot.Controls.Add(settings);
        slot.Controls.Add(quote);
        Controls.Add(tv);
        Controls.Add(planner);
        Controls.Add(slot);
        Controls.Add(toast);
        toast.BringToFront();

        // 설정 값 반영
        for (int i = 0; i < 4; i++) settings.Steps[i].SetSilently(CfgValue(i));
        settings.SwitchAuto.Checked = cfg.Auto;
        settings.SwitchSound.Checked = cfg.Sound;
        settings.Volume.Value = cfg.Volume;
        string[] keys = { "focus", "short", "long", "cycles" };
        for (int i = 0; i < 4; i++)
        {
            string key = keys[i];
            Stepper st = settings.Steps[i];
            st.Changed += delegate { SetCfg(key, st.Value); };
        }
        settings.SwitchAuto.Changed += delegate { cfg.Auto = settings.SwitchAuto.Checked; Store.SaveConfig(cfg); };
        settings.SwitchSound.Changed += delegate { cfg.Sound = settings.SwitchSound.Checked; Store.SaveConfig(cfg); };
        settings.Volume.Changed += delegate { cfg.Volume = settings.Volume.Value; Store.SaveConfig(cfg); };
        settings.Volume.MouseUp += delegate { Sound.Play(true, cfg.Volume); };
        settings.TestFocus.Click += delegate { Sound.Play(true, cfg.Volume); };
        settings.TestRest.Click += delegate { Sound.Play(false, cfg.Volume); };

        // 플래너
        planner.List.Items = tasks;
        planner.List.Changed += delegate { Store.SaveTasks(tasks); RefreshTasks(); };
        planner.AddRequested += delegate (string t)
        {
            TaskItem it = new TaskItem(); it.Text = t; it.Done = false;
            tasks.Add(it);
            Store.SaveTasks(tasks);
            RefreshTasks();
            planner.List.ScrollToEnd();
        };

        tv.Command += OnCommand;
        quote.Set(Quotes.Next());

        ticker.Interval = 250; ticker.Tick += delegate { Tick(); }; ticker.Start();
        anim.Interval = 15; anim.Tick += delegate { AnimStep(); };
        ApplySlot();
        RefreshTasks();
        ApplyTheme(this);
        Render();

        Shown += delegate { ActiveControl = tv; };
        FormClosing += delegate { Tick(); SaveAll(); };
    }

    int P(int v) { return (int)Math.Round(v * K); }

    int CfgValue(int i) { return i == 0 ? cfg.Focus : i == 1 ? cfg.Short : i == 2 ? cfg.Long : cfg.Cycles; }

    // ---------- 명령 ----------
    void OnCommand(string id)
    {
        if (id == "theme") ToggleTheme();
        else if (id == "play") Toggle();
        else if (id == "skip") { Tick(); Go(false); }
        else if (id == "reset") { running = false; remaining = Total(phase); Render(); }
        else if (id == "end") EndSession();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Space && !(ActiveControl is TextBox))
        {
            Toggle();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    void ToggleTheme()
    {
        cfg.Dark = !cfg.Dark;
        Store.SaveConfig(cfg);
        Theme.SetDark(cfg.Dark);
        ApplyTheme(this);
        Invalidate(true);
    }

    void ApplyTheme(Control root)
    {
        if (root == this || root == slot) root.BackColor = Theme.Bg;
        else if (root is Surface) root.BackColor = Theme.Card;
        if (root is Stepper) ((Stepper)root).ApplyTheme();
        if (root is TextBox) { root.BackColor = Theme.Card2; root.ForeColor = Theme.Text; }
        foreach (Control c in root.Controls) ApplyTheme(c);
    }

    void Toggle()
    {
        Tick();
        running = !running;
        last = DateTime.UtcNow;
        Render();
    }

    // ---------- 타이머 ----------
    void Tick()
    {
        DateTime now = DateTime.UtcNow;
        if (running)
        {
            double dt = (now - last).TotalSeconds;
            last = now;
            double used = Math.Min(dt, Math.Max(0, remaining));
            if (phase == "focus") statFocus += used; else statRest += used;
            remaining -= dt;
            if (remaining <= 0)
            {
                string done = phase;
                Go(true);
                if (cfg.Sound) Sound.Play(phase == "focus", cfg.Volume);
                if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
                Activate();
                toast.ShowMessage(done == "focus" ? "집중 완료! " + NameOf(phase) + " 시간이에요" : "휴식 끝! 다시 집중해 볼까요?", ClientSize.Width);
            }
            else Render();
        }
        if ((now - lastSave).TotalSeconds >= 5) { lastSave = now; Store.SaveStat(statFocus, statRest, statCycles); }
    }

    void Go(bool natural)
    {
        string n = NextPhase();
        if (phase == "focus")
        {
            if (natural) statCycles++;
            cycle = n == "long" ? 0 : cycle + 1;
        }
        phase = n;
        remaining = Total(n);
        running = natural && cfg.Auto;
        last = DateTime.UtcNow;
        Render();
        Store.SaveStat(statFocus, statRest, statCycles);
    }

    void SetCfg(string key, int v)
    {
        bool untouched = !running && Math.Abs(remaining - Total(phase)) < 0.5;
        if (key == "focus") cfg.Focus = v;
        else if (key == "short") cfg.Short = v;
        else if (key == "long") cfg.Long = v;
        else cfg.Cycles = v;
        Store.SaveConfig(cfg);
        if (untouched && key == phase) remaining = Total(phase);
        Render();
    }

    void EndSession()
    {
        Tick();
        running = false;
        Render();
        int d = 0;
        foreach (TaskItem t in tasks) if (t.Done) d++;
        using (StatsDialog dlg = new StatsDialog(statFocus, statRest, statCycles, d, tasks.Count, K))
        {
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                statFocus = 0; statRest = 0; statCycles = 0;
                phase = "focus"; cycle = 0; remaining = Total("focus");
                Store.SaveStat(statFocus, statRest, statCycles);
                Render();
            }
        }
    }

    void SaveAll()
    {
        Store.SaveStat(statFocus, statRest, statCycles);
        Store.SaveTasks(tasks);
        Store.SaveConfig(cfg);
    }

    // ---------- 화면 ----------
    void RefreshTasks()
    {
        int d = 0;
        foreach (TaskItem t in tasks) if (t.Done) d++;
        planner.SetProgress(d, tasks.Count);
        planner.List.Invalidate();
    }

    void Render()
    {
        int t = Math.Max(0, (int)Math.Ceiling(remaining));
        string txt = string.Format("{0:00}:{1:00}", t / 60, t % 60);
        if (Theme.SetPhase(phase)) Invalidate(true);

        int n = Math.Min(cfg.Cycles, 12);
        int dotsDone = phase == "long" ? n : Math.Min(cycle, n);
        int dotNow = phase == "focus" && cycle < n ? cycle : -1;
        tv.Phase = phase;
        tv.Set(txt, "다음 · " + NameOf(NextPhase()), Math.Max(0, remaining) / Total(phase), running,
               Fmt.DurShort(statFocus), Fmt.DurShort(statRest), n, dotsDone, dotNow);
        Text = txt + " · " + NameOf(phase);

        // 집중 중에는 설정을 접고 명언을 보여 준다
        bool focusing = running && phase == "focus";
        if (focusing != wasFocusing)
        {
            wasFocusing = focusing;
            if (focusing) quote.Set(Quotes.Next());
            slotTarget = focusing ? 1 : 0;
            lastAnim = DateTime.UtcNow;
            anim.Start();
        }
    }

    void AnimStep()
    {
        DateTime now = DateTime.UtcNow;
        double step = (now - lastAnim).TotalSeconds / 0.4;
        lastAnim = now;
        slotP = slotP < slotTarget ? Math.Min(slotTarget, slotP + step) : Math.Max(slotTarget, slotP - step);
        ApplySlot();
        if (slotP == slotTarget) anim.Stop();
    }

    void ApplySlot()
    {
        double e = slotP * slotP * (3 - 2 * slotP);
        int h = slot.Height, sh = (int)Math.Round(h * (1 - e));
        settings.SetBounds(0, 0, slot.Width, Math.Max(0, sh));
        quote.SetBounds(0, sh, slot.Width, Math.Max(0, h - sh));
        settings.Visible = sh > 2;
        quote.Visible = h - sh > 2;
    }
}
