using SA.Domain.Entities.Events;

namespace SA.Application.Abstractions;

/// <summary>
/// 人工事件标注的读写。
/// </summary>
public interface IEventAnnotationStore
{
    /// <summary>取某标的的全部标注（按事件键索引）。</summary>
    Task<IReadOnlyDictionary<string, EventAnnotation>> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>写入或覆盖一条标注。</summary>
    Task<int> UpsertAsync(EventAnnotation annotation, CancellationToken cancellationToken = default);

    /// <summary>删除一条标注（恢复为派生判读）。</summary>
    Task<int> RemoveAsync(string code, string eventKey, CancellationToken cancellationToken = default);
}
