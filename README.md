# DS3ConnectionInfo
Simple console program showing active P2P connection information along ping and geolocation for Dark Souls III. In a game whose PvP is highly dependent on spacing and timing, knowing the latency between you and your opponent before the fight can be pretty useful. Also implements a simple in-game overlay (**windowed or borderless windowed mode only, check `settings.txt` for more information**).

## [Download](https://github.com/tremwil/DS3ConnectionInfo/releases/download/V3.3/DS3ConnectionInfo-V3.3.zip)

## DISCLAIMER: 
**Do not share the location information provided by this program. While you should be free to view it yourself since the players are connected to your computer, respect the privacy of others. I am not responsible for any misuse of this information.**

**I release the source code of the program for transparency and because C# is easy to decompile anyways. You are free to re-use parts of this code for other projects, but please give proper attribution.** 

![](http://s01.geekpic.net/di-F9WL5Y.png)

# Installation
Download the lastest release from the Releases tab and extract the ZIP file in any folder on your computer. Start `DS3ConnectionInfo.exe` whenever you want to use it. If you restart the game, you will also have to restart the program.

# FAQ
### Is it bannable?
It is 100% ban-safe, as the program does not modify the game's memory in any way. 

### Why does it require administrator privileges? (V2+)
In V2+, the pings are computed by monitoring STUN packets that are sent to and recieved from the players' IPs. This is more accurate and updates faster than the 
traceroute method used in V1. However, to capture these packets I use Event Tracing for Windows (ETW), which requires administrator privileges for "kernel" events like networking. 

### Why does the program close when the game does?
Since the code uses the Steam API with DS3's Steam App ID, letting the program run after the game closes would make Steam think the it is still running. Calling `SteamAPI_Shutdown` does not seem to fix the problem, so we have to close the process. A DLL mod could make this seamless, but making an external program is simpler and doesn't require ban testing.

### Why show the location of players?
The ping system used in V1 could sometimes be inaccurate due to early network nodes blocking ping packets. Showing basic geolocation information could help to get a more reliable idea of the latency in that case. With V2+ this is no longer necessary, but since it is still possible to access the old release and source I have 
decided to keep this feature in. When playing any direct P2P game such as Dark Souls III you should be aware that your public IP address (which is linked to your location) is transmitted to other players. **This is not a security exploit.** Use a VPN if you wish to keep this information private.

### I found a bug / I have something to say about the mod
Feel free to open an issue on this Github or direct message me on discord at tremwil#3713.

# How it works (V2+)
The Steam API is used to query the Steam ID of recently met players. Using `GetP2PSessionState`, we are able to query if each player is currently connected and get
the remote IP address. This is then matched to the slot and character name from the game's memory. The reason for using recently met players instead of simply
reading the Steam ID from the game's memory is that the latter can be spoofed by players (for example when running the PyreProtecc anti cheat). To calculate the pings, ETW (Event Tracing for Windows) networking events are monitored to find when STUN packets are sent to and recieved from player IPs. The region-specific geolocating comes from [ip-api](https://ip-api.com).

# How it works (V1)
The program reads the Steam ID and character name of active players from the game's memory (like Cheat Engine). From there we use the Steam API function `GetP2PSessionState` to get the remote IP address. Since most routers deny ICMP ping requests, I use a traceroute like method to ping the network node that is closest to the player IP. This gives a pretty good estimate for the ping, but it will always be lower than the true value. Hence I also provide region-specific geolocating using [ip-api](https://ip-api.com) to query the country and region (state) information.

Credits to the developers of the DS3 Grand Archives Cheat Table for the player data pointers.
