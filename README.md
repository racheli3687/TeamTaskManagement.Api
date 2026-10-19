# ניהול משימות בצוות (Team Task Management API)

Web API ב-.NET 8 לניהול משימות בצוותים, בכמה חברות במקביל (multi-tenant). מנהל חברה נרשם בעצמו, מקים צוותים וממנה ראשי צוות. ראש הצוות מפרסם משימות, וחברי הצוות **תופסים** אותן: כל משימה יכולה להיות בידיים של חבר אחד בלבד. בנוסף יש הודעות פנימיות בתוך החברה.

לקוח React: [TeamTaskManagement.Client](https://github.com/racheli3687/TeamTaskManagement.Client)



## סביבה חיה

- **לקוח:** https://ttm-client.onrender.com
- **API (Swagger):** https://ttm-api-ppdt.onrender.com/swagger

השירות פועל בתוכנית החינמית של Render: אחרי כ-15 דקות ללא פעילות הוא נרדם, והבקשה הראשונה עשויה להימשך עד דקה. ההתחברות היא עם משתמשי הדמו שבטבלה למטה.


## המשאב המוגבל והתחרות עליו

**המשאב המוגבל הוא משימה (`TaskItem`).** חבר צוות תופס משימה פתוחה (`POST /api/tasks/{id}/assignment`), והיא עוברת לסטטוס "בביצוע" על שמו. התחרות נוצרת כששני חברים לוחצים "תפוס" על אותה משימה כמעט באותו רגע: שניהם קוראים אותה כפתוחה, ושניהם מנסים לשמור.

**הטיפול: Optimistic Concurrency.** לישות `TaskItem` יש שדה `Version` (Guid) שמוגדר כ-concurrency token, ומתחלף בכל שמירה של המשימה. EF Core מוסיף את הערך שנקרא לתנאי ה-`WHERE` של ה-`UPDATE`. הבדיקה העסקית (המשימה פתוחה ואין לה מחזיק) והשמירה (שיוך חדש ומעבר ל"בביצוע") מתבצעות ב-`SaveChangesAsync` אחד, בטרנזקציה אחת. הראשון שומר בהצלחה. אצל השני ה-`Version` כבר לא תואם, אף שורה לא מתעדכנת, ו-EF Core זורק `DbUpdateConcurrencyException`. כרשת ביטחון נוספת, אינדקס ייחודי מסונן מונע שני שיוכים פעילים לאותה משימה ברמת בסיס הנתונים.

**התגובה:** שכבת הנתונים מתרגמת את החריגה ל-`ConcurrencyConflictException`, וה-Service מחזיר **409 Conflict** עם הודעה ברורה ("המשימה עודכנה במקביל על ידי משתמש אחר...") ו-`correlationId`. ההתנגשות נרשמת בלוג ברמת Warning. הלקוח מציג חלון ייעודי ומרענן את הרשימה. אותו מנגנון מגן גם על שחרור, עדכון סטטוס, עריכה ומחיקה של משימה.

**ההוכחה:** הבדיקה `TaskConcurrencyTests` פותחת שני `DbContext` נפרדים (SQLite in-memory, חיבור נפרד לכל אחד). שניהם קוראים את אותה משימה ושניהם משנים אותה. הראשון שומר, והשני מקבל `ConcurrencyConflictException`.

## משתמשי דמו

נוצרים אוטומטית בעליית השרת כשבסיס הנתונים ריק (`DataSeeder`), בחברה "Demo Company".

| תפקיד | שם משתמש | סיסמה | צוות |
|---|---|---|---|
| מנהל חברה | `manager` | `<סיסמת הדמו>` | – |
| ראש צוות | `lead_dev` | `<סיסמת הדמו>` | Development |
| חבר צוות | `dev1` | `<סיסמת הדמו>` | Development (מחזיק במשימה "Fix login bug") |
| חבר צוות | `dev2` | `<סיסמת הדמו>` | Development |
| ראש צוות | `lead_mkt` | `<סיסמת הדמו>` | Marketing |
| חבר צוות | `mkt1` | `<סיסמת הדמו>` | Marketing |

## תפקידים והרשאות

| תפקיד | יכולות עיקריות |
|---|---|
| מנהל חברה (Manager) | ניהול צוותים, מינוי ראש צוות, צירוף והסרה של חברים, צפייה בכל המשימות בחברה |
| ראש צוות (TeamLead) | יצירה, עריכה ומחיקה של משימות בצוות שלו, שחרור, פתיחה מחדש, צירוף והסרה של חברים |
| חבר צוות (Member) | תפיסה, סיום ושחרור של משימה שלו, צפייה במשימות הצוות |

ההרשאות נבדקות בשכבת ה-Service. התפקיד נטען מבסיס הנתונים בכל בקשה (`CurrentUserMiddleware`), כך שמינוי או הסרה חלים מיד, גם עם token ישן. משאב של חברה אחרת מחזיר 404, כדי לא לחשוף שהוא קיים.

## ארכיטקטורה

```
TeamTaskManagement.Api      Controllers, Middleware, JWT, Swagger, NLog
TeamTaskManagement.Service  לוגיקה עסקית, הרשאות, AutoMapper, PasswordHasher, JwtTokenGenerator
TeamTaskManagement.Data     AppDbContext, Configurations (Fluent API), Repositories, UnitOfWork, Migrations, Seed
TeamTaskManagement.Core     ישויות, DTOs, ממשקים, ServiceResult. ללא תלות בחבילות חיצוניות
TeamTaskManagement.Tests    xUnit, Moq, SQLite in-memory
```

תלויות: Api ← Service ← Data, וכל השכבות ← Core. ה-Service משתמש רק בממשקים שב-Core, וה-Controllers לא ניגשים ל-DbContext.

**עיקרי המימוש:**
- REST עם קודי סטטוס מדויקים, ו-ProblemDetails אחיד עם `correlationId` לכל שגיאה.
- ולידציה ב-Data Annotations.
- Pagination, סינון, חיפוש ומיון בתוך השאילתה (`Skip`/`Take`). `AsNoTracking` בקריאה. `Include`/`ThenInclude` בלי N+1.
- קשר many-to-many בין משימות למשתמשים דרך `TaskAssignment`, כולל היסטוריית שיוכים.
- שרשרת אסינכרונית מלאה עם `CancellationToken`, מה-Controller ועד בסיס הנתונים.
- Middleware: `CorrelationId` (כותרת `X-Correlation-Id`), לוג לכל בקשה, וטיפול גלובלי בשגיאות (500 בלי חשיפת פרטים).
- JWT (HS256), סיסמאות ב-PBKDF2, CORS ללקוח.
- NLog: קונסולה וקובץ יומי. Warning לכל התנגשות, Error לכל חריגה. בלי סיסמאות או tokens בלוג.

## הרצה מקומית

### דרישות
- .NET 8 SDK
- Docker Desktop (עבור PostgreSQL 16)

### 1. בסיס הנתונים

```powershell
docker run --name my-postgres -e POSTGRES_PASSWORD=<סיסמת-postgres> -p 5432:5432 -d postgres:16
```

בהרצות הבאות: `docker start my-postgres`.

### 2. הגדרות סודיות (User Secrets)

אין סודות בקוד או ב-repository. מתיקיית ה-solution:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=TeamTaskManagement;Username=postgres;Password=<סיסמת-postgres>" --project TeamTaskManagement.Api
dotnet user-secrets set "Jwt:Key" "<מפתח אקראי באורך 32 תווים לפחות>" --project TeamTaskManagement.Api
dotnet user-secrets set "Seed:DemoPassword" "<סיסמת הדמו>" --project TeamTaskManagement.Api
```

### 3. הרצה

```powershell
dotnet run --project TeamTaskManagement.Api --launch-profile http
```

- בעלייה מוחלות ה-migrations, ונוצרים נתוני הדמו אם הבסיס ריק.
- Swagger זמין ב-`http://localhost:5102/swagger`. ה-token מתקבל מ-`POST /api/auth/login` ומודבק בכפתור Authorize, בלי המילה Bearer.
- הלקוח מצפה לשרת בכתובת הזו, ומותר ב-CORS מ-`http://localhost:5173` (`appsettings.Development.json`).

### 4. בדיקות

```powershell
dotnet test
```

40 בדיקות:
- `ClaimAsync`: מקרה תקין, תפיסה שנדחית, והתנגשות שמחזירה Conflict.
- הרשאות והפרדת חברות, כולל כללי מי כותב למי בהודעות.
- בדיקות תחרות אמיתיות על SQLite.
- `AssertConfigurationIsValid` של AutoMapper.

## מפת ה-API

| משאב | Endpoints |
|---|---|
| Auth | `POST /api/auth/register`, `POST /api/auth/login` |
| Users | `GET/PUT /api/users/me`, `PUT /api/users/me/password` |
| Teams | `GET/POST /api/teams`, `GET/PUT/DELETE /api/teams/{id}`, `PUT /api/teams/{id}/lead`, `GET/POST /api/teams/{id}/members`, `DELETE /api/teams/{id}/members/{userId}` |
| Tasks | `GET/POST /api/teams/{teamId}/tasks`, `GET /api/users/{memberId}/tasks`, `GET/PUT/DELETE /api/tasks/{id}`, `PATCH /api/tasks/{id}/status`, `POST/DELETE /api/tasks/{id}/assignment` |
| Messages | `GET/POST /api/messages`, `GET /api/messages/unread-count`, `PUT /api/messages/{id}/read`, `DELETE /api/messages/{id}` |

התיעוד המלא, כולל קודי תשובה, מופיע ב-Swagger.