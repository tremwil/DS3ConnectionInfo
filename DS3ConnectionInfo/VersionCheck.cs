using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace DS3ConnectionInfo
{
    static class VersionCheck
    {
        public static readonly string CurrentVersion = "V4.1";
        public static JObject LatestRelease { get; private set; }

        public static bool FetchLatest()
        {
            string query = "https://api.github.com/repos/tremwil/DS3ConnectionInfo/releases";
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(query);
            req.UserAgent = "request";
            HttpWebResponse resp;

            try
            {
                resp = (HttpWebResponse)req.GetResponse();
            }
            catch (WebException)
            {
                LatestRelease = null;
                return false;
            }

            if (resp.StatusCode == HttpStatusCode.OK)
            {
                using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                {
                    JArray data = JArray.Parse(reader.ReadToEnd());
                    LatestRelease = (JObject)data[0];
                    return true;
                }
            }

            LatestRelease = null;
            return false;
        }
    }
}
