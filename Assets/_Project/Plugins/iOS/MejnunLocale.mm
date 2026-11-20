#import <Foundation/Foundation.h>
#include <string.h>

// Used by Mejnun.Platform.DeviceLanguageInfo. Returns the user's ordered
// language list (Settings > General > Language & Region, including the
// per-app language) as comma-separated BCP-47 tags. The marshaller frees the
// returned buffer, so it must come from malloc (strdup).
extern "C" const char* MejnunPreferredLanguages()
{
    @autoreleasepool
    {
        NSMutableArray<NSString*>* languages = [NSMutableArray array];
        for (NSString* language in [[NSBundle mainBundle] preferredLocalizations])
        {
            if (![languages containsObject:language])
                [languages addObject:language];
        }

        for (NSString* language in [NSLocale preferredLanguages])
        {
            if (![languages containsObject:language])
                [languages addObject:language];
        }

        NSString* joined = [languages componentsJoinedByString:@","];
        return strdup([joined UTF8String]);
    }
}
