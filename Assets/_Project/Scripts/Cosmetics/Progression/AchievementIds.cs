using System;
using Mejnun.Localization;

namespace Mejnun.Cosmetics
{
    public static class AchievementIds
    {
        public const string Regular = "regular";
        public const string Veteran = "veteran";
        public const string Seasoned = "seasoned";
        public const string MasterOfMejnun = "master_of_mejnun";
        public const string LevelTen = "level_ten";
        public const string LevelTwentyFive = "level_twenty_five";
        public const string LevelFifty = "level_fifty";
        public const string Collector = "collector";
        public const string MejnunTen = "mejnun_ten";
        public const string CleanSweep = "clean_sweep";
        public const string Lightning = "lightning";
        public const string LastGasp = "last_gasp";
        public const string Comeback = "comeback";
        public const string DownToTheWire = "down_to_the_wire";
        public const string RoyalRoad = "royal_road";
        public const string AceHigh = "ace_high";
        public const string BigEntrance = "big_entrance";
        public const string Architect = "architect";
        public const string Surgeon = "surgeon";
        public const string BorrowedTime = "borrowed_time";
        public const string SeeingDouble = "seeing_double";
        public const string Patient = "patient";
        public const string NoTrainingWheels = "no_training_wheels";
        public const string Halas = "halas";
        public const string CoolUnderPressure = "cool_under_pressure";
        public const string SpeedDemon = "speed_demon";
        public const string GiantSlayer = "giant_slayer";
        public const string HatTrick = "hat_trick";
        public const string Unstoppable = "unstoppable";
        public const string GoingOnline = "going_online";
        public const string CrowdPleaser = "crowd_pleaser";
        public const string PartyHost = "party_host";
        public const string PassItOn = "pass_it_on";
        public const string EvenSteven = "even_steven";
        public const string HotSeat = "hot_seat";

        public const string FirstGame = "first_game";
        public const string FirstWin = "first_win";
        public const string FirstMejnun = "first_mejnun";
        public const string TenWins = "ten_wins";
        public const string BeatHardBot = "beat_hard_bot";
        public const string FullTable = "full_table";

        public static readonly string[] All =
        {
            FirstGame, FirstWin, FirstMejnun, TenWins, BeatHardBot, FullTable,
            Regular, Veteran, Seasoned, MasterOfMejnun, LevelTen, LevelTwentyFive, LevelFifty, Collector,
            MejnunTen, CleanSweep, Lightning, LastGasp, Comeback, DownToTheWire,
            RoyalRoad, AceHigh, BigEntrance, Architect, Surgeon, BorrowedTime, SeeingDouble, Patient,
            NoTrainingWheels, Halas, CoolUnderPressure, SpeedDemon, GiantSlayer, HatTrick, Unstoppable,
            GoingOnline, CrowdPleaser, PartyHost, PassItOn, EvenSteven, HotSeat
        };

        // Every achievement's name and description live in the UI String Table
        // under keys derived from the stable id; these two builders are the only
        // place the key scheme is spelled out.
        public static string NameKey(string id) => "achievement." + id + ".name";

        public static string DescriptionKey(string id) => "achievement." + id + ".description";

        public static bool IsKnown(string id) => !string.IsNullOrWhiteSpace(id) && Array.IndexOf(All, id) >= 0;

        public static string DisplayName(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Loc.Get("achievement.unknown.name");
            }

            return IsKnown(id) ? Loc.Get(NameKey(id)) : id;
        }

        public static string Description(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Loc.Get("achievement.unknown.description");
            }

            return IsKnown(id) ? Loc.Get(DescriptionKey(id)) : id;
        }
    }
}