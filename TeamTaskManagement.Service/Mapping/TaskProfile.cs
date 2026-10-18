// TeamTaskManagement.Service/Mapping/TaskProfile.cs
using AutoMapper;
using TeamTaskManagement.Core.DTOs.TaskItems;
using TeamTaskManagement.Core.Entities;

namespace TeamTaskManagement.Service.Mapping;

public class TaskProfile : Profile
{
    public TaskProfile()
    {
        // שיוך פעיל = UnassignedAt == null. ה-Repository טוען אותו יחד עם המשתמש (Include/ThenInclude).
        // אם אין שיוך פעיל - שני השדות null, והמשימה פנויה.
        CreateMap<TaskItem, TaskResponseDto>()
            .ForMember(d => d.AssignedUserId, o => o.MapFrom(s =>
                s.Assignments
                    .Where(a => a.UnassignedAt == null)
                    .Select(a => (int?)a.UserId)
                    .FirstOrDefault()))
            .ForMember(d => d.AssignedUserName, o => o.MapFrom(s =>
                s.Assignments
                    .Where(a => a.UnassignedAt == null)
                    .Select(a => a.User.UserName)
                    .FirstOrDefault()));
    }
}