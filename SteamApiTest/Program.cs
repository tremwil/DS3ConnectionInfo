using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Steamworks;
using System.Runtime.InteropServices;
using System.Reflection;

namespace SteamApiTest
{
    class Program
    {
        static void Main(string[] args)
        {
            if (!SteamAPI.Init())
            {
                Console.WriteLine("Steam API Init() fail");
                return;
            }

            //var connChangeCB = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(ConnStatusChanged);
            //var p2pReqCB = Callback<P2PSessionRequest_t>.Create(OnP2PRequest);
            //var socketChangeCB = Callback<SocketStatusCallback_t>.Create(OnSocketChanged);

            while (true)
            {
                GetCurrPlayers_Coplay();

                Thread.Sleep(1000);
            }
        }

        public static ESteamNetworkingConnectionState GetSessionConnectionInfo(ref SteamNetworkingIdentity identityRemote, out SteamNetConnectionInfo_t pConnectionInfo, out SteamNetworkingQuickConnectionStatus pQuickStatus)
        {
            InteropHelp.TestIfAvailableClient();
            IntPtr inst = GetSteamNetowkingMsgHandle();
            return ISteamNetworkingMessages_GetSessionConnectionInfo(inst, ref identityRemote, out pConnectionInfo, out pQuickStatus);
        }

        [DllImport("steam_api64.dll", EntryPoint = "SteamAPI_ISteamNetworkingMessages_GetSessionConnectionInfo", CallingConvention = CallingConvention.Cdecl)]
        public static extern ESteamNetworkingConnectionState ISteamNetworkingMessages_GetSessionConnectionInfo(
            IntPtr instancePtr, 
            ref SteamNetworkingIdentity identityRemote, 
            out SteamNetConnectionInfo_t pConnectionInfo, 
            out SteamNetworkingQuickConnectionStatus pQuickStatus);

        /// <summary>
        /// SteamNetworkingMessages API is in Steamworks.NET, but not public.
        /// So we take it by force
        /// </summary>
        /// <returns></returns>

        static IntPtr GetSteamNetowkingMsgHandle()
        {
            Assembly steamworks = Assembly.GetAssembly(typeof(SteamAPI));
            Type[] classes = new Type[0];

            try { classes = steamworks.GetTypes(); }
            catch (ReflectionTypeLoadException e) { classes = e.Types; }

            foreach (Type t in classes)
            {
                if (t != null && t.Name == "CSteamAPIContext")
                    return (IntPtr)t.GetField("m_pSteamNetworkingMessages", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            }
            return IntPtr.Zero;
            }

        static void GetCurrPlayers_Coplay()
        {
            SteamNetConnectionInfo_t connInfo;
            SteamNetworkingQuickConnectionStatus QuickStatus;

            var currPlayers = new Dictionary<CSteamID, P2PSessionState_t>();
            int cnt = SteamFriends.GetCoplayFriendCount();
            for (int i = 0; i < cnt; i++)
            {
                CSteamID id = SteamFriends.GetCoplayFriend(i);
                SteamNetworkingIdentity nid = new SteamNetworkingIdentity();
                nid.SetSteamID(id);

                var cState = GetSessionConnectionInfo(ref nid, out connInfo, out QuickStatus);
                Console.WriteLine(cState);
            }
        }

        // Old API
        private static void OnSocketChanged(SocketStatusCallback_t status)
        {
            Console.WriteLine("[STEAM API] SOCKET STATUS CHANGED");
        }

        // Old API
        private static void OnP2PRequest(P2PSessionRequest_t req)
        {
            Console.WriteLine("[STEAM API] CONN REQ.");
        }

        // New API
        private static void ConnStatusChanged(SteamNetConnectionStatusChangedCallback_t connInfo)
        {
            Console.WriteLine("[STEAM API] NET CONNECTION STATUS CHANGED");

            if (connInfo.m_info.m_hListenSocket.m_HSteamListenSocket != 0 && 
                connInfo.m_eOldState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_None &&
                connInfo.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting)
            {
                CSteamID user = connInfo.m_info.m_identityRemote.GetSteamID();
                string name = SteamFriends.GetFriendPersonaName(user);
                Console.WriteLine("[STEAM API] NEW CONNECTION TO {0} ({1})", name, user.m_SteamID);
            }
        }
    }
}
