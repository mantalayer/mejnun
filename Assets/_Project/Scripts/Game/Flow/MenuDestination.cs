namespace Mejnun.Game
{
    public enum MenuDestinationKind
    {
        MainMenu,
        Setup,
        TablePicker,
        Gallery,
        Settings
    }

    public readonly struct MenuDestination
    {
        public MenuDestination(MenuDestinationKind kind, string itemId = null)
        {
            Kind = kind;
            ItemId = itemId;
        }

        public MenuDestinationKind Kind { get; }
        public string ItemId { get; }

        public static MenuDestination MainMenu => new MenuDestination(MenuDestinationKind.MainMenu);
        public static MenuDestination Setup => new MenuDestination(MenuDestinationKind.Setup);
        public static MenuDestination TablePicker => new MenuDestination(MenuDestinationKind.TablePicker);
        public static MenuDestination Gallery(string itemId = null) =>
            new MenuDestination(MenuDestinationKind.Gallery, itemId);
        public static MenuDestination Settings => new MenuDestination(MenuDestinationKind.Settings);
    }
}