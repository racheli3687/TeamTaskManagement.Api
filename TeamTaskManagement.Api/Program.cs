// TeamTaskManagement.Api/Program.cs
using System.Text.Json.Serialization;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TeamTaskManagement.Service.Security;
using TeamTaskManagement.Api.Middleware;
using TeamTaskManagement.Api.Security;
using TeamTaskManagement.Core.Options;
using TeamTaskManagement.Core.Services;
using TeamTaskManagement.Data;
using TeamTaskManagement.Service;


var builder = WebApplication.CreateBuilder(args);

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

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// אימות ה-token וטעינת context.User. חייב לרוץ לפני CurrentUserMiddleware.
app.UseAuthentication();
app.UseMiddleware<CurrentUserMiddleware>();

app.UseAuthorization();



app.MapControllers();

app.Run();