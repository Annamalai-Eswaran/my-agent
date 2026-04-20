using FluentAssertions;
using MyAgent.McpServer.AzureDevOps.Tools;
using Xunit;

namespace MyAgent.McpServer.Tests;

public class WorkItemToolsTests
{
    [Fact]
    public void GetDefinitions_ShouldReturnThreeTools()
    {
        var tools = WorkItemTools.GetDefinitions().ToList();
        tools.Should().HaveCount(3);
    }

    [Fact]
    public void GetDefinitions_ShouldIncludeListReadyWorkItems()
    {
        var tools = WorkItemTools.GetDefinitions().ToList();
        tools.Should().Contain(t => t.Name == "list-ready-work-items");
    }

    [Fact]
    public void GetDefinitions_ShouldIncludeGetWorkItem()
    {
        var tools = WorkItemTools.GetDefinitions().ToList();
        tools.Should().Contain(t => t.Name == "get-work-item");
    }

    [Fact]
    public void GetDefinitions_ShouldIncludeUpdateWorkItemState()
    {
        var tools = WorkItemTools.GetDefinitions().ToList();
        tools.Should().Contain(t => t.Name == "update-work-item-state");
    }

    [Fact]
    public void AllTools_ShouldHaveNonEmptyDescriptions()
    {
        var tools = WorkItemTools.GetDefinitions().ToList();
        tools.Should().AllSatisfy(t => t.Description.Should().NotBeNullOrEmpty());
    }
}
