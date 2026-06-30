using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ElvantoKiosk.Services;

/// <summary>
/// Hook clavier bas niveau (WH_KEYBOARD_LL) destiné à neutraliser les raccourcis
/// système gênants pour une borne kiosque : touche Windows, Alt+Tab, Alt+Échap,
/// Ctrl+Échap, Alt+F4, F11, et touches multimédia du navigateur.
///
/// Remarque : Ctrl+Alt+Suppr et Ctrl+Maj+Échap (gestionnaire des tâches) ne peuvent
/// pas être bloqués par un hook — cela nécessite une stratégie de groupe / configuration
/// kiosque Windows (voir README).
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private const int VK_TAB = 0x09;
    private const int VK_ESCAPE = 0x1B;
    private const int VK_F4 = 0x73;
    private const int VK_F11 = 0x7A;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_APPS = 0x5D;
    private const int VK_BROWSER_BACK = 0xA6;
    private const int VK_BROWSER_FORWARD = 0xA7;
    private const int VK_BROWSER_REFRESH = 0xA8;
    private const int VK_BROWSER_HOME = 0xAC;

    private const int LLKHF_ALTDOWN = 0x20;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int VK_CONTROL = 0x11;
    private const int VK_RMENU = 0xA5;
    private const int VK_PACKET = 0xE7;

    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private volatile bool _formInputActive;

    public KeyboardHook()
    {
        _proc = HookCallback;
    }

    /// <summary>
    /// En mode formulaire, laisse passer toutes les frappes (clavier tactile TabTip, AltGr, etc.).
    /// Le hook kiosque ne bloque que hors saisie dans Notion.
    /// </summary>
    public void SetFormInputActive(bool active) => _formInputActive = active;

    public void Install()
    {
        if (_hookId != IntPtr.Zero)
            return;

        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(module.ModuleName), 0);

        if (_hookId == IntPtr.Zero)
            Logger.Error("Échec de l'installation du hook clavier.");
        else
            Logger.Info("Hook clavier installé (touches système bloquées).");
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (_formInputActive)
            return CallNextHookEx(_hookId, nCode, wParam, lParam);

        if (nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
        {
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            if (ShouldBlock(data, wParam))
                return (IntPtr)1; // Touche absorbée.
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static bool ShouldBlock(KBDLLHOOKSTRUCT data, IntPtr wParam)
    {
        var vk = (int)data.vkCode;
        var altDown = (data.flags & LLKHF_ALTDOWN) != 0;
        var ctrlDown = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
        var rightAltDown = (GetAsyncKeyState(VK_RMENU) & 0x8000) != 0;

        // Clavier tactile Windows : caractères Unicode injectés (ex. « @ » depuis &123)
        if (vk == VK_PACKET)
            return false;

        // AltGr (touche Alt droite) et Ctrl+Alt : caractères spéciaux AZERTY (@, #, etc.)
        if (vk == VK_RMENU || rightAltDown || (altDown && ctrlDown))
            return false;

        // Touche Windows / menu contextuel
        if (vk is VK_LWIN or VK_RWIN or VK_APPS)
            return true;

        // Alt+Tab, Alt+Échap, Alt+F4
        if (altDown && vk is VK_TAB or VK_ESCAPE or VK_F4)
            return true;

        // Ctrl+Échap (ouvre le menu Démarrer)
        if (ctrlDown && vk == VK_ESCAPE)
            return true;

        // F11 (plein écran), touches multimédia de navigation
        if (vk is VK_F11 or VK_BROWSER_BACK or VK_BROWSER_FORWARD or VK_BROWSER_REFRESH or VK_BROWSER_HOME)
            return true;

        return false;
    }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            Logger.Info("Hook clavier désinstallé.");
        }
    }
}
