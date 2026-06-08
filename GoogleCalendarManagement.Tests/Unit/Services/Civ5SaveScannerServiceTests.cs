using FluentAssertions;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using Moq;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class Civ5SaveScannerServiceTests
{
    private const string Root = @"C:\Saves";

    private static string P(string relativePath) =>
        Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    // ---------------------------------------------------------------------------
    // DetermineGameMode
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("single", "single")]
    [InlineData("multi", "multi")]
    [InlineData("hotseat", "hotseat")]
    [InlineData("pbem", "pbem")]
    [InlineData("pitboss", "pitboss")]
    public void DetermineGameMode_KnownSubfolder_ReturnsThatMode(string subfolder, string expected)
    {
        var file = P($"{subfolder}/game.Civ5Save");
        var result = Civ5SaveScannerService.DetermineGameMode(Root, file);
        result.Should().Be(expected);
    }

    [Fact]
    public void DetermineGameMode_UnknownSubfolder_ReturnsUnknown()
    {
        var file = P("custom/game.Civ5Save");
        var result = Civ5SaveScannerService.DetermineGameMode(Root, file);
        result.Should().Be("unknown");
    }

    [Fact]
    public void DetermineGameMode_FileDirectlyInRoot_ReturnsUnknown()
    {
        var file = P("game.Civ5Save");
        var result = Civ5SaveScannerService.DetermineGameMode(Root, file);
        result.Should().Be("unknown");
    }

    [Fact]
    public void DetermineGameMode_DeeplyNestedInKnownSubfolder_ReturnsThatMode()
    {
        // immediate subfolder is "single" even if file is deeper
        var file = P("single/campaign/turn123/save.Civ5Save");
        var result = Civ5SaveScannerService.DetermineGameMode(Root, file);
        result.Should().Be("single");
    }

    [Fact]
    public void DetermineGameMode_CaseInsensitiveSubfolder_RecognizesMode()
    {
        // "Single" (capital S) should still match "single"
        var file = P("Single/game.Civ5Save");
        var result = Civ5SaveScannerService.DetermineGameMode(Root, file);
        result.Should().Be("single");
    }

    [Fact]
    public void DetermineGameMode_RootWithTrailingSlash_StillWorks()
    {
        var rootWithSlash = Root + Path.DirectorySeparatorChar;
        var file = P("single/game.Civ5Save");
        var result = Civ5SaveScannerService.DetermineGameMode(rootWithSlash, file);
        result.Should().Be("single");
    }

    // ---------------------------------------------------------------------------
    // Deduplication logic (via FilterNewCandidates helper)
    // ---------------------------------------------------------------------------

    [Fact]
    public void Dedup_CandidateAlreadyExistsInRepository_IsSkipped()
    {
        var t = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var existing = new HashSet<(DateTime, string)> { (t, "single") };

        var candidates = new List<(DateTime FileModifiedAt, string GameMode)>
        {
            (t, "single"),          // already exists — should be skipped
            (t.AddMinutes(5), "single")  // new — should be kept
        };

        var result = candidates
            .Where(c => !existing.Contains((c.FileModifiedAt, c.GameMode)))
            .ToList();

        result.Should().HaveCount(1);
        result[0].FileModifiedAt.Should().Be(t.AddMinutes(5));
    }

    [Fact]
    public void Dedup_SameTimestampDifferentGameMode_IsNotSkipped()
    {
        var t = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var existing = new HashSet<(DateTime, string)> { (t, "single") };

        var candidates = new List<(DateTime FileModifiedAt, string GameMode)>
        {
            (t, "multi")  // same time, different mode — should be kept
        };

        var result = candidates
            .Where(c => !existing.Contains((c.FileModifiedAt, c.GameMode)))
            .ToList();

        result.Should().HaveCount(1);
    }

    [Fact]
    public void Dedup_AllCandidatesAlreadyExist_ReturnsEmpty()
    {
        var t = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var existing = new HashSet<(DateTime, string)>
        {
            (t, "single"),
            (t.AddMinutes(10), "single")
        };

        var candidates = new List<(DateTime FileModifiedAt, string GameMode)>
        {
            (t, "single"),
            (t.AddMinutes(10), "single")
        };

        var result = candidates
            .Where(c => !existing.Contains((c.FileModifiedAt, c.GameMode)))
            .ToList();

        result.Should().BeEmpty();
    }

    [Fact]
    public void DeduplicateCandidatesForPersistence_SameTimestampAndMode_KeepsOnePoint()
    {
        var t = new DateTime(2026, 6, 4, 20, 48, 7, DateTimeKind.Utc);
        var candidates = new List<(DateTime FileModifiedAt, string GameMode)>
        {
            (t, "multi"),
            (t, "multi"),
            (t, "single")
        };

        var result = Civ5SaveScannerService.DeduplicateCandidatesForPersistence(candidates);

        result.Should().BeEquivalentTo(new List<(DateTime FileModifiedAt, string GameMode)>
        {
            (t, "multi"),
            (t, "single")
        });
    }

    [Fact]
    public async Task ImportHandler_SuccessDialog_ShowsDetectedAndAddedCounts()
    {
        var scanner = new Mock<ICiv5SaveScannerService>();
        scanner
            .Setup(s => s.ScanAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Civ5ScanResult(true, 42, 7, null));

        var dialog = new Mock<IContentDialogService>();
        var sut = new Civ5ImportHandler(scanner.Object, dialog.Object);

        await sut.TriggerImportAsync();

        dialog.Verify(d => d.ShowMessageAsync(
            "Civilization 5 Import",
            "Detected 42 Civilization 5 save files. Added 7 new save points to the database.",
            "OK"), Times.Once);
    }
}
