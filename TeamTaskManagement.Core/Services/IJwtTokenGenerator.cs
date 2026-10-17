using TeamTaskManagement.Core.DTOs.Auth;
using TeamTaskManagement.Core.Entities;

namespace TeamTaskManagement.Core.Services;

public interface IJwtTokenGenerator
{
    // יוצר token עם claims של המשתמש: מזהה, תפקיד, חברה וצוות,
    // ומחזיר את התשובה המלאה להתחברות (Token, תוקף ופרטי המשתמש)
    AuthResponseDto Generate(User user);
}