using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mejnun.Game
{
    [CreateAssetMenu(menuName = "Mejnun/Dealer Questions")]
    public sealed class DealerQuestions : ScriptableObject
    {
        [SerializeField] private List<string> questions = new List<string>();

        public IReadOnlyList<string> Questions => questions;

        public string GetRandomQuestion(System.Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (questions == null || questions.Count == 0)
            {
                return "Who should deal this game?";
            }

            return questions[random.Next(questions.Count)];
        }

        public void Configure(IReadOnlyList<string> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            var configuredQuestions = new List<string>(entries.Count);
            for (var index = 0; index < entries.Count; index++)
            {
                if (!string.IsNullOrWhiteSpace(entries[index]))
                {
                    configuredQuestions.Add(entries[index].Trim());
                }
            }

            questions = configuredQuestions;
        }
    }
}