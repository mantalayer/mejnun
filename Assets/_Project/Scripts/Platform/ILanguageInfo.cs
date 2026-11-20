using System.Collections.Generic;

namespace Mejnun.Platform
{
    public interface ILanguageInfo
    {
        // The player's preferred languages as BCP-47 tags (for example "en-US",
        // "zh-Hans", "pt-BR"), most preferred first. Store backends put the
        // language the store selected for this game ahead of the device list.
        IReadOnlyList<string> PreferredLanguageTags { get; }
    }
}
