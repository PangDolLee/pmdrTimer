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
using System.Net.WebSockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

// 화면(index.html)을 Edge/Chrome 앱 창으로 띄우고, 창을 닫으려 하면 "세션 종료" 때와 같은 요약과 함께
// 닫을지 묻는 확인 창을 보여 준다. 취소하면 창이 닫히지 않는다.
//  - 화면은 127.0.0.1의 임시 포트로 상태(집중·휴식 시간 등)를 주기적으로 알려 준다.
//  - 창을 닫으려 하면 화면의 beforeunload 확인이 열리는데, 브라우저의 기본 확인 창은 문구를 바꿀 수 없다.
//    그래서 브라우저를 로컬 디버깅 포트(127.0.0.1, 임의 포트)로 열고, 이 실행기가 그 확인을 가로채
//    요약이 담긴 확인 창을 대신 띄운 뒤 사용자의 선택(종료/계속 공부하기)을 브라우저에 전달한다.
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
        int debugPort = port > 0 ? FreePort() : 0;

        // --app: 주소창·탭 없이 앱처럼 뜨는 창. 전용 프로필을 써서 설정/할 일이 그대로 저장된다
        string url = new Uri(html).AbsoluteUri + (debugPort > 0 ? "#lp=" + port : "");
        string args = "--app=\"" + url + "\""
                    + " --user-data-dir=\"" + Path.Combine(dir, "profile") + "\""
                    + " --window-size=1100,860 --no-first-run --no-default-browser-check";
        if (debugPort > 0) args += " --remote-debugging-port=" + debugPort;
        string extra = Environment.GetEnvironmentVariable("POMODORO_BROWSER_ARGS");
        if (!string.IsNullOrEmpty(extra)) args += " " + extra;
        Process.Start(browser, args);

        if (debugPort > 0) Application.Run(new Watcher(server, debugPort));
    }

    // 문제 확인용 기록 (%APPDATA%\PomodoroTimer\launcher.log)
    public static void Log(string text)
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PomodoroTimer");
            File.AppendAllText(Path.Combine(dir, "launcher.log"), DateTime.Now.ToString("s") + " " + text + "\r\n");
        }
        catch (Exception) { }
    }

    static int FreePort()
    {
        try
        {
            TcpListener l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int p = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return p;
        }
        catch (Exception) { return 0; }
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
    Summary last;

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
        lock (gate) { last = s; }
    }

    public Summary Snapshot()
    {
        lock (gate) { return last; }
    }
}

// 화면이 없는 감시자: 브라우저의 디버깅 연결로 "창을 닫으려 함" 확인을 가로채 요약 확인 창을 대신 보여 준다
class Watcher : ApplicationContext
{
    readonly StateServer server;
    readonly int debugPort;
    readonly System.Windows.Forms.Timer starter = new System.Windows.Forms.Timer();
    readonly Control ui = new Control();                       // 다른 스레드에서 화면 작업을 UI 스레드로 넘기기 위한 용도
    readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);   // 웹소켓은 한 번에 하나씩만 보낼 수 있다
    int nextId;
    SummaryForm form;                                         // UI 스레드에서만 다룬다

    public Watcher(StateServer s, int port)
    {
        server = s; debugPort = port;
        ui.CreateControl();
        IntPtr unused = ui.Handle;
        starter.Interval = 300;
        starter.Tick += delegate { starter.Stop(); Run(); };
        starter.Start();
    }

    void OnUi(Action a)
    {
        try { ui.BeginInvoke((MethodInvoker)delegate { try { a(); } catch (Exception ex) { Launcher.Log("ui: " + ex); } }); }
        catch (Exception ex) { Launcher.Log("invoke: " + ex); }
    }

    async void Run()
    {
        try
        {
            string wsUrl = await FindTarget();
            if (wsUrl == null) { OnUi(ExitThread); return; }
            using (ClientWebSocket ws = new ClientWebSocket())
            {
                await ws.ConnectAsync(new Uri(wsUrl), CancellationToken.None);
                await Send(ws, "Page.enable", "{}");
                // beforeunload 확인은 사용자가 화면을 한 번이라도 조작한 뒤에만 뜨므로, 시작 직후 빈 곳을 한 번 눌러 둔다
                await Send(ws, "Input.dispatchMouseEvent", "{\"type\":\"mousePressed\",\"x\":2,\"y\":2,\"button\":\"left\",\"clickCount\":1}");
                await Send(ws, "Input.dispatchMouseEvent", "{\"type\":\"mouseReleased\",\"x\":2,\"y\":2,\"button\":\"left\",\"clickCount\":1}");

                while (true)
                {
                    string msg = await Receive(ws);
                    if (msg == null) break;
                    if (Environment.GetEnvironmentVariable("POMODORO_DEBUG") != null) Launcher.Log("cdp: " + (msg.Length > 200 ? msg.Substring(0, 200) : msg));
                    if (msg.Contains("Page.javascriptDialogOpening") && msg.Contains("\"beforeunload\""))
                    {
                        // 브라우저 기본 확인 창은 바로 닫아 창 닫기를 일단 보류하고, 요약이 담긴 우리 확인 창을 띄운다
                        await Send(ws, "Page.handleJavaScriptDialog", "{\"accept\":false}");
                        await Task.Delay(200);   // 화면이 보낸 최신 상태가 도착할 시간
                        Summary sum = server.Snapshot() ?? new Summary();
                        OnUi(delegate { ShowConfirm(ws, sum); });
                    }
                }
            }
        }
        catch (WebSocketException) { }   // 브라우저가 닫히면서 연결이 끊긴 정상적인 경우
        catch (Exception ex) { Launcher.Log("Watcher: " + ex); }
        OnUi(ExitThread);
    }

    void ShowConfirm(ClientWebSocket ws, Summary sum)
    {
        if (form != null && !form.IsDisposed) { form.Activate(); return; }   // 이미 떠 있으면 앞으로만 가져온다
        SummaryForm f = new SummaryForm(sum);
        form = f;
        f.FormClosed += delegate { if (f.CloseApp) CloseBrowser(ws); };
        f.Show();
        f.Activate();
    }

    // 사용자가 "종료"를 고르면 화면의 확인을 건너뛰도록 표시하고 브라우저(이 앱 전용 인스턴스)를 닫는다
    async void CloseBrowser(ClientWebSocket ws)
    {
        try
        {
            await Send(ws, "Runtime.evaluate", "{\"expression\":\"window.__allowClose=true\"}");
            string json = await Task.Run(() => HttpGet("/json/version"));
            Match m = Regex.Match(json, "\"webSocketDebuggerUrl\"\\s*:\\s*\"([^\"]+)\"");
            if (!m.Success) { await Send(ws, "Page.close", "{}"); return; }
            using (ClientWebSocket b = new ClientWebSocket())
            {
                await b.ConnectAsync(new Uri(m.Groups[1].Value), CancellationToken.None);
                byte[] cmd = Encoding.UTF8.GetBytes("{\"id\":1,\"method\":\"Browser.close\"}");
                await b.SendAsync(new ArraySegment<byte>(cmd), WebSocketMessageType.Text, true, CancellationToken.None);
                try { await Receive(b); } catch (WebSocketException) { }
            }
        }
        catch (Exception ex) { Launcher.Log("close: " + ex); }
    }

    string HttpGet(string path)
    {
        HttpWebRequest rq = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + debugPort + path);
        rq.Timeout = 1500;
        using (WebResponse rs = rq.GetResponse())
        using (StreamReader sr = new StreamReader(rs.GetResponseStream(), Encoding.UTF8))
            return sr.ReadToEnd();
    }

    // 브라우저가 디버깅 포트를 열 때까지 기다렸다가 우리 화면의 연결 주소를 찾는다
    async Task<string> FindTarget()
    {
        for (int i = 0; i < 100; i++)
        {
            try
            {
                string json = await Task.Run(() => HttpGet("/json"));
                Match m = Regex.Match(json, "\"type\"\\s*:\\s*\"page\",\\s*\"url\"\\s*:\\s*\"[^\"]*index\\.html[^\"]*\",\\s*\"webSocketDebuggerUrl\"\\s*:\\s*\"([^\"]+)\"");
                if (m.Success) return m.Groups[1].Value;
            }
            catch (Exception) { }
            await Task.Delay(300);
        }
        return null;
    }

    async Task Send(ClientWebSocket ws, string method, string parameters)
    {
        string json = "{\"id\":" + Interlocked.Increment(ref nextId) + ",\"method\":\"" + method + "\",\"params\":" + parameters + "}";
        await sendLock.WaitAsync();
        try { await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, CancellationToken.None); }
        finally { sendLock.Release(); }
    }

    static async Task<string> Receive(ClientWebSocket ws)
    {
        byte[] buf = new byte[16384];
        StringBuilder sb = new StringBuilder();
        WebSocketReceiveResult r;
        do
        {
            r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
            if (r.MessageType == WebSocketMessageType.Close) return null;
            sb.Append(Encoding.UTF8.GetString(buf, 0, r.Count));
        } while (!r.EndOfMessage);
        return sb.ToString();
    }
}

// 세션 종료 대화상자와 같은 내용의 요약 창
class SummaryForm : Form
{
    public bool CloseApp;   // "종료"를 골랐는지 (창의 X 버튼이나 Esc는 계속 공부하기와 같다)
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

        Button keep = MakeButton("계속 공부하기", 30, false);
        keep.Click += delegate { CloseApp = false; Close(); };
        Button quit = MakeButton("종료", 225, true);
        quit.Click += delegate { CloseApp = true; Close(); };
        Controls.Add(keep); Controls.Add(quit);
        AcceptButton = keep;
        CancelButton = keep;
    }

    Button MakeButton(string label, int x, bool primary)
    {
        Button b = new Button();
        b.Text = label;
        b.FlatStyle = FlatStyle.Flat;
        b.Font = new Font(family, 14 * K, FontStyle.Bold, GraphicsUnit.Pixel);
        if (primary)
        {
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = Color.FromArgb(255, 107, 107);
            b.ForeColor = Color.White;
        }
        else
        {
            b.FlatAppearance.BorderColor = track;
            b.BackColor = bg;
            b.ForeColor = text;
        }
        b.SetBounds((int)(x * K), (int)(328 * K), (int)(185 * K), (int)(46 * K));
        return b;
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
        Label(g, "오늘의 공부 기록입니다. 창을 닫을까요?", 13, false, muted, new RectangleF(30 * k, 62 * k, 380 * k, 22 * k), StringAlignment.Near);

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
