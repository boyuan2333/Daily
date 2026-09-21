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

    public static string HistoryKindLabel(LanguagePreference preference, HistoryEventKind kind) => kind switch
    {
        HistoryEventKind.RouteStarted => Choose(preference, "开始路线", "Route started"),
        HistoryEventKind.RouteResumed => Choose(preference, "恢复路线", "Route resumed"),
        HistoryEventKind.StepCompleted => Choose(preference, "完成步骤", "Step completed"),
        HistoryEventKind.RouteCompleted => Choose(preference, "完成路线", "Route completed"),
        HistoryEventKind.Paused => Choose(preference, "暂停", "Paused"),
        _ => kind.ToString()
    };
}
