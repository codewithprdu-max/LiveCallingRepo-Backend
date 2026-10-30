using FM.LiveSwitch;
using Microsoft.Extensions.Options;

namespace LiveSwitch.API.Services;

public class LiveSwitchOptions
{
    public const string SectionName = "LiveSwitch";
    public string ApplicationId { get; set; } = "my-liveswitch-app";
    public string SharedSecret { get; set; } = "--your-shared-secret--";
    public string GatewayUrl { get; set; } = "https://cloud.liveswitch.io/";
    public int TokenExpirySeconds { get; set; } = 3600;
}

public interface ILiveSwitchTokenService
{
    string GenerateRegisterToken(string userId, string deviceId, string clientId, string[]? roles = null, string[]? channelIds = null);
    string GenerateJoinToken(string userId, string deviceId, string clientId, string channelId, bool canUpdate = false);
    LiveSwitchOptions GetPublicConfig();
}

public class LiveSwitchTokenService : ILiveSwitchTokenService
{
    private readonly LiveSwitchOptions _options;
    private readonly ILogger<LiveSwitchTokenService> _logger;

    public LiveSwitchTokenService(IOptions<LiveSwitchOptions> options, ILogger<LiveSwitchTokenService> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.SharedSecret))
        {
            _logger.LogWarning("LiveSwitch:SharedSecret is not configured. LiveSwitch tokens will use a development fallback. Set your secret using 'dotnet user-secrets set \"LiveSwitch:SharedSecret\" \"<your-secret>\"' during development.");
            _options.SharedSecret = "dev-fallback-liveswitch-shared-secret-key";
        }
    }

    public LiveSwitchOptions GetPublicConfig()
    {
        return new LiveSwitchOptions
        {
            ApplicationId = _options.ApplicationId,
            GatewayUrl = _options.GatewayUrl,
            SharedSecret = string.Empty, // NEVER expose shared secret!
            TokenExpirySeconds = _options.TokenExpirySeconds
        };
    }

    public string GenerateRegisterToken(string userId, string deviceId, string clientId, string[]? roles = null, string[]? channelIds = null)
    {
        ChannelClaim[]? claims = null;
        if (channelIds != null && channelIds.Length > 0)
        {
            claims = channelIds.Select(c => new ChannelClaim(c)).ToArray();
        }

        _logger.LogInformation("Generating LiveSwitch registration token for user {UserId}, device {DeviceId}, client {ClientId}", userId, deviceId, clientId);

        return Token.GenerateClientRegisterToken(
            _options.ApplicationId,
            userId,
            deviceId,
            clientId,
            roles ?? Array.Empty<string>(),
            claims,
            _options.SharedSecret
        );
    }

    public string GenerateJoinToken(string userId, string deviceId, string clientId, string channelId, bool canUpdate = false)
    {
        var channelClaim = new ChannelClaim(channelId)
        {
            CanUpdate = canUpdate
        };

        _logger.LogInformation("Generating LiveSwitch join token for user {UserId}, channel {ChannelId}", userId, channelId);

        return Token.GenerateClientJoinToken(
            _options.ApplicationId,
            userId,
            deviceId,
            clientId,
            channelClaim,
            _options.SharedSecret
        );
    }
}
