using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Mejnun.Localization;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.TextCore.Text;

namespace Mejnun.UI
{
    public sealed class FontChainLoader : MonoBehaviour
    {
        private static readonly string[] CjkLocales = { "ja", "ko", "zh-Hans", "zh-Hant" };
        private readonly Dictionary<string, FontAsset> loadedFonts = new(StringComparer.Ordinal);
        private readonly List<AsyncOperationHandle<FontAsset>> handles = new();
        private ScriptableObject textSettings;
        private FontAsset[] baseFallbacks;
        private Coroutine loadRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (!Application.isPlaying)
                return;

            var host = new GameObject(nameof(FontChainLoader));
            DontDestroyOnLoad(host);
            host.AddComponent<FontChainLoader>();
        }

        private void Awake()
        {
            textSettings = Resources.Load<ScriptableObject>("MejnunTextSettings");
            if (textSettings == null)
                textSettings = Resources.Load<ScriptableObject>("Fonts/MejnunTextSettings");
            if (textSettings == null)
                return;

            baseFallbacks = GetFallbacks(textSettings);
            Loc.LanguageChanged += OnLanguageChanged;
            loadRoutine = StartCoroutine(LoadLocaleFonts());
        }

        private void OnDestroy()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
            if (textSettings != null && baseFallbacks != null)
                SetFallbacks(textSettings, baseFallbacks);
            foreach (var handle in handles)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }
            handles.Clear();
        }

        private void OnApplicationQuit()
        {
            if (textSettings != null && baseFallbacks != null)
                SetFallbacks(textSettings, baseFallbacks);
        }

        private void OnLanguageChanged()
        {
            if (textSettings == null)
                return;

            if (loadRoutine == null)
                ApplyFallbackChain();
        }

        private IEnumerator LoadLocaleFonts()
        {
            string currentLocale = Loc.CurrentLocaleCode;
            var orderedLocales = new List<string> { currentLocale };
            foreach (string locale in CjkLocales)
            {
                if (!orderedLocales.Contains(locale))
                    orderedLocales.Add(locale);
            }

            foreach (string locale in orderedLocales)
            {
                if (loadedFonts.ContainsKey(locale))
                    continue;

                var locationHandle = Addressables.LoadResourceLocationsAsync("fonts/" + locale, typeof(FontAsset));
                yield return locationHandle;
                if (locationHandle.Status == AsyncOperationStatus.Succeeded && locationHandle.Result != null &&
                    locationHandle.Result.Count > 0)
                {
                    var fontHandle = Addressables.LoadAssetAsync<FontAsset>("fonts/" + locale);
                    handles.Add(fontHandle);
                    yield return fontHandle;
                    if (fontHandle.Status == AsyncOperationStatus.Succeeded && fontHandle.Result != null)
                        loadedFonts[locale] = fontHandle.Result;
                }
                Addressables.Release(locationHandle);
            }

            ApplyFallbackChain();
            loadRoutine = null;
        }

        private void ApplyFallbackChain()
        {
            var fallbacks = new List<FontAsset>();
            string currentLocale = Loc.CurrentLocaleCode;
            AddLocaleFont(fallbacks, currentLocale);
            foreach (string locale in CjkLocales)
                AddLocaleFont(fallbacks, locale);
            if (baseFallbacks != null)
            {
                foreach (var font in baseFallbacks)
                {
                    if (font != null && !fallbacks.Contains(font))
                        fallbacks.Add(font);
                }
            }

            SetFallbacks(textSettings, fallbacks.ToArray());
        }

        private void AddLocaleFont(List<FontAsset> fallbacks, string locale)
        {
            if (loadedFonts.TryGetValue(locale, out var font) && font != null && !fallbacks.Contains(font))
                fallbacks.Add(font);
        }

        private static FontAsset[] GetFallbacks(object settings)
        {
            var member = FindFallbackMember(settings.GetType());
            return ToFontArray(ReadMember(member, settings));
        }

        private static void SetFallbacks(object settings, FontAsset[] fallbacks)
        {
            var member = FindFallbackMember(settings.GetType());
            if (member == null)
                return;

            var memberType = member is PropertyInfo property ? property.PropertyType : ((FieldInfo)member).FieldType;
            object value = memberType == typeof(FontAsset[]) ? fallbacks : new List<FontAsset>(fallbacks);
            WriteMember(member, settings, value);
        }

        private static MemberInfo FindFallbackMember(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var property in type.GetProperties(flags))
            {
                if (property.Name.IndexOf("fallback", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    (property.PropertyType == typeof(FontAsset[]) || typeof(IList<FontAsset>).IsAssignableFrom(property.PropertyType)))
                    return property;
            }

            foreach (var field in type.GetFields(flags))
            {
                if (field.Name.IndexOf("fallback", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    (field.FieldType == typeof(FontAsset[]) || typeof(IList<FontAsset>).IsAssignableFrom(field.FieldType)))
                    return field;
            }

            return null;
        }

        private static object ReadMember(MemberInfo member, object target)
        {
            if (member is PropertyInfo property)
                return property.GetValue(target);
            if (member is FieldInfo field)
                return field.GetValue(target);
            return null;
        }

        private static void WriteMember(MemberInfo member, object target, object value)
        {
            if (member is PropertyInfo property && property.CanWrite)
                property.SetValue(target, value);
            else if (member is FieldInfo field)
                field.SetValue(target, value);
        }

        private static FontAsset[] ToFontArray(object value)
        {
            if (value is FontAsset[] assets)
                return (FontAsset[])assets.Clone();
            if (value is IList<FontAsset> list)
            {
                var fonts = new FontAsset[list.Count];
                list.CopyTo(fonts, 0);
                return fonts;
            }

            return Array.Empty<FontAsset>();
        }
    }
}