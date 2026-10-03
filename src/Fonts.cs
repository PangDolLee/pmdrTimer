using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;

// exe에 내장한 Pretendard(Variable 폰트의 Regular 400 / Bold 700 인스턴스)를 불러온다.
// GDI+는 가변 폰트의 축을 직접 다루지 못하므로 두 굵기를 고정 인스턴스로 만들어 넣었다.
static class FontLoader
{
    [DllImport("gdi32.dll")]
    static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, out uint pcFonts);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr CreateFontW(int h, int w, int esc, int ori, int weight, uint italic, uint underline, uint strike,
        uint charset, uint outPrec, uint clipPrec, uint quality, uint pitchFamily, string face);

    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    static readonly System.Collections.Generic.Dictionary<string, IntPtr> gdiFonts = new System.Collections.Generic.Dictionary<string, IntPtr>();
    static PrivateFontCollection collection;
    static FontFamily family;

    public static FontFamily Family { get { return family; } }

    public static void Load()
    {
        try
        {
            collection = new PrivateFontCollection();
            foreach (string name in new[] { "Pretendard-Regular.ttf.gz", "Pretendard-Bold.ttf.gz" })
            {
                byte[] data = Read(name);
                IntPtr p = Marshal.AllocCoTaskMem(data.Length);   // 프로세스가 끝날 때까지 유지
                Marshal.Copy(data, 0, p, data.Length);
                collection.AddMemoryFont(p, data.Length);
                try { uint n; AddFontMemResourceEx(p, (uint)data.Length, IntPtr.Zero, out n); } catch (Exception) { }  // TextBox(GDI)용
            }
            if (collection.Families.Length > 0) family = collection.Families[0];
            if (family != null && !Usable(family)) family = null;
        }
        catch (Exception) { family = null; }
    }

    // 불러온 글꼴이 실제로 측정·그리기에 쓸 수 있는지 확인한다 (안 되면 시스템 글꼴로 대체)
    static bool Usable(FontFamily f)
    {
        try
        {
            using (Bitmap bmp = new Bitmap(4, 4))
            using (Graphics g = Graphics.FromImage(bmp))
            foreach (FontStyle st in new[] { FontStyle.Regular, FontStyle.Bold })
                using (Font t = new Font(f, 12, st, GraphicsUnit.Pixel)) t.GetHeight(g);
            return true;
        }
        catch (Exception) { return false; }
    }

    // TextBox는 GDI로 글자를 그려서 GDI+ 전용 글꼴 객체를 쓸 수 없다. 같은 이름으로 GDI 글꼴을 직접 만들어 지정한다.
    // 높이 계산용 Font는 시스템 글꼴로 두고, 등록에 실패하면 그 글꼴이 그대로 쓰인다.
    public static void ApplyTo(System.Windows.Forms.TextBox tb, float px, bool bold)
    {
        px = Math.Max(1f, px);
        tb.Font = new Font(Theme.Family, px, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        if (family == null) return;
        try
        {
            string key = ((int)Math.Round(px)) + (bold ? "b" : "r");
            IntPtr h;
            if (!gdiFonts.TryGetValue(key, out h))
            {
                h = CreateFontW(-(int)Math.Round(px), 0, 0, 0, bold ? 700 : 400, 0, 0, 0, 1, 0, 0, 5, 0, family.Name);
                gdiFonts[key] = h;
            }
            if (h != IntPtr.Zero) SendMessage(tb.Handle, 0x30, h, (IntPtr)1);   // WM_SETFONT
        }
        catch (Exception) { }
    }

    static byte[] Read(string name)
    {
        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
        using (GZipStream z = new GZipStream(s, CompressionMode.Decompress))
        using (MemoryStream ms = new MemoryStream())
        {
            byte[] buf = new byte[65536];
            int n;
            while ((n = z.Read(buf, 0, buf.Length)) > 0) ms.Write(buf, 0, n);
            return ms.ToArray();
        }
    }
}
