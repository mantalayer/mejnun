using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mejnun.Cosmetics
{
    [CreateAssetMenu(menuName = "Mejnun/Table Felt Catalog")]
    public sealed class TableFeltCatalog : ScriptableObject
    {
        [SerializeField] private List<TableFelt> felts = new List<TableFelt>();

        public IReadOnlyList<TableFelt> Felts => felts;

        public TableFelt Resolve(string id)
        {
            if (felts == null)
            {
                return null;
            }

            for (var index = 0; index < felts.Count; index++)
            {
                var felt = felts[index];
                if (felt != null && string.Equals(felt.Id, id, StringComparison.Ordinal))
                {
                    return felt;
                }
            }

            return null;
        }

        public void Configure(IReadOnlyList<TableFelt> tableFelts)
        {
            if (tableFelts == null)
            {
                throw new ArgumentNullException(nameof(tableFelts));
            }

            var configured = new List<TableFelt>(tableFelts.Count);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < tableFelts.Count; index++)
            {
                var felt = tableFelts[index];
                if (felt == null || string.IsNullOrWhiteSpace(felt.Id) || !ids.Add(felt.Id))
                {
                    throw new ArgumentException("A felt catalog must have unique, configured felts.",
                        nameof(tableFelts));
                }

                configured.Add(felt);
            }

            felts = configured;
        }
    }
}