using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using SteamworksSharp;
using SteamworksSharp.Native;
using Valve.Steamworks;
using Alba.CsConsoleFormat;

using static System.ConsoleColor;

namespace DS3ConnectionInfo
{
    class Player : IDisposable
    {
        public ulong steamID;
        public string steamName;

        public IpData connData;
        public P2PSessionState_t sessionState;

        public string charName;
        public string team;

        public void Dispose()
        {
            if (connData != null) connData.Dispose();
        }
    }

    class Program
    {
        static MemoryManager mem;

        static Player[] players;

        const long baseB = 0x4768E78;
        const int ds3AppId = 374320;

        static readonly Dictionary<int, string> TeamNames = new Dictionary<int, string>()
        {
            {1, "Host"},
            {2, "Phantom"},
            {3, "Black Phantom"},
            {4, "Hollow"},
            {6, "Enemy"},
            {7, "Boss (giants, big lizard)"},
            {8, "Friend"},
            {9, "AngryFriend"},
            {10, "DecoyEnemy"},
            {11, "BloodChild"},
            {12, "BattleFriend"},
            {13, "Dragon"},
            {16, "Dark Spirit"},
            {17, "Watchdog of Farron"},
            {18, "Aldrich Faithful"},
            {24, "Darkwraiths"},
            {26, "NPC"},
            {27, "Hostile NPC"},
            {29, "Arena"},
            {31, "Mad Phantom"},
            {32, "Mad Spirit"},
            {33, "Giant crabs, Dragons from Lothric castle"},
            {0, "None"}
        };

        static void Main(string[] args)
        {
            Console.CursorVisible = false;
            Console.Title = "DS3 Connection Info";
            Console.OutputEncoding = Encoding.Unicode;

            SteamNative.Initialize();

            players = new Player[5];
            mem = new MemoryManager();

            Console.WriteLine("Dark Souls III: Closed");
            do
            {
                try { mem.OpenProcess("DarkSoulsIII"); }
                catch { }
                Thread.Sleep(2000);
            } while (mem.ProcHandle == IntPtr.Zero);

            if (!SteamApi.Initialize(ds3AppId))
            {
                Console.WriteLine("ERROR: Could not initalize SteamAPI.");
                Console.Read();
                return;
            }

            Console.Clear();
            while (!mem.HasExited)
            {
                UpdatePlayerList();
                PrintConnInfo();

                Thread.Sleep(1000);
            }
        }

        static void UpdatePlayerList()
        {
            if (mem.ProcHandle == IntPtr.Zero || mem.HasExited)
            {
                mem.OpenProcess("DarkSoulsIII");
            }
            if (mem.ProcHandle == IntPtr.Zero) return;

            for (int slot = 0; slot < 5; slot++)
            {
                try
                {
                    DeepPointer<long> playerBase = new DeepPointer<long>(mem.Process, "DarkSoulsIII.exe", baseB, new int[] { 0x40, 0x38 * (slot + 1) });
                    if (playerBase.GetValue() == 0)
                    {
                        if (players[slot] != null) { players[slot].Dispose(); }
                        players[slot] = null;
                        continue;
                    }

                    DeepPointerStr idPtr = new DeepPointerStr(mem.Process, "DarkSoulsIII.exe", baseB, new int[] { 0x40, 0x38 * (slot + 1), 0x1FA0, 0x7D8 });
                    string charName = new DeepPointerStr(mem.Process, "DarkSoulsIII.exe", baseB, new int[] { 0x40, 0x38 * (slot + 1), 0x1FA0, 0x88 }).GetValueUnicode(16);
                    int teamType = new DeepPointer<int>(mem.Process, "DarkSoulsIII.exe", baseB, new int[] { 0x40, 0x38 * (slot + 1), 0x74 }).GetValue();
                    ulong steamID = ulong.Parse(idPtr.GetValueUnicode(16), System.Globalization.NumberStyles.HexNumber);

                    if (players[slot] != null && players[slot].steamID == steamID) { continue; }
                    if (players[slot] != null && players[slot].steamID != steamID) { players[slot].Dispose(); }

                    players[slot] = new Player();
                    players[slot].steamID = steamID;
                    players[slot].steamName = SteamApi.SteamFriends.GetFriendPersonaName(steamID);
                    players[slot].charName = charName;
                    players[slot].team = TeamNames[teamType];

                    players[slot].sessionState = new P2PSessionState_t();
                    if (SteamApi.SteamNetworking.GetP2PSessionState(steamID, ref players[slot].sessionState))
                    {
                        if (players[slot].sessionState.m_bUsingRelay == 0)
                        {
                            byte[] ip = BitConverter.GetBytes(players[slot].sessionState.m_nRemoteIP).Reverse().ToArray();
                            players[slot].connData = new IpData(new IPAddress(ip));
                        }
                    }
                }
                catch
                {
                    if (players[slot] != null) { players[slot].Dispose(); }
                    players[slot] = null;
                }
            }
        }
        static void PrintConnInfo()
        {
            Console.SetCursorPosition(0, 0);
            Console.CursorVisible = false;

            LineThickness StrokeRight = LineThickness.Heavy;
            LineThickness StrokeHeader = LineThickness.Double;

            var doc = new Document(
                new Span("Dark Souls III: Open\n") { Color = Yellow },
                new Grid
                {
                    Stroke = StrokeHeader,
                    StrokeColor = DarkGray,
                    Columns = { GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Auto, 
                                GridLength.Auto, GridLength.Auto, GridLength.Star(1)},
                    Children =
                    {
                        new Cell("Slot") { Stroke = StrokeHeader, Color = White },
                        new Cell("Char. Name") { Stroke = StrokeHeader, Color = White },
                        new Cell("Team") { Stroke = StrokeHeader, Color = White },
                        new Cell("Steam Name") { Stroke = StrokeHeader, Color = White },
                        new Cell("Steam ID 64") { Stroke = StrokeHeader, Color = White },
                        new Cell("Ping") { Stroke = StrokeHeader, Color = White },
                        new Cell("Location") { Stroke = StrokeHeader, Color = White },
                        Enumerable.Range(0, 5).Select(slot =>
                        {
                            var cells = new List<Cell>();
                            cells.Add(new Cell(slot) { Align = Align.Center});
                            if (players[slot] == null)
                            {
                                for (int i = 0; i < 6; i++) { cells.Add(new Cell("")); }
                                return cells;
                            }
                            else
                            {
                                cells.Add(new Cell(players[slot].charName));
                                cells.Add(new Cell(players[slot].team));
                                cells.Add(new Cell(players[slot].steamName));
                                cells.Add(new Cell(players[slot].steamID));
                                if (players[slot].connData != null)
                                {
                                    ConsoleColor pingColor;
                                    switch (players[slot].connData.ApproxPing)
                                    {
                                        case -1:
                                            pingColor = White;
                                            break;
                                        case long n when (n <= 50):
                                            pingColor = Blue;
                                            break;
                                        case long n when (n <= 100):
                                            pingColor = Green;
                                            break;
                                        case long n when (n <= 200):
                                            pingColor = Yellow;
                                            break;
                                        default:
                                            pingColor = Red;
                                            break;
                                    }
                                    cells.Add(new Cell(players[slot].connData.ApproxPing) { Color = pingColor, Align = Align.Center});
                                    cells.Add(new Cell(players[slot].connData.Region));
                                }
                                else
                                {
                                    cells.Add(new Cell("N/A"));
                                    cells.Add(new Cell("[STEAM RELAY]"));
                                }
                            }
                            return cells;
                        })
                    }
            });
            ConsoleRenderer.RenderDocument(doc);
            for (int i = Console.CursorTop; i < Console.WindowHeight - 1; i++)
            {
                Console.CursorTop = i;
                Console.WriteLine("".PadRight(Console.BufferWidth - 1));
            }
        }
    }
}
