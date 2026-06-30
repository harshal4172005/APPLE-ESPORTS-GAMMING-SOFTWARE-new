using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AppleEsportsErp.ClientAgent.Services;

/// <summary>
/// System-level security service that prevents customers from bypassing the lock screen.
/// Uses low-level keyboard hooks to block Alt+Tab, Windows Key, Ctrl+Esc, Alt+F4.
/// Also disables Task Manager via the Windows Registry when locked.
/// </summary>
public class SystemLockService
{
    // Win32 API imports for keyboard hooks
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    // Virtual key codes
    private const int VK_TAB = 0x09;
    private const int VK_ESCAPE = 0x1B;
    private const int VK_F4 = 0x73;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_DELETE = 0x2E;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _hookProc;
    private bool _isLocked = false;

    /// <summary>Enable the lock — block keyboard shortcuts and disable Task Manager</summary>
    public void EnableLock()
    {
        if (_isLocked) return;
        _isLocked = true;

        // Install keyboard hook
        _hookProc = HookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(curModule.ModuleName!), 0);

        // Disable Task Manager via Registry
        SetTaskManagerState(disabled: true);
    }

    /// <summary>Disable the lock — restore all keyboard shortcuts and Task Manager</summary>
    public void DisableLock()
    {
        if (!_isLocked) return;
        _isLocked = false;

        // Remove keyboard hook
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }

        // Re-enable Task Manager
        SetTaskManagerState(disabled: false);
    }

    /// <summary>The keyboard hook callback — intercepts and blocks dangerous key combinations</summary>
    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _isLocked)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            bool alt = (GetAsyncKeyState(0x12) & 0x8000) != 0;   // VK_MENU (Alt key)
            bool ctrl = (GetAsyncKeyState(0x11) & 0x8000) != 0;  // VK_CONTROL

            // Block Alt + Tab
            if (alt && vkCode == VK_TAB)
                return (IntPtr)1;

            // Block Alt + F4
            if (alt && vkCode == VK_F4)
                return (IntPtr)1;

            // Block Windows Key (left and right)
            if (vkCode == VK_LWIN || vkCode == VK_RWIN)
                return (IntPtr)1;

            // Block Ctrl + Escape (Start Menu)
            if (ctrl && vkCode == VK_ESCAPE)
                return (IntPtr)1;
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    /// <summary>Enable or disable Task Manager via Windows Registry</summary>
    private static void SetTaskManagerState(bool disabled)
    {
        try
        {
            const string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
            using var key = Registry.CurrentUser.CreateSubKey(keyPath, true);
            if (disabled)
            {
                key.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);
            }
            else
            {
                key.DeleteValue("DisableTaskMgr", throwOnMissingValue: false);
            }
        }
        catch
        {
            // Registry access may fail without admin rights — log but continue
        }
    }
}
