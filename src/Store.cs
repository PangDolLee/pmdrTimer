using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

class TaskItem
{
    public string Text;
    public bool Done;
}

class Config
{
    public int Focus = 25, Short = 5, Long = 15, Cycles = 4, Volume = 70;
    public bool Auto = true, Sound = true;
}

// %APPDATA%\PomodoroTimer 아래에 설정·할 일·통계를 저장한다.
static class Store
{
    static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PomodoroTimer");

    static string PathOf(string name)
    {
        try { Directory.CreateDirectory(Dir); } catch (Exception) { }
        return Path.Combine(Dir, name);
    }

    static Dictionary<string, string> ReadIni(string name)
    {
        Dictionary<string, string> d = new Dictionary<string, string>();
        try
        {
            string p = PathOf(name);
            if (File.Exists(p))
                foreach (string line in File.ReadAllLines(p, Encoding.UTF8))
                {
                    int i = line.IndexOf('=');
                    if (i > 0) d[line.Substring(0, i)] = line.Substring(i + 1);
                }
        }
        catch (Exception) { }
        return d;
    }

    static void WriteIni(string name, Dictionary<string, string> d)
    {
        try
        {
            List<string> lines = new List<string>();
            foreach (KeyValuePair<string, string> kv in d) lines.Add(kv.Key + "=" + kv.Value);
            File.WriteAllLines(PathOf(name), lines.ToArray(), Encoding.UTF8);
        }
        catch (Exception) { }
    }

    static int Int(Dictionary<string, string> d, string k, int def, int lo, int hi)
    {
        int v;
        if (d.ContainsKey(k) && int.TryParse(d[k], out v)) return Math.Min(hi, Math.Max(lo, v));
        return def;
    }

    static bool Bool(Dictionary<string, string> d, string k, bool def)
    {
        return d.ContainsKey(k) ? d[k] == "1" : def;
    }

    public static Config LoadConfig()
    {
        Dictionary<string, string> d = ReadIni("settings.ini");
        Config c = new Config();
        c.Focus = Int(d, "focus", c.Focus, 1, 180);
        c.Short = Int(d, "short", c.Short, 1, 60);
        c.Long = Int(d, "long", c.Long, 1, 120);
        c.Cycles = Int(d, "cycles", c.Cycles, 1, 12);
        c.Volume = Int(d, "volume", c.Volume, 0, 100);
        c.Auto = Bool(d, "auto", c.Auto);
        c.Sound = Bool(d, "sound", c.Sound);
        return c;
    }

    public static void SaveConfig(Config c)
    {
        Dictionary<string, string> d = new Dictionary<string, string>();
        d["focus"] = c.Focus.ToString(); d["short"] = c.Short.ToString(); d["long"] = c.Long.ToString();
        d["cycles"] = c.Cycles.ToString(); d["volume"] = c.Volume.ToString();
        d["auto"] = c.Auto ? "1" : "0"; d["sound"] = c.Sound ? "1" : "0";
        WriteIni("settings.ini", d);
    }

    public static void LoadStat(out double focus, out double rest, out int cycles)
    {
        Dictionary<string, string> d = ReadIni("stat.ini");
        double f = 0, r = 0; int c = 0;
        if (d.ContainsKey("focus")) double.TryParse(d["focus"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f);
        if (d.ContainsKey("rest")) double.TryParse(d["rest"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out r);
        if (d.ContainsKey("cycles")) int.TryParse(d["cycles"], out c);
        focus = Math.Max(0, f); rest = Math.Max(0, r); cycles = Math.Max(0, c);
    }

    public static void SaveStat(double focus, double rest, int cycles)
    {
        Dictionary<string, string> d = new Dictionary<string, string>();
        d["focus"] = focus.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        d["rest"] = rest.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        d["cycles"] = cycles.ToString();
        WriteIni("stat.ini", d);
    }

    public static List<TaskItem> LoadTasks()
    {
        List<TaskItem> list = new List<TaskItem>();
        try
        {
            string p = PathOf("tasks.txt");
            if (File.Exists(p))
                foreach (string line in File.ReadAllLines(p, Encoding.UTF8))
                    if (line.Length > 2 && line[1] == '|')
                    {
                        TaskItem t = new TaskItem();
                        t.Done = line[0] == '1';
                        t.Text = line.Substring(2);
                        list.Add(t);
                    }
        }
        catch (Exception) { }
        return list;
    }

    public static void SaveTasks(List<TaskItem> tasks)
    {
        try
        {
            List<string> lines = new List<string>();
            foreach (TaskItem t in tasks)
                lines.Add((t.Done ? "1|" : "0|") + t.Text.Replace('\r', ' ').Replace('\n', ' '));
            File.WriteAllLines(PathOf("tasks.txt"), lines.ToArray(), Encoding.UTF8);
        }
        catch (Exception) { }
    }
}
