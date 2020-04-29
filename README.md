# DS3ConnectionInfo
Simple console program showing active P2P connection information along ping and geolocation for Dark Souls III. In a game whose PvP is highly dependent on spacing and timing, knowing the latency between you and your opponent before the fight can be pretty useful.

## DISCLAIMER: 
**Do not share the location information provided by this program. While you should be free to view it yourself since the players are connected to your computer, respect the privacy of others. I am not responsible for any misuse of this infomration.**

**I release the source code of the program for transparency and because C# is easy to decompile anyways. You are free to re-use parts of this code for other projects, but please give proper attribution.** 

![](http://s01.geekpic.net/di-F9WL5Y.png)

# Installation
Download the lastest release from the Releases tab and extract the ZIP file in any folder on your computer. Start `DS3ConnectionInfo.exe` whenever you want to use it. If you restart the game, you will also have to restart the program.

# FAQ
### Is it bannable?
It is 100% ban-safe, as the program does not modify the game's memory in any way. 

### Why does the program close when the game does?
Since the code uses the Steam API with DS3's Steam App ID, letting the program run after the game would make Steam think the game closes is still running. Calling `SteamAPI_Shutdown` does not seem to fix the problem, so we have to close the process. A DLL mod could make this seamless, but making an external program is simpler and doesn't require ban testing.

### Why show the location of players?
Sometimes the ping shown can be inaccurate due to early network nodes blocking ping packets. Showing basic geolocation information can help to get a more reliable idea of the latency in that case. When playing any direct P2P game such as Dark Souls III you should be aware that your public IP address (which is linked to your location) is transmitted to other players. **This is not a security exploit.** Use a VPN if you wish to keep this information private.

### I found a bug / I have something to say about the mod
Feel free to open an issue on this Github or direct message me on discord at tremwil#3713.

# How it works
The program reads the Steam ID and character name of active players from the game's memory (like Cheat Engine). From there we use the Steam API function `GetP2PSessionState` to get the remote IP address. Since most routers deny ICMP ping requests, I use a TraceRoute like method to ping the network node that is closest to the player IP. This gives a pretty good estimate for the ping, but it will always be lower than the true value. Hence I also provide region-specific geolocating using [ip-api](https://ip-api.com) to query the country and region (state) information.
