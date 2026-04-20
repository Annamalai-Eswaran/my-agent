using FluentAssertions;
using MyAgent.Orchestrator.Agent;
using Xunit;

namespace MyAgent.Orchestrator.Tests;

public class HumanLoopTests
{
    [Fact]
    public void GetToolDefinition_ShouldReturnCorrectName()
    {
        var def = HumanLoop.GetToolDefinition();
        def.Name.Should().Be("ask_human");
    }

    [Fact]
    public void GetToolDefinition_ShouldHaveDescription()
    {
        var def = HumanLoop.GetToolDefinition();
        def.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetToolDefinition_ShouldHaveQuestionProperty()
    {
        var def = HumanLoop.GetToolDefinition();
        def.InputSchema["properties"]?.AsObject().ContainsKey("question").Should().BeTrue();
    }
}
