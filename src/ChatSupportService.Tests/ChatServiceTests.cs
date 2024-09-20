using Moq;
using ChatSupportService.Data.Interfaces;
using chatSupportService.Data.Services;
using ChatSupportService.Models;

[TestFixture]
public class ChatServiceTests
{
    private Mock<IChatSessionRepository> _chatSessionRepositoryMock;
    private Mock<IAgentRepository> _agentRepositoryMock;
    private ChatService _chatService;

    [SetUp]
    public void Setup()
    {
        // Mock repositories
        _chatSessionRepositoryMock = new Mock<IChatSessionRepository>();
        _agentRepositoryMock = new Mock<IAgentRepository>();

        // Create service instance with mock dependencies
        _chatService = new ChatService(_chatSessionRepositoryMock.Object, _agentRepositoryMock.Object);
    }

    [Test]
    public async Task QueueChatSessionAsync_ShouldQueueChat_WhenCapacityIsAvailable()
    {
        // Arrange
        var availableAgents = GetMockAgents(2, AgentLevel.MidLevel); // Capacity for 12 chats
        var queuedChats = new List<ChatSession>();

        _agentRepositoryMock.Setup(repo => repo.GetAvailableAgentsAsync()).ReturnsAsync(availableAgents);
        _chatSessionRepositoryMock.Setup(repo => repo.GetQueuedChatsAsync()).ReturnsAsync(queuedChats);

        var newChatSession = new ChatSession { Id = Guid.NewGuid() };

        // Act
        var result = await _chatService.QueueChatSessionAsync(newChatSession, isOfficeHours: false);

        // Assert
        Assert.That(result, Is.True, "The chat session should be queued as capacity is available.");
        _chatSessionRepositoryMock.Verify(repo => repo.CreateChatSessionAsync(newChatSession), Times.Once);
    }

    [Test]
    public async Task QueueChatSessionAsync_ShouldNotQueueChat_WhenQueueCapacityIsFull()
    {
        // Arrange
        var availableAgents = GetMockAgents(2, AgentLevel.MidLevel); // Capacity for 12 chats
        var queuedChats = new List<ChatSession>();
        for (int i = 0; i < 18; i++) queuedChats.Add(new ChatSession { Id = Guid.NewGuid() }); // Full queue

        _agentRepositoryMock.Setup(repo => repo.GetAvailableAgentsAsync()).ReturnsAsync(availableAgents);
        _chatSessionRepositoryMock.Setup(repo => repo.GetQueuedChatsAsync()).ReturnsAsync(queuedChats);

        var newChatSession = new ChatSession { Id = Guid.NewGuid() };

        // Act
        var result = await _chatService.QueueChatSessionAsync(newChatSession, isOfficeHours: false);

        // Assert
        Assert.That(result, Is.False, "The chat session should not be queued as the queue is full.");
        Assert.That(queuedChats, Has.Count.EqualTo(18), "The number of queued chats should remain the same.");
        _chatSessionRepositoryMock.Verify(repo => repo.CreateChatSessionAsync(It.IsAny<ChatSession>()), Times.Never);
    }

    [Test]
    public async Task QueueChatSessionAsync_ShouldQueueChat_WhenOverflowIsAvailable()
    {
        // Arrange
        var availableAgents = GetMockAgents(2, AgentLevel.MidLevel); // Capacity for 12 chats
        var queuedChats = new List<ChatSession>();
        for (int i = 0; i < 12; i++) queuedChats.Add(new ChatSession { Id = Guid.NewGuid() }); // Full queue

        _agentRepositoryMock.Setup(repo => repo.GetAvailableAgentsAsync()).ReturnsAsync(availableAgents);
        _chatSessionRepositoryMock.Setup(repo => repo.GetQueuedChatsAsync()).ReturnsAsync(queuedChats);

        var newChatSession = new ChatSession { Id = Guid.NewGuid() };

        // Act
        var result = await _chatService.QueueChatSessionAsync(newChatSession, isOfficeHours: true); // Office hours, overflow available

        // Assert
        Assert.That(result, Is.True, "The chat session should be queued as overflow is available during office hours.");
        _chatSessionRepositoryMock.Verify(repo => repo.CreateChatSessionAsync(newChatSession), Times.Once);
    }

    [Test]
    public async Task AssignChatsToAgents_ShouldAssignChatsBasedOnRoundRobin()
    {
        // Arrange
        var availableAgents = new List<Agent>
        {
            new Agent { Id = Guid.NewGuid(), Level = AgentLevel.Junior, CurrentChats = 0 },
            new Agent { Id = Guid.NewGuid(), Level = AgentLevel.MidLevel, CurrentChats = 0 }
        };

        var queuedChats = new List<ChatSession>
        {
            new ChatSession { Id = Guid.NewGuid() },
            new ChatSession { Id = Guid.NewGuid() }
        };

        _agentRepositoryMock.Setup(repo => repo.GetAvailableAgentsAsync()).ReturnsAsync(availableAgents);
        _chatSessionRepositoryMock.Setup(repo => repo.GetQueuedChatsAsync()).ReturnsAsync(queuedChats);

        // Act
        await _chatService.AssignChatsToAgentsAsync();

        // Assert
        // Verify that each agent has been assigned 1 chat in round-robin fashion
        Assert.That(availableAgents[0].CurrentChats, Is.EqualTo(2), "Junior agent should have 1 chat.");
        Assert.That(availableAgents[1].CurrentChats, Is.EqualTo(0), "Mid-level agent should have 1 chat.");
    }

    [Test]
    public async Task MonitorPollingAsync_ShouldMarkChatInactive_WhenPollsAreMissed()
    {
        // Arrange
        var chatSessions = new List<ChatSession>
        {
            new ChatSession { Id = Guid.NewGuid(), PollCount = 3, IsActive = true }, // Poll limit exceeded
            new ChatSession { Id = Guid.NewGuid(), PollCount = 1, IsActive = true }  // Within limit
        };

        _chatSessionRepositoryMock.Setup(repo => repo.GetQueuedChatsAsync()).ReturnsAsync(chatSessions);

        // Act
        await _chatService.MonitorPollingAsync();

        // Assert
        // Verify that the session with exceeded poll count is marked inactive
        Assert.That(chatSessions[0].IsActive, Is.False, "Chat session should be marked inactive after 3 missed polls.");
        Assert.That(chatSessions[1].IsActive, Is.True, "Chat session should remain active as it's within poll limit.");
    }

    [Test]
    public void CalculateTeamCapacity_ShouldReturnCorrectCapacity()
    {
        // Arrange
        var availableAgents = new List<Agent>
        {
            new Agent { Level = AgentLevel.Junior, MaxConcurrency = 10 },    // Junior
            new Agent { Level = AgentLevel.MidLevel, MaxConcurrency = 10 }, // Mid-level
            new Agent { Level = AgentLevel.MidLevel, MaxConcurrency = 10 } // Mid-level
        };

        // Act
        var teamCapacity = _chatService.CalculateTeamCapacity(availableAgents);

        // Assert
        Assert.That(teamCapacity, Is.EqualTo(16), "Team capacity should be (2 * 10 * 0.6) + (1 * 10 * 0.4) = 16.");
    }

    [Test]
    public void CalculateOverflowCapacity_ShouldReturnCorrectOverflowCapacity()
    {
        // Act
        var overflowCapacity = _chatService.CalculateOverflowCapacity();

        // Assert
        Assert.That(overflowCapacity, Is.EqualTo(24), "Overflow capacity should be 6 * 10 * 0.4 = 24.");
    }

    // Mock agents helper method
    private List<Agent> GetMockAgents(int count, AgentLevel seniority)
    {
        var agents = new List<Agent>();
        for (int i = 0; i < count; i++)
        {
            agents.Add(new Agent
            {
                Id = Guid.NewGuid(),
                Level = seniority,
                MaxConcurrency = 10,
                CurrentChats = 0
            });
        }
        return agents;
    }
}
