using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EyeCarer;

public sealed class Settings
{
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    public Settings Clone() => JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(this))!;
    public double IntervalSeconds { get; set; } = 900;
    public double DurationSeconds { get; set; } = 15;
    public bool StartImmediately { get; set; }
    public double BlurRadius { get; set; } = 22;
    public double GrainStrength { get; set; } = .70;
    public double HazeCoverage { get; set; } = .50;
    public double HazeOpacity { get; set; } = .65;
    public bool ShowCenterText { get; set; } = true;
    public string CenterText { get; set; } = "Take rest";
    public bool StartWithWindows { get; set; } = true;
    public bool EnableAnimations { get; set; } = true;
    public static string Path => System.IO.Path.Combine(AppContext.BaseDirectory, "config.json");

    public static Settings Load()
    {
        Settings settings;
        try { settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path)) ?? new(); }
        catch (Exception e) { Log.Error("config", e); settings = new(); }
        settings.Validate();
        return settings;
    }

    public void Validate()
    {
        IntervalSeconds = Safe(IntervalSeconds, 30, 7200, 900);
        DurationSeconds = Safe(DurationSeconds, 1, Math.Min(300, IntervalSeconds - .001), Math.Min(15, IntervalSeconds - 1));
        BlurRadius = Safe(BlurRadius, 0, 50, 22);
        GrainStrength = Safe(GrainStrength, 0, 1, .7);
        HazeCoverage = Safe(HazeCoverage, 0, 1, .5);
        HazeOpacity = Safe(HazeOpacity, 0, 1, .65);
        if (string.IsNullOrWhiteSpace(CenterText)) CenterText = "Take rest";
        if (CenterText.Length > 80) CenterText = CenterText.Substring(0, 80);
    }

    private static double Safe(double value, double min, double max, double fallback) =>
        double.IsFinite(value) && value >= min && value <= max ? value : fallback;

    public void Save()
    {
        var temporary = Path + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, this, new JsonSerializerOptions { WriteIndented = true });
                stream.Flush(true);
            }
            if (File.Exists(Path)) File.Replace(temporary, Path, Path + ".bak");
            else File.Move(temporary, Path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

internal static class Log
{
    private static readonly object Gate = new();
    public static bool Diagnostics { get; set; }
    public static void Error(string area, Exception e) => Write("error.log", $"{area}: {e.GetType().Name} (0x{e.HResult:X8})");
    public static void Event(string text) { if (Diagnostics) Write("diagnostics.log", text); }
    private static void Write(string file, string text)
    {
        try
        {
            lock (Gate)
            {
                var path = System.IO.Path.Combine(AppContext.BaseDirectory, file);
                if (File.Exists(path) && new FileInfo(path).Length > 262144) File.WriteAllText(path, "");
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} {text}{Environment.NewLine}");
            }
        }
        catch { /* Logging must never keep an overlay alive. */ }
    }
}
