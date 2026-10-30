using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace EyeCarer;

public partial class App : Application
{
    private const string InstanceName = "Local\\EyeCarer.30F702CA";
    private static readonly string PipeName = "EyeCarer.30F702CA." + Environment.UserName;
    private Mutex? mutex;
    private Settings settings = new();
    private SettingsWindow? settingsWindow;
    private void OpenSettings()
    {
        if(settingsWindow != null) { if(settingsWindow.WindowState == WindowState.Minimized) settingsWindow.WindowState=WindowState.Normal; settingsWindow.Activate(); return; }
        var current=settings.Clone(); current.StartWithWindows=StartupShortcut.Enabled;
        settingsWindow=new SettingsWindow(current, ApplySettings, draft=> { draft.DurationSeconds=5; StartBreak("preview",draft); }, () =>
        {
            var paused=schedule.IsPaused(clock.Elapsed.TotalSeconds);
            return "Status: " + (overlays.Count>0?"Resting":paused?"Paused":"Running") + "   ·   Next Rest: " + (paused?"Paused":TimeSpan.FromSeconds(Math.Max(0,schedule.Next-clock.Elapsed.TotalSeconds)).ToString(@"hh\:mm\:ss"));
        });
        settingsWindow.Closed+=(_,_)=>settingsWindow=null;
        settingsWindow.Show();
    }
    private void ApplySettings(Settings draft)
    {
        var previous=settings.Clone(); bool startupBefore=StartupShortcut.Enabled;
        draft.Save();
        try
        {
            if(draft.StartWithWindows != startupBefore) StartupShortcut.Set(draft.StartWithWindows);
            if(StartupShortcut.Enabled != draft.StartWithWindows) throw new IOException("Startup state mismatch");
        }
        catch { previous.StartWithWindows=startupBefore; previous.Save(); StartupShortcut.Set(startupBefore); throw; }
        settings=draft;
        schedule.Reconfigure(settings.IntervalSeconds,clock.Elapsed.TotalSeconds); Arm(); RefreshMenu();
    }
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private Schedule schedule = null!;
    private Forms.NotifyIcon? tray;
    private System.Drawing.Icon? icon;
    private Forms.ContextMenuStrip? menu;
    private Forms.ToolStripMenuItem pauseItem = null!, resumeItem = null!, startupItem = null!;
    private readonly List<OverlayWindow> overlays = new();
    private readonly CancellationTokenSource stopping = new();
    private DispatcherTimer wake = null!, finish = null!, fade = null!;
    private bool exiting, locked, sleeping, preview;
    private int breakCount;
    private IntPtr previousForeground;
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        mutex = new Mutex(true, InstanceName, out bool first);
        if (!first)
        {
            if (e.Args.Length > 0) SendCommand(e.Args[0].TrimStart('-'));
            Shutdown(); return;
        }
        Log.Diagnostics = e.Args.Contains("--diagnostics");
        preview = e.Args.Contains("--preview");
        DispatcherUnhandledException += (_, args) => { Log.Error("dispatcher", args.Exception); EndBreak("error"); args.Handled = true; };
        try
        {
            settings = Settings.Load();
            schedule = new Schedule(settings.IntervalSeconds, clock.Elapsed.TotalSeconds, settings.StartImmediately);
            wake = Timer(OnWake); finish = Timer(() => EndBreak("elapsed")); fade = Timer(() => { foreach (var window in overlays) window.FadeOut(); });
            CreateTray();
            try { StartupShortcut.Set(settings.StartWithWindows); } catch (Exception error) { Log.Error("startup", error); }
            SystemEvents.PowerModeChanged += PowerChanged;
            SystemEvents.SessionSwitch += SessionChanged;
            SystemEvents.DisplaySettingsChanged += DisplayChanged;
            _ = Task.Run(Listen);
            Arm();
            Log.Event("started");
            if (e.Args.Contains("--rest")) Dispatcher.BeginInvoke(new Action(() => StartBreak("manual")));
            if (e.Args.Contains("--settings")) Dispatcher.BeginInvoke(new Action(OpenSettings));
        }
        catch (Exception error)
        {
            Log.Error("startup-fatal", error);
            MessageBox.Show("EyeCarer could not start. See error.log in the application folder.", "EyeCarer", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void SendCommand(string command)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
            client.Connect(2000);
            using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, leaveOpen: true);
            writer.WriteLine(command);
            _ = reader.ReadLine();
        }
        catch (Exception error) { Log.Error("command", error); }
    }

    private async Task Listen()
    {
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(stopping.Token);
                using var reader = new StreamReader(pipe, leaveOpen: true);
                using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);
                timeout.CancelAfter(2000);
                var command = await reader.ReadLineAsync(timeout.Token);
                var reply = await Dispatcher.InvokeAsync(() => Command(command ?? ""));
                await writer.WriteLineAsync(reply);
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (!exiting) Log.Error("pipe", error); }
        }
    }

    private string Command(string command)
    {
        switch (command)
        {
            case "settings": OpenSettings(); break;
            case "rest": StartBreak("manual"); break;
            case "pause": Pause(); break;
            case "resume": Resume(); break;
            case "dismiss": EndBreak("command"); break;
            case "exit": Dispatcher.BeginInvoke(new Action(() => Shutdown())); break;
            case "menu": menu?.Show(Forms.Cursor.Position); break;
        }
        RefreshMenu();
        return JsonSerializer.Serialize(new { Running = !exiting, OverlayCount = overlays.Count, BreakCount = breakCount, Paused = schedule.IsPaused(clock.Elapsed.TotalSeconds), Suspended = schedule.Suspended, NextInSeconds = Math.Round(schedule.Next - clock.Elapsed.TotalSeconds, 2), IntervalSeconds = settings.IntervalSeconds, DurationSeconds = settings.DurationSeconds, StartWithWindows = StartupShortcut.Enabled });
    }

    private void CreateTray()
    {
        menu = new Forms.ContextMenuStrip();
        menu.Items.Add(new Forms.ToolStripMenuItem("EyeCarer") { Enabled=false });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Take Rest Now", null, (_, _) => StartBreak("manual"));
        menu.Items.Add("Settings...", null, (_, _) => OpenSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        pauseItem = new Forms.ToolStripMenuItem("Pause for 1 Hour", null, (_, _) => Pause()); menu.Items.Add(pauseItem);
        resumeItem = new Forms.ToolStripMenuItem("Resume", null, (_, _) => Resume()); menu.Items.Add(resumeItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        startupItem = new Forms.ToolStripMenuItem("Start with Windows", null, (_, _) =>
        {
            try { var draft=settings.Clone(); draft.StartWithWindows=!StartupShortcut.Enabled; ApplySettings(draft); }
            catch (Exception error) { Log.Error("startup-toggle", error); MessageBox.Show("Could not update Windows Startup.","EyeCarer"); }
            RefreshMenu();
        }); menu.Items.Add(startupItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit EyeCarer", null, (_, _) => Shutdown());
        menu.Opening += (_, _) => RefreshMenu();
        icon = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "EyeCarer.ico"));
        tray = new Forms.NotifyIcon { Text = "EyeCarer — next rest in 15 minutes", Icon = icon, ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => OpenSettings();
        RefreshMenu();
    }
    private void RefreshMenu()
    {
        var paused = schedule.IsPaused(clock.Elapsed.TotalSeconds);
        pauseItem.Enabled = !paused; resumeItem.Enabled = paused;
        startupItem.Checked = StartupShortcut.Enabled;
        if (tray != null) tray.Text = paused ? "EyeCarer — paused for up to one hour" : "EyeCarer — eye rest reminder";
    }

    private static DispatcherTimer Timer(Action callback)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Normal);
        timer.Tick += (_, _) => { timer.Stop(); callback(); };
        return timer;
    }
    private void Arm()
    {
        wake.Stop();
        if (schedule.Suspended || exiting) return;
        wake.Interval = TimeSpan.FromSeconds(schedule.Delay(clock.Elapsed.TotalSeconds)); wake.Start();
    }
    private void OnWake()
    {
        if (schedule.TakeDue(clock.Elapsed.TotalSeconds)) StartBreak("automatic");
        RefreshMenu(); Arm();
    }
    private void Pause() { schedule.Pause(clock.Elapsed.TotalSeconds); EndBreak("pause"); Arm(); RefreshMenu(); Log.Event("paused"); }
    private void Resume() { schedule.Reset(clock.Elapsed.TotalSeconds); if (locked || sleeping) schedule.Suspend(); Arm(); RefreshMenu(); Log.Event("resumed"); }

    private void StartBreak(string reason, Settings? temporary = null)
    {
        if (overlays.Count != 0 || exiting || locked || sleeping) return;
        var effectSettings=temporary ?? settings;
        previousForeground = GetForegroundWindow();
        try
        {
            // Complete every capture before creating or showing the first overlay.
            var captures = Forms.Screen.AllScreens.Select(screen => (screen.Bounds, Image: OverlayWindow.Capture(screen.Bounds))).ToArray();
            foreach (var capture in captures)
            {
                var window = new OverlayWindow(capture.Bounds, capture.Image, effectSettings, preview);
                window.Dismiss += () => EndBreak("escape");
                overlays.Add(window);
            }
            foreach (var window in overlays) window.Show();
            finish.Interval = TimeSpan.FromSeconds(effectSettings.DurationSeconds); finish.Start();
            if (effectSettings.EnableAnimations)
            {
                fade.Interval = TimeSpan.FromSeconds(effectSettings.DurationSeconds - .3); fade.Start();
            }
            breakCount++;
            Log.Event($"break-start {reason} monitors={overlays.Count}");
        }
        catch (Exception error) { Log.Error("overlay", error); EndBreak("error"); }
    }
    private void EndBreak(string reason)
    {
        finish?.Stop(); fade?.Stop();
        bool hadWindows = overlays.Count > 0;
        foreach (var window in overlays.ToArray())
        {
            try { window.Close(); } catch (Exception error) { Log.Error("close", error); }
        }
        overlays.Clear();
        if (hadWindows)
        {
            if (previousForeground != IntPtr.Zero) SetForegroundWindow(previousForeground);
            Log.Event("break-end " + reason);
            // WPF bitmaps live in unmanaged memory. After the compositor releases the
            // closed windows, collect their wrappers once per break, never per frame.
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => GC.Collect(2, GCCollectionMode.Forced, false, false)));
        }
    }
    private void PowerChanged(object sender, PowerModeChangedEventArgs e) => Dispatcher.BeginInvoke(new Action(() =>
    {
        if (e.Mode == PowerModes.Suspend) { sleeping = true; schedule.Suspend(); wake.Stop(); EndBreak("suspend"); }
        else if (e.Mode == PowerModes.Resume) { sleeping = false; Resume(); }
    }));
    private void SessionChanged(object sender, SessionSwitchEventArgs e) => Dispatcher.BeginInvoke(new Action(() =>
    {
        if (e.Reason == SessionSwitchReason.SessionLock || e.Reason == SessionSwitchReason.RemoteDisconnect || e.Reason == SessionSwitchReason.ConsoleDisconnect)
        { locked = true; schedule.Suspend(); wake.Stop(); EndBreak("lock"); }
        else if (e.Reason == SessionSwitchReason.SessionUnlock || e.Reason == SessionSwitchReason.RemoteConnect || e.Reason == SessionSwitchReason.ConsoleConnect)
        { locked = false; Resume(); }
    }));
    private void DisplayChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(() => { EndBreak("display-change"); Resume(); }));

    protected override void OnExit(ExitEventArgs e)
    {
        exiting = true;
        settingsWindow?.Close();
        stopping.Cancel(); wake?.Stop(); EndBreak("exit");
        SystemEvents.PowerModeChanged -= PowerChanged;
        SystemEvents.SessionSwitch -= SessionChanged;
        SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        if (tray != null) { tray.Visible = false; tray.Dispose(); }
        menu?.Dispose(); icon?.Dispose();
        mutex?.Dispose();
        Log.Event("stopped");
        base.OnExit(e);
    }
}
