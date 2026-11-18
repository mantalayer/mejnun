using System.Collections.Generic;
using Mejnun.Core;

namespace Mejnun.Game
{
    public sealed class BoardArrangement
    {
        private readonly List<int> meldIds = new List<int>();
        private readonly List<Meld> orderedMelds = new List<Meld>();

        public IReadOnlyList<Meld> Arrange(IReadOnlyList<Meld> board)
        {
            Synchronize(board);
            orderedMelds.Clear();
            for (var orderIndex = 0; orderIndex < meldIds.Count; orderIndex++)
            {
                var meld = Find(board, meldIds[orderIndex]);
                if (meld != null)
                {
                    orderedMelds.Add(meld);
                }
            }

            return orderedMelds;
        }

        public bool Move(int meldId, int targetIndex)
        {
            var sourceIndex = meldIds.IndexOf(meldId);
            if (sourceIndex < 0 || meldIds.Count < 2)
            {
                return false;
            }

            meldIds.RemoveAt(sourceIndex);
            var insertion = targetIndex;
            if (insertion > sourceIndex)
            {
                insertion = System.Math.Max(0, insertion - 1);
            }

            insertion = insertion < 0 ? 0 : insertion > meldIds.Count ? meldIds.Count : insertion;

            meldIds.Insert(insertion, meldId);
            return insertion != sourceIndex;
        }

        public int IndexOf(int meldId)
        {
            return meldIds.IndexOf(meldId);
        }

        private void Synchronize(IReadOnlyList<Meld> board)
        {
            for (var index = meldIds.Count - 1; index >= 0; index--)
            {
                if (Find(board, meldIds[index]) == null)
                {
                    meldIds.RemoveAt(index);
                }
            }

            for (var index = 0; index < board.Count; index++)
            {
                if (!meldIds.Contains(board[index].Id))
                {
                    meldIds.Add(board[index].Id);
                }
            }
        }

        private static Meld Find(IReadOnlyList<Meld> board, int meldId)
        {
            for (var index = 0; index < board.Count; index++)
            {
                if (board[index].Id == meldId)
                {
                    return board[index];
                }
            }

            return null;
        }
    }
}