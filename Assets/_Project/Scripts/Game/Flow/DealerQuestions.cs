using System;
using System.Collections.Generic;
using Mejnun.Localization;
using UnityEngine;

namespace Mejnun.Game
{
    /// <summary>
    /// Picks the "who goes first" question. The questions live in the <c>DealerQuestions</c> String Table
    /// (<c>dq.001</c>, <c>dq.002</c>, …); a match keeps the chosen key and resolves it when shown, so the text is
    /// never stored and follows the current language.
    /// </summary>
    [CreateAssetMenu(menuName = "Mejnun/Dealer Questions")]
    public sealed class DealerQuestions : ScriptableObject
    {
        /// <summary>Every question key, sorted ordinally (the order the seeded pick indexes into).</summary>
        public static IReadOnlyList<string> QuestionKeys => Loc.KeysOf(Loc.DealerQuestionsTable);

        public string GetRandomQuestionKey(System.Random random)
        {
            return PickKey(random);
        }

        /// <summary>A question key chosen by <paramref name="random"/>, or null when the table is empty.</summary>
        public static string PickKey(System.Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            var keys = QuestionKeys;
            return keys.Count == 0 ? null : keys[random.Next(keys.Count)];
        }

        /// <summary>The question for <paramref name="key"/> in the current language; the generic one for null.</summary>
        public static string Resolve(string key)
        {
            return string.IsNullOrEmpty(key) || !Loc.Has(Loc.DealerQuestionsTable, key)
                ? Loc.Get("dealer.question.fallback")
                : Loc.GetFrom(Loc.DealerQuestionsTable, key);
        }

        [Obsolete("Dealer questions are entries of the DealerQuestions String Table; this asset no longer stores text.")]
        public void Configure(IReadOnlyList<string> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }
        }
    }
}
