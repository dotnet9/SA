using SA.Domain.Entities.Market;

namespace SA.Application.Search;

/// <summary>
/// 搜索用的证券索引（内存）。基础信息每天才变一次，而搜索是高频入口，
/// 因此与行情快照分开缓存：命中集合只依赖基础信息，价格等行情从
/// <see cref="Market.MarketSnapshotCache"/> 取，避免两处数据不一致。
/// </summary>
public sealed class SearchIndexCache
{
    private volatile IReadOnlyList<Instrument> _instruments = [];
    private volatile IReadOnlyDictionary<string, Instrument> _byCode =
        new Dictionary<string, Instrument>(StringComparer.Ordinal);

    /// <summary>全部证券基础信息（按代码升序）。</summary>
    public IReadOnlyList<Instrument> Instruments => _instruments;

    /// <summary>代码到基础信息的映射。</summary>
    public IReadOnlyDictionary<string, Instrument> ByCode => _byCode;

    /// <summary>索引是否为空（尚未装载）。</summary>
    public bool IsEmpty => _instruments.Count == 0;

    /// <summary>整体替换索引。</summary>
    public void Replace(IReadOnlyList<Instrument> instruments)
    {
        _byCode = instruments.ToDictionary(i => i.Code, StringComparer.Ordinal);
        _instruments = instruments;
    }
}
