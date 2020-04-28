using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using System.Net;
using System.Net.NetworkInformation;
using System.Diagnostics;

namespace DS3ConnectionInfo
{
    class IpData : IDisposable
    {
        public IPAddress ip;

        Timer pingTimer;

        public long ApproxPing { get; private set; }
        public string Region { get; private set; }

        public IpData(IPAddress ip)
        {
            this.ip = ip;

            ApproxPing = -1;
            pingTimer = new Timer(o => ApproxPing = GetTracePing(), null, 100, 20000);

            Region = "...";
            IpLocationAPI.GetLocationAsync(ip.ToString(), s => Region = s);
        }

        public override string ToString()
        {
            byte[] ipBytes = ip.GetAddressBytes();
            string ipFmt = string.Join(".", ipBytes.Select(x => string.Format("{0,3:D3}", x)));
            string tracePing = (ApproxPing == -1) ? "..." : ApproxPing.ToString();

            string fmt = "IP = {0}, PING = {1}, LOCATION = {2}";
            return string.Format(fmt, ipFmt, tracePing, Region);
        }

        private long GetTracePing()
        {
            const int timeout = 500;
            const int maxTTL = 30;
            const int bufferSize = 32;

            byte[] buffer = new byte[bufferSize];
            new Random().NextBytes(buffer);
            Ping pinger = new Ping();

            Stopwatch sw = new Stopwatch();

            long ping = -1;

            for (int ttl = 1; ttl <= maxTTL; ttl++)
            {
                PingOptions options = new PingOptions(ttl, true);
                sw.Restart();
                PingReply reply = pinger.Send(ip, timeout, buffer, options);
                sw.Stop();

                if (reply.Status == IPStatus.TtlExpired && sw.ElapsedMilliseconds > ping)
                {
                    ping = sw.ElapsedMilliseconds; // RoundtripTime not set on TtlExpired for some reason
                }
                if (reply.Status == IPStatus.Success)
                {
                    ping = reply.RoundtripTime;
                    break;
                }
            }

            pinger.Dispose();
            return ping;
        }

        public void Dispose()
        {
            pingTimer.Dispose();
        }
    }
}
