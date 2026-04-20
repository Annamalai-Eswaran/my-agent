using FluentAssertions;
using MyAgent.Orchestrator.Utils;
using Xunit;

namespace MyAgent.Orchestrator.Tests;

public class BranchUtilsTests
{
    [Theory]
    [InlineData("Fix login page", "fix-login-page")]
    [InlineData("Add user authentication module", "add-user-authentication-module")]
    [InlineData("  spaces  around  ", "spaces-around")]
    [InlineData("UPPER CASE", "upper-case")]
    [InlineData("special!@#chars", "special-chars")]
    [InlineData("multiple---hyphens", "multiple-hyphens")]
    public void Slugify_ShouldReturnExpectedSlug(string input, string expected)
    {
        var result = BranchUtils.Slugify(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void Slugify_ShouldTruncateLongText()
    {
        var longText = new string('a', 100);
        var result = BranchUtils.Slugify(longText);
        result.Length.Should().BeLessOrEqualTo(50);
    }

    [Theory]
    [InlineData("", "untitled")]
    [InlineData("   ", "untitled")]
    public void Slugify_ShouldHandleEmptyInput(string input, string expected)
    {
        var result = BranchUtils.Slugify(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void MakeBranchName_ShouldReturnCorrectFormat()
    {
        var result = BranchUtils.MakeBranchName(1234, "Fix login page");
        result.Should().Be("feature/ae/1234-fix-login-page");
    }

    [Fact]
    public void MakeBranchName_ShouldSlugifyTitle()
    {
        var result = BranchUtils.MakeBranchName(42, "Add User Authentication!");
        result.Should().Be("feature/ae/42-add-user-authentication");
    }
}
