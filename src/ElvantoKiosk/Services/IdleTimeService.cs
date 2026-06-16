using System;
using System.Runtime.InteropServices;

namespace ElvantoKiosk.Services;

/// <summary>
/// Mesure l'inactivité globale (clavier + souris), y compris à l'intérieur de WebView2
/// qui s'exécute dans un processus séparé. On utilise GetLastInputInfo de Win32.
/// </summary>
public static class IdleTimeService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    /// <summary>Durée d'inactivité globale.</summary>
    public static TimeSpan GetIdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };

        if (!GetLastInputInfo(ref info))
            return TimeSpan.Zero;

        var idleMs = (uint)Environment.TickCount - info.dwTime;
        return TimeSpan.FromMilliseconds(idleMs);
    }
}
