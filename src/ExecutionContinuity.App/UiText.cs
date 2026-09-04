using System.Globalization;
using ExecutionContinuity.Domain;

namespace ExecutionContinuity.App;

public static class UiText
{
    public static bool IsEnglish(LanguagePreference preference) =>
        preference == LanguagePreference.English ||
        (preference == LanguagePreference.FollowSystem &&
            string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "en", StringComparison.OrdinalIgnoreCase));

    public static string Choose(LanguagePreference preference, string simplifiedChinese, string english) =>
        IsEnglish(preference) ? english : simplifiedChinese;
}
