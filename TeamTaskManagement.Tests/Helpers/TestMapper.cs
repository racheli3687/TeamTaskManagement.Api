// TeamTaskManagement.Tests/Helpers/TestMapper.cs
using AutoMapper;
using TeamTaskManagement.Service.Mapping;

namespace TeamTaskManagement.Tests.Helpers;

// IMapper אמיתי עם כל ה-Profiles של Service, בדיוק כמו ב-AddServiceLayer.
// הקונפיגורציה נבנית פעם אחת לכל ריצת הבדיקות.
public static class TestMapper
{
    public static readonly MapperConfiguration Configuration =
        new(cfg => cfg.AddMaps(typeof(TeamProfile).Assembly));

    public static IMapper Create() => Configuration.CreateMapper();
}