// TeamTaskManagement.Service/Mapping/TeamProfile.cs
using AutoMapper;
using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.DTOs.Teams;
using TeamTaskManagement.Core.Entities;

namespace TeamTaskManagement.Service.Mapping;

public class TeamProfile : Profile
{
    public TeamProfile()
    {
        // תשובה: ה-Repository מחזיר צוות יחד עם מספר החברים (COUNT בשאילתה אחת)
        CreateMap<TeamWithMembersCount, TeamResponseDto>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Team.Id))
            .ForMember(d => d.Name, o => o.MapFrom(s => s.Team.Name))
            .ForMember(d => d.MembersCount, o => o.MapFrom(s => s.MembersCount));

        // יצירה: רק Name מגיע מהלקוח. OrganizationId נקבע ב-Service מתוך ICurrentUser.
        CreateMap<CreateTeamRequestDto, Team>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.OrganizationId, o => o.Ignore())
            .ForMember(d => d.Organization, o => o.Ignore())
            .ForMember(d => d.Users, o => o.Ignore())
            .ForMember(d => d.Tasks, o => o.Ignore());

        // שינוי שם: ממופה לתוך ישות קיימת במעקב - mapper.Map(request, team)
        CreateMap<UpdateTeamRequestDto, Team>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.OrganizationId, o => o.Ignore())
            .ForMember(d => d.Organization, o => o.Ignore())
            .ForMember(d => d.Users, o => o.Ignore())
            .ForMember(d => d.Tasks, o => o.Ignore());
    }
}