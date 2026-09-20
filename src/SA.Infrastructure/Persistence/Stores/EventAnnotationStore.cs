using Microsoft.EntityFrameworkCore;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Domain.Entities.Events;

namespace SA.Infrastructure.Persistence.Stores;

/// <summary>
/// 人工事件标注存储。按（代码, 事件键）upsert，重复标注即更新。
/// </summary>
public sealed class EventAnnotationStore(SaDbContext db) : IEventAnnotationStore
{
    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, EventAnnotation>> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.EventAnnotations
            .AsNoTracking()
            .Where(annotation => annotation.Code == code)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return rows.ToDictionary(row => row.EventKey, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public async Task<int> UpsertAsync(EventAnnotation annotation, CancellationToken cancellationToken = default)
    {
        var row = await _db.EventAnnotations
            .FirstOrDefaultAsync(
                existing => existing.Code == annotation.Code && existing.EventKey == annotation.EventKey,
                cancellationToken).ConfigureAwait(false);

        if (row is null)
        {
            await _db.EventAnnotations.AddAsync(annotation, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            row.Tone = annotation.Tone;
            row.Impact = annotation.Impact;
            row.Note = annotation.Note;
            row.UserId = annotation.UserId;
            row.UpdatedAt = SaTime.Now;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return 1;
    }

    /// <inheritdoc />
    public async Task<int> RemoveAsync(string code, string eventKey, CancellationToken cancellationToken = default)
    {
        var removed = await _db.EventAnnotations
            .Where(annotation => annotation.Code == code && annotation.EventKey == eventKey)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        return removed;
    }
}
