using System;
using ChatSupportService.Models;

namespace ChatSupportService.Data.Interfaces;

public interface IChatSessionRepository
{
    Task<IEnumerable<ChatSession>> GetQueuedChatsAsync();
    Task CreateChatSessionAsync(ChatSession chatSession);
    Task UpdateChatSessionAsync(ChatSession chatSession);
    Task<ChatSession> GetChatSessionByIdAsync(Guid sessionId);
    Task<int> GetActiveChatCountForAgentAsync(Guid agentId);
}


