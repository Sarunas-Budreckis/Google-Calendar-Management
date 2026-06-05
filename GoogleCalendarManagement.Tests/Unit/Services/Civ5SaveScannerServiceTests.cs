using FluentAssertions;
using GoogleCalendarManagement.Services;

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
}
