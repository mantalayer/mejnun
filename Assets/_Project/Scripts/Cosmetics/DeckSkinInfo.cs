namespace Mejnun.Cosmetics
{
    public readonly struct DeckSkinInfo
    {
        public DeckSkinInfo(string id, string displayName, UnlockRequirement unlock, bool isAddressable,
            DeckSkin skin, string artist = null)
        {
            Id = id;
            DisplayName = displayName;
            Unlock = unlock ?? UnlockRequirement.Free;
            IsAddressable = isAddressable;
            Skin = skin;
            Artist = artist ?? (skin == null ? string.Empty : skin.Artist);
        }

        public string Id { get; }
        public string DisplayName { get; }
        public UnlockRequirement Unlock { get; }
        public bool IsAddressable { get; }
        public DeckSkin Skin { get; }
        public string Artist { get; }
    }
}