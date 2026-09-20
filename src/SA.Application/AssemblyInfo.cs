using System.Runtime.CompilerServices;

// 应用层的部分算法（分布统计、相关性、贝塔、前向收益等）以 internal 暴露给测试，
// 便于用可手算的样本逐点固化：这些函数算错不会抛异常，只会让数字静默偏移。
[assembly: InternalsVisibleTo("SA.Application.Tests")]
[assembly: InternalsVisibleTo("SA.Api.Tests")]
