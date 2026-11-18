using System;
using System.Collections.Generic;

namespace Mejnun.UI
{
    internal static class BotNames
    {
        internal static readonly string[] Male =
        {
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
            "Bima", "Rizky", "Dimas", "Arya", "Budi", "Fajar", "Bayu", "Raka"
        };

        internal static readonly string[] Female =
        {
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

        internal static readonly List<string> All = new List<string>(Male.Length + Female.Length);

        static BotNames()
        {
            All.AddRange(Male);
            All.AddRange(Female);
        }

        internal static bool Contains(string name)
        {
            return All.Contains(name);
        }

        internal static string[] PickDistinct(int count)
        {
            if (count < 0 || count > All.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var candidates = new List<string>(All);
            for (var index = candidates.Count - 1; index > 0; index--)
            {
                var other = UnityEngine.Random.Range(0, index + 1);
                var name = candidates[index];
                candidates[index] = candidates[other];
                candidates[other] = name;
            }

            var result = new string[count];
            candidates.CopyTo(0, result, 0, count);
            return result;
        }
    }
}
