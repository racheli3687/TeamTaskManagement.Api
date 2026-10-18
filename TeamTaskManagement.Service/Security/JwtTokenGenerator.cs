// TeamTaskManagement.Service/Security/JwtTokenGenerator.cs
using System.Text;
using AutoMapper;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TeamTaskManagement.Core.DTOs.Auth;
using TeamTaskManagement.Core.Entities;
using TeamTaskManagement.Core.Options;
using TeamTaskManagement.Core.Services;

namespace TeamTaskManagement.Service.Security;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    // שמות ה-claims. ICurrentUser ב-Api (משימה 4.1) יקרא בדיוק את השמות האלה.
    public const string UserNameClaim = "name";
    public const string RoleClaim = "role";
    public const string OrganizationIdClaim = "orgId";
    public const string TeamIdClaim = "teamId";

    private readonly JwtOptions _options;
    private readonly IMapper _mapper;
    private readonly SigningCredentials _signingCredentials;
    private readonly JsonWebTokenHandler _handler = new();

    public JwtTokenGenerator(IOptions<JwtOptions> options, IMapper mapper)
    {
        _options = options.Value;
        _mapper = mapper;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public AuthResponseDto Generate(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        DateTime now = DateTime.UtcNow;
        DateTime expiresAt = now.AddMinutes(_options.ExpiryMinutes);

        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            [UserNameClaim] = user.UserName,
            [RoleClaim] = user.Role.ToString(),
            [OrganizationIdClaim] = user.OrganizationId.ToString()
        };

        // למנהל החברה אין צוות - במקרה כזה ה-claim פשוט לא נכלל ב-token
        if (user.TeamId.HasValue)
            claims[TeamIdClaim] = user.TeamId.Value.ToString();

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Claims = claims,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            SigningCredentials = _signingCredentials
        };

        // פרטי המשתמש מהמיפוי ב-UserProfile, ואז שני השדות שסומנו שם ב-Ignore
        var response = _mapper.Map<AuthResponseDto>(user);
        response.Token = _handler.CreateToken(descriptor);
        response.ExpiresAt = expiresAt;

        return response;
    }
}