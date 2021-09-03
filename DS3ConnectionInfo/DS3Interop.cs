using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
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
        /// Ways of joining a DS3 online session.
        /// </summary>
        public enum JoinMethod
        {
            None,
            Arena,
            Convenant,
            RedEyeOrb,
            WhiteSign,
            RedSign
        }

        private static readonly JoinMethod[] invTypeToJoinMethod = new JoinMethod[22]
        {
            JoinMethod.None,
            JoinMethod.WhiteSign,
            JoinMethod.RedSign,
            JoinMethod.RedEyeOrb,
            JoinMethod.WhiteSign,
            JoinMethod.None, // 5 Missing from TGA list
            JoinMethod.Convenant,
            JoinMethod.Convenant,
            JoinMethod.None, // (Guardian of Rosaria)
            JoinMethod.Convenant,
            JoinMethod.Convenant,
            JoinMethod.None, // (Avatar)
            JoinMethod.Arena,
            JoinMethod.None, // (Umbasa White)
            JoinMethod.WhiteSign,
            JoinMethod.RedSign,
            JoinMethod.RedSign,
            JoinMethod.RedEyeOrb,
            JoinMethod.RedEyeOrb,
            JoinMethod.None, // (Force Join Session)
            JoinMethod.None, // (Red Hunter)
            JoinMethod.Convenant
        };

        public struct SpEffect
        {
            public int id;
            public float durationLeft;
            public float duration;
            public float interval;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct SpEffectInternal
        {
            [FieldOffset(0x00)]
            public float durationLeft;
            [FieldOffset(0x04)]
            public float duration;
            [FieldOffset(0x08)]
            public float interval;
            [FieldOffset(0x60)]
            public int id;
            [FieldOffset(0x78)]
            public long ptrNext;
            public SpEffect ToSpEffect()
            {
                return new SpEffect { id = id, duration = duration, durationLeft = durationLeft, interval = interval };
            }
        }

        /// <summary>
        /// BaseA (Game) address
        /// </summary>
        public const long BaseA = 0x144740178;

        /// <summary>
        /// BaseB (WorldChrMan) address
        /// </summary>
        public const long BaseB = 0x144768E78;

        /// <summary>
        /// BaseC (GameOptionMan) address
        /// </summary>
        public const long BaseC = 0x144743AB0;

        /// <summary>
        /// FRPGNet address
        /// </summary>
        public const long BaseE = 0x14473FD08;

        /// <summary>
        /// SprjSessionManager address
        /// </summary>
        public const long SprjSession = 0x144780990;

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

        public static long GetPlayerNetHandle(int slot)
        {
            return MemoryManager.ReadGenericPtr<long>(ProcHandle, BaseB, 0x40, 0x38 * (slot + 1), 0x1FD0, 0x8);
        }

        /// <summary>
        /// Bypasses SessionInfo Steam ID spoofing.
        /// Adaptation of a Lua CE script by Amir.
        /// </summary>
        /// <param name="slot"></param>
        /// <returns></returns>
        public static Steamworks.CSteamID GetTruePlayerSteamId(int slot)
        {
            long sprjSessionPtr = MemoryManager.ReadGenericPtr<long>(ProcHandle, SprjSession);
            long playerNetHandle = GetPlayerNetHandle(slot);
            if (sprjSessionPtr != 0 && playerNetHandle != 0)
            {
                long conn = MemoryManager.ReadGenericPtr<long>(ProcHandle, sprjSessionPtr + 0x18) + 0x68;
                if (conn != 0)
                {
                    for (int i = 0; i <= 100; i++)
                    {
                        long ptr = MemoryManager.ReadGenericPtr<long>(ProcHandle, conn);
                        long ptr2 = MemoryManager.ReadGenericPtr<long>(ProcHandle, conn + 8);
                        if (0 == ptr || ptr + 8 * i >= ptr2) continue;

                        long currHandle = MemoryManager.ReadGenericPtr<long>(ProcHandle, ptr + 8 * i, 13 * 8);
                        ulong currSteamId = MemoryManager.ReadGenericPtr<ulong>(ProcHandle, ptr + 8 * i, 25 * 8);
                        if (playerNetHandle == currHandle)
                            return new Steamworks.CSteamID(currSteamId);
                    }
                }
            }

            return new Steamworks.CSteamID(0);
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

        public static IEnumerable<SpEffect> ActiveSpEffects()
        {
            long addrNext = MemoryManager.ReadGenericPtr<long>(ProcHandle, BaseA, 0x10, 0x920, 0x8);

            while (addrNext != 0)
            {
                SpEffectInternal fx = MemoryManager.ReadGeneric<SpEffectInternal>(ProcHandle, addrNext);
                addrNext = fx.ptrNext;
                yield return fx.ToSpEffect();
            }
        }

        public static bool IsSearchingInvasion()
        {
            foreach (SpEffect fx in ActiveSpEffects())
            {
                if (fx.id == 9200) return true;
            }
            return false;
        }

        public static JoinMethod GetJoinMethod()
        {
            int invType = MemoryManager.ReadGenericPtr<int>(ProcHandle, BaseC, 0xC54);
            if (invType > 0 || invType < -21) return JoinMethod.None;
            else return invTypeToJoinMethod[-invType];
        }
    }
}
