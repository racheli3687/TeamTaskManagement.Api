using System.ComponentModel.DataAnnotations;

namespace TeamTaskManagement.Core.Options;

// ההגדרות נקראות מהסעיף "Jwt" בקונפיגורציה.
// Issuer, Audience ו-ExpiryMinutes ב-appsettings.json. Key (סוד) ב-User Secrets / משתנה סביבה.
public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = default!;

    [Required]
    public string Audience { get; set; } = default!;

    // HS256 דורש לפחות 256 ביט, כלומר 32 תווים
    [Required, MinLength(32)]
    public string Key { get; set; } = default!;

    [Range(1, 1440)]
    public int ExpiryMinutes { get; set; } = 60;
}