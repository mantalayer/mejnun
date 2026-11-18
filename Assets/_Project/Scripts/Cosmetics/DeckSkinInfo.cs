namespace Mejnun.Cosmetics
{
    public readonly struct DeckSkinInfo
    {
        public DeckSkinInfo(string id, string displayName, UnlockRequirement unlock, bool isAddressable,
            DeckSkin skin)
        {
            Id = id;
            DisplayName = displayName;
            Unlock = unlock ?? UnlockRequirement.Free;
            IsAddressable = isAddressable;
            Skin = skin;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public UnlockRequirement Unlock { get; }
        public bool IsAddressable { get; }
        public DeckSkin Skin { get; }
    }
}