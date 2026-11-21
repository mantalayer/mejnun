using System;
using System.Collections.Generic;
using Mejnun.Cosmetics;
using Mejnun.Game;
using UnityEngine.UIElements;

namespace Mejnun.UI
{
    public sealed class DeckUnlockPlayer
    {
        private readonly DeckSkinLoader skinLoader;
        private readonly DeckUnlockPresentation unlockPresentation;
        private readonly FeltUnlockPresentation feltUnlockPresentation;
        private ProgressionReport lastPlayedReport;
        private int loadGeneration;

        public DeckUnlockPlayer(VisualElement documentRoot, DeckSkinLoader loader)
        {
            skinLoader = loader;
            unlockPresentation = new DeckUnlockPresentation(documentRoot);
            feltUnlockPresentation = new FeltUnlockPresentation(documentRoot);
        }

        public void Play(ProgressionReport report, Action finished = null)
        {
            if (report == null || ReferenceEquals(lastPlayedReport, report))
            {
                finished?.Invoke();
                return;
            }

            lastPlayedReport = report;
            var generation = ++loadGeneration;
            unlockPresentation.Cancel();
            feltUnlockPresentation.Cancel();
            var catalog = ProgressionService.Instance.Catalog;
            var feltItems = ResolveFelts(report, catalog);
            var entries = catalog?.Decks?.GetEntries();
            if (entries == null)
            {
                PlayResolvedFelts(feltItems, finished, generation);
                return;
            }

            var candidates = new List<DeckSkinInfo>();
            var seenIds = new HashSet<string>();
            for (var unlockIndex = 0; unlockIndex < report.NewlyUnlockedItems.Count; unlockIndex++)
            {
                var itemId = report.NewlyUnlockedItems[unlockIndex];
                if (string.IsNullOrWhiteSpace(itemId) || !seenIds.Add(itemId))
                {
                    continue;
                }

                for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                {
                    if (string.Equals(entries[entryIndex].Id, itemId, StringComparison.Ordinal))
                    {
                        candidates.Add(entries[entryIndex]);
                        break;
                    }
                }
            }

            if (candidates.Count == 0)
            {
                PlayResolvedFelts(feltItems, finished, generation);
                return;
            }

            var resolved = new DeckUnlockPresentationItem[candidates.Count];
            var pendingLoads = 0;
            for (var index = 0; index < candidates.Count; index++)
            {
                if (candidates[index].Skin == null && candidates[index].IsAddressable && skinLoader != null)
                {
                    pendingLoads++;
                }
            }

            if (pendingLoads == 0)
            {
                for (var index = 0; index < candidates.Count; index++)
                {
                    resolved[index] = CreatePresentationItem(candidates[index], candidates[index].Skin);
                }

                PlayResolvedDecks(resolved, feltItems, finished, generation);
                return;
            }

            for (var index = 0; index < candidates.Count; index++)
            {
                var entry = candidates[index];
                if (entry.Skin != null)
                {
                    resolved[index] = CreatePresentationItem(entry, entry.Skin);
                    continue;
                }

                if (!entry.IsAddressable || skinLoader == null)
                {
                    continue;
                }

                var candidateIndex = index;
                skinLoader.Load(entry.Id, loaded =>
                {
                    if (generation != loadGeneration)
                    {
                        return;
                    }

                    resolved[candidateIndex] = CreatePresentationItem(entry, loaded);
                    pendingLoads--;
                    if (pendingLoads == 0)
                    {
                        PlayResolvedDecks(resolved, feltItems, finished, generation);
                    }
                });
            }
        }

        public void Cancel()
        {
            loadGeneration++;
            unlockPresentation.Cancel();
            feltUnlockPresentation.Cancel();
        }

        private static List<FeltUnlockPresentationItem> ResolveFelts(ProgressionReport report,
            CosmeticCatalog catalog)
        {
            var felts = new List<FeltUnlockPresentationItem>();
            var feltCatalog = catalog == null ? null : catalog.Felts;
            if (feltCatalog == null)
            {
                return felts;
            }

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < report.NewlyUnlockedItems.Count; index++)
            {
                var itemId = report.NewlyUnlockedItems[index];
                if (string.IsNullOrWhiteSpace(itemId) || !seenIds.Add(itemId))
                {
                    continue;
                }

                var felt = feltCatalog.Resolve(itemId);
                if (felt != null)
                {
                    felts.Add(new FeltUnlockPresentationItem(felt, felt.DisplayName));
                }
            }

            return felts;
        }

        private static DeckUnlockPresentationItem CreatePresentationItem(DeckSkinInfo entry, DeckSkin skin)
        {
            return skin == null ? null : new DeckUnlockPresentationItem(skin, entry.DisplayName,
                string.IsNullOrWhiteSpace(entry.Artist) ? skin.Artist : entry.Artist);
        }

        private void PlayResolvedDecks(IReadOnlyList<DeckUnlockPresentationItem> resolved,
            IReadOnlyList<FeltUnlockPresentationItem> feltItems, Action finished, int generation)
        {
            if (generation != loadGeneration)
            {
                return;
            }

            var items = new List<DeckUnlockPresentationItem>();
            for (var index = 0; index < resolved.Count; index++)
            {
                if (resolved[index] != null)
                {
                    items.Add(resolved[index]);
                }
            }

            if (items.Count == 0)
            {
                PlayResolvedFelts(feltItems, finished, generation);
                return;
            }

            unlockPresentation.Play(items, () => PlayResolvedFelts(feltItems, finished, generation));
        }

        private void PlayResolvedFelts(IReadOnlyList<FeltUnlockPresentationItem> felts, Action finished,
            int generation)
        {
            if (generation != loadGeneration)
            {
                return;
            }

            feltUnlockPresentation.Play(felts, () =>
            {
                if (generation == loadGeneration)
                {
                    finished?.Invoke();
                }
            });
        }
    }
}