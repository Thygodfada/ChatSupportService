using ChatSupportService.Data;
using ChatSupportService.Data.Interfaces;
using ChatSupportService.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatSupportService.Data.Repository
{
    public class AgentRepository : IAgentRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public AgentRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IEnumerable<Agent>> GetAvailableAgentsAsync()
        {
            return await _dbContext.Agents
                                   .Where(a => a.Status == AgentStatus.Available)
                                   .ToListAsync();
        }

        public async Task<Agent> GetAgentByIdAsync(Guid agentId)
        {
            return await _dbContext.Agents.FindAsync(agentId);
        }

        public async Task<IEnumerable<Agent>> GetAgentsByShiftAsync(int shiftNumber)
        {
            return await _dbContext.Agents
                                   .Where(a => a.ShiftNumber == shiftNumber)
                                   .ToListAsync();
        }

        public async Task UpdateAgentStatusAsync(Agent agent)
        {
            _dbContext.Agents.Update(agent);
            await _dbContext.SaveChangesAsync();
        }
    }
}
