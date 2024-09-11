using System;

namespace ChatSupportService.Models;

public class ChatSession
{
    public Guid Id { get; set; }
    public Guid AssignedAgentId { get; set; }
    public bool IsActive { get; set; }
    public ChatStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int PollCount { get; set; }
    public DateTime? LastPollAt { get; set; }
}
