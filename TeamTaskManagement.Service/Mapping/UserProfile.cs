// TeamTaskManagement.Service/Mapping/UserProfile.cs
using AutoMapper;
using TeamTaskManagement.Core.DTOs.Auth;
using TeamTaskManagement.Core.DTOs.Users;
using TeamTaskManagement.Core.Entities;

namespace TeamTaskManagement.Service.Mapping;

public class UserProfile : Profile
{
    public UserProfile()
    {
        // כל השדות ב-DTO זהים בשמם לישות, ולכן אין צורך ב-ForMember.
        // PasswordHash לא קיים ב-DTO, ולכן לעולם לא יוצא החוצה.
        CreateMap<User, UserResponseDto>();

        // החלק של המשתמש בתשובת ההתחברות.
        // Token ו-ExpiresAt נקבעים ב-JwtTokenGenerator אחרי המיפוי.
        CreateMap<User, AuthResponseDto>()
            .ForMember(d => d.UserId, o => o.MapFrom(s => s.Id))
            .ForMember(d => d.Token, o => o.Ignore())
            .ForMember(d => d.ExpiresAt, o => o.Ignore());
    }
}