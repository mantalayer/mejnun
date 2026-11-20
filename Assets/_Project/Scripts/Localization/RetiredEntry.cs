using System;
using UnityEngine.Localization.Metadata;

namespace Mejnun.Localization
{
    [Serializable]
    [Metadata(AllowedTypes = MetadataType.SharedStringTableEntry)]
    public sealed class RetiredEntry : IMetadata
    {
    }
}