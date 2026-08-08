namespace ExecutionContinuity.App;

public enum PlanningDetail
{
    None,
    Route,
    Inbox
}

public sealed record PlanningListContext(
    string GroupingKey = "status",
    Guid? SelectedItemId = null,
    double ScrollOffset = 0);

public sealed record ResponsivePlanningPresentation(
    bool IsCompact,
    PlanningDestination Destination,
    PlanningDetail Detail,
    PlanningListContext Routes,
    PlanningListContext Inbox)
{
    public static ResponsivePlanningPresentation Create(double width) =>
        new(
            IsCompactWidth(width),
            PlanningDestination.Routes,
            PlanningDetail.None,
            new PlanningListContext(),
            new PlanningListContext());

    public static bool IsCompactWidth(double width) => width > 0 && width < 860;

    public ResponsivePlanningPresentation WithWidth(double width) =>
        this with { IsCompact = IsCompactWidth(width) };

    public ResponsivePlanningPresentation OpenDetail(
        PlanningDestination destination,
        Guid itemId,
        double listScrollOffset)
    {
        if (destination is not (PlanningDestination.Routes or PlanningDestination.Inbox))
        {
            throw new ArgumentOutOfRangeException(nameof(destination), "Only Routes and Inbox have compact detail views.");
        }

        var detail = destination == PlanningDestination.Routes
            ? PlanningDetail.Route
            : PlanningDetail.Inbox;
        var context = new PlanningListContext(
            destination == PlanningDestination.Routes ? Routes.GroupingKey : Inbox.GroupingKey,
            itemId,
            listScrollOffset);

        return this with
        {
            Destination = destination,
            Detail = detail,
            Routes = destination == PlanningDestination.Routes ? context : Routes,
            Inbox = destination == PlanningDestination.Inbox ? context : Inbox
        };
    }

    public ResponsivePlanningPresentation ReturnToList() =>
        this with { Detail = PlanningDetail.None };
}
