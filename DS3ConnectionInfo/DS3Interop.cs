using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DS3ConnectionInfo.WinAPI;

namespace DS3ConnectionInfo
{
    /// <summary>
    /// Static class implementing most interactions with the DS3 process.
    /// </summary>
    public static class DS3Interop
    {
        public enum WindowState
        {
            NoWindow,
            Border,
            Borderless
        }

        public enum NetStatus
        {
            None,
            TryCreateSession,
            FailCreateSession,
            Host,
            TryJoinSession,
            FailJoinSession,
            Client,
            OnLeaveSession,
            FailLeaveSession
        }

        /// <summary>
        /// BaseB (WorldChrMan) address
        /// </summary>
        public const long BaseB = 0x144768E78;

        /// <summary>
        /// SprjSessionManager address
        /// </summary>
        public const long SprjSession = 0x144780990;

        /// <summary>
        /// BaseC (GameOptionMan) address
        /// </summary>
        public const long BaseC = 0x144743AB0;

        /// <summary>
        /// DS3.exe Base Address
        /// </summary>
        public const long MainModuleBase = 0x140000000;

        /// <summary>
        /// Apply Effect script x86_64
        /// </summary>
        private static byte[] applyEffect = "48A10000000000000000488BD048A1788E764401000000488B80800000004C8BC0488BC84883EC4849BE406C88400100000041FFD64883C448C3".HexToBytes();

        /// <summary>
        /// Session Disconnect script x86_64
        /// </summary>
        private static byte[] leaveSession = "48B89009784401000000488B084883EC2849BEF0B7DE400100000041FFD64883C428C3".HexToBytes();

        private static IntPtr ds3WindowedLong = new IntPtr(0x14ca0000);
        private static RECT prevWindowRect;

        public static bool Borderless { get; private set; }

        /// <summary>
        /// If true, is attached to the Dark Souls III process.
        /// </summary>
        public static bool Attached => ProcHandle != IntPtr.Zero;

        public static Process Process { get; private set; }

        public static IntPtr ProcHandle { get; private set; }

        public static IntPtr WinHandle { get; private set; }

        public static uint WinThread { get; private set; }

        public static bool FindWindow()
        {
            do
            {
                WinHandle = User32.FindWindowEx(IntPtr.Zero, WinHandle, null, "DARK SOULS III");
                WinThread = User32.GetWindowThreadProcessId(WinHandle, out uint pid);
                if (pid == Process.Id) { return true; }
            } while (WinHandle != IntPtr.Zero);

            WinThread = 0;
            return false;
        }

        public static void MakeBorderless(bool b)
        {
            if (Attached && b && !Borderless)
            {
                User32.GetWindowRect(WinHandle, out prevWindowRect);

                int w = User32.GetSystemMetrics(0);
                int h = User32.GetSystemMetrics(1);

                User32.SetWindowLongPtr(WinHandle, -16, new IntPtr(0x90000000L)); // POPUP | VISIBLE
                User32.SetWindowPos(WinHandle, IntPtr.Zero, 0, 0, w, h, 0x20); // SWP_FRAMECHANGED

                Borderless = true;
            }
            if (Attached && !b && Borderless)
            {
                User32.SetWindowLongPtr(WinHandle, -16, ds3WindowedLong);
                User32.SetWindowPos(WinHandle, IntPtr.Zero, prevWindowRect.x1, prevWindowRect.y1,
                    prevWindowRect.x2 - prevWindowRect.x1, prevWindowRect.y2 - prevWindowRect.y1, 0x20);

                Borderless = false;
            }
        }

        public static bool TryAttach()
        {
            if (Attached) return true;

            Process[] pArr = Process.GetProcessesByName("DarkSoulsIII");
            if (pArr.Length == 0) return false;

            Process = pArr[0];
            for (int i = 1; i < pArr.Length; i++) pArr[i].Dispose();

            ProcHandle = Kernel32.OpenProcess(ProcessAccessFlags.AllAccess, false, Process.Id);
            if (ProcHandle == IntPtr.Zero) Detach();
            return ProcHandle != IntPtr.Zero;
        }

        /// <summary>
        /// Close the link between the memory manager and the process.
        /// </summary>
        public static void Detach()
        {
            if (Process != null)
            {
                Process.Dispose();
                Process = null;
            }

            Kernel32.CloseHandle(ProcHandle);
            ProcHandle = IntPtr.Zero;
            WinHandle = IntPtr.Zero;
            WinThread = 0;
        }

        public static bool IsGameFocused()
        {
            return User32.GetForegroundWindow() == WinHandle;
        }

        public static long GetPlayerBase(int slot)
        {
            return MemoryManager.ReadGenericPtr<long>(ProcHandle, BaseB, 0x40, 0x38 * (slot + 1));
        }

        public static Steamworks.CSteamID GetPlayerSteamId(int slot)
        {
            string sid = MemoryManager.ReadStringPtr(ProcHandle, 32, Encoding.Unicode, BaseB, 0x40, 0x38 * (slot + 1), 0x1FA0, 0x7D8);
            if (ulong.TryParse(sid, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong id)) return new Steamworks.CSteamID(id);
            return new Steamworks.CSteamID(0);
        }

        public static string GetPlayerName(int slot)
        {
            return MemoryManager.ReadStringPtrNT(ProcHandle, 32, Encoding.Unicode, BaseB, 0x40, 0x38 * (slot + 1), 0x1FA0, 0x88);
        }

        public static int GetPlayerTeam(int slot)
        {
            return MemoryManager.ReadGenericPtr<int>(ProcHandle, BaseB, 0x40, 0x38 * (slot + 1), 0x74);
        }

        public static void ApplyEffect(int effectId)
        {
            MemoryManager.ExecuteFunction(ProcHandle, applyEffect, new Dictionary<int, object>()
            {
                { 0x2, effectId }
            });
        }

        public static void LeaveSession()
        {
            MemoryManager.ExecuteFunction(ProcHandle, leaveSession);
        }

        public static NetStatus GetNetworkState()
        {
            return MemoryManager.ReadGenericPtr<NetStatus>(ProcHandle, SprjSession, 0x16C);
        }

        public static bool InLoadingScreen()
        {
            try
            {   // Current animation undefined
                return MemoryManager.ReadGenericPtr<int>(ProcHandle, BaseB, 0x80, 0x1F90, 0x80, 0xC8) == -1;
            }
            catch (Exception)
            {
                return true;
            }
        }

        public static bool IsMyWorld()
        {
            return MemoryManager.ReadGenericPtr<int>(ProcHandle, BaseC, 0xB1E) == 1;
        }
    }
}
