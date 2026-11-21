using System;

namespace Mejnun.Net
{
    public static class FirstPlayerVote
    {
        public const int NoVote = -2;
        public const int RandomVote = -1;

        public static int Resolve(int[] votes, bool[] voters, bool[] candidates, Random random)
        {
            if (votes == null)
            {
                throw new ArgumentNullException(nameof(votes));
            }

            if (voters == null)
            {
                throw new ArgumentNullException(nameof(voters));
            }

            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (votes.Length == 0 || votes.Length > 6 || voters.Length != votes.Length ||
                candidates.Length != votes.Length)
            {
                throw new ArgumentException("Vote, voter, and candidate counts must match and be between one and six.");
            }

            var candidateCount = 0;
            var voterCount = 0;
            var allVotedForSelf = true;
            for (var seat = 0; seat < votes.Length; seat++)
            {
                if (candidates[seat])
                {
                    candidateCount++;
                }

                if (!voters[seat])
                {
                    continue;
                }

                voterCount++;
                if (!candidates[seat] || votes[seat] == NoVote)
                {
                    throw new ArgumentException("Every voter must be a candidate and must have voted.", nameof(votes));
                }

                if (votes[seat] != RandomVote &&
                    (votes[seat] < 0 || votes[seat] >= candidates.Length || !candidates[votes[seat]]))
                {
                    throw new ArgumentException("A vote must target a candidate or the random choice.", nameof(votes));
                }

                if (votes[seat] != seat)
                {
                    allVotedForSelf = false;
                }
            }

            if (candidateCount == 0 || voterCount == 0)
            {
                throw new ArgumentException("At least one candidate and voter are required.");
            }

            if (allVotedForSelf)
            {
                return ChooseRandomCandidate(candidates, random);
            }

            var tallies = new int[candidates.Length];
            for (var seat = 0; seat < votes.Length; seat++)
            {
                if (!voters[seat])
                {
                    continue;
                }

                var choice = votes[seat] == RandomVote
                    ? ChooseRandomCandidate(candidates, random)
                    : votes[seat];
                tallies[choice]++;
            }

            var highestTally = 0;
            var tiedCandidateCount = 0;
            for (var seat = 0; seat < tallies.Length; seat++)
            {
                if (!candidates[seat])
                {
                    continue;
                }

                if (tallies[seat] > highestTally)
                {
                    highestTally = tallies[seat];
                    tiedCandidateCount = 1;
                }
                else if (tallies[seat] == highestTally)
                {
                    tiedCandidateCount++;
                }
            }

            var tiedCandidates = new int[tiedCandidateCount];
            var tiedIndex = 0;
            for (var seat = 0; seat < tallies.Length; seat++)
            {
                if (candidates[seat] && tallies[seat] == highestTally)
                {
                    tiedCandidates[tiedIndex++] = seat;
                }
            }

            return tiedCandidates[random.Next(tiedCandidates.Length)];
        }

        private static int ChooseRandomCandidate(bool[] candidates, Random random)
        {
            var candidateCount = 0;
            for (var seat = 0; seat < candidates.Length; seat++)
            {
                if (candidates[seat])
                {
                    candidateCount++;
                }
            }

            var choice = random.Next(candidateCount);
            for (var seat = 0; seat < candidates.Length; seat++)
            {
                if (!candidates[seat])
                {
                    continue;
                }

                if (choice == 0)
                {
                    return seat;
                }

                choice--;
            }

            throw new InvalidOperationException("No vote candidates are available.");
        }
    }
}