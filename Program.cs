using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

class Program
{
    public class Challenge
    {
        public string Name { get; set; }
        public int XP { get; set; }
        public bool Completed { get; set; }
        public string Category { get; set; }
        public string Note { get; set; }
    }

    // AppData\Local resolves to the correct user's folder on any machine
    static string saveFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CSharpXPTracker",
        "xp_tracker.json"
    );

    // Level thresholds — based on 11,750 total XP across 10 levels
    static int[] LevelThresholds = { 0, 500, 1200, 2200, 3400, 4800, 6300, 7800, 9300, 10800 };

    static void Main()
    {
        // Required for checkmarks and progress bar to render correctly on Windows
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine($"  Save file: {saveFile}");
        Console.WriteLine();

        List<Challenge> challenges = LoadChallenges();

        while (true)
        {
            Console.Clear();
            PrintHeader();

            // Group by category
            var categories = challenges
                .Select(c => c.Category)
                .Distinct()
                .ToList();

            int totalXP = challenges.Where(c => c.Completed).Sum(c => c.XP);
            int totalPossible = challenges.Sum(c => c.XP);
            int level = GetLevel(totalXP);
            int nextLevelXP = level < 10 ? LevelThresholds[level] : totalPossible;
            int prevLevelXP = LevelThresholds[level - 1];

            // Print level info
            Console.ForegroundColor = ConsoleColor.Yellow;
            int barFilled = (int)(20.0 * (totalXP - prevLevelXP) / Math.Max(1, nextLevelXP - prevLevelXP));
            barFilled = Math.Min(20, Math.Max(0, barFilled));
            string bar = "[" + new string('█', barFilled) + new string('░', 20 - barFilled) + "]";
            Console.WriteLine($"  Level {level,2} / 10   {bar}   {totalXP} / {totalPossible} XP total");
            if (level < 10)
                Console.WriteLine($"            Next level at {nextLevelXP} XP  ({nextLevelXP - totalXP} to go)");
            else
                Console.WriteLine("            *** LEVEL 10 REACHED — MASTER PROGRAMMER! ***");
            Console.ResetColor();
            Console.WriteLine();

            // Display mode menu
            Console.WriteLine("  [V]iew all   [F]ilter by category   [S]earch   [0] Save & Exit");
            Console.Write("  Choice: ");
            string input = Console.ReadLine()?.Trim().ToUpper() ?? "";

            if (input == "0")
            {
                SaveChallenges(challenges);
                Console.WriteLine("Progress saved. Farewell, programmer!");
                break;
            }
            else if (input == "V" || input == "")
            {
                BrowseAndToggle(challenges, challenges);
            }
            else if (input == "F")
            {
                Console.Clear();
                PrintHeader();
                Console.WriteLine("  Categories:");
                for (int i = 0; i < categories.Count; i++)
                {
                    var catChallenges = challenges.Where(c => c.Category == categories[i]).ToList();
                    int catXP = catChallenges.Where(c => c.Completed).Sum(c => c.XP);
                    int catTotal = catChallenges.Sum(c => c.XP);
                    Console.WriteLine($"    {i + 1}. {categories[i],-35} {catXP,4} / {catTotal,4} XP");
                }
                Console.Write("\n  Category number (or Enter to cancel): ");
                if (int.TryParse(Console.ReadLine(), out int catChoice) && catChoice >= 1 && catChoice <= categories.Count)
                {
                    var filtered = challenges.Where(c => c.Category == categories[catChoice - 1]).ToList();
                    BrowseAndToggle(challenges, filtered);
                }
            }
            else if (input == "S")
            {
                Console.Write("  Search: ");
                string query = Console.ReadLine()?.Trim().ToLower() ?? "";
                if (!string.IsNullOrEmpty(query))
                {
                    var filtered = challenges
                        .Where(c => c.Name.ToLower().Contains(query) || (c.Note?.ToLower().Contains(query) ?? false))
                        .ToList();
                    BrowseAndToggle(challenges, filtered);
                }
            }

            SaveChallenges(challenges);
        }
    }

    static void BrowseAndToggle(List<Challenge> allChallenges, List<Challenge> displayList)
    {
        while (true)
        {
            Console.Clear();
            PrintHeader();

            string currentCategory = null;
            for (int i = 0; i < displayList.Count; i++)
            {
                var c = displayList[i];

                // Print category header when it changes
                if (c.Category != currentCategory)
                {
                    currentCategory = c.Category;
                    var catChallenges = displayList.Where(x => x.Category == currentCategory).ToList();
                    int catXP = catChallenges.Where(x => x.Completed).Sum(x => x.XP);
                    int catTotal = catChallenges.Sum(x => x.XP);
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\n  ── {currentCategory} ──  ({catXP}/{catTotal} XP)");
                    Console.ResetColor();
                }

                // Challenge line
                Console.ForegroundColor = c.Completed ? ConsoleColor.Green : ConsoleColor.Gray;
                string check = c.Completed ? "✓" : " ";
                string noteTag = c.Note != null ? " *" : "";
                Console.WriteLine($"  {i + 1,3}. [{check}] {c.Name,-45} {c.XP,4} XP{noteTag}");
                Console.ResetColor();
            }

            int totalXP = allChallenges.Where(c => c.Completed).Sum(c => c.XP);
            int earned = displayList.Where(c => c.Completed).Sum(c => c.XP);
            int possible = displayList.Sum(c => c.XP);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n  Showing: {earned} / {possible} XP earned   |   All-time Total: {totalXP} XP   |   Level {GetLevel(totalXP)}");
            Console.ResetColor();
            Console.WriteLine("  Enter number to toggle, or 0 to go back:");
            Console.Write("  > ");

            if (!int.TryParse(Console.ReadLine(), out int choice))
                continue;

            if (choice == 0) return;

            if (choice >= 1 && choice <= displayList.Count)
            {
                var target = displayList[choice - 1];
                // Find it in the master list and toggle
                var master = allChallenges.First(c => c.Name == target.Name && c.Category == target.Category);
                master.Completed = !master.Completed;

                // Save immediately on every toggle — don't wait for menu exit
                SaveChallenges(allChallenges);

                // Always show feedback so the change is visible before redraw
                Console.ForegroundColor = master.Completed ? ConsoleColor.Green : ConsoleColor.DarkGray;
                string status = master.Completed
                    ? $"✓  Marked complete!  +{master.XP} XP  →  {master.Name}"
                    : $"○  Marked incomplete  -{master.XP} XP  →  {master.Name}";
                Console.WriteLine($"\n  {status}");
                Console.ResetColor();

                if (master.Note != null && master.Completed)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine($"  Note: {master.Note}");
                    Console.ResetColor();
                }

                Console.WriteLine("  Press Enter to continue...");
                Console.ReadLine();
            }
            else
            {
                Console.WriteLine("  Invalid choice. Press Enter...");
                Console.ReadLine();
            }
        }
    }

    static void PrintHeader()
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine();
        Console.WriteLine("  ╔══════════════════════════════════════════╗");
        Console.WriteLine("  ║   C# Player's Guide — XP Tracker v5.0   ║");
        Console.WriteLine("  ╚══════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
    }

    static int GetLevel(int xp)
    {
        for (int i = LevelThresholds.Length - 1; i >= 0; i--)
        {
            if (xp >= LevelThresholds[i])
                return i + 1;
        }
        return 1;
    }

    static List<Challenge> LoadChallenges()
    {
        try
        {
            if (File.Exists(saveFile))
            {
                string json = File.ReadAllText(saveFile);
                var loaded = JsonSerializer.Deserialize(json, AppJsonContext.Default.ListChallenge);
                if (loaded != null && loaded.Count > 0) return loaded;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading save file: {ex.Message}");
            Console.WriteLine("Starting fresh. Press Enter...");
            Console.ReadLine();
        }

        return DefaultChallenges();
    }

    static void SaveChallenges(List<Challenge> challenges)
    {
        try
        {
            string json = JsonSerializer.Serialize(challenges, AppJsonContext.Default.ListChallenge);
            Directory.CreateDirectory(Path.GetDirectoryName(saveFile)!); // creates folder if it doesn't exist
            File.WriteAllText(saveFile, json);
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine($"  [Saved]");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  [Save FAILED: {ex.Message}]");
            Console.ResetColor();
        }
    }

    static List<Challenge> DefaultChallenges() => new List<Challenge>
    {
        // ── PART 1: THE BASICS ─────────────────────────────────────────────
        new Challenge { Category = "Part 1: The Basics", Name = "Knowledge Check - C#",                XP = 25  },
        new Challenge { Category = "Part 1: The Basics", Name = "Install Visual Studio",               XP = 75  },
        new Challenge { Category = "Part 1: The Basics", Name = "Hello, World!",                       XP = 50  },
        new Challenge { Category = "Part 1: The Basics", Name = "What Comes Next",                     XP = 50  },
        new Challenge { Category = "Part 1: The Basics", Name = "The Makings of a Programmer",         XP = 50  },
        new Challenge { Category = "Part 1: The Basics", Name = "Consolas and Telim",                  XP = 50  },
        new Challenge { Category = "Part 1: The Basics", Name = "The Thing Namer 3000",                XP = 100 },
        new Challenge { Category = "Part 1: The Basics", Name = "Knowledge Check - Variables",         XP = 25  },
        new Challenge { Category = "Part 1: The Basics", Name = "The Variable Shop",                   XP = 100 },
        new Challenge { Category = "Part 1: The Basics", Name = "The Variable Shop Returns",           XP = 50  },
        new Challenge { Category = "Part 1: The Basics", Name = "Knowledge Check - Type System",       XP = 25  },
        new Challenge { Category = "Part 1: The Basics", Name = "The Triangle Farmer",                 XP = 100 },
        new Challenge { Category = "Part 1: The Basics", Name = "The Four Sisters and the Duckbear",   XP = 100 },
        new Challenge { Category = "Part 1: The Basics", Name = "The Dominion of Kings",               XP = 100 },
        new Challenge { Category = "Part 1: The Basics", Name = "The Defense of Consolas",             XP = 200 },
        new Challenge { Category = "Part 1: The Basics", Name = "Repairing the Clocktower",            XP = 100 },
        new Challenge { Category = "Part 1: The Basics", Name = "Watchtower",                          XP = 100 },
        new Challenge { Category = "Part 1: The Basics", Name = "Buying Inventory",                    XP = 100 },
        new Challenge { Category = "Part 1: The Basics", Name = "Discounted Inventory",                XP = 50  },
        new Challenge { Category = "Part 1: The Basics", Name = "The Prototype",                       XP = 100 },
        new Challenge { Category = "Part 1: The Basics", Name = "The Magic Cannon",                    XP = 100 },
        new Challenge { Category = "Part 1: The Basics", Name = "The Replicator of D'To",              XP = 100 },
        new Challenge { Category = "Part 1: The Basics", Name = "The Laws of Freach",                  XP = 50  },
        new Challenge { Category = "Part 1: The Basics", Name = "Taking a Number",                     XP = 100 },
        new Challenge { Category = "Part 1: The Basics", Name = "Countdown",                           XP = 100 },
        new Challenge { Category = "Part 1: The Basics", Name = "Knowledge Check - Memory",            XP = 25  },
        new Challenge { Category = "Part 1: The Basics", Name = "Hunting the Manticore",               XP = 250 },

        // ── PART 2: OBJECT-ORIENTED PROGRAMMING ───────────────────────────
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "Knowledge Check - Objects",          XP = 25  },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "Simula's Test",                      XP = 100 },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "Simula's Soup",                      XP = 100 },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "Vin Fletcher's Arrows",              XP = 100 },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "Vin's Trouble",                      XP = 50  },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "The Properties of Arrows",           XP = 100 },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "Arrow Factories",                    XP = 100 },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "The Point",                          XP = 75  },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "The Color",                          XP = 100 },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "The Card",                           XP = 100 },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "The Locked Door",                    XP = 100 },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "The Password Validator",             XP = 100 },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "Rock-Paper-Scissors",                XP = 150 },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "15-Puzzle",                          XP = 150 },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "Hangman",                            XP = 150 },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "Tic-Tac-Toe",                        XP = 300 },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "Packing Inventory",                  XP = 150 },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "Labeling Inventory",                 XP = 50  },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "The Old Robot",                      XP = 200 },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "Robotic Interface",                  XP = 75  },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "Room Coordinates",                   XP = 50  },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "War Preparations",                   XP = 100 },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "Colored Items",                      XP = 100 },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "The Fountain of Objects",            XP = 500 },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "Small, Medium, or Large",            XP = 100 },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "Pits",                               XP = 100 },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "Maelstroms",                         XP = 100 },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "Amaroks",                            XP = 100 },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "Getting Armed",                      XP = 100 },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "Getting Help",                       XP = 100 },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "The Robot Pilot",                    XP = 50  },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "Time in the Cavern",                 XP = 50  },
        new Challenge { Category = "Part 2: Object-Oriented Programming", Name = "Lists of Commands",                  XP = 75  },

        // ── PART 3: ADVANCED FEATURES ─────────────────────────────────────
        new Challenge { Category = "Part 3: Advanced Features", Name = "Knowledge Check - Large Programs",      XP = 25  },
        new Challenge { Category = "Part 3: Advanced Features", Name = "The Feud",                              XP = 75  },
        new Challenge { Category = "Part 3: Advanced Features", Name = "Dueling Traditions",                    XP = 100 },
        new Challenge { Category = "Part 3: Advanced Features", Name = "Safer Number Crunching",                XP = 50  },
        new Challenge { Category = "Part 3: Advanced Features", Name = "Knowledge Check - Methods",             XP = 25  },
        new Challenge { Category = "Part 3: Advanced Features", Name = "Better Random",                         XP = 100 },
        new Challenge { Category = "Part 3: Advanced Features", Name = "Exepti's Game",                         XP = 100 },
        new Challenge { Category = "Part 3: Advanced Features", Name = "The Sieve",                             XP = 100 },
        new Challenge { Category = "Part 3: Advanced Features", Name = "Knowledge Check - Events",              XP = 25  },
        new Challenge { Category = "Part 3: Advanced Features", Name = "Charberry Trees",                       XP = 100 },
        new Challenge { Category = "Part 3: Advanced Features", Name = "Knowledge Check - Lambdas",             XP = 25  },
        new Challenge { Category = "Part 3: Advanced Features", Name = "The Lambda Sieve",                      XP = 50  },
        new Challenge { Category = "Part 3: Advanced Features", Name = "The Long Game",                         XP = 100 },
        new Challenge { Category = "Part 3: Advanced Features", Name = "The Potion Masters of Pattren",         XP = 150 },
        new Challenge { Category = "Part 3: Advanced Features", Name = "Knowledge Check - Operators",           XP = 25  },
        new Challenge { Category = "Part 3: Advanced Features", Name = "Navigating Operand City",               XP = 100 },
        new Challenge { Category = "Part 3: Advanced Features", Name = "Indexing Operand City",                 XP = 75  },
        new Challenge { Category = "Part 3: Advanced Features", Name = "Converting Directions to Offsets",      XP = 50  },
        new Challenge { Category = "Part 3: Advanced Features", Name = "Knowledge Check - Queries",             XP = 25  },
        new Challenge { Category = "Part 3: Advanced Features", Name = "The Three Lenses",                      XP = 100 },
        new Challenge { Category = "Part 3: Advanced Features", Name = "The Repeating Stream",                  XP = 150 },
        new Challenge { Category = "Part 3: Advanced Features", Name = "Knowledge Check - Async",               XP = 25  },
        new Challenge { Category = "Part 3: Advanced Features", Name = "Asynchronous Random Words",             XP = 150 },
        new Challenge { Category = "Part 3: Advanced Features", Name = "Many Random Words",                     XP = 50  },
        new Challenge { Category = "Part 3: Advanced Features", Name = "Uniter of Adds",                        XP = 75  },
        new Challenge { Category = "Part 3: Advanced Features", Name = "The Robot Factory",                     XP = 100 },
        new Challenge { Category = "Part 3: Advanced Features", Name = "Knowledge Check - Unsafe Code",         XP = 25  },
        new Challenge { Category = "Part 3: Advanced Features", Name = "Knowledge Check - Other Features",      XP = 25  },
        new Challenge { Category = "Part 3: Advanced Features", Name = "Colored Console",                       XP = 100 },
        new Challenge { Category = "Part 3: Advanced Features", Name = "The Great Humanizer",                   XP = 100 },
        new Challenge { Category = "Part 3: Advanced Features", Name = "Knowledge Check - Compiling",           XP = 25  },
        new Challenge { Category = "Part 3: Advanced Features", Name = "Knowledge Check - .NET",                XP = 25  },
        new Challenge { Category = "Part 3: Advanced Features", Name = "Altar of Publication",                  XP = 100 },

        // ── PART 4: THE ENDGAME ───────────────────────────────────────────
        new Challenge { Category = "Part 4: The Endgame", Name = "Core Game: Building Character",   XP = 300 },
        new Challenge { Category = "Part 4: The Endgame", Name = "Core Game: The True Programmer",  XP = 100 },
        new Challenge { Category = "Part 4: The Endgame", Name = "Core Game: Actions and Players",  XP = 300 },
        new Challenge { Category = "Part 4: The Endgame", Name = "Core Game: Attacks",              XP = 200 },
        new Challenge { Category = "Part 4: The Endgame", Name = "Core Game: Damage and HP",        XP = 150 },
        new Challenge { Category = "Part 4: The Endgame", Name = "Core Game: Death",                XP = 150 },
        new Challenge { Category = "Part 4: The Endgame", Name = "Core Game: Battle Series",        XP = 150 },
        new Challenge { Category = "Part 4: The Endgame", Name = "Core Game: The Uncoded One",      XP = 100 },
        new Challenge { Category = "Part 4: The Endgame", Name = "Core Game: The Player Decides",   XP = 200 },
        new Challenge { Category = "Part 4: The Endgame", Name = "Expansion: The Game's Status",    XP = 100 },
        new Challenge { Category = "Part 4: The Endgame", Name = "Expansion: Items",                XP = 200 },
        new Challenge { Category = "Part 4: The Endgame", Name = "Expansion: Gear",                 XP = 300 },
        new Challenge { Category = "Part 4: The Endgame", Name = "Expansion: Stolen Inventory",     XP = 200 },
        new Challenge { Category = "Part 4: The Endgame", Name = "Expansion: Vin Fletcher",         XP = 200 },
        new Challenge { Category = "Part 4: The Endgame", Name = "Expansion: Attack Modifiers",     XP = 200 },
        new Challenge { Category = "Part 4: The Endgame", Name = "Expansion: Damage Types",         XP = 200, Note = "Requires Attack Modifiers to be completed first." },
        new Challenge { Category = "Part 4: The Endgame", Name = "Expansion: Making it Yours",      XP = 50,  Note = "Variable XP: 50–400. Use your best judgment based on the effort put in. XP here is set to the minimum (50); adjust the save file if you award yourself more." },
        new Challenge { Category = "Part 4: The Endgame", Name = "Expansion: Restoring Balance",    XP = 150 },

        // ── PART 5: BONUS LEVELS ──────────────────────────────────────────
        new Challenge { Category = "Part 5: Bonus Levels", Name = "Knowledge Check - Visual Studio",    XP = 25 },
        new Challenge { Category = "Part 5: Bonus Levels", Name = "Knowledge Check - Compiler Errors",  XP = 25 },
        new Challenge { Category = "Part 5: Bonus Levels", Name = "Knowledge Check - Debugging",        XP = 25 },
    };
}

// Source generator: produces reflection-free JSON serialization at compile time
[JsonSerializable(typeof(List<Program.Challenge>))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class AppJsonContext : JsonSerializerContext { }
