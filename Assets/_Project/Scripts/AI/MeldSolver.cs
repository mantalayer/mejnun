using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Mejnun.Core;

namespace Mejnun.AI
{
    public sealed class MeldSolution
    {
        private static readonly IReadOnlyList<IReadOnlyList<Card>> NoMelds = Array.AsReadOnly(new IReadOnlyList<Card>[0]);
        private static readonly IReadOnlyList<Card> NoCards = Array.AsReadOnly(new Card[0]);

        internal MeldSolution(bool feasible, bool budgetExceeded, IReadOnlyList<IReadOnlyList<Card>> melds,
            IReadOnlyList<Card> usedOptional, int searchedStates)
        {
            Feasible = feasible;
            BudgetExceeded = budgetExceeded;
            Melds = melds ?? NoMelds;
            UsedOptional = usedOptional ?? NoCards;
            SearchedStates = searchedStates;
        }

        /// <summary>True when every required card fits into a valid partition.</summary>
        public bool Feasible { get; }

        /// <summary>True when the search stopped early because it ran out of budget.</summary>
        public bool BudgetExceeded { get; }

        public IReadOnlyList<IReadOnlyList<Card>> Melds { get; }
        public IReadOnlyList<Card> UsedOptional { get; }
        public int OptionalUsed => UsedOptional.Count;
        public int SearchedStates { get; }

        internal static MeldSolution Infeasible(bool budgetExceeded, int searchedStates)
        {
            return new MeldSolution(false, budgetExceeded, null, null, searchedStates);
        }
    }

    /// <summary>
    /// Exact solver for "partition every required card plus as many optional cards as possible into valid sets".
    /// <para>
    /// Doubles are interchangeable for validity, so the search runs over (suit, value) counts. It is a memoised
    /// dynamic programme over the values A(low), 2 … K, A(high), one suit at a time, tracking for every suit the
    /// runs in progress (at most two, because each card exists twice) and the cards put into same-kind sets.
    /// </para>
    /// <para>
    /// An ace can be low or high, so the same physical aces are shared by both ends. Instead of carrying that
    /// choice through every state, each state stores a small vector with one result per "how many aces of each
    /// suit are kept for ace-high" combination (at most 3^4 = 81 entries). That keeps the search exact and fast.
    /// </para>
    /// </summary>
    public static class MeldSolver
    {
        /// <summary>Upper bound on states × ace combinations (also the bytes of the result pool).</summary>
        public const int DefaultBudget = 24000000;

        private const int SuitCount = 4;
        private const int LowAce = 1;
        private const int HighAce = 14;
        private const int ValueSlots = 15;
        private const int OpenRun = 3;

        public static MeldSolution Solve(IReadOnlyList<Card> required, IReadOnlyList<Card> optional,
            int budget = DefaultBudget)
        {
            var search = new Search(required ?? Array.Empty<Card>(), optional ?? Array.Empty<Card>(), budget);
            return search.Run();
        }

        /// <summary>Best partition of <paramref name="hand"/> alone (new sets only).</summary>
        public static MeldSolution SolveHandOnly(IReadOnlyList<Card> hand, int budget = DefaultBudget)
        {
            return Solve(Array.Empty<Card>(), hand, budget);
        }

        private static int ValueOf(Rank rank)
        {
            return (int)rank + 1;
        }

        private static Rank RankOf(int value)
        {
            return value == HighAce ? Rank.Ace : (Rank)(value - 1);
        }

        private static bool GroupsFeasible(int groupCards, bool anySuitTwice)
        {
            if (groupCards == 0)
            {
                return true;
            }

            if (!anySuitTwice && (groupCards == 3 || groupCards == 4))
            {
                return true;
            }

            return groupCards >= 6 && groupCards <= 8;
        }

        private static bool AllRunsClosed(int runs)
        {
            for (var suit = 0; suit < SuitCount; suit++)
            {
                var bits = (runs >> (suit * 4)) & 0xF;
                var shorter = bits >> 2;
                var longer = bits & 3;
                if ((shorter != 0 && shorter != OpenRun) || (longer != 0 && longer != OpenRun))
                {
                    return false;
                }
            }

            return true;
        }

        private static long Key(int value, int suit, int runs, int groupCards, bool anySuitTwice)
        {
            return runs | ((long)groupCards << 16) | ((anySuitTwice ? 1L : 0L) << 20) | ((long)suit << 21) |
                   ((long)value << 24);
        }

        private sealed class RunTrack
        {
            public RunTrack(Suit suit)
            {
                Suit = suit;
            }

            public readonly List<int> Values = new List<int>(13);
            public Suit Suit { get; }
            public int CappedLength => Values.Count < OpenRun ? Values.Count : OpenRun;
        }

        private sealed class Search
        {
            private const int ClosedVector = 0;
            private const int DeadVector = 1;

            private readonly int[] requiredCounts = new int[SuitCount * ValueSlots];
            private readonly int[] optionalCounts = new int[SuitCount * ValueSlots];
            private readonly List<Card>[] pools = new List<Card>[Card.CardsPerDeck];
            private readonly int[] requiredInPool = new int[Card.CardsPerDeck];
            private readonly int[] aceTotal = new int[SuitCount];
            private readonly int[] aceRequired = new int[SuitCount];
            private readonly int[] stride = new int[SuitCount];
            private readonly int width;
            private readonly sbyte[][] buffers;
            private readonly StateTable table = new StateTable();
            private readonly long budget;
            private readonly bool inputValid = true;
            private sbyte[] results;
            private int vectorCount;
            private bool budgetExceeded;

            public Search(IReadOnlyList<Card> required, IReadOnlyList<Card> optional, int cellBudget)
            {
                budget = cellBudget <= 0 ? DefaultBudget : cellBudget;
                for (var index = 0; index < pools.Length; index++)
                {
                    pools[index] = new List<Card>(2);
                }

                var seen = new HashSet<byte>();
                for (var index = 0; index < required.Count; index++)
                {
                    inputValid &= seen.Add(required[index].Id);
                    Add(required[index], true);
                }

                for (var index = 0; index < optional.Count; index++)
                {
                    inputValid &= seen.Add(optional[index].Id);
                    Add(optional[index], false);
                }

                width = 1;
                for (var suit = 0; suit < SuitCount; suit++)
                {
                    aceRequired[suit] = requiredCounts[suit * ValueSlots + LowAce];
                    aceTotal[suit] = aceRequired[suit] + optionalCounts[suit * ValueSlots + LowAce];
                    stride[suit] = width;
                    width *= aceTotal[suit] + 1;
                }

                buffers = new sbyte[ValueSlots * (SuitCount + 1)][];
                results = new sbyte[width * 256];
                for (var index = 0; index < width; index++)
                {
                    results[DeadVector * width + index] = -1;
                }

                vectorCount = 2;
            }

            public MeldSolution Run()
            {
                if (!inputValid)
                {
                    return MeldSolution.Infeasible(false, 0);
                }

                var root = Vector(LowAce, 0, 0, 0, false);
                if (root < 0)
                {
                    return MeldSolution.Infeasible(true, vectorCount);
                }

                var best = -1;
                var bestCombination = -1;
                for (var combination = 0; combination < width; combination++)
                {
                    var score = results[root * width + combination];
                    if (score > best)
                    {
                        best = score;
                        bestCombination = combination;
                    }
                }

                return best < 0 ? MeldSolution.Infeasible(false, vectorCount) : Reconstruct(bestCombination);
            }

            private void Add(Card card, bool isRequired)
            {
                var slot = (int)card.Suit * 13 + (int)card.Rank;
                var counts = isRequired ? requiredCounts : optionalCounts;
                counts[(int)card.Suit * ValueSlots + ValueOf(card.Rank)]++;
                if (isRequired)
                {
                    pools[slot].Insert(requiredInPool[slot], card);
                    requiredInPool[slot]++;
                }
                else
                {
                    pools[slot].Add(card);
                }
            }

            /// <summary>
            /// Returns the index of the result vector for this state: for every ace combination, the most optional
            /// cards that can still be placed from here on (-1 when impossible). Returns -1 when over budget.
            /// </summary>
            private int Vector(int value, int suit, int runs, int groupCards, bool anySuitTwice)
            {
                if (value > HighAce)
                {
                    return AllRunsClosed(runs) ? ClosedVector : DeadVector;
                }

                if (suit == SuitCount)
                {
                    return GroupsFeasible(groupCards, anySuitTwice)
                        ? Vector(value + 1, 0, runs, 0, false)
                        : DeadVector;
                }

                var key = Key(value, suit, runs, groupCards, anySuitTwice);
                if (table.TryGet(key, out var cached))
                {
                    return cached;
                }

                if (budgetExceeded || (long)(vectorCount + 1) * width > budget)
                {
                    budgetExceeded = true;
                    return -1;
                }

                var level = value * (SuitCount + 1) + suit;
                var result = buffers[level] ?? (buffers[level] = new sbyte[width]);
                for (var index = 0; index < width; index++)
                {
                    result[index] = -1;
                }

                if (value == LowAce || value == HighAce)
                {
                    for (var kept = 0; kept <= aceTotal[suit]; kept++)
                    {
                        AceUsage(value, suit, kept, out var usedMin, out var usedMax, out var gainBase);
                        var options = new OptionEnumerator(value, suit, runs, usedMin, usedMax);
                        while (options.MoveNext())
                        {
                            var option = options.Current;
                            var next = Vector(value, suit + 1, option.Runs, groupCards + option.GroupCards,
                                anySuitTwice || option.GroupCards == 2);
                            if (next < 0)
                            {
                                return -1;
                            }

                            Merge(result, next, option.Used - gainBase, suit, kept);
                        }
                    }
                }
                else
                {
                    var required = requiredCounts[suit * ValueSlots + value];
                    var maximum = required + optionalCounts[suit * ValueSlots + value];
                    var options = new OptionEnumerator(value, suit, runs, required, maximum);
                    while (options.MoveNext())
                    {
                        var option = options.Current;
                        var next = Vector(value, suit + 1, option.Runs, groupCards + option.GroupCards,
                            anySuitTwice || option.GroupCards == 2);
                        if (next < 0)
                        {
                            return -1;
                        }

                        Merge(result, next, option.Used - required, -1, 0);
                    }
                }

                var stored = Allocate();
                Array.Copy(result, 0, results, stored * width, width);
                table.Set(key, stored);
                return stored;
            }

            /// <summary>
            /// For an ace combination digit <paramref name="kept"/> (aces of this suit kept for ace-high): how many
            /// aces are used at this end and how many of those are required cards. Required aces are spent first at
            /// the low end, so optional aces are the ones kept back.
            /// </summary>
            private void AceUsage(int value, int suit, int kept, out int usedMin, out int usedMax, out int gainBase)
            {
                var total = aceTotal[suit];
                var required = aceRequired[suit];
                if (value == LowAce)
                {
                    usedMin = total - kept;
                    usedMax = usedMin;
                    gainBase = Math.Min(usedMin, required);
                    return;
                }

                var requiredKept = Math.Max(0, required - (total - kept));
                usedMin = requiredKept;
                usedMax = kept;
                gainBase = requiredKept;
            }

            private void Merge(sbyte[] result, int next, int gain, int digitSuit, int digit)
            {
                var offset = next * width;
                if (digitSuit < 0)
                {
                    for (var index = 0; index < width; index++)
                    {
                        var score = results[offset + index];
                        if (score >= 0 && score + gain > result[index])
                        {
                            result[index] = (sbyte)(score + gain);
                        }
                    }

                    return;
                }

                var digitStride = stride[digitSuit];
                var block = digitStride * (aceTotal[digitSuit] + 1);
                for (var high = 0; high < width; high += block)
                {
                    var start = high + digit * digitStride;
                    for (var index = start; index < start + digitStride; index++)
                    {
                        var score = results[offset + index];
                        if (score >= 0 && score + gain > result[index])
                        {
                            result[index] = (sbyte)(score + gain);
                        }
                    }
                }
            }

            private int Allocate()
            {
                if ((vectorCount + 1) * width > results.Length)
                {
                    Array.Resize(ref results, results.Length * 2);
                }

                return vectorCount++;
            }

            private int Digit(int combination, int suit)
            {
                return combination / stride[suit] % (aceTotal[suit] + 1);
            }

            private MeldSolution Reconstruct(int combination)
            {
                var activeRuns = new List<RunTrack>[SuitCount];
                for (var suit = 0; suit < SuitCount; suit++)
                {
                    activeRuns[suit] = new List<RunTrack>(2);
                }

                var finishedRuns = new List<RunTrack>();
                var melds = new List<IReadOnlyList<Card>>();
                var usedOptional = new List<Card>();
                var taken = new int[Card.CardsPerDeck];
                var runs = 0;
                var groupSuits = new List<int>(8);

                for (var value = LowAce; value <= HighAce; value++)
                {
                    groupSuits.Clear();
                    var groupCards = 0;
                    var anySuitTwice = false;
                    for (var suit = 0; suit < SuitCount; suit++)
                    {
                        var option = Choose(value, suit, runs, groupCards, anySuitTwice, combination);
                        ApplyToRuns(activeRuns[suit], finishedRuns, (Suit)suit, value, option);
                        for (var copy = 0; copy < option.GroupCards; copy++)
                        {
                            groupSuits.Add(suit);
                        }

                        runs = option.Runs;
                        groupCards += option.GroupCards;
                        anySuitTwice |= option.GroupCards == 2;
                    }

                    if (groupSuits.Count > 0)
                    {
                        AddGroups(groupSuits, value, melds, taken, usedOptional);
                    }
                }

                for (var suit = 0; suit < SuitCount; suit++)
                {
                    finishedRuns.AddRange(activeRuns[suit]);
                }

                for (var index = 0; index < finishedRuns.Count; index++)
                {
                    AddRun(finishedRuns[index], melds, taken, usedOptional);
                }

                return new MeldSolution(true, false, new ReadOnlyCollection<IReadOnlyList<Card>>(melds),
                    usedOptional.AsReadOnly(), vectorCount);
            }

            private Option Choose(int value, int suit, int runs, int groupCards, bool anySuitTwice, int combination)
            {
                var current = Vector(value, suit, runs, groupCards, anySuitTwice);
                var target = results[current * width + combination];
                int usedMin;
                int usedMax;
                int gainBase;
                if (value == LowAce || value == HighAce)
                {
                    AceUsage(value, suit, Digit(combination, suit), out usedMin, out usedMax, out gainBase);
                }
                else
                {
                    usedMin = requiredCounts[suit * ValueSlots + value];
                    usedMax = usedMin + optionalCounts[suit * ValueSlots + value];
                    gainBase = usedMin;
                }

                var options = new OptionEnumerator(value, suit, runs, usedMin, usedMax);
                while (options.MoveNext())
                {
                    var option = options.Current;
                    var next = Vector(value, suit + 1, option.Runs, groupCards + option.GroupCards,
                        anySuitTwice || option.GroupCards == 2);
                    var score = results[next * width + combination];
                    if (score >= 0 && score + option.Used - gainBase == target)
                    {
                        return option;
                    }
                }

                throw new InvalidOperationException("The solver could not replay its own choice.");
            }

            private static void ApplyToRuns(List<RunTrack> runs, List<RunTrack> finishedRuns, Suit suit, int value,
                Option option)
            {
                runs.Sort((left, right) => right.CappedLength.CompareTo(left.CappedLength));
                var survivors = new List<RunTrack>(2);
                for (var index = 0; index < runs.Count; index++)
                {
                    if ((option.ExtendMask & (1 << index)) != 0)
                    {
                        runs[index].Values.Add(value);
                        survivors.Add(runs[index]);
                    }
                    else
                    {
                        finishedRuns.Add(runs[index]);
                    }
                }

                for (var index = 0; index < option.NewRuns; index++)
                {
                    var run = new RunTrack(suit);
                    run.Values.Add(value);
                    survivors.Add(run);
                }

                runs.Clear();
                runs.AddRange(survivors);
            }

            private void AddGroups(List<int> groupSuits, int value, List<IReadOnlyList<Card>> melds, int[] taken,
                List<Card> usedOptional)
            {
                var rank = RankOf(value);
                if (groupSuits.Count <= 4)
                {
                    var group = new List<Card>(groupSuits.Count);
                    for (var index = 0; index < groupSuits.Count; index++)
                    {
                        group.Add(Take((Suit)groupSuits[index], rank, taken, usedOptional));
                    }

                    melds.Add(group.AsReadOnly());
                    return;
                }

                // Two sets of the same rank: suits used twice go in both, the rest balance the two sizes.
                var copies = new int[SuitCount];
                for (var index = 0; index < groupSuits.Count; index++)
                {
                    copies[groupSuits[index]]++;
                }

                var first = new List<Card>(4);
                var second = new List<Card>(4);
                for (var suit = 0; suit < SuitCount; suit++)
                {
                    if (copies[suit] == 2)
                    {
                        first.Add(Take((Suit)suit, rank, taken, usedOptional));
                        second.Add(Take((Suit)suit, rank, taken, usedOptional));
                    }
                }

                for (var suit = 0; suit < SuitCount; suit++)
                {
                    if (copies[suit] == 1)
                    {
                        (first.Count <= second.Count ? first : second).Add(Take((Suit)suit, rank, taken,
                            usedOptional));
                    }
                }

                melds.Add(first.AsReadOnly());
                melds.Add(second.AsReadOnly());
            }

            private void AddRun(RunTrack run, List<IReadOnlyList<Card>> melds, int[] taken,
                List<Card> usedOptional)
            {
                if (run.Values.Count == 0)
                {
                    return;
                }

                if (run.Values.Count > 13)
                {
                    // A-2-…-K-A uses both aces of one suit; any run that long splits into two valid halves.
                    var split = run.Values.Count / 2;
                    var head = new RunTrack(run.Suit);
                    var tail = new RunTrack(run.Suit);
                    for (var index = 0; index < run.Values.Count; index++)
                    {
                        (index < split ? head : tail).Values.Add(run.Values[index]);
                    }

                    AddRun(head, melds, taken, usedOptional);
                    AddRun(tail, melds, taken, usedOptional);
                    return;
                }

                var cards = new List<Card>(run.Values.Count);
                for (var index = 0; index < run.Values.Count; index++)
                {
                    cards.Add(Take(run.Suit, RankOf(run.Values[index]), taken, usedOptional));
                }

                melds.Add(cards.AsReadOnly());
            }

            private Card Take(Suit suit, Rank rank, int[] taken, List<Card> usedOptional)
            {
                var slot = (int)suit * 13 + (int)rank;
                var index = taken[slot]++;
                var card = pools[slot][index];
                if (index >= requiredInPool[slot])
                {
                    usedOptional.Add(card);
                }

                return card;
            }
        }

        /// <summary>Open-addressing hash table from state key to result-vector index. Keys are never 0.</summary>
        private sealed class StateTable
        {
            private long[] keys = new long[1 << 12];
            private int[] values = new int[1 << 12];
            private int mask = (1 << 12) - 1;
            private int count;

            public bool TryGet(long key, out int value)
            {
                var slot = Hash(key) & mask;
                while (true)
                {
                    var stored = keys[slot];
                    if (stored == key)
                    {
                        value = values[slot];
                        return true;
                    }

                    if (stored == 0)
                    {
                        value = -1;
                        return false;
                    }

                    slot = (slot + 1) & mask;
                }
            }

            public void Set(long key, int value)
            {
                if ((count + 1) * 2 > keys.Length)
                {
                    Grow();
                }

                Insert(key, value);
            }

            private void Insert(long key, int value)
            {
                var slot = Hash(key) & mask;
                while (keys[slot] != 0 && keys[slot] != key)
                {
                    slot = (slot + 1) & mask;
                }

                if (keys[slot] == 0)
                {
                    count++;
                }

                keys[slot] = key;
                values[slot] = value;
            }

            private void Grow()
            {
                var oldKeys = keys;
                var oldValues = values;
                keys = new long[oldKeys.Length * 2];
                values = new int[oldValues.Length * 2];
                mask = keys.Length - 1;
                count = 0;
                for (var index = 0; index < oldKeys.Length; index++)
                {
                    if (oldKeys[index] != 0)
                    {
                        Insert(oldKeys[index], oldValues[index]);
                    }
                }
            }

            private static int Hash(long key)
            {
                var mixed = (ulong)key * 0x9E3779B97F4A7C15UL;
                return (int)(mixed >> 32);
            }
        }

        private readonly struct Option
        {
            public Option(int runs, int used, int groupCards, int extendMask, int newRuns)
            {
                Runs = runs;
                Used = used;
                GroupCards = groupCards;
                ExtendMask = extendMask;
                NewRuns = newRuns;
            }

            public int Runs { get; }
            public int Used { get; }
            public int GroupCards { get; }
            public int ExtendMask { get; }
            public int NewRuns { get; }
        }

        /// <summary>
        /// Enumerates every legal way to use <c>used</c> copies of one (suit, value): extend runs in progress,
        /// start new runs, or put copies into same-kind sets. Runs that are not extended end here.
        /// </summary>
        private struct OptionEnumerator
        {
            private readonly int runs;
            private readonly int value;
            private readonly int suit;
            private readonly int maximum;
            private readonly int runCount;
            private readonly int longerRun;
            private readonly int shorterRun;
            private int used;
            private int mask;
            private int groupCards;

            public OptionEnumerator(int value, int suit, int runs, int minimum, int maximum)
            {
                this.runs = runs;
                this.value = value;
                this.suit = suit;
                this.maximum = maximum;
                var bits = (runs >> (suit * 4)) & 0xF;
                shorterRun = bits >> 2;
                longerRun = bits & 3;
                runCount = (shorterRun != 0 ? 1 : 0) + (longerRun != 0 ? 1 : 0);
                used = minimum;
                mask = 0;
                groupCards = -1;
                Current = default;
            }

            public Option Current { get; private set; }

            public bool MoveNext()
            {
                while (used <= maximum)
                {
                    groupCards++;
                    if (groupCards > used)
                    {
                        groupCards = 0;
                        mask++;
                    }

                    if (mask >= 1 << runCount)
                    {
                        mask = 0;
                        groupCards = -1;
                        used++;
                        continue;
                    }

                    if (TryBuild(out var option))
                    {
                        Current = option;
                        return true;
                    }
                }

                return false;
            }

            private bool TryBuild(out Option option)
            {
                option = default;
                var extended = (mask & 1) + ((mask >> 1) & 1);
                var newRuns = used - extended - groupCards;
                if (newRuns < 0 || extended + newRuns > 2)
                {
                    return false;
                }

                if (runCount == 2 && longerRun == shorterRun && mask == 2)
                {
                    return false;
                }

                // A run started at Q can still become Q-K-A; one started at K or A-high cannot reach 3 cards.
                if (newRuns > 0 && value > 12)
                {
                    return false;
                }

                if (groupCards > 0 && value > 13)
                {
                    return false;
                }

                // Bit 0 is the longer run, bit 1 the shorter one (the same order Reconstruct sorts by).
                // Runs that are not extended end here, so they must already be valid (3 or more cards).
                var first = 0;
                var second = 0;
                var lengthCount = 0;
                if (runCount >= 1)
                {
                    if ((mask & 1) != 0)
                    {
                        Push(ref first, ref second, ref lengthCount, Math.Min(OpenRun, longerRun + 1));
                    }
                    else if (longerRun != OpenRun)
                    {
                        return false;
                    }
                }

                if (runCount == 2)
                {
                    if ((mask & 2) != 0)
                    {
                        Push(ref first, ref second, ref lengthCount, Math.Min(OpenRun, shorterRun + 1));
                    }
                    else if (shorterRun != OpenRun)
                    {
                        return false;
                    }
                }

                for (var index = 0; index < newRuns; index++)
                {
                    Push(ref first, ref second, ref lengthCount, 1);
                }

                var longer = Math.Max(first, second);
                var shorter = Math.Min(first, second);
                var bits = (shorter << 2) | longer;
                var newRunsPacked = (runs & ~(0xF << (suit * 4))) | (bits << (suit * 4));
                option = new Option(newRunsPacked, used, groupCards, mask, newRuns);
                return true;
            }

            private static void Push(ref int first, ref int second, ref int count, int length)
            {
                if (count == 0)
                {
                    first = length;
                }
                else
                {
                    second = length;
                }

                count++;
            }
        }
    }
}
