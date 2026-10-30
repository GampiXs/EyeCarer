using System;
using System.IO;
using System.Runtime.InteropServices;

namespace EyeCarer;

internal static class StartupShortcut
{
    public static string Path => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "EyeCarer.lnk");
    public static bool Enabled => File.Exists(Path);
    public static void Set(bool enabled)
    {
        if (!enabled)
        {
            if (File.Exists(Path))
            {
                object? checkShell = null, checkLink = null;
                try
                {
                    checkShell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
                    dynamic ownLink = ((dynamic)checkShell!).CreateShortcut(Path);
                    checkLink = ownLink;
                    if (string.Equals((string)ownLink.TargetPath, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase)) File.Delete(Path);
                }
                finally
                {
                    if (checkLink != null) Marshal.FinalReleaseComObject(checkLink);
                    if (checkShell != null) Marshal.FinalReleaseComObject(checkShell);
                }
            }
            return;
        }
        object? shell = null, shortcut = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            dynamic link = ((dynamic)shell!).CreateShortcut(Path);
            shortcut = link;
            link.TargetPath = Environment.ProcessPath!;
            link.WorkingDirectory = AppContext.BaseDirectory;
            link.IconLocation = System.IO.Path.Combine(AppContext.BaseDirectory, "EyeCarer.ico") + ",0";
            link.Description = "EyeCarer eye rest reminder";
            link.Save();
        }
        finally
        {
            if (shortcut != null) Marshal.FinalReleaseComObject(shortcut);
            if (shell != null) Marshal.FinalReleaseComObject(shell);
        }
    }
}
