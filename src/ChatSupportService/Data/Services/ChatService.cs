
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
			var totalCapacity = (int)Math.Floor(CalculateTeamCapacity(agents) * _queueMultiplier);

			var queuedChats = await _chatSessionRepository.GetQueuedChatsAsync();
			var queuedChatsCount = queuedChats.Count();

			if (queuedChatsCount >= totalCapacity)
			{
				if (isOfficeHours)
				{
					totalCapacity += CalculateOverflowCapacity(); // Add overflow capacity
				}

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

		private int CalculateTeamCapacity(IEnumerable<Agent> agents)
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

		private int CalculateOverflowCapacity()
		{
			const int overflowTeamSize = 6; // 6 junior agents
			return (int)(MaxConcurrency * 0.4 * overflowTeamSize);
		}

		private Task<Agent> GetAvailableAgentAsync(IEnumerable<Agent> agents)
		{
			var sortedAgents = agents.OrderBy(a => a.Level).ThenBy(a => a.CurrentChats);

			foreach (var agent in sortedAgents)
			{
				if (agent.CurrentChats < MaxConcurrency)
				{
					return Task.FromResult(agent); // Wrap agent in a Task to return asynchronously
				}
			}

			return Task.FromResult<Agent>(null); // Return null if no available agent is found
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
			var now = DateTime.UtcNow;
			var chatSessions = await _chatSessionRepository.GetQueuedChatsAsync();

			foreach (var session in chatSessions)
			{
				if ((now - session.LastPollAt.Value).TotalSeconds > PollingTimeoutInSeconds)
				{
					await MarkSessionInactiveAsync(session.Id);
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



/*public class ChatService
{
	private readonly IAgentRepository _agentRepository;
	private readonly IChatSessionRepository _chatSessionRepository;
	private const int MaxConcurrency = 10;
	private Queue<ChatSession> _chatQueue = new Queue<ChatSession>();
	private Dictionary<Guid, DateTime> _pollingTracker = new Dictionary<Guid, DateTime>();
	private readonly double queueMultiplier = 1.5;
	private const int PollingTimeoutInSeconds = 3;

	public ChatService(IAgentRepository agentRepository, IChatSessionRepository chatSessionRepository)
	{
		_agentRepository = agentRepository;
		_chatSessionRepository = chatSessionRepository;

	}

	public async Task<bool> QueueChatSessionAsync(ChatSession chatSession, bool isOfficeHours)
	{
		// Fetch available agents
		var agents = await _agentRepository.GetAvaliableAgentsAsync();

		// Calculate total team capacity using multiplier
		var totalCapacity = (int)Math.Floor(CalculateTeamCapacity(agents) * queueMultiplier);

		// Get the number of queued chats
		var queuedChatsCount = (await _chatSessionRepository.GetQueuedChatsAsync()).Count();

		// Check if queue is full, consider overflow if during office hours
		if (isOfficeHours)
		{
			totalCapacity += CalculateOverflowCapacity();
		}


		// If the queue is still full after overflow consideration, reject the chat session
		if (queuedChatsCount >= totalCapacity)
		{
			return false; // Queue is full, reject the chat session
		}


		// Queue is not full, add chat session to the queue
		await _chatSessionRepository.CreateChatSessionAsync(chatSession);
		_chatQueue.Enqueue(chatSession);
		return true;
	}



	public async Task AssignChatToAgentAsync()
	{
		var queuedChats = await _chatSessionRepository.GetQueuedChatsAsync();
		var availableAgents = await _agentRepository.GetAvaliableAgentsAsync();

		foreach (var chat in queuedChats)
		{
			var agent = await GetAvailableAgentAsync(availableAgents);
			if (agent != null)
			{
				await AssignChatToAgent(chat, agent);
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

	public Dictionary<Guid, DateTime> GetPollingTracker()
	{
		return _pollingTracker;
	}
	private int CalculateOverflowCapacity()
	{
		var overflowTeamSize = 6; // 6 junior agents
		return (int)(MaxConcurrency * GetEfficiencyMultiplier(AgentLevel.Junior) * overflowTeamSize);
	}
	public void PollChat(Guid sessionId)
	{
		_pollingTracker[sessionId] = DateTime.UtcNow;
	}

	public void SetPollingTime(Guid sessionId, DateTime pollingTime)
	{
		if (_pollingTracker.ContainsKey(sessionId))
		{
			_pollingTracker[sessionId] = pollingTime;
		}
	}
	public async Task MonitorPollingAsync()
	{
		var now = DateTime.UtcNow;
		foreach (var session in _pollingTracker.Keys.ToList())
		{
			if ((now - _pollingTracker[session]).TotalSeconds > PollingTimeoutInSeconds)
			{
				await MarkSessionInactive(session);
			}
		}
	}

	private async Task MarkSessionInactive(Guid sessionId)
	{
		if (_pollingTracker.ContainsKey(sessionId))
		{
			_pollingTracker.Remove(sessionId);
			// Mark session inactive
			var session = _chatQueue.FirstOrDefault(s => s.Id == sessionId);
			if (session != null)
			{
				session.IsActive = false;
				await _chatSessionRepository.UpdateChatSessionAsync(session);
			}
		}
	}

	private Task<Agent> GetAvailableAgentAsync(IEnumerable<Agent> agents)

	{
		// Sort agents by level (junior first), then by the number of chats they are handling
		var sortedAgents = agents.OrderBy(a => a.Level).ThenBy(a => a.CurrentChats);

		// Return the first available agent that has capacity for more chats
		foreach (var agent in sortedAgents)
		{
			if (agent.CurrentChats < MaxConcurrency)
			{
				return Task.FromResult<Agent>(agent);
			}
		}

		// If no agents are available, return null
		return Task.FromResult<Agent>(null);
	}

	private async Task AssignChatToAgent(ChatSession chat, Agent agent)
	{
		await _chatSessionRepository.AssignChatToAgentAsync(chat, agent);
		agent.CurrentChats++;
		chat.AssignedAgentId = agent.Id;
		chat.IsActive = true;
		await _agentRepository.UpdateAgentAsync(agent);
		await _chatSessionRepository.UpdateChatSessionAsync(chat);
	}
}
*/