using System;
using System.Collections.Generic;
using System.Globalization;
using Mejnun.Localization;

namespace Mejnun.Game
{
    /// <summary>
    /// Seat names the game generates (the default "Player 2", stock bot names, the tutorial's "You" and "Layla") are
    /// stored as IDs in <see cref="SeatConfig.Name"/>, saves and match requests, and resolved in the current language
    /// only when shown. Names a player typed are stored and shown as typed.
    /// </summary>
    public static class SeatNames
    {
        /// <summary>"Player {seat}" for a human seat without a name.</summary>
        public const string DefaultPlayer = "match.seat.player";

        /// <summary>"Bot {seat}" for a bot seat without a name.</summary>
        public const string DefaultBot = "match.seat.bot";

        public const string TutorialPlayer = "tutorial.name.player";
        public const string TutorialCpu = "tutorial.name.cpu";

        /// <summary>The name to show for a seat, in the current language.</summary>
        public static string Display(string name, int seat)
        {
            switch (name)
            {
                case DefaultPlayer:
                    return Loc.Format("match.seat.player", seat + 1);
                case DefaultBot:
                    return Loc.Format("match.seat.bot", seat + 1);
                case TutorialPlayer:
                    return Loc.Get("tutorial.name.player");
                case TutorialCpu:
                    return Loc.Get("tutorial.name.cpu");
            }

            if (BotNameCatalog.TryParseKey(name, out var botIndex))
            {
                return BotNameCatalog.DisplayName(botIndex);
            }

            return string.IsNullOrWhiteSpace(name) ? Loc.Format("match.seat.player", seat + 1) : name;
        }

        public static bool IsId(string name)
        {
            return name == DefaultPlayer || name == DefaultBot || name == TutorialPlayer || name == TutorialCpu ||
                   BotNameCatalog.TryParseKey(name, out _);
        }

        /// <summary>
        /// The value to store for a seat name: an ID when the name is empty or is an untouched generated name (in
        /// English or the current language, e.g. from the setup screen or a save made before localization),
        /// otherwise the trimmed name as typed.
        /// </summary>
        public static string ToId(string name, bool isBot, int seat)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return isBot ? DefaultBot : DefaultPlayer;
            }

            var trimmed = name.Trim();
            if (IsId(trimmed))
            {
                return trimmed;
            }

            var number = (seat + 1).ToString(CultureInfo.InvariantCulture);
            // English forms are only compared against, to recognise names saved before localization.
            if (trimmed == "Player " + number || trimmed == Loc.Format("match.seat.player", seat + 1))
            {
                return DefaultPlayer;
            }

            if (trimmed == "Bot " + number || trimmed == Loc.Format("match.seat.bot", seat + 1))
            {
                return DefaultBot;
            }

            if (isBot && BotNameCatalog.TryFind(trimmed, out var botIndex))
            {
                return BotNameCatalog.KeyOf(botIndex);
            }

            return trimmed;
        }
    }

    /// <summary>
    /// The stock bot names. Each is the String Table entry <c>bot.name.NNN</c> (001 = Liam … 368 = Wulan), so the
    /// pipeline can transliterate them; seats store the key, never the display text.
    /// </summary>
    public static class BotNameCatalog
    {
        private const string KeyPrefix = "bot.name.";
        public const int MaleCount = 184;

        // English source text of the bot.name.NNN entries, in key order. Never shown directly: display goes through
        // the String Table. Kept to recognise names typed or saved in English.
        private static readonly string[] SourceNames =
        {
            // Male
            // United States
            "Liam", "Noah", "Oliver", "Theodore", "James", "Henry", "Elijah", "William",
            // United Kingdom
            "Muhammad", "Arthur", "George", "Thomas", "Oscar", "Jack", "Harry", "Alfie",
            // Spain
            "Mateo", "Hugo", "Martin", "Leo", "Lucas", "Pablo", "Alejandro", "Manuel",
            // Mexico
            "Santiago", "Sebastian", "Emiliano", "Diego", "Leonardo", "Daniel", "Mauricio", "Iker",
            // France
            "Gabriel", "Raphael", "Louis", "Jules", "Adam", "Mael", "Sacha", "Gabin",
            // Germany
            "Matteo", "Emil", "Elias", "Finn", "Paul", "Felix", "Jonas", "Ben",
            // Italy
            "Lorenzo", "Tommaso", "Edoardo", "Andrea", "Francesco", "Alessandro", "Mattia", "Riccardo",
            // Brazil
            "Miguel", "Heitor", "Artur", "Gael", "Ravi", "Bernardo", "Davi", "Theo",
            // Sweden
            "Nils", "August", "Axel", "Arvid", "Ludvig", "Olle", "Vincent", "Hjalmar",
            // Poland
            "Antoni", "Jan", "Aleksander", "Franciszek", "Nikodem", "Szymon", "Filip", "Wojciech",
            // Russia
            "Mikhail", "Artem", "Maxim", "Ivan", "Dmitry", "Timofey", "Lev", "Matvey",
            // Greece
            "Georgios", "Dimitris", "Nikos", "Andreas", "Konstantinos", "Ioannis", "Panagiotis", "Christos",
            // Turkey
            "Yusuf", "Emir", "Kerem", "Eymen", "Mustafa", "Ali", "Ahmet", "Omer",
            // Iran
            "Kian", "Omid", "Arman", "Darius", "Reza", "Amir", "Parsa", "Hossein",
            // Arab world
            "Zayn", "Omar", "Ibrahim", "Tariq", "Hamza", "Khalid", "Karim", "Faisal",
            // India
            "Aarav", "Arjun", "Vihaan", "Aditya", "Reyansh", "Vivaan", "Krishna", "Ishaan",
            // Nigeria
            "Chukwuemeka", "Oluwaseun", "Babatunde", "Olumide", "Chinedu", "Obinna", "Adebayo", "Ifeanyi",
            // China
            "Yichen", "Yuxuan", "Haoyu", "Zimo", "Haoran", "Muchen", "Zirui", "Junjie",
            // Japan
            "Haruto", "Minato", "Ren", "Asahi", "Sota", "Yuto", "Riku", "Itsuki",
            // South Korea
            "Min-jun", "Seo-jun", "Ha-jun", "Do-yun", "Ji-ho", "Eun-woo", "Si-woo", "Ye-jun",
            // Vietnam
            "Minh", "Khoa", "Duy", "Tuan", "Huy", "Bao", "Phuc", "Quang",
            // Philippines
            "Jose", "Juan", "Carlo", "Joaquin", "Nathaniel", "Ezekiel", "Jacob", "Angelo",
            // Indonesia
            "Bima", "Rizky", "Dimas", "Arya", "Budi", "Fajar", "Bayu", "Raka",

            // Female
            // United States
            "Olivia", "Emma", "Amelia", "Charlotte", "Ava", "Isabella", "Evelyn", "Harper",
            // United Kingdom
            "Isla", "Lily", "Poppy", "Freya", "Ivy", "Florence", "Grace", "Willow",
            // Spain
            "Lucia", "Sofia", "Martina", "Valeria", "Julia", "Paula", "Daniela", "Alba",
            // Mexico
            "Regina", "Camila", "Valentina", "Ximena", "Renata", "Romina", "Mariana", "Natalia",
            // France
            "Louise", "Jade", "Ambre", "Alma", "Rose", "Anna", "Lina", "Agathe",
            // Germany
            "Sophia", "Emilia", "Hannah", "Mia", "Clara", "Lea", "Ella", "Marie",
            // Italy
            "Giulia", "Ginevra", "Vittoria", "Aurora", "Beatrice", "Ludovica", "Francesca", "Chiara",
            // Brazil
            "Helena", "Cecilia", "Maite", "Alice", "Laura", "Manuela", "Heloisa", "Liz",
            // Sweden
            "Elsa", "Vera", "Selma", "Nora", "Astrid", "Alva", "Ebba", "Saga",
            // Poland
            "Zofia", "Zuzanna", "Hanna", "Maja", "Lena", "Oliwia", "Pola", "Antonina",
            // Russia
            "Anastasia", "Polina", "Victoria", "Ekaterina", "Alisa", "Varvara", "Darya", "Ksenia",
            // Greece
            "Eleni", "Katerina", "Dimitra", "Georgia", "Maria", "Vasiliki", "Ioanna", "Despina",
            // Turkey
            "Zeynep", "Elif", "Defne", "Asli", "Eylul", "Azra", "Nehir", "Ecrin",
            // Iran
            "Leyla", "Sara", "Negin", "Parisa", "Zahra", "Shirin", "Roya", "Mahsa",
            // Arab world
            "Aisha", "Noor", "Maryam", "Fatima", "Layan", "Salma", "Jana", "Reem",
            // India
            "Aadhya", "Saanvi", "Ananya", "Diya", "Myra", "Kiara", "Anika", "Avni",
            // Nigeria
            "Amina", "Adaeze", "Ngozi", "Chioma", "Nneka", "Folake", "Temitope", "Ifeoma",
            // China
            "Yinuo", "Xinyi", "Zihan", "Yutong", "Ruoxi", "Yuxi", "Xinyan", "Mengyao",
            // Japan
            "Tsumugi", "Himari", "Rin", "Ema", "Sakura", "Yui", "Mio", "Hina",
            // South Korea
            "Seo-yeon", "Ji-woo", "Min-seo", "Ha-eun", "Ji-an", "Seo-ah", "Ha-yoon", "Su-ah",
            // Vietnam
            "Linh", "Mai", "Thao", "Ngoc", "Anh", "Trang", "Huong", "Lan",
            // Philippines
            "Angel", "Althea", "Samantha", "Nathalie", "Princess", "Janelle", "Kristine", "Jasmine",
            // Indonesia
            "Putri", "Dewi", "Siti", "Ayu", "Sari", "Intan", "Indah", "Wulan"
        };

        private static Dictionary<string, int> displayIndex;
        private static string displayIndexLocale;

        public static int Count => SourceNames.Length;

        /// <summary>The String Table key of a bot name: <c>bot.name.001</c> for index 0.</summary>
        public static string KeyOf(int index)
        {
            if (index < 0 || index >= SourceNames.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return KeyPrefix + (index + 1).ToString("000", CultureInfo.InvariantCulture);
        }

        /// <summary>All bot name keys, in order.</summary>
        public static IReadOnlyList<string> Keys
        {
            get
            {
                var keys = new string[SourceNames.Length];
                for (var index = 0; index < keys.Length; index++)
                {
                    keys[index] = KeyOf(index);
                }

                return keys;
            }
        }

        public static bool IsMale(int index)
        {
            return index >= 0 && index < MaleCount;
        }

        /// <summary>The bot name in the current language.</summary>
        public static string DisplayName(int index)
        {
            return Loc.GetFrom(Loc.UiTable, KeyOf(index));
        }

        public static bool TryParseKey(string name, out int index)
        {
            index = -1;
            if (name == null || name.Length != KeyPrefix.Length + 3 ||
                !name.StartsWith(KeyPrefix, StringComparison.Ordinal) ||
                !int.TryParse(name.Substring(KeyPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture,
                    out var number) ||
                number < 1 || number > SourceNames.Length)
            {
                return false;
            }

            index = number - 1;
            return true;
        }

        /// <summary>Finds a bot name from its key, its current-language text or its English text.</summary>
        public static bool TryFind(string name, out int index)
        {
            if (TryParseKey(name, out index))
            {
                return true;
            }

            index = -1;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var lookup = DisplayIndex();
            if (lookup.TryGetValue(name, out index))
            {
                return true;
            }

            index = Array.IndexOf(SourceNames, name);
            return index >= 0;
        }

        /// <summary><paramref name="count"/> different bot name indices in random order.</summary>
        public static int[] PickDistinct(int count)
        {
            if (count < 0 || count > SourceNames.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var candidates = new int[SourceNames.Length];
            for (var index = 0; index < candidates.Length; index++)
            {
                candidates[index] = index;
            }

            for (var index = candidates.Length - 1; index > 0; index--)
            {
                var other = UnityEngine.Random.Range(0, index + 1);
                var swap = candidates[index];
                candidates[index] = candidates[other];
                candidates[other] = swap;
            }

            var result = new int[count];
            Array.Copy(candidates, result, count);
            return result;
        }

        private static Dictionary<string, int> DisplayIndex()
        {
            var locale = Loc.CurrentLocaleCode;
            if (displayIndex != null && displayIndexLocale == locale)
            {
                return displayIndex;
            }

            var lookup = new Dictionary<string, int>(SourceNames.Length, StringComparer.Ordinal);
            for (var index = 0; index < SourceNames.Length; index++)
            {
                var display = DisplayName(index);
                if (!string.IsNullOrWhiteSpace(display) && !lookup.ContainsKey(display))
                {
                    lookup.Add(display, index);
                }
            }

            displayIndex = lookup;
            displayIndexLocale = locale;
            return lookup;
        }
    }
}
