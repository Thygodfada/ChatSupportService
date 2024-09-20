
using ChatSupportService.Data.Interfaces;
using ChatSupportService.Models;
namespace chatSupportService.Data.Services
{

    public class ChatService
    {
        private readonly IChatSessionRepository _chatSessionRepository;
        private readonly IAgentRepository _agentRepository;
        private const int MaxConcurrency = 10;
        private readonly double _queueMultiplier = 1.5;
        private const int PollingTimeoutInSeconds = 3;

        public ChatService(IChatSessionRepository chatSessionRepository, IAgentRepository agentRepository)
        {
            _chatSessionRepository = chatSessionRepository;
            _agentRepository = agentRepository;
        }

        public async Task<bool> QueueChatSessionAsync(ChatSession chatSession, bool isOfficeHours)
        {
            var agents = await _agentRepository.GetAvailableAgentsAsync();
            var baseCapacity = CalculateTeamCapacity(agents);
            var totalCapacity = (int)Math.Floor(baseCapacity * _queueMultiplier);

            var queuedChats = await _chatSessionRepository.GetQueuedChatsAsync();
            var queuedChatsCount = queuedChats.Count();

            if (queuedChatsCount >= totalCapacity)
            {
                if (!isOfficeHours)
                {
                    return false; // Queue is full and it's not office hours, reject chat session
                }

                totalCapacity += CalculateOverflowCapacity();

                if (queuedChatsCount >= totalCapacity)
                {
                    return false; // Queue is full, reject chat session
                }
            }

            await _chatSessionRepository.CreateChatSessionAsync(chatSession);
            return true;
        }

        public async Task AssignChatsToAgentsAsync()
        {
            var queuedChats = await _chatSessionRepository.GetQueuedChatsAsync();
            var availableAgents = await _agentRepository.GetAvailableAgentsAsync();

            foreach (var chat in queuedChats)
            {
                var agent = await GetAvailableAgentAsync(availableAgents);
                if (agent != null)
                {
                    await AssignChatToAgentAsync(chat, agent);
                }
                else
                {
                    break; // No available agent to assign chat
                }
            }
        }

        public int CalculateTeamCapacity(IEnumerable<Agent> agents)
        {
            var capacity = agents.Sum(agent => agent.MaxConcurrency * GetEfficiencyMultiplier(agent.Level));
            return (int)Math.Floor(capacity);
        }

        private double GetEfficiencyMultiplier(AgentLevel level)
        {
            return level switch
            {
                AgentLevel.Junior => 0.4,
                AgentLevel.MidLevel => 0.6,
                AgentLevel.Senior => 0.8,
                AgentLevel.TeamLead => 0.5,
                _ => 0.4 // Default to junior level efficiency
            };
        }

        public int CalculateOverflowCapacity()
        {
            const int overflowTeamSize = 6; // Overflow team size
            return (int)(MaxConcurrency * 0.4 * overflowTeamSize);
        }

        private Task<Agent> GetAvailableAgentAsync(IEnumerable<Agent> agents)
        {
            var sortedAgents = agents.OrderBy(a => a.Level).ThenBy(a => a.CurrentChats);

            foreach (var agent in sortedAgents)
            {
                if (agent.CurrentChats < MaxConcurrency)
                {
                    return Task.FromResult(agent);
                }
            }

            return Task.FromResult<Agent>(null);
        }

        private async Task AssignChatToAgentAsync(ChatSession chat, Agent agent)
        {
            chat.AssignedAgentId = agent.Id;
            chat.IsActive = true;
            agent.CurrentChats++;

            await _chatSessionRepository.UpdateChatSessionAsync(chat);
            await _agentRepository.UpdateAgentStatusAsync(agent);
        }

        public async Task MonitorPollingAsync()
        {
            var pollLimit = 3;
            var chatSessions = await _chatSessionRepository.GetQueuedChatsAsync();

            foreach (var session in chatSessions)
            {

                if (session.PollCount >= pollLimit)
                {
                    session.IsActive = false;
                    await MarkSessionInactiveAsync(session.Id);
                }
                else
                {
                    session.PollCount++;
                    await _chatSessionRepository.UpdateChatSessionAsync(session);
                }
            }
        }

        private async Task MarkSessionInactiveAsync(Guid sessionId)
        {
            var chatSession = await _chatSessionRepository.GetChatSessionByIdAsync(sessionId);
            if (chatSession != null)
            {
                chatSession.IsActive = false;
                await _chatSessionRepository.UpdateChatSessionAsync(chatSession);
            }
        }
    }
}


