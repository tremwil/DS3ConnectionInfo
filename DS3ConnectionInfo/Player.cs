using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Valve.Steamworks;
using SteamworksSharp;
using SteamworksSharp.Native;
using System.Diagnostics;
using System.Globalization;

namespace DS3ConnectionInfo
{
    class Player
    {
        private const long baseB = 0x4768E78;

        public static readonly Dictionary<int, string> TeamNames = new Dictionary<int, string>()
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

        private static Dictionary<ulong, Player> activePlayers = new Dictionary<ulong, Player>();

        private P2PSessionState_t sessionState;
        public ulong SteamID { get; private set; }
        public string SteamName { get; private set; }

        public IPAddress Ip { get; private set; }
        public string Region { get; private set; }
        public int Ping => (Ip == null) ? -1 : (int)ETWPingMonitor.GetPing(Ip);

        public string CharSlot { get; private set; }
        public int TeamId { get; private set; }
        public string TeamName => TeamNames.ContainsKey(TeamId) ? TeamNames[TeamId] : "";
        public string CharName { get; set; }

        private Player(ulong steamID, P2PSessionState_t session)
        {
            sessionState = session;
            SteamID = steamID;
            SteamName = SteamApi.SteamFriends.GetFriendPersonaName(steamID);

            Ip = null;
            Region = "...";

            if (session.m_bUsingRelay == 0)
            {
                byte[] ipBytes = BitConverter.GetBytes(sessionState.m_nRemoteIP).Reverse().ToArray();
                Ip = new IPAddress(ipBytes);
                IpLocationAPI.GetLocationAsync(Ip.ToString(), r => Region = r);
            }

            CharSlot = "";
            TeamId = -1;
            CharName = "";
        }

        public static IEnumerable<Player> ActivePlayers()
        {
            return activePlayers.Values.AsEnumerable();
        }

        public static void UpdateInGameInfo(Process ds3Proc)
        {
            for (int slot = 0; slot < 5; slot++)
            {
                try
                {
                    DeepPointer<long> playerBase = new DeepPointer<long>(ds3Proc, "DarkSoulsIII.exe", baseB, new int[] { 0x40, 0x38 * (slot + 1) });
                    if (playerBase.GetValue() == 0) continue;

                    DeepPointerStr idPtr = new DeepPointerStr(ds3Proc, "DarkSoulsIII.exe", baseB, new int[] { 0x40, 0x38 * (slot + 1), 0x1FA0, 0x7D8 });
                    if (!ulong.TryParse(idPtr.GetValueUnicode(16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong id))
                        continue;

                    if (!activePlayers.ContainsKey(id)) continue;
                    activePlayers[id].CharSlot = slot.ToString();
                    activePlayers[id].CharName = new DeepPointerStr(ds3Proc, "DarkSoulsIII.exe", baseB, new int[] { 0x40, 0x38 * (slot + 1), 0x1FA0, 0x88 }).GetValueUnicode(16);
                    activePlayers[id].TeamId = new DeepPointer<int>(ds3Proc, "DarkSoulsIII.exe", baseB, new int[] { 0x40, 0x38 * (slot + 1), 0x74 }).GetValue();
                }
                catch (Exception)
                {
                    
                }
            }
        }

        public static void UpdatePlayerList()
        {
            // There's probably a better way to get all current active P2P connections,
            // but I couldn't find one. The SessionInfo pointers are not reliable as 
            // players can spoof their Steam ID there.
            int cnt = SteamApi.SteamFriends.GetCoplayFriendCount();
            for (int i = 0; i < cnt; i++)
            {
                ulong id = SteamApi.SteamFriends.GetCoplayFriend(i);

                P2PSessionState_t session = new P2PSessionState_t();
                if (!SteamApi.SteamNetworking.GetP2PSessionState(id, ref session))
                {
                    activePlayers.Remove(id);
                }
                else if (!activePlayers.ContainsKey(id))
                {
                    activePlayers[id] = new Player(id, session);
                }
            }
        }
    }
}
