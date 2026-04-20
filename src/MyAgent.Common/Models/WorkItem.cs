namespace MyAgent.Common.Models;

public record WorkItem(int Id, string Title, string State, string WorkItemType, string? AssignedTo);
