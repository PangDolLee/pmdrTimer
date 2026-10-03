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

        // 시스템 기본 브라우저로 연다
        try
        {
            Process.Start(new ProcessStartInfo(new Uri(html).AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception)
        {
            MessageBox.Show("기본 브라우저를 열 수 없습니다.", "포모도로 타이머");
        }
    }
}
