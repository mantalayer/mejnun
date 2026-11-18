namespace Mejnun.Cosmetics
{
    public static class AchievementIds
    {
        public const string FirstGame = "first_game";
        public const string FirstWin = "first_win";
        public const string FirstMejnun = "first_mejnun";
        public const string TenWins = "ten_wins";
        public const string BeatHardBot = "beat_hard_bot";
        public const string FullTable = "full_table";

        public static string DisplayName(string id)
        {
            switch (id)
            {
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
    }
}