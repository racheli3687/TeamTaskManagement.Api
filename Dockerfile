# שלב 1: build עם ה-SDK
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# קודם רק קובצי הפרויקטים: ה-restore נשמר ב-cache כל עוד התלויות לא השתנו
COPY Directory.Build.props ./
COPY TeamTaskManagement.Core/TeamTaskManagement.Core.csproj TeamTaskManagement.Core/
COPY TeamTaskManagement.Data/TeamTaskManagement.Data.csproj TeamTaskManagement.Data/
COPY TeamTaskManagement.Service/TeamTaskManagement.Service.csproj TeamTaskManagement.Service/
COPY TeamTaskManagement.Api/TeamTaskManagement.Api.csproj TeamTaskManagement.Api/
RUN dotnet restore TeamTaskManagement.Api/TeamTaskManagement.Api.csproj

COPY . .
RUN dotnet publish TeamTaskManagement.Api/TeamTaskManagement.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# שלב 2: הרצה עם ה-runtime בלבד
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# משתמש לא-root (מובנה בתמונה). תיקיית logs עבור יעד הקובץ של NLog.
RUN mkdir -p /app/logs && chown "$APP_UID" /app/logs
USER $APP_UID

ENTRYPOINT ["dotnet", "TeamTaskManagement.Api.dll"]