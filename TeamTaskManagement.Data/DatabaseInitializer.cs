// TeamTaskManagement.Data/DatabaseInitializer.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeamTaskManagement.Core.Services;

namespace TeamTaskManagement.Data;

// הכנת בסיס הנתונים בעליית השרת: migrations שטרם הוחלו, ואחר כך נתוני דמו אם הבסיס ריק.
// כך בסיס נתונים חדש (מקומי או ב-Render) מוכן מיד, עם משתמש לכל תפקיד.
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider services,
        string? demoPassword,
        CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseInitializer));
        var context = provider.GetRequiredService<AppDbContext>();

        await context.Database.MigrateAsync(ct);
        logger.LogInformation("Database migrations are up to date.");

        // בלי סיסמת דמו אין Seed: הסיסמה מגיעה רק מההגדרות (User Secrets / משתנה סביבה), לא מהקוד
        if (string.IsNullOrWhiteSpace(demoPassword))
        {
            logger.LogWarning("Seed:DemoPassword is not configured. Demo data was not seeded.");
            return;
        }

        // DataSeeder עצמו בודק אם הבסיס ריק, ולכן בטוח להריץ בכל עלייה
        await DataSeeder.SeedAsync(context, provider.GetRequiredService<IPasswordHasher>(), demoPassword, ct);
    }
}