using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using System.Net;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;

namespace DS3ConnectionInfo
{
    public static class ETWPingMonitor
    {
        private class PingInfo
        {
            public double tPacketSent, tLastRecv, ping;

            public PingInfo(double ctime)
            {
                tLastRecv = ctime;
                tPacketSent = ping = -1;
            }
        }

        private static TraceEventSession kernelSession;
        private static Thread eventThread;
        public static bool Running { get; private set; }

        private static Dictionary<IPAddress, PingInfo> pings;

        static ETWPingMonitor()
        {
            Running = false;
            pings = new Dictionary<IPAddress, PingInfo>();
        }

        /// <summary>
        /// Begin monitoring STUN pings.
        /// </summary>
        public static void Start()
        {
            if (Running) return;

            if (!(TraceEventSession.IsElevated() ?? false))
            {
                throw new Exception("Program must be run in administrator mode");
            }

            kernelSession = new TraceEventSession(KernelTraceEventParser.KernelSessionName);
            kernelSession.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);
            kernelSession.Source.Kernel.UdpIpSend += Kernel_UdpIpSend;
            kernelSession.Source.Kernel.UdpIpRecv += Kernel_UdpIpRecv;

            Running = true;
            eventThread = new Thread(() => kernelSession.Source.Process());
            eventThread.Start();
        }

        /// <summary>
        /// Stop monitoring STUN pings.
        /// </summary>
        public static void Stop()
        {
            if (!Running) return;

            Running = false;
            kernelSession.Stop();
        }

        public static double GetPing(IPAddress ip)
        {
            return pings.ContainsKey(ip) ? pings[ip].ping : -1;
        }

        private static void Kernel_UdpIpSend(UdpIpTraceData packet)
        {
            if (packet.size == 56)
            {
                if (!pings.ContainsKey(packet.daddr))
                    pings[packet.daddr] = new PingInfo(packet.TimeStampRelativeMSec);

                pings[packet.daddr].tPacketSent = packet.TimeStampRelativeMSec;

                foreach (IPAddress ip in pings.Keys)
                {
                    if (pings[ip].tLastRecv - packet.TimeStampRelativeMSec > 10000)
                        pings.Remove(ip);
                }
            }
        }

        private static void Kernel_UdpIpRecv(UdpIpTraceData packet)
        {
            if (pings.ContainsKey(packet.saddr) && packet.size == 68)
            {
                if (pings[packet.saddr].tPacketSent != -1)
                {
                    pings[packet.saddr].tLastRecv = packet.TimeStampRelativeMSec;
                    pings[packet.saddr].ping = packet.TimeStampRelativeMSec - pings[packet.saddr].tPacketSent;
                    pings[packet.saddr].tPacketSent = -1;
                }
            }
        }
    }
}
