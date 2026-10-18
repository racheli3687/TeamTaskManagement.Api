// TeamTaskManagement.Service/Mapping/MessageProfile.cs
using AutoMapper;
using TeamTaskManagement.Core.DTOs.Messages;
using TeamTaskManagement.Core.Entities;

namespace TeamTaskManagement.Service.Mapping;

public class MessageProfile : Profile
{
    public MessageProfile()
    {
        // Sender ו-Recipient נטענים ב-Repository (Include).
        // השם בישות הוא UserName ולא Name, ולכן שיטוח אוטומטי לא היה מוצא אותו.
        CreateMap<Message, MessageResponseDto>()
            .ForMember(d => d.SenderName, o => o.MapFrom(s => s.Sender.UserName))
            .ForMember(d => d.RecipientName, o => o.MapFrom(s => s.Recipient.UserName));
    }
}