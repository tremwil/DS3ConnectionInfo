using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DS3ConnectionInfo
{
    /// <summary>
    /// "Loyalty" refers to who that individual is not allowed to hurt, "Mission" refers to who the individual is incentivized to hurt
    /// </summary>
	public enum TeamAllegiance
	{
		Host,       /// Loyal to host, mission to keep host alive until and through critical battle
        Protector,  /// Loyal to host, mission to keep host alive until invaders are dispatched
        Mad,        /// Loyal to none, mission to kill the host or any phantom
        Invader,    /// Loyal to none, mission to kill the host
        Defender,   /// Loyal to each other, mission to kill host
        Enemy,      /// Loyal to each other, mission to kill host and those loyal, or any invader if Seed of a Giant used
        Unknown     /// Loyalty and mission unidentified, complicated, or depends on host actions
	}

    public class Team
    {
        public string Name { get; private set; }
        public TeamAllegiance Allegiance { get; private set; }
        public string Color => colors[Allegiance];

        private static readonly Dictionary<int, Team> teams = new Dictionary<int, Team>()
        {
            {1,  new Team("Host",                                       TeamAllegiance.Host) },
            {2,  new Team("Phantom",                                    TeamAllegiance.Host) },
            {3,  new Team("Black Phantom",                              TeamAllegiance.Invader) },
            {4,  new Team("Hollow",                                     TeamAllegiance.Host) },
            {6,  new Team("Enemy",                                      TeamAllegiance.Enemy) },
            {7,  new Team("Boss (giants, big lizard)",                  TeamAllegiance.Enemy) },
            {8,  new Team("Friend",                                     TeamAllegiance.Host) },
            {9,  new Team("AngryFriend",                                TeamAllegiance.Enemy) },
            {10, new Team("DecoyEnemy",                                 TeamAllegiance.Enemy) },
            {11, new Team("BloodChild",                                 TeamAllegiance.Unknown) },
            {12, new Team("BattleFriend",                               TeamAllegiance.Unknown) },
            {13, new Team("Dragon",                                     TeamAllegiance.Unknown) },
            {16, new Team("Dark Spirit",                                TeamAllegiance.Invader) },
            {17, new Team("Watchdog of Farron",                         TeamAllegiance.Defender) },
            {18, new Team("Aldrich Faithful",                           TeamAllegiance.Defender) },
            {24, new Team("Darkwraiths",                                TeamAllegiance.Unknown) },
            {26, new Team("NPC",                                        TeamAllegiance.Unknown) },
            {27, new Team("Hostile NPC",                                TeamAllegiance.Unknown) },
            {29, new Team("Arena",                                      TeamAllegiance.Unknown) },
            {31, new Team("Mad Phantom",                                TeamAllegiance.Mad) },
            {32, new Team("Mad Spirit",                                 TeamAllegiance.Mad) },
            {33, new Team("Giant crabs, Dragons from Lothric castle",   TeamAllegiance.Enemy) },
            {0,  new Team("None",                                       TeamAllegiance.Unknown) }
        };

        private static readonly Dictionary<TeamAllegiance, string> colors = new Dictionary<TeamAllegiance, string>()
        {
            { TeamAllegiance.Host,      "#FFFFFFFF" },
            { TeamAllegiance.Protector, "#FF0000FF" },
            { TeamAllegiance.Mad,       "#FFC71585" },
            { TeamAllegiance.Invader,   "#FFFF0000" },
            { TeamAllegiance.Defender,  "#FF7B68EE" },
            { TeamAllegiance.Enemy,     "#FF006400" },
            { TeamAllegiance.Unknown,   "#FFFFA500" },
        };

        private Team(string name, TeamAllegiance allegiance)
        {
            Name = name;
            Allegiance = allegiance;
		}

        public static Team GetTeamFromId(int id)
        {
            return teams.ContainsKey(id) ? teams[id] : teams[0];
		}
	}
}
