// TeamTaskManagement.Service/ServiceLayerServiceCollectionExtensions.cs
using Microsoft.Extensions.DependencyInjection;
using TeamTaskManagement.Core.Services;
using TeamTaskManagement.Service.Mapping;
using TeamTaskManagement.Service.Security;
using TeamTaskManagement.Service.Services;

namespace TeamTaskManagement.Service;

public static class ServiceLayerServiceCollectionExtensions
{
    // רישום שכבת ה-Service ב-DI. ICurrentUser נרשם ב-Api (משימה 4.1), כי המימוש שלו תלוי ב-HTTP.
    public static IServiceCollection AddServiceLayer(this IServiceCollection services)
    {
        // סורק את ה-assembly של Service ומוצא את כל ה-Profiles (Team, User, Task, Message)
        services.AddAutoMapper(typeof(TeamProfile).Assembly);

        // חסרי מצב: מופע אחד לכל האפליקציה. JwtTokenGenerator בונה את מפתח החתימה פעם אחת.
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        // תלויים ב-Repositories, ב-IUnitOfWork וב-ICurrentUser (כולם Scoped): מופע לכל בקשה
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IMessageService, MessageService>();

        return services;
    }
}