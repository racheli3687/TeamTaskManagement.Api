// TeamTaskManagement.Api/Program.cs
using System.Text.Json.Serialization;
using System.Text;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NLog;
using NLog.Web;
using TeamTaskManagement.Service.Security;
using TeamTaskManagement.Api.Middleware;
using TeamTaskManagement.Api.Security;
using TeamTaskManagement.Api.Swagger;
using TeamTaskManagement.Core.Options;
using TeamTaskManagement.Core.Services;
using TeamTaskManagement.Data;
using TeamTaskManagement.Service;


// NLog נטען לפני בניית ה-host, כדי שגם שגיאה בעלייה (הגדרה חסרה, ולידציה שנכשלה) תיכתב ללוג.
// LoadConfigurationFromAppSettings: אין סעיף NLog ב-appsettings, ולכן נטען nlog.config מתיקיית הריצה.
var startupLogger = LogManager.Setup()
    .LoadConfigurationFromAppSettings()
    .GetCurrentClassLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // NLog הוא ספק הלוג היחיד. הסינון לפי רמות מוגדר ב-nlog.config (ולא ב-appsettings).
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    // Render (ופלטפורמות דומות) קובעת את הפורט במשתנה PORT. מקומית המשתנה לא קיים, ונשאר launchSettings.
    var port = Environment.GetEnvironmentVariable("PORT");
    if (!string.IsNullOrEmpty(port))
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

    // מאחורי ה-proxy של Render: הסכמה (https) וכתובת הלקוח מגיעות בכותרות X-Forwarded-*.
    // כתובת ה-proxy אינה ידועה מראש, והאפליקציה נגישה רק דרכו, ולכן הרשימות המוכרות מתרוקנות.
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });

    // Add services to the container.
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

    builder.Services.AddDataServices(connectionString);
    builder.Services.AddServiceLayer();

    builder.Services.AddOptions<JwtOptions>()
        .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();


    // אימות JWT. ההגדרות נלקחות מ-JwtOptions שעבר ולידציה (אותו מופע שמשמש את JwtTokenGenerator).
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer();

    builder.Services
        .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
        .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
        {
            var jwt = jwtOptions.Value;

            // שמות ה-claims נשארים כפי שנכתבו ב-token (sub, name, role)
            bearer.MapInboundClaims = false;

            bearer.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,

                ValidateAudience = true,
                ValidAudience = jwt.Audience,

                ValidateLifetime = true,
                RequireExpirationTime = true,
                // אותו שרת מנפיק ובודק, ולכן אין צורך בסבולת של 5 דקות (ברירת המחדל)
                ClockSkew = TimeSpan.FromSeconds(30),

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                // רק האלגוריתם שבו JwtTokenGenerator חותם
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },

                NameClaimType = JwtTokenGenerator.UserNameClaim,
                RoleClaimType = JwtTokenGenerator.RoleClaim
            };
        });


    // המשתמש המחובר: מופע אחד לכל בקשה. ה-Middleware ממלא את CurrentUser,
    // וה-Services מקבלים את אותו מופע דרך הממשק ICurrentUser.
    builder.Services.AddScoped<CurrentUser>();
    builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());


    // enums נכתבים ונקראים ב-JSON כשמות ("InProgress") ולא כמספרים.
    // allowIntegerValues: false: מספר בגוף הבקשה מחזיר 400, כך שלכל ערך יש ייצוג אחד בלבד.
    // ההגדרה לא חלה על פרמטרים בכתובת (route/query). אותם בודק [EnumDataType] ב-DTO.
    builder.Services
        .AddControllers()
        .AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter(allowIntegerValues: false)));

    // CORS ללקוח React (repository נפרד, origin אחר). הכתובות מההגדרות:
    // פיתוח: appsettings.Development.json. Render: משתני סביבה Cors__AllowedOrigins__0 וכו'.
    // X-Correlation-Id נחשף, כדי שהלקוח יוכל להציג את המזהה בהודעת שגיאה.
    const string ClientCorsPolicy = "ReactClient";
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

    builder.Services.AddCors(options =>
        options.AddPolicy(ClientCorsPolicy, policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders(CorrelationIdMiddleware.HeaderName)));

    // ProblemDetails אחיד בכל המערכת: Controllers, ולידציה, CurrentUserMiddleware, 500,
    // ותשובות 401/403/404 של ה-framework (דרך UseStatusCodePages).
    // correlationId בכל גוף שגיאה: הלקוח יכול לצטט אותו, ובלוג מוצאים את הבקשה לפיו.
    builder.Services.AddProblemDetails(options =>
        options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier);

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Team Task Management API",
            Version = "v1",
            Description = "ניהול משימות בצוות: חברות, צוותים, משימות עם תפיסה מבוקרת (Version), והודעות פנימיות."
        });

        // כפתור Authorize: מדביקים את ה-token בלבד, בלי המילה Bearer
        options.AddSecurityDefinition(AuthorizeOperationFilter.SchemeId, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Name = "Authorization",
            Description = "ה-token מתקבל מ-POST /api/auth/login. יש להדביק את ה-token בלבד."
        });

        // מנעול רק על endpoints שדורשים token
        options.OperationFilter<AuthorizeOperationFilter>();

        // הערות ה-/// מה-Controllers. includeControllerXmlComments: התיאור של כל Controller מוצג בכותרת הקבוצה
        var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    });

    var app = builder.Build();

    // migrations ו-Seed לפני קבלת בקשות. שגיאה כאן (למשל בסיס נתונים לא זמין) נרשמת כ-Fatal ב-catch שלמטה.
    await DatabaseInitializer.InitializeAsync(
        app.Services,
        builder.Configuration["Seed:DemoPassword"],
        app.Lifetime.ApplicationStopping);

    // סדר ה-pipeline (5.3, 6.2):
   
    // 0. ראשון: כל שאר ה-pipeline (כולל HttpsRedirection והלוגים) רואה את הסכמה וה-IP המקוריים
    app.UseForwardedHeaders();
    // 1. CorrelationId ראשון: כל לוג ותשובה, גם תשובת שגיאה, נושאים את המזהה
    app.UseMiddleware<CorrelationIdMiddleware>();
    // 2. לוג לכל בקשה. לפני טיפול השגיאות, כדי לרשום את הסטטוס הסופי (גם 500 ו-499)
    app.UseMiddleware<RequestLoggingMiddleware>();
    // 3. תופס כל חריגה מכל השלבים שאחריו
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    // 4. תשובת 4xx/5xx בלי גוף (401/403 של ה-framework, 404 לנתיב לא קיים) מקבלת ProblemDetails
    app.UseStatusCodePages();

    // 5. CORS לפני האימות. הכותרות נכתבות בתחילת התשובה, ולכן נשמרות גם בתשובות שגיאה.
    app.UseCors(ClientCorsPolicy);
    // Configure the HTTP request pipeline.
    // Swagger גם ב-Production: הבודק יכול לנסות את ה-API ישירות (המנעול מסמן endpoints מוגנים)
    app.UseSwagger();
    app.UseSwaggerUI();


    app.UseHttpsRedirection();

    // אימות ה-token וטעינת context.User. חייב לרוץ לפני CurrentUserMiddleware.
    app.UseAuthentication();
    app.UseMiddleware<CurrentUserMiddleware>();

    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
// HostAbortedException: נזרקת בכוונה כשכלי EF (dotnet ef migrations) עוצרים את ה-host. אינה שגיאה.
catch (Exception ex) when (ex is not HostAbortedException)
{
    // שגיאה בעליית השרת (למשל הגדרה חסרה). נרשמת ללוג ונזרקת הלאה, כך שהתהליך מסתיים עם שגיאה.
    startupLogger.Fatal(ex, "Application failed to start.");
    throw;
}
finally
{
    // כתיבת כל הלוגים שבתור לפני סיום התהליך
    LogManager.Shutdown();
}