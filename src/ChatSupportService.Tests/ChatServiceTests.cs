using chatSupportService.Data.Services;
using ChatSupportService.Data.Interfaces;
using ChatSupportService.Models;
using Moq;

namespace ChatSupportService.Tests
{
	[TestFixture]
	public class ChatServiceTests
	{
		private ChatService _chatService;
		private Mock<IChatSessionRepository> _chatSessionRepositoryMock;
		private Mock<IAgentRepository> _agentRepositoryMock;

		[SetUp]
		public void Setup()
		{
			_chatSessionRepositoryMock = new Mock<IChatSessionRepository>();
			_agentRepositoryMock = new Mock<IAgentRepository>();
			_chatService = new ChatService(_chatSessionRepositoryMock.Object, _agentRepositoryMock.Object);
		}

		[Test]
		public async Task QueueChatSessionAsync_ShouldQueueChat_WhenBelowCapacity()
		{
			// Arrange
			var agents = GetMockAgents(2); // Mock 2 available agents
			_agentRepositoryMock.Setup(repo => repo.GetAvailableAgentsAsync())
				.ReturnsAsync(agents);

			_chatSessionRepositoryMock.Setup(repo => repo.GetQueuedChatsAsync())
				.ReturnsAsync(new List<ChatSession>()); // Empty queue

			var chatSession = new ChatSession { Id = Guid.NewGuid() };

			// Act
			var result = await _chatService.QueueChatSessionAsync(chatSession, isOfficeHours: true);

			// Assert
			Assert.That(result, Is.True, "The chat session should be queued.");
			_chatSessionRepositoryMock.Verify(repo => repo.CreateChatSessionAsync(chatSession), Times.Once);
		}

		[Test]
		public async Task QueueChatSessionAsync_ShouldNotQueueChat_WhenQueueCapacityExceeded()
		{
			// Arrange
			var agents = GetMockAgents(2); // Mock 2 agents available
			_agentRepositoryMock.Setup(repo => repo.GetAvailableAgentsAsync())
				.ReturnsAsync(agents);

			// Mock chat queue to be full
			var queuedChats = Enumerable.Range(1, 30).Select(_ => new ChatSession { Id = Guid.NewGuid() }).ToList();
			_chatSessionRepositoryMock.Setup(repo => repo.GetQueuedChatsAsync())
				.ReturnsAsync(queuedChats);

			var chatSession = new ChatSession { Id = Guid.NewGuid() };

			// Act
			var result = await _chatService.QueueChatSessionAsync(chatSession, isOfficeHours: false);

			// Assert
			Assert.That(result, Is.False, "The chat session should not be queued because the queue is full.");
			_chatSessionRepositoryMock.Verify(repo => repo.CreateChatSessionAsync(It.IsAny<ChatSession>()), Times.Never);
		}

		[Test]
		public async Task AssignChatsToAgentsAsync_ShouldAssignChatsToAvailableAgents()
		{
			// Arrange
			var agents = GetMockAgents(2);
			_agentRepositoryMock.Setup(repo => repo.GetAvailableAgentsAsync())
				.ReturnsAsync(agents);

			var queuedChats = new List<ChatSession>
			{
				new ChatSession { Id = Guid.NewGuid() },
				new ChatSession { Id = Guid.NewGuid() }
			};

			_chatSessionRepositoryMock.Setup(repo => repo.GetQueuedChatsAsync())
				.ReturnsAsync(queuedChats);

			// Act
			await _chatService.AssignChatsToAgentsAsync();

			// Assert
			_chatSessionRepositoryMock.Verify(repo => repo.UpdateChatSessionAsync(It.IsAny<ChatSession>()), Times.Exactly(queuedChats.Count));
			_agentRepositoryMock.Verify(repo => repo.UpdateAgentStatusAsync(It.IsAny<Agent>()), Times.Exactly(queuedChats.Count));
		}

	
		[Test]
		public async Task MonitorPollingAsync_ShouldMarkSessionInactive_WhenPollingTimeoutExceeded()
		{
			// Arrange
			var session = new ChatSession
			{
				Id = Guid.NewGuid(),
				IsActive = true,
				LastPollAt = DateTime.UtcNow.AddSeconds(-4) // Simulate a polling timeout (exceeds the 3-second timeout)
			};

			var chatSessions = new List<ChatSession> { session };

			// Mocking the repository methods
			_chatSessionRepositoryMock.Setup(repo => repo.GetQueuedChatsAsync())
									  .ReturnsAsync(chatSessions);

			_chatSessionRepositoryMock.Setup(repo => repo.GetChatSessionByIdAsync(session.Id))
									  .ReturnsAsync(session);

			// Act
			await _chatService.MonitorPollingAsync();

			// Assert
			// Verify that UpdateChatSessionAsync was called with the session marked inactive
			_chatSessionRepositoryMock.Verify(repo => repo.UpdateChatSessionAsync(It.Is<ChatSession>(
				s => s.Id == session.Id && s.IsActive == false)), Times.Once);
		}

		[Test]
		public async Task QueueChatSessionAsync_ShouldQueueChat_WhenOverflowCapacityAvailable()
		{
			// Arrange: Set up the normal team to be full
			var agents = GetMockAgents(3); // Mock 3 agents
			_agentRepositoryMock.Setup(repo => repo.GetAvailableAgentsAsync())
				.ReturnsAsync(agents);

			// Simulate full chat queue
			var queuedChats = Enumerable.Range(1, 45).Select(_ => new ChatSession { Id = Guid.NewGuid() }).ToList();
			_chatSessionRepositoryMock.Setup(repo => repo.GetQueuedChatsAsync())
				.ReturnsAsync(queuedChats);

			var chatSession = new ChatSession { Id = Guid.NewGuid() };

			// Act: During office hours with overflow capacity
			var result = await _chatService.QueueChatSessionAsync(chatSession, isOfficeHours: true);

			// Assert
			Assert.That(result, Is.True, "The chat session should be queued due to overflow capacity.");
			_chatSessionRepositoryMock.Verify(repo => repo.CreateChatSessionAsync(chatSession), Times.Once);
		}

		[Test]
		public async Task QueueChatSessionAsync_ShouldNotQueueChat_WhenOverflowCapacityExceeded()
		{
			// Arrange: Set up agents and normal capacity full
			var agents = GetMockAgents(3); // Mock 3 agents
			_agentRepositoryMock.Setup(repo => repo.GetAvailableAgentsAsync())
				.ReturnsAsync(agents);

			// Simulate full chat queue
			var queuedChats = Enumerable.Range(1, 51).Select(_ => new ChatSession { Id = Guid.NewGuid() }).ToList(); // More than total capacity
			_chatSessionRepositoryMock.Setup(repo => repo.GetQueuedChatsAsync())
				.ReturnsAsync(queuedChats);

			var chatSession = new ChatSession { Id = Guid.NewGuid() };

			// Act: During office hours, but overflow capacity exceeded
			var result = await _chatService.QueueChatSessionAsync(chatSession, isOfficeHours: true);

			// Assert
			Assert.That(result, Is.False, "The chat session should not be queued because overflow capacity is exceeded.");
			_chatSessionRepositoryMock.Verify(repo => repo.CreateChatSessionAsync(It.IsAny<ChatSession>()), Times.Never);
		}
		[Test]
		public async Task AssignChatsToAgentsAsync_ShouldAssignInRoundRobin_FavoringJuniors()
		{
			// Arrange
			var juniorAgentId1 = Guid.NewGuid();
			var juniorAgentId2 = Guid.NewGuid();
			var midLevelAgentId = Guid.NewGuid();
			var seniorAgentId = Guid.NewGuid();

				var agents = new List<Agent>
		{
			new Agent { Id = juniorAgentId1, Level = AgentLevel.Junior, CurrentChats = 0 },
			new Agent { Id = juniorAgentId2, Level = AgentLevel.Junior, CurrentChats = 0 },
			new Agent { Id = midLevelAgentId, Level = AgentLevel.MidLevel, CurrentChats = 0 },
			new Agent { Id = seniorAgentId, Level = AgentLevel.Senior, CurrentChats = 0 }
		};

				var chatSessions = new List<ChatSession>
		{
			new ChatSession { Id = Guid.NewGuid(), IsActive = false },
			new ChatSession { Id = Guid.NewGuid(), IsActive = false },
			new ChatSession { Id = Guid.NewGuid(), IsActive = false },
			new ChatSession { Id = Guid.NewGuid(), IsActive = false },
			new ChatSession { Id = Guid.NewGuid(), IsActive = false },
			new ChatSession { Id = Guid.NewGuid(), IsActive = false }
		};

			_chatSessionRepositoryMock.Setup(repo => repo.GetQueuedChatsAsync())
									  .ReturnsAsync(chatSessions);

			_agentRepositoryMock.Setup(repo => repo.GetAvailableAgentsAsync())
								.ReturnsAsync(agents);

			// Act
			await _chatService.AssignChatsToAgentsAsync();

			// Assert
			// Verify assignments
			_chatSessionRepositoryMock.Verify(repo => repo.UpdateChatSessionAsync(It.Is<ChatSession>(s =>
				s.AssignedAgentId == juniorAgentId1)), Times.Exactly(3)); // Each junior should get 3 chats

			_chatSessionRepositoryMock.Verify(repo => repo.UpdateChatSessionAsync(It.Is<ChatSession>(s =>
				s.AssignedAgentId == juniorAgentId2)), Times.Exactly(3)); // Each junior should get 3 chats

			_chatSessionRepositoryMock.Verify(repo => repo.UpdateChatSessionAsync(It.Is<ChatSession>(s =>
				s.AssignedAgentId == midLevelAgentId)), Times.Never); // Mid-level should not be assigned

			_chatSessionRepositoryMock.Verify(repo => repo.UpdateChatSessionAsync(It.Is<ChatSession>(s =>
				s.AssignedAgentId == seniorAgentId)), Times.Never); // Senior should not be assigned
		}






		private List<Agent> GetMockAgents(int count)
		{
			var agents = new List<Agent>();
			for (int i = 0; i < count; i++)
			{
				agents.Add(new Agent
				{
					Id = Guid.NewGuid(),
					MaxConcurrency = 10,
					Level = AgentLevel.MidLevel,
					CurrentChats = 0
				});
			}
			return agents;
		}
	}
}
