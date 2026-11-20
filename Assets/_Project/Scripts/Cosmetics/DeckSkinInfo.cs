namespace Mejnun.Cosmetics
{
    public readonly struct DeckSkinInfo
    {
        private readonly string displayName;

        public DeckSkinInfo(string id, string displayName, UnlockRequirement unlock, bool isAddressable,
            DeckSkin skin, string artist = null)
        {
            Id = id;
            this.displayName = displayName;
            Unlock = unlock ?? UnlockRequirement.Free;
            IsAddressable = isAddressable;
            Skin = skin;
            Artist = artist ?? (skin == null ? string.Empty : skin.Artist);
        }

        public string Id { get; }

        // Resolved on access so a language change shows up in cached entries.
        public string DisplayName => DeckSkin.ResolveDisplayName(Id, displayName);
        public UnlockRequirement Unlock { get; }
        public bool IsAddressable { get; }
        public DeckSkin Skin { get; }
        public string Artist { get; }
        public string ArtistCredit => DeckSkin.FormatArtistCredit(Artist);
    }
}
