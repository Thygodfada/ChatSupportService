using System;

namespace ChatSupportService.Models;

public class ChatQueue
{
    public Queue<ChatSession> Sessions { get; set; } = new Queue<ChatSession>();
}
