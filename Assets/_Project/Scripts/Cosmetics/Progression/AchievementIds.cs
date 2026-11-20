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

        public static string DisplayName(string id)
        {
            switch (id)
            {
                case Regular:
                    return "Regular";
                case Veteran:
                    return "Veteran";
                case Seasoned:
                    return "Seasoned";
                case MasterOfMejnun:
                    return "Master of MEJNUN";
                case LevelTen:
                    return "Level 10";
                case LevelTwentyFive:
                    return "Level 25";
                case LevelFifty:
                    return "Level 50";
                case Collector:
                    return "Collector";
                case MejnunTen:
                    return "MEJNUN ×10";
                case CleanSweep:
                    return "Clean Sweep";
                case Lightning:
                    return "Lightning";
                case LastGasp:
                    return "Last Gasp";
                case Comeback:
                    return "Comeback";
                case DownToTheWire:
                    return "Down to the Wire";
                case RoyalRoad:
                    return "Royal Road";
                case AceHigh:
                    return "Ace High";
                case BigEntrance:
                    return "Big Entrance";
                case Architect:
                    return "Architect";
                case Surgeon:
                    return "Surgeon";
                case BorrowedTime:
                    return "Borrowed Time";
                case SeeingDouble:
                    return "Seeing Double";
                case Patient:
                    return "Patient";
                case NoTrainingWheels:
                    return "No Training Wheels";
                case Halas:
                    return "Halas!";
                case CoolUnderPressure:
                    return "Cool Under Pressure";
                case SpeedDemon:
                    return "Speed Demon";
                case GiantSlayer:
                    return "Giant Slayer";
                case HatTrick:
                    return "Hat Trick";
                case Unstoppable:
                    return "Unstoppable";
                case GoingOnline:
                    return "Going Online";
                case CrowdPleaser:
                    return "Crowd Pleaser";
                case PartyHost:
                    return "Party Host";
                case PassItOn:
                    return "Pass It On";
                case EvenSteven:
                    return "Even Steven";
                case HotSeat:
                    return "Hot Seat";
                case FirstGame:
                    return "First Game";
                case FirstWin:
                    return "First Win";
                case FirstMejnun:
                    return "First MEJNUN";
                case TenWins:
                    return "Ten Wins";
                case BeatHardBot:
                    return "Beat a Hard Bot";
                case FullTable:
                    return "Full Table";
                default:
                    return string.IsNullOrWhiteSpace(id) ? "an achievement" : id;
            }
        }

        public static string Description(string id)
        {
            switch (id)
            {
                case FirstGame: return "Finish your first game.";
                case FirstWin: return "Win your first game.";
                case FirstMejnun: return "Win by emptying your hand.";
                case TenWins: return "Win 10 games.";
                case BeatHardBot: return "Beat a hard bot.";
                case FullTable: return "Play a game with six seats.";
                case Regular: return "Play 50 games.";
                case Veteran: return "Play 250 games.";
                case Seasoned: return "Win 50 games.";
                case MasterOfMejnun: return "Win 200 games.";
                case LevelTen: return "Reach level 10.";
                case LevelTwentyFive: return "Reach level 25.";
                case LevelFifty: return "Reach level 50.";
                case Collector: return "Unlock every free or earnable deck face and felt.";
                case MejnunTen: return "Win 10 games by emptying your hand.";
                case CleanSweep: return "Go down and empty your hand in the same turn, with hints and Fix Board off.";
                case Lightning: return "Win within your first five turns.";
                case LastGasp: return "Win by emptying your hand with the draw pile empty.";
                case Comeback: return "Win after holding 20 or more cards.";
                case DownToTheWire: return "Win the end-of-pile count by exactly one card.";
                case RoyalRoad: return "Make a full 13-card straight from ace to king.";
                case AceHigh: return "Complete a queen-king-ace straight.";
                case BigEntrance: return "Go down with at least three sets at once.";
                case Architect: return "Place at least eight hand cards in one turn.";
                case Surgeon: return "Split a straight and end the turn with a valid board.";
                case BorrowedTime: return "Take a card from a four-card same-kind set and use it.";
                case SeeingDouble: return "End your turn with both copies of a card on separate sets.";
                case Patient: return "Win without using Undo, Revert, or Fix Board.";
                case NoTrainingWheels: return "Win with hints turned off.";
                case Halas: return "Have your turn time out.";
                case CoolUnderPressure: return "End a valid turn with less than three seconds left.";
                case SpeedDemon: return "Win a game played on the 30-second timer.";
                case GiantSlayer: return "Win against five hard bots in a full table.";
                case HatTrick: return "Win three bot games in a row.";
                case Unstoppable: return "Win five games in a row.";
                case GoingOnline: return "Finish an online game.";
                case CrowdPleaser: return "Win an online game with at least four human players.";
                case PartyHost: return "Host 10 online games.";
                case PassItOn: return "Finish a pass-and-play game with at least four humans.";
                case EvenSteven: return "Finish a game in a stalemate.";
                case HotSeat: return "Be chosen to go first by the question 10 times.";
                default: return string.IsNullOrWhiteSpace(id) ? "An achievement." : id;
            }
        }
    }
}