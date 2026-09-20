namespace SA.Contracts.Common;

/// <summary>
/// 健康检查返回体。
/// </summary>
public sealed record HealthDto(string Status, string Version, string Environment, string ServerTime);
