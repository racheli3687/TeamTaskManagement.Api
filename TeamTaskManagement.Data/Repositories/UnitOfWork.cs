using Microsoft.EntityFrameworkCore;
using Npgsql;
using TeamTaskManagement.Core.Exceptions;
using TeamTaskManagement.Core.Repositories;

namespace TeamTaskManagement.Data.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // חייב להופיע לפני DbUpdateException, כי הוא יורש ממנה.
            // ה-Version של השורה השתנה בין הקריאה לשמירה: מישהו אחר הקדים אותנו.
            throw new ConcurrencyConflictException(
                "The row was modified by another user between read and save.", ex);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_TaskAssignments_TaskItemId"
            })
        {
            // רשת הביטחון: נסיון ליצור שיוך פעיל שני לאותה משימה
            throw new ConcurrencyConflictException(
                "The task already has an active assignment.", ex);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            } pg)
        {
            // כל Unique Index אחר (שם משתמש, שם צוות בחברה): ערך כפול, בדרך כלל בגלל בקשה מקבילה
            throw new UniqueConstraintException(
                "A unique value already exists.", pg.ConstraintName, ex);
        }
    }
}