using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LiveSwitch.API.Data;
using LiveSwitch.API.DTOs;
using LiveSwitch.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LiveSwitch.API.Services;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Key { get; set; } = "LiveSwitch_SuperSecret_Jwt_SigningKey_ForTesting_2026!#$";
    public string Issuer { get; set; } = "LiveSwitchAPI";
    public string Audience { get; set; } = "LiveSwitchWeb";
    public int ExpiryMinutes { get; set; } = 1440; // 24 hours
}

public interface IAuthService
{
    Task<AuthResponseDto?> LoginAsync(LoginRequestDto request);
    Task<AuthResponseDto?> RegisterAsync(RegisterRequestDto request);
    Task<UserDto?> GetUserByIdAsync(string userId);
    Task<List<UserDto>> GetAllUsersExceptAsync(string currentUserId);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(AppDbContext context, IOptions<JwtOptions> jwtOptions, ILogger<AuthService> logger)
    {
        _context = context;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    public async Task<AuthResponseDto?> LoginAsync(LoginRequestDto request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username.ToLower());

        if (user == null || !AppDbContext.VerifyPassword(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for username {Username}", request.Username);
            return null;
        }

        var token = GenerateJwtToken(user);
        return new AuthResponseDto
        {
            Token = token,
            User = MapToDto(user)
        };
    }

    public async Task<AuthResponseDto?> RegisterAsync(RegisterRequestDto request)
    {
        var exists = await _context.Users
            .AnyAsync(u => u.Username.ToLower() == request.Username.ToLower() || u.Email.ToLower() == request.Email.ToLower());

        if (exists)
        {
            _logger.LogWarning("Registration failed: Username or email already exists: {Username}, {Email}", request.Username, request.Email);
            return null;
        }

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            DisplayName = request.DisplayName,
            AvatarUrl = $"https://api.dicebear.com/7.x/bottts/svg?seed={Uri.EscapeDataString(request.Username)}",
            PasswordHash = AppDbContext.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var token = GenerateJwtToken(user);
        return new AuthResponseDto
        {
            Token = token,
            User = MapToDto(user)
        };
    }

    public async Task<UserDto?> GetUserByIdAsync(string userId)
    {
        var user = await _context.Users.FindAsync(userId);
        return user != null ? MapToDto(user) : null;
    }

    public async Task<List<UserDto>> GetAllUsersExceptAsync(string currentUserId)
    {
        var users = await _context.Users
            .Where(u => u.Id != currentUserId)
            .OrderBy(u => u.DisplayName)
            .ToListAsync();

        return users.Select(MapToDto).ToList();
    }

    private string GenerateJwtToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwtOptions.Key);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new("displayName", user.DisplayName),
            new("avatarUrl", user.AvatarUrl)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes),
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private static UserDto MapToDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            CreatedAt = user.CreatedAt
        };
    }
}
