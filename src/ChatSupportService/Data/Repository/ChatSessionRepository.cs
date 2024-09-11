using ChatSupportService.Data;
using ChatSupportService.Data.Interfaces;
using ChatSupportService.Models;
using Microsoft.EntityFrameworkCore;

public class ChatSessionRepository : IChatSessionRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ChatSessionRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<ChatSession>> GetQueuedChatsAsync()
    {
        return await _dbContext.ChatSessions
                               .Where(cs => cs.Status == ChatStatus.Queued)
                               .ToListAsync();
    }

    public async Task CreateChatSessionAsync(ChatSession chatSession)
    {
        await _dbContext.ChatSessions.AddAsync(chatSession);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateChatSessionAsync(ChatSession chatSession)
    {
        _dbContext.ChatSessions.Update(chatSession);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<ChatSession> GetChatSessionByIdAsync(Guid sessionId)
    {
        return await _dbContext.ChatSessions.FindAsync(sessionId);
    }

    public async Task<int> GetActiveChatCountForAgentAsync(Guid agentId)
    {
        return await _dbContext.ChatSessions
                               .CountAsync(cs => cs.AssignedAgentId == agentId && cs.Status == ChatStatus.Active);
    }
}
