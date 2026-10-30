using LiveSwitch.API.Data;
using LiveSwitch.API.DTOs;
using LiveSwitch.API.Hubs;
using LiveSwitch.API.Models;
using LiveSwitch.API.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LiveSwitch.API.Tests;

public class LiveSwitchServiceTests
{
    private readonly LiveSwitchOptions _liveSwitchOptions;
    private readonly LiveSwitchTokenService _tokenService;

    public LiveSwitchServiceTests()
    {
        _liveSwitchOptions = new LiveSwitchOptions
        {
            ApplicationId = "test-app-id",
            SharedSecret = "test-shared-secret-key-12345",
            GatewayUrl = "https://cloud.liveswitch.io/",
            TokenExpirySeconds = 3600
        };

        var optionsWrapper = Options.Create(_liveSwitchOptions);
        _tokenService = new LiveSwitchTokenService(optionsWrapper, NullLogger<LiveSwitchTokenService>.Instance);
    }

    [Fact]
    public void GenerateRegisterToken_ReturnsNonEmptyJwtToken()
    {
        // Act
        var token = _tokenService.GenerateRegisterToken("user-123", "device-abc", "client-xyz");

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);
        // JWT tokens have 3 parts separated by dots
        Assert.Equal(3, token.Split('.').Length);
    }

    [Fact]
    public void GenerateJoinToken_ReturnsValidSignedJwtForChannel()
    {
        // Act
        var token = _tokenService.GenerateJoinToken("user-123", "device-abc", "client-xyz", "channel-999");

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);
        Assert.Equal(3, token.Split('.').Length);
    }

    [Fact]
    public void GetPublicConfig_NeverExposesSharedSecret()
    {
        // Act
        var config = _tokenService.GetPublicConfig();

        // Assert
        Assert.Equal("test-app-id", config.ApplicationId);
        Assert.Equal("https://cloud.liveswitch.io/", config.GatewayUrl);
        Assert.Empty(config.SharedSecret);
    }

    [Fact]
    public async Task CallService_Lifecycle_CreateJoinLeaveEnd()
    {
        // Arrange in-memory SQLite database
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        // Mock HubContext
        var mockHubContext = new TestHubContext();

        var callService = new CallService(
            context,
            mockHubContext,
            NullLogger<CallService>.Instance
        );

        // Act 1: Create Call
        var alice = await context.Users.FirstAsync(u => u.Username == "alice");
        var bob = await context.Users.FirstAsync(u => u.Username == "bob");
        var charlie = await context.Users.FirstAsync(u => u.Username == "charlie");

        var call = await callService.CreateCallAsync(alice.Id, new CreateCallRequestDto
        {
            Title = "Weekly Standup",
            CallType = CallType.Video,
            ParticipantUserIds = new List<string> { bob.Id }
        });

        // Assert Call created
        Assert.NotNull(call);
        Assert.Equal("Weekly Standup", call.Title);
        Assert.Equal(CallStatus.Ringing, call.Status);
        Assert.Equal(2, call.Participants.Count);

        // Act 2: Authorization Checks
        var aliceAuthorized = await callService.AuthorizeUserForCallAsync(call.Id, alice.Id);
        var bobAuthorized = await callService.AuthorizeUserForCallAsync(call.Id, bob.Id);
        var charlieAuthorized = await callService.AuthorizeUserForCallAsync(call.Id, charlie.Id);

        Assert.True(aliceAuthorized, "Alice (initiator) must be authorized");
        Assert.True(bobAuthorized, "Bob (invited participant) must be authorized");
        Assert.False(charlieAuthorized, "Charlie (not invited) must NOT be authorized");

        // Act 3: Bob joins the call
        var joinedCall = await callService.JoinCallAsync(call.Id, bob.Id);
        Assert.NotNull(joinedCall);
        Assert.Equal(CallStatus.InProgress, joinedCall.Status);
        Assert.NotNull(joinedCall.StartedAt);

        var bobParticipant = joinedCall.Participants.First(p => p.UserId == bob.Id);
        Assert.Equal(ParticipantStatus.Joined, bobParticipant.Status);

        // Act 4: Bob leaves
        var leftCall = await callService.LeaveCallAsync(call.Id, bob.Id);
        Assert.NotNull(leftCall);

        // Act 5: Alice ends call
        var endedCall = await callService.EndCallAsync(call.Id, alice.Id);
        Assert.NotNull(endedCall);
        Assert.Equal(CallStatus.Ended, endedCall.Status);
        Assert.NotNull(endedCall.EndedAt);
        Assert.NotNull(endedCall.DurationSeconds);

        // Act 6: Call History
        var aliceHistory = await callService.GetCallHistoryAsync(alice.Id);
        Assert.Single(aliceHistory);
        Assert.Equal(call.Id, aliceHistory[0].Id);
    }

    [Fact]
    public async Task AuthService_Login_ValidAndInvalidCredentials()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var jwtOptions = Options.Create(new JwtOptions
        {
            Key = "TestSecretKeyForAuthServiceTests_2026!#$",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpiryMinutes = 60
        });

        var authService = new AuthService(context, jwtOptions, NullLogger<AuthService>.Instance);

        // Valid Login
        var validResult = await authService.LoginAsync(new LoginRequestDto
        {
            Username = "alice",
            Password = "Password123!"
        });

        Assert.NotNull(validResult);
        Assert.NotEmpty(validResult.Token);
        Assert.Equal("Alice Johnson", validResult.User.DisplayName);

        // Invalid Password
        var invalidResult = await authService.LoginAsync(new LoginRequestDto
        {
            Username = "alice",
            Password = "WrongPassword!"
        });

        Assert.Null(invalidResult);
    }

    // Minimal Test HubContext for unit testing
    private class TestHubContext : IHubContext<CallHub>
    {
        public IHubClients Clients => new TestHubClients();
        public IGroupManager Groups => new TestGroupManager();
    }

    private class TestHubClients : IHubClients
    {
        public IClientProxy All => new TestClientProxy();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new TestClientProxy();
        public IClientProxy Client(string connectionId) => new TestClientProxy();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new TestClientProxy();
        public IClientProxy Group(string groupName) => new TestClientProxy();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new TestClientProxy();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => new TestClientProxy();
        public IClientProxy User(string userId) => new TestClientProxy();
        public IClientProxy Users(IReadOnlyList<string> userIds) => new TestClientProxy();
    }

    private class TestClientProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private class TestGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
