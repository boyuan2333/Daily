namespace ExecutionContinuity.App;

public static class WindowPresentation
{
    public const int DefaultWidth = 960;
    public const int DefaultHeight = 680;

    public static TimeSpan StatusLifetime(bool isError) =>
        TimeSpan.FromSeconds(isError ? 5 : 3);
}
