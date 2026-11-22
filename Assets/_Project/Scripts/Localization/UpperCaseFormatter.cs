using System;
using System.Globalization;
using UnityEngine.Localization.SmartFormat.Core.Extensions;

namespace Mejnun.Localization
{
    // "{app.name:upper}" writes the value in upper case using the rules of the
    // locale being formatted. Scripts without case (Arabic, Japanese, ...) are
    // unchanged. Registered in LocalizationSettings > Smart Format > Formatters.
    [Serializable]
    public sealed class UpperCaseFormatter : FormatterBase
    {
        public override string[] DefaultNames => new[] { "upper" };

        public override bool TryEvaluateFormat(IFormattingInfo formattingInfo)
        {
            string text = formattingInfo.CurrentValue?.ToString() ?? string.Empty;
            var culture = formattingInfo.FormatDetails?.Provider as CultureInfo ?? CultureInfo.InvariantCulture;
            formattingInfo.Write(text.ToUpper(culture));
            return true;
        }
    }
}
