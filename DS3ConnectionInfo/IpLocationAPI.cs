using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace DS3ConnectionInfo
{
    static class IpLocationAPI
    {
        private static object lockObj;
        private static Timer queryTimer;
        private static Queue<string> ipQueue;
        private static Queue<Action<string>> actionQueue;

        static IpLocationAPI()
        {
            ipQueue = new Queue<string>();
            actionQueue = new Queue<Action<string>>();
            lockObj = new object();
            queryTimer = new Timer(QueryTask, null, 100, 2000); // To stay well below rate limit of 1 request every 1.25 seconds
        }

        private static string GetLocation(string ip)
        {
            string query = "http://ip-api.com/json/" + ip;
            WebRequest req = WebRequest.Create(query);
            HttpWebResponse resp;

            try
            {
                resp = (HttpWebResponse)req.GetResponse();
            }
            catch (WebException err)
            {
                return "WEB ERROR: " + err.Message;
            }

            if (resp.StatusCode == HttpStatusCode.OK)
            {
                using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                {
                    JObject data = JObject.Parse(reader.ReadToEnd());
                    if (data["status"].ToString() == "success")
                    {
                        return string.Format("{0}, {1}, {2}", data["city"], data["regionName"], data["country"]);
                    }
                    return "GEOLOCATION FAIL: " + data["message"].ToString();
                }
            }

            return string.Format("HTTP ERROR ({0}): {1}", resp.StatusCode, resp.StatusDescription);
        }

        private static void QueryTask(object o)
        {
            if (ipQueue.Count != 0)
            {
                string ip;
                Action<string> cb;
                lock (lockObj)
                {
                    ip = ipQueue.Dequeue();
                    cb = actionQueue.Dequeue();
                }

                cb(GetLocation(ip));
            }
        }

        public static void GetLocationAsync(string ip, Action<string> callback)
        {
            lock (lockObj)
            {
                ipQueue.Enqueue(ip);
                actionQueue.Enqueue(callback);
            }
        }
    }
}
