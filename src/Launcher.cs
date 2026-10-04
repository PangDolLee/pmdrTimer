using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

// 화면(index.html)을 Edge/Chrome 앱 창으로 띄우고, 창이 닫히면 "세션 종료" 때와 같은 요약을 보여 준다.
// 화면은 127.0.0.1의 임시 포트로 상태(집중·휴식 시간 등)를 주기적으로 알려 주고,
// 창이 닫힐 때(pagehide) 마지막 상태를 한 번 더 보낸다. 브라우저는 창을 닫을 때 직접 대화상자를 띄울 수 없어서
// 이 실행기가 대신 요약 창을 보여 준다.
static class Launcher
{
    [DllImport("user32.dll")]
    static extern bool SetProcessDPIAware();

    [STAThread]
    static void Main()
    {
        try { SetProcessDPIAware(); } catch (Exception) { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PomodoroTimer");
        Directory.CreateDirectory(dir);

        // 내장된 화면(index.html)을 꺼내 둔다
        string html = Path.Combine(dir, "index.html");
        using (Stream r = Assembly.GetExecutingAssembly().GetManifestResourceStream("index.html"))
        using (FileStream f = File.Create(html))
            r.CopyTo(f);

        // POMODORO_BROWSER / POMODORO_BROWSER_ARGS: 개발·테스트용 (브라우저 경로와 추가 인자 지정)
        string browser = Environment.GetEnvironmentVariable("POMODORO_BROWSER");
        if (string.IsNullOrEmpty(browser)) browser = Find();
        if (browser == null)
        {
            MessageBox.Show("Microsoft Edge 또는 Google Chrome이 필요합니다.", "포모도로 타이머");
            return;
        }

        StateServer server = new StateServer();
        int port = server.Start();

        // --app: 주소창·탭 없이 앱처럼 뜨는 창. 전용 프로필을 써서 설정/할 일이 그대로 저장된다
        string url = new Uri(html).AbsoluteUri + (port > 0 ? "#lp=" + port : "");
        string args = "--app=\"" + url + "\""
                    + " --user-data-dir=\"" + Path.Combine(dir, "profile") + "\""
                    + " --window-size=1100,860 --no-first-run --no-default-browser-check";
        string extra = Environment.GetEnvironmentVariable("POMODORO_BROWSER_ARGS");
        if (!string.IsNullOrEmpty(extra)) args += " " + extra;
        Process.Start(browser, args);

        if (port > 0) Application.Run(new Watcher(server));
    }

    static string Find()
    {
        string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] candidates = {
            Path.Combine(pf86, @"Microsoft\Edge\Application\msedge.exe"),
            Path.Combine(pf,   @"Microsoft\Edge\Application\msedge.exe"),
            Path.Combine(pf,   @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(pf86, @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(local, @"Google\Chrome\Application\chrome.exe"),
        };
        foreach (string c in candidates) if (File.Exists(c)) return c;
        return null;
    }
}

class Summary
{
    public double Focus, Rest;
    public int Cycles, Done, Total;
    public bool Dark;
}

// 화면이 보내는 상태를 받는 아주 작은 HTTP 서버 (127.0.0.1 전용)
class StateServer
{
    TcpListener listener;
    readonly object gate = new object();
    readonly DateTime started = DateTime.UtcNow;
    Summary last;
    DateTime lastBeat = DateTime.MinValue, closingAt = DateTime.MinValue;

    public int Start()
    {
        for (int port = 47633; port < 47663; port++)
        {
            try
            {
                listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                Thread t = new Thread(Loop);
                t.IsBackground = true;
                t.Start();
                return port;
            }
            catch (Exception) { listener = null; }
        }
        return 0;
    }

    void Loop()
    {
        while (true)
        {
            TcpClient c;
            try { c = listener.AcceptTcpClient(); }
            catch (Exception) { return; }
            ThreadPool.QueueUserWorkItem(Handle, c);
        }
    }

    void Handle(object o)
    {
        try
        {
            using (TcpClient c = (TcpClient)o)
            {
                c.ReceiveTimeout = 3000;
                Stream st = c.GetStream();
                byte[] buf = new byte[32768];
                int len = 0, headerEnd = -1;
                while (len < buf.Length)
                {
                    int n = st.Read(buf, len, buf.Length - len);
                    if (n <= 0) break;
                    len += n;
                    headerEnd = IndexOf(buf, len, new byte[] { 13, 10, 13, 10 });
                    if (headerEnd >= 0) break;
                }
                if (headerEnd < 0) return;
                string head = Encoding.ASCII.GetString(buf, 0, headerEnd);
                Match m = Regex.Match(head, @"Content-Length:\s*(\d+)", RegexOptions.IgnoreCase);
                int want = m.Success ? Math.Min(int.Parse(m.Groups[1].Value), 8192) : 0;
                int bodyStart = headerEnd + 4;
                while (len - bodyStart < want && len < buf.Length)
                {
                    int n = st.Read(buf, len, buf.Length - len);
                    if (n <= 0) break;
                    len += n;
                }
                if (head.StartsWith("POST", StringComparison.OrdinalIgnoreCase))
                    Update(Encoding.UTF8.GetString(buf, bodyStart, Math.Max(0, len - bodyStart)));

                byte[] resp = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 204 No Content\r\nAccess-Control-Allow-Origin: *\r\nAccess-Control-Allow-Private-Network: true\r\n" +
                    "Access-Control-Allow-Methods: POST, OPTIONS\r\nAccess-Control-Allow-Headers: *\r\nConnection: close\r\nContent-Length: 0\r\n\r\n");
                st.Write(resp, 0, resp.Length);
            }
        }
        catch (Exception) { }
    }

    static int IndexOf(byte[] b, int len, byte[] pat)
    {
        for (int i = 0; i + pat.Length <= len; i++)
        {
            bool ok = true;
            for (int j = 0; j < pat.Length; j++) if (b[i + j] != pat[j]) { ok = false; break; }
            if (ok) return i;
        }
        return -1;
    }

    static double Num(string body, string key)
    {
        Match m = Regex.Match(body, "\"" + key + "\":\\s*(-?[0-9.eE+-]+)");
        double v;
        return m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0;
    }

    void Update(string body)
    {
        Summary s = new Summary();
        s.Focus = Num(body, "focus"); s.Rest = Num(body, "rest");
        s.Cycles = (int)Num(body, "cycles"); s.Done = (int)Num(body, "done"); s.Total = (int)Num(body, "total");
        s.Dark = body.Contains("\"dark\":true");
        bool final = body.Contains("\"final\":true");
        lock (gate)
        {
            last = s;
            lastBeat = DateTime.UtcNow;
            closingAt = final ? DateTime.UtcNow : DateTime.MinValue;   // 새로고침이면 곧 다음 신호가 와서 취소된다
        }
    }

    // 창이 닫혔다고 볼 수 있는지: 닫힘 신호 뒤 2.5초 동안 새 신호가 없거나, 5분 넘게 아무 신호가 없을 때
    public bool Closed(out Summary s)
    {
        lock (gate)
        {
            s = last;
            if (last == null) return false;
            DateTime now = DateTime.UtcNow;
            if (closingAt != DateTime.MinValue) return (now - closingAt).TotalSeconds >= 2.5;
            return (now - lastBeat).TotalMinutes >= 5;
        }
    }

    public bool NeverStarted(int seconds)
    {
        lock (gate) { return last == null && (DateTime.UtcNow - started).TotalSeconds >= seconds; }
    }
}

// 화면이 없는 감시자: 창이 닫히면 요약을 보여 주고 끝낸다
class Watcher : ApplicationContext
{
    readonly StateServer server;
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();

    public Watcher(StateServer s)
    {
        server = s;
        timer.Interval = 500;
        timer.Tick += delegate
        {
            Summary sum;
            if (server.Closed(out sum))
            {
                timer.Stop();
                using (SummaryForm f = new SummaryForm(sum)) f.ShowDialog();
                ExitThread();
            }
            else if (server.NeverStarted(90)) { timer.Stop(); ExitThread(); }
        };
        timer.Start();
    }
}

// 세션 종료 대화상자와 같은 내용의 요약 창
class SummaryForm : Form
{
    readonly Summary s;
    readonly float K;
    readonly Color bg, card, track, text, muted;
    readonly string family;

    static string Dur(double sec)
    {
        int t = (int)Math.Round(sec), h = t / 3600, m = t % 3600 / 60, x = t % 60;
        if (h > 0) return h + "시간 " + m + "분";
        if (m > 0) return m + "분 " + x + "초";
        return x + "초";
    }

    public SummaryForm(Summary sum)
    {
        s = sum;
        float k = 1f;
        try { using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) k = Math.Max(1f, g.DpiX / 96f); } catch (Exception) { }
        K = k;
        if (s.Dark)
        {
            bg = Color.FromArgb(13, 15, 20); card = Color.FromArgb(32, 37, 50); track = Color.FromArgb(48, 54, 70);
            text = Color.FromArgb(236, 239, 246); muted = Color.FromArgb(140, 149, 168);
        }
        else
        {
            bg = Color.FromArgb(243, 244, 248); card = Color.FromArgb(228, 230, 236); track = Color.FromArgb(220, 223, 232);
            text = Color.FromArgb(20, 23, 31); muted = Color.FromArgb(106, 114, 130);
        }
        family = "Malgun Gothic";
        try { using (Font probe = new Font(family, 12)) { if (probe.Name != family) family = "Segoe UI"; } } catch (Exception) { family = "Segoe UI"; }

        Text = "포모도로 타이머";
        BackColor = bg;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        ClientSize = new Size((int)(440 * K), (int)(396 * K));
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

        Button ok = new Button();
        ok.Text = "확인";
        ok.FlatStyle = FlatStyle.Flat;
        ok.FlatAppearance.BorderSize = 0;
        ok.BackColor = Color.FromArgb(255, 107, 107);
        ok.ForeColor = Color.White;
        ok.Font = new Font(family, 14 * K, FontStyle.Bold, GraphicsUnit.Pixel);
        ok.SetBounds((int)(30 * K), (int)(328 * K), (int)(380 * K), (int)(46 * K));
        ok.Click += delegate { Close(); };
        Controls.Add(ok);
        AcceptButton = ok;
    }

    void Label(Graphics g, string str, float px, bool bold, Color c, RectangleF r, StringAlignment h)
    {
        using (Font f = new Font(family, px * K, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
        using (SolidBrush b = new SolidBrush(c))
        using (StringFormat sf = new StringFormat(StringFormatFlags.NoWrap))
        {
            sf.Alignment = h; sf.LineAlignment = StringAlignment.Center;
            g.DrawString(str, f, b, r, sf);
        }
    }

    static GraphicsPath Round(RectangleF r, float rad)
    {
        GraphicsPath p = new GraphicsPath();
        float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.Clear(bg);
        float k = K;
        Label(g, "수고하셨어요", 22, true, text, new RectangleF(30 * k, 26 * k, 380 * k, 34 * k), StringAlignment.Near);
        Label(g, "오늘의 공부 기록입니다.", 13, false, muted, new RectangleF(30 * k, 62 * k, 380 * k, 22 * k), StringAlignment.Near);

        string[] cap = { "총 집중 시간", "총 휴식 시간" };
        string[] val = { Dur(s.Focus), Dur(s.Rest) };
        Color[] col = s.Dark ? new[] { Color.FromArgb(255, 138, 138), Color.FromArgb(79, 224, 180) }
                             : new[] { Color.FromArgb(229, 72, 77), Color.FromArgb(18, 163, 127) };
        for (int i = 0; i < 2; i++)
        {
            RectangleF r = new RectangleF(30 * k + i * 195 * k, 104 * k, 185 * k, 80 * k);
            using (GraphicsPath p = Round(r, 18 * k)) using (SolidBrush b = new SolidBrush(card)) g.FillPath(b, p);
            Label(g, cap[i], 12, false, muted, new RectangleF(r.X + 16 * k, r.Y + 12 * k, r.Width - 24 * k, 20 * k), StringAlignment.Near);
            Label(g, val[i], 22, true, col[i], new RectangleF(r.X + 16 * k, r.Y + 36 * k, r.Width - 24 * k, 34 * k), StringAlignment.Near);
        }

        double sum = s.Focus + s.Rest;
        if (sum <= 0) sum = 1;
        RectangleF bar = new RectangleF(30 * k, 204 * k, 380 * k, 10 * k);
        float fw = (float)(bar.Width * s.Focus / sum);
        using (GraphicsPath clip = Round(bar, 5 * k))
        {
            Region old = g.Clip;
            g.SetClip(clip);
            using (SolidBrush b = new SolidBrush(track)) g.FillRectangle(b, bar);
            using (SolidBrush b = new SolidBrush(col[0])) g.FillRectangle(b, bar.X, bar.Y, fw, bar.Height);
            using (SolidBrush b = new SolidBrush(col[1])) g.FillRectangle(b, bar.X + fw, bar.Y, bar.Width - fw, bar.Height);
            g.Clip = old;
        }

        string[] rowL = { "완료한 사이클", "완료한 할 일" };
        string[] rowV = { s.Cycles + "회", s.Done + " / " + s.Total };
        for (int i = 0; i < 2; i++)
        {
            RectangleF r = new RectangleF(30 * k, 232 * k + i * 38 * k, 380 * k, 32 * k);
            Label(g, rowL[i], 14, false, muted, r, StringAlignment.Near);
            Label(g, rowV[i], 14, true, text, r, StringAlignment.Far);
        }
    }
}
