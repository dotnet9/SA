using System.Runtime.CompilerServices;

// 采集适配器的「上游字段 → 领域字段」映射用 internal 解析方法承载，
// 由测试程序集直接调用固定样本进行固化验证（详细设计 §13.2：字段映射正确、上游漂移可被发现）。
[assembly: InternalsVisibleTo("SA.Api.Tests")]
[assembly: InternalsVisibleTo("SA.Application.Tests")]
[assembly: InternalsVisibleTo("SA.Domain.Tests")]
