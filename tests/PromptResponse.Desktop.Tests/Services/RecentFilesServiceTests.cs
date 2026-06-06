using AwesomeAssertions;
using NSubstitute;
using PromptResponse.Desktop.Models;
using PromptResponse.Desktop.Services;
using Xunit;

namespace PromptResponse.Desktop.Tests.Services;

/// <summary>
/// Verifies recent-files tracking: move-to-front, de-dup, cap, blank-path
/// guard, and persistence/restore via <see cref="ISettingsService"/>.
/// </summary>
public class RecentFilesServiceTests
{
    private static (RecentFilesService svc, AppSettings settings, ISettingsService mock) WithSettings()
    {
        var mock = Substitute.For<ISettingsService>();
        var app = new AppSettings();
        mock.Settings.Returns(app);
        return (new RecentFilesService(mock), app, mock);
    }

    [Fact]
    public void Add_InsertsMostRecentFirst()
    {
        var svc = new RecentFilesService();

        svc.Add("/a.aprt", "A");
        svc.Add("/b.aprt", "B");

        svc.Items.Select(i => i.Path).Should().Equal("/b.aprt", "/a.aprt");
    }

    [Fact]
    public void Add_SamePath_MovesToFrontWithoutDuplicating()
    {
        var svc = new RecentFilesService();
        svc.Add("/a.aprt", "A");
        svc.Add("/b.aprt", "B");

        svc.Add("/a.aprt", "A");

        svc.Items.Select(i => i.Path).Should().Equal("/a.aprt", "/b.aprt");
        svc.Items.Should().HaveCount(2);
    }

    [Fact]
    public void Add_CapsAtMaxItems()
    {
        var svc = new RecentFilesService();
        for (var i = 0; i < RecentFilesService.MaxItems + 5; i++)
        {
            svc.Add($"/f{i}.aprt", $"F{i}");
        }

        svc.Items.Should().HaveCount(RecentFilesService.MaxItems);
        svc.Items[0].Path.Should().Be($"/f{RecentFilesService.MaxItems + 4}.aprt");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Add_BlankPath_Ignored(string? path)
    {
        var svc = new RecentFilesService();
        svc.Add(path, "x");
        svc.Items.Should().BeEmpty();
    }

    [Fact]
    public void Add_BlankTitle_FallsBackToFileName()
    {
        var svc = new RecentFilesService();
        svc.Add("/some/dir/report.aprf", "  ");
        svc.Items[0].Title.Should().Be("report.aprf");
    }

    [Fact]
    public void Add_PersistsToSettings()
    {
        var (svc, app, mock) = WithSettings();

        svc.Add("/a.aprt", "A");

        app.RecentFiles.Should().ContainSingle().Which.Path.Should().Be("/a.aprt");
        mock.Received().Save();
    }

    [Fact]
    public void Constructor_RestoresFromSettings()
    {
        var mock = Substitute.For<ISettingsService>();
        mock.Settings.Returns(new AppSettings
        {
            RecentFiles = [new RecentFileSetting { Path = "/x.aprt", Title = "X" }],
        });

        var svc = new RecentFilesService(mock);

        svc.Items.Should().ContainSingle().Which.Title.Should().Be("X");
    }

    [Fact]
    public void Add_RaisesChanged()
    {
        var svc = new RecentFilesService();
        var raised = false;
        svc.Changed += (_, _) => raised = true;

        svc.Add("/a.aprt", "A");

        raised.Should().BeTrue();
    }
}
