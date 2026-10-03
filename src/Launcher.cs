using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

static class Launcher
{
    [STAThread]
    static void Main()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PomodoroTimer");
        Directory.CreateDirectory(dir);

        // 내장된 화면(index.html)을 꺼내 둔다
        string html = Path.Combine(dir, "index.html");
        using (Stream r = Assembly.GetExecutingAssembly().GetManifestResourceStream("index.html"))
        using (FileStream f = File.Create(html))
            r.CopyTo(f);

        string browser = Find();
        if (browser == null)
        {
            MessageBox.Show("Microsoft Edge 또는 Google Chrome이 필요합니다.", "포모도로 타이머");
            return;
        }

        // --app: 주소창·탭 없이 앱처럼 뜨는 창. 전용 프로필을 써서 설정/할 일이 그대로 저장된다
        string args = "--app=\"" + new Uri(html).AbsoluteUri + "\""
                    + " --user-data-dir=\"" + Path.Combine(dir, "profile") + "\""
                    + " --window-size=1100,860 --no-first-run --no-default-browser-check";
        Process.Start(browser, args);
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
