// TeamTaskManagement.Service/Services/AuthService.cs
using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.DTOs.Auth;
using TeamTaskManagement.Core.Entities;
using TeamTaskManagement.Core.Enums;
using TeamTaskManagement.Core.Repositories;
using TeamTaskManagement.Core.Services;

namespace TeamTaskManagement.Service.Services;

public class AuthService : IAuthService
{
    private const string InvalidCredentialsMessage = "שם משתמש או סיסמה שגויים.";

    // hash בפורמט תקין שלא תואם שום סיסמה. משמש כשהמשתמש לא נמצא,
    // כדי שזמן התגובה יהיה זהה לזה של סיסמה שגויה (מניעת זיהוי שמות משתמש לפי זמן).
    private static readonly string DummyHash =
        $"600000.{Convert.ToBase64String(new byte[16])}.{Convert.ToBase64String(new byte[32])}";

    private readonly IUserRepository _users;
    private readonly IOrganizationRepository _organizations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthService(
        IUserRepository users,
        IOrganizationRepository organizations,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _users = users;
        _organizations = organizations;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<ServiceResult<AuthResponseDto>> RegisterAsync(
        RegisterRequestDto request, CancellationToken ct = default)
    {
        string userName = request.UserName.Trim();

        if (await _users.UserNameExistsAsync(userName, ct: ct))
            return ServiceResult<AuthResponseDto>.Fail(ResultStatus.Conflict, "שם המשתמש כבר תפוס.");

        var organization = new Organization
        {
            Name = request.OrganizationName.Trim()
        };

        var manager = new User
        {
            UserName = userName,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = UserRole.Manager,
            Organization = organization, // EF ממלא OrganizationId באותה שמירה
            TeamId = null                // למנהל אין צוות
        };

        await _organizations.AddAsync(organization, ct);
        await _users.AddAsync(manager, ct);

        // שמירה אחת = טרנזקציה אחת: החברה והמנהל נוצרים יחד, או שאף אחד מהם לא
        await _unitOfWork.SaveChangesAsync(ct);

        // אחרי השמירה ל-manager כבר יש Id ו-OrganizationId מבסיס הנתונים
        return ServiceResult<AuthResponseDto>.Success(_jwtTokenGenerator.Generate(manager));
    }

    public async Task<ServiceResult<AuthResponseDto>> LoginAsync(
        LoginRequestDto request, CancellationToken ct = default)
    {
        User? user = await _users.GetByUserNameAsync(request.UserName.Trim(), ct);

        if (user is null)
        {
            _passwordHasher.Verify(request.Password, DummyHash); // השוואת זמן תגובה
            return ServiceResult<AuthResponseDto>.Fail(ResultStatus.Unauthorized, InvalidCredentialsMessage);
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            return ServiceResult<AuthResponseDto>.Fail(ResultStatus.Unauthorized, InvalidCredentialsMessage);

        return ServiceResult<AuthResponseDto>.Success(_jwtTokenGenerator.Generate(user));
    }
}