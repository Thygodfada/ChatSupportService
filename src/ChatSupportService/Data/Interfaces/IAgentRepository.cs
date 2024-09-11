using System;
using ChatSupportService.Models;

namespace ChatSupportService.Data.Interfaces;


public interface IAgentRepository
{
    Task<IEnumerable<Agent>> GetAvailableAgentsAsync();
    Task<Agent> GetAgentByIdAsync(Guid agentId);
    Task<IEnumerable<Agent>> GetAgentsByShiftAsync(int shiftNumber);
    Task UpdateAgentStatusAsync(Agent agent);
}
