using ExecutionContinuity.App;
using Xunit;

namespace ExecutionContinuity.App.Tests;

public sealed class PlanningNavigationTests
{
    [Fact]
    public void Planning_destinations_cover_four_sections_plus_secondary_archive()
    {
        Assert.Equal(
            ["Tasks", "Routes", "Inbox", "Review", "Archive"],
            Enum.GetNames<PlanningDestination>());
    }

    [Fact]
    public void Planning_shell_exposes_a_navigation_entry_and_a_workspace_for_every_section()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var xaml = File.ReadAllText(Path.Combine(root, "src", "ExecutionContinuity.App", "MainWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "src", "ExecutionContinuity.App", "MainWindow.xaml.cs"));

        foreach (var section in new[] { "Tasks", "Routes", "Inbox", "Review", "Archive" })
        {
            Assert.Contains($"x:Name=\"{section}NavButton\"", xaml);
            Assert.Contains($"x:Name=\"{section}Workspace\"", xaml);
            Assert.Contains($"x:Name=\"Compact{section}NavButton\"", xaml);
            Assert.Contains($"NavigatePlanning(PlanningDestination.{section})", code);
        }

        Assert.Contains("x:Name=\"CompactPlanningNavigation\"", xaml);
        Assert.Contains("SyncNavigationSelection", code);
        Assert.Contains("CompactPlanningNavigation.Visibility = ToVisibility(compact)", code);
    }

    [Fact]
    public void Switching_destination_preserves_the_other_section_list_context()
    {
        var routeId = Guid.NewGuid();
        var presentation = ResponsivePlanningPresentation.Create(520);
        presentation = presentation.OpenDetail(PlanningDestination.Routes, routeId, 184);
        presentation = presentation.ReturnToList();
        presentation = presentation with { Destination = PlanningDestination.Inbox };

        Assert.Equal(PlanningDestination.Inbox, presentation.Destination);
        Assert.Equal(PlanningDetail.None, presentation.Detail);
        Assert.Equal(routeId, presentation.Routes.SelectedItemId);
        Assert.Equal(184, presentation.Routes.ScrollOffset);
    }

    [Fact]
    public void Sections_without_a_detail_view_yet_reject_open_detail()
    {
        var presentation = ResponsivePlanningPresentation.Create(520);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            presentation.OpenDetail(PlanningDestination.Tasks, Guid.NewGuid(), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            presentation.OpenDetail(PlanningDestination.Review, Guid.NewGuid(), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            presentation.OpenDetail(PlanningDestination.Archive, Guid.NewGuid(), 0));
    }
}
