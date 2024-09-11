using System;

namespace ChatSupportService.Models;

public class Agent
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public AgentLevel Level { get; set; }
    public int MaxConcurrency { get; set; } = 10; // Max chats an agent can handle
    public int ShiftNumber { get; set; }
    public int CurrentChats { get; set; }

    public AgentStatus Status { get; set; }
    // public bool HasLeftShift { get; set; }
}
