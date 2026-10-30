using EyeCarer;

int count = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); count++; }
var schedule = new Schedule(900, 0);
Check(!schedule.TakeDue(899.99), "No early first break");
Check(schedule.TakeDue(900) && schedule.Next == 1800, "First deadline and next start at 900 seconds");
Check(schedule.Next - 915 == 885, "15-second break leaves 885 seconds");
Check(!schedule.TakeDue(915), "Dismiss/finish does not change next deadline");
Check(schedule.TakeDue(1800) && schedule.Next == 2700, "Second automatic deadline");
Check(schedule.TakeDue(10000) && schedule.Next == 10900, "Late wake discards catch-up queue");
schedule.Suspend();
Check(!schedule.TakeDue(20000) && double.IsPositiveInfinity(schedule.Delay(20000)), "Lock/suspend stops schedule");
schedule.Reset(20000);
Check(!schedule.TakeDue(20001) && schedule.Next == 20900, "Unlock/resume starts full interval");
schedule.Pause(21000);
Check(schedule.IsPaused(24599) && !schedule.TakeDue(24599), "One-hour pause");
Check(!schedule.IsPaused(24600) && schedule.Next == 25500, "Pause ends with full fresh interval");
schedule.Reset(22000);
Check(!schedule.IsPaused(22000) && schedule.Next == 22900, "Manual resume cancels pause");
var immediate = new Schedule(30, 0, true);
Check(immediate.TakeDue(0) && immediate.Next == 30, "StartImmediately option");
var settings = new Settings { IntervalSeconds = double.NaN, DurationSeconds = -1, BlurRadius = 101, GrainStrength = 5, HazeCoverage = -.1, HazeOpacity = double.PositiveInfinity, CenterText = "" };
settings.Validate();
Check(settings.IntervalSeconds == 900 && settings.DurationSeconds == 15 && settings.BlurRadius == 22, "Invalid timing/blur safely defaulted");
Check(settings.GrainStrength == .7 && settings.HazeCoverage == .5 && settings.HazeOpacity == .65 && settings.CenterText == "Take rest", "Invalid visual settings safely defaulted");
var original = File.Exists(Settings.Path) ? File.ReadAllText(Settings.Path) : null;
try
{
    File.WriteAllText(Settings.Path, "{broken");
    var fallback = Settings.Load();
    Check(fallback.IntervalSeconds == 900 && fallback.DurationSeconds == 15, "Malformed JSON recovers");
    File.WriteAllText(Settings.Path, "null");
    Check(Settings.Load().CenterText == "Take rest", "Null JSON recovers");
    File.WriteAllText(Settings.Path, "{\"FutureSetting\":{\"value\":42},\"IntervalSeconds\":30,\"DurationSeconds\":5,\"HazeCoverage\":0}");
    var preserved = Settings.Load(); preserved.Save();
    Check(File.ReadAllText(Settings.Path).Contains("FutureSetting") && preserved.HazeCoverage == 0, "Atomic save retains unknown fields and zero haze");
    Check(File.ReadAllText(Settings.Path + ".bak").Contains("FutureSetting"), "Atomic replacement keeps previous contents");
    schedule.Pause(100); schedule.Reconfigure(60, 200);
    Check(schedule.IsPaused(200) && schedule.Next == 3760, "Applying interval preserves active pause");
}
finally { if (original != null) File.WriteAllText(Settings.Path, original); else File.Delete(Settings.Path); }
Console.WriteLine($"{count} checks passed.");
