namespace SA.Domain.Authorization;

/// <summary>
/// 一个功能点。编码是授权判定的唯一键，必须与 docs/需求规格.md §7.1 及原型
/// <c>design/web/_shared/data.js</c> 的 <c>functionPoints</c> 完全一致。
/// </summary>
/// <param name="Code">功能点编码，如 <c>stock.trend</c>。</param>
/// <param name="Name">显示名。</param>
/// <param name="Description">用途说明，用于后台权限矩阵的提示文案。</param>
public sealed record FunctionPoint(string Code, string Name, string Description);

/// <summary>
/// 功能点分组（权限矩阵的行分组）。
/// </summary>
/// <param name="Name">分组名，如「模块访问」。</param>
/// <param name="Items">组内功能点。</param>
public sealed record FunctionPointGroup(string Name, IReadOnlyList<FunctionPoint> Items);
