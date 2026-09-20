import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { EmptyState, ErrorState } from '@/components/ui/States';
import { errorText } from '@/lib/errorText';
import { useToast } from '@/providers/ToastProvider';
import {
  createUser,
  deleteUser,
  fetchPermissionMatrix,
  fetchUsers,
  resetPassword,
  revokeUserSessions,
  updateRoleFunctionPoints,
  updateRoleQuotas,
  updateUser
} from './api';

/**
 * 后台：用户管理与角色权限（同一页两个标签）。
 *
 * 结构与 `design/web/admin-users.html` / `admin-permissions.html` 对应：
 * 用户列表与增删改 → 权限矩阵（功能点 × 角色）→ 配额 → 口径说明。
 *
 * 三条安全边界由服务端强制，界面也同步体现（不给会失败的按钮）：
 * 不能停用自己、不能删除最后一个管理员、管理员角色必须保留全部功能点。
 */
export function AdminUsersPage() {
  const [tab, setTab] = useState<'users' | 'roles'>('users');

  return (
    <>
      <div className="sa-pagehead">
        <div>
          <div className="breadcrumb">
            <Link to="/market">市场概览</Link>
            <span className="sep">/</span>
            <span>用户与权限</span>
          </div>
          <h1>用户与权限</h1>
          <div className="sub">账号由管理员创建，不开放注册；功能点与配额的改动立即生效</div>
        </div>
      </div>

      <div className="row gap-2 wrap">
        <span className="segmented">
          <span className={tab === 'users' ? 'is-active' : undefined} onClick={() => setTab('users')}>
            用户管理
          </span>
          <span className={tab === 'roles' ? 'is-active' : undefined} onClick={() => setTab('roles')}>
            角色与权限
          </span>
        </span>
      </div>

      {tab === 'users' ? <UsersPanel /> : <RolesPanel />}
    </>
  );
}

/* ------------------------------------------------------------------
   用户管理
   ------------------------------------------------------------------ */

function UsersPanel() {
  const queryClient = useQueryClient();
  const toast = useToast();

  const [keyword, setKeyword] = useState('');
  const [newUsername, setNewUsername] = useState('');
  const [newRoleId, setNewRoleId] = useState('');

  const { data, error, isPending, refetch, failureCount } = useQuery({
    queryKey: ['admin', 'users', keyword],
    queryFn: () => fetchUsers(keyword || undefined)
  });

  const { data: matrix } = useQuery({
    queryKey: ['admin', 'permissions'],
    queryFn: fetchPermissionMatrix
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['admin', 'users'] });

  const createMutation = useMutation({
    mutationFn: () => createUser(newUsername.trim(), newUsername.trim(), newRoleId || matrix?.roles[0]?.id || ''),
    onSuccess: async (password) => {
      // 初始密码只返回一次，因此必须显式提示管理员保存
      toast.toast(`用户已创建，初始密码：${password}（仅显示一次，请立即转交并提示改密）`, 'ok');
      setNewUsername('');
      await invalidate();
    },
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  const resetMutation = useMutation({
    mutationFn: (userId: string) => resetPassword(userId),
    onSuccess: (password) => toast.toast(`密码已重置为：${password}（仅显示一次，已撤销该用户全部会话）`, 'ok'),
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  const toggleMutation = useMutation({
    mutationFn: ({ userId, status }: { userId: string; status: string }) => updateUser(userId, { status }),
    onSuccess: async () => {
      toast.toast('状态已更新', 'ok');
      await invalidate();
    },
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  const roleMutation = useMutation({
    mutationFn: ({ userId, roleId }: { userId: string; roleId: string }) => updateUser(userId, { roleId }),
    onSuccess: async () => {
      toast.toast('角色已更新（下次请求即生效）', 'ok');
      await invalidate();
    },
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  const deleteMutation = useMutation({
    mutationFn: (userId: string) => deleteUser(userId),
    onSuccess: async () => {
      toast.toast('用户已删除', 'ok');
      await invalidate();
    },
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  const revokeMutation = useMutation({
    mutationFn: (userId: string) => revokeUserSessions(userId),
    onSuccess: async (count) => {
      toast.toast(`已强制下线（撤销 ${count} 个会话）`, 'ok');
      await queryClient.invalidateQueries({ queryKey: ['admin', 'sessions'] });
    },
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  if (isPending) {
    return <div className="sa-boot">正在载入用户…</div>;
  }

  if (!data) {
    return (
      <div className="card mt-3">
        <div className="card-body">
          <ErrorState error={error} onRetry={() => void refetch()} retryCount={failureCount} />
        </div>
      </div>
    );
  }

  return (
    <>
      <div className="grid grid-4 mt-3">
        <StatCard label="用户总数" value={String(data.total)} />
        <StatCard label="启用" value={String(data.activeCount)} />
        <StatCard label="停用" value={String(data.disabledCount)} />
        <StatCard label="锁定" value={String(data.lockedCount)} tone={data.lockedCount > 0 ? 'down' : undefined} />
      </div>

      {/* 新建用户 */}
      <div className="card mt-4">
        <div className="card-head">
          <span className="card-title">新建用户</span>
          <span className="card-sub">初始密码由系统生成并只显示一次</span>
        </div>
        <div className="card-body row gap-3 wrap" style={{ alignItems: 'flex-end' }}>
          <label className="col gap-1">
            <span className="label" style={{ width: 'auto' }}>
              用户名
            </span>
            <input
              className="input input-sm"
              style={{ width: 160 }}
              placeholder="至少 3 个字符"
              value={newUsername}
              onChange={(event) => setNewUsername(event.target.value)}
            />
          </label>

          <label className="col gap-1">
            <span className="label" style={{ width: 'auto' }}>
              角色
            </span>
            <select
              className="input input-sm"
              style={{ width: 180 }}
              value={newRoleId || matrix?.roles[0]?.id || ''}
              onChange={(event) => setNewRoleId(event.target.value)}
            >
              {(matrix?.roles ?? []).map((role) => (
                <option key={role.id} value={role.id}>
                  {role.name}
                </option>
              ))}
            </select>
          </label>

          <button
            type="button"
            className="btn btn-sm btn-primary"
            disabled={createMutation.isPending || newUsername.trim().length < 3}
            onClick={() => createMutation.mutate()}
          >
            创建用户
          </button>
        </div>
      </div>

      {/* 用户列表 */}
      <div className="card mt-4">
        <div className="card-head">
          <span className="card-title">用户列表</span>
          <span className="card-sub">共 {data.items.length} 条</span>
          <div className="card-tools">
            <input
              className="input input-sm"
              style={{ width: 160 }}
              placeholder="搜索用户名 / 昵称"
              value={keyword}
              onChange={(event) => setKeyword(event.target.value)}
            />
          </div>
        </div>
        <div className="card-body is-flush">
          {data.items.length === 0 ? (
            <EmptyState title="没有匹配的用户" hint="换个关键词，或在上方创建新用户。" />
          ) : (
            <div className="tbl-wrap">
              <table className="tbl is-comfort">
                <thead>
                  <tr>
                    <th>用户名</th>
                    <th>昵称</th>
                    <th>角色</th>
                    <th>状态</th>
                    <th>二次验证</th>
                    <th>最近登录</th>
                    <th className="col-actions" style={{ width: 260 }}>
                      操作
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((user) => (
                    <tr key={user.id}>
                      <td className="mono fs-12">{user.username}</td>
                      <td className="fs-12">
                        {user.nickname}
                        {user.mustChangePwd ? <span className="tag tag-warn" style={{ marginLeft: 6 }}>待改密</span> : null}
                      </td>
                      <td>
                        <select
                          className="input input-sm"
                          style={{ width: 150 }}
                          value={user.roleId}
                          onChange={(event) => roleMutation.mutate({ userId: user.id, roleId: event.target.value })}
                        >
                          {(matrix?.roles ?? []).map((role) => (
                            <option key={role.id} value={role.id}>
                              {role.name}
                            </option>
                          ))}
                        </select>
                      </td>
                      <td>
                        <span className={`tag ${user.status === 'active' ? 'tag-up' : 'tag-outline'}`}>
                          {user.status === 'active' ? '启用' : user.status === 'locked' ? '锁定' : '停用'}
                        </span>
                      </td>
                      <td className="fs-11 t-2">{user.hasTotp ? '已绑定' : '未绑定'}</td>
                      <td className="fs-11 t-3">{user.lastLoginAt ?? '从未登录'}</td>
                      <td className="col-actions">
                        <span className="row gap-2">
                          <button
                            type="button"
                            className="btn btn-sm btn-outline"
                            disabled={toggleMutation.isPending}
                            onClick={() =>
                              toggleMutation.mutate({
                                userId: user.id,
                                status: user.status === 'active' ? 'disabled' : 'active'
                              })
                            }
                          >
                            {user.status === 'active' ? '停用' : '启用'}
                          </button>
                          <button
                            type="button"
                            className="btn btn-sm btn-ghost"
                            disabled={resetMutation.isPending}
                            onClick={() => resetMutation.mutate(user.id)}
                          >
                            重置密码
                          </button>
                          <button
                            type="button"
                            className="btn btn-sm btn-ghost"
                            disabled={revokeMutation.isPending}
                            onClick={() => revokeMutation.mutate(user.id)}
                          >
                            强制下线
                          </button>
                          <button
                            type="button"
                            className="btn btn-sm btn-ghost"
                            disabled={deleteMutation.isPending}
                            onClick={() => deleteMutation.mutate(user.id)}
                          >
                            删除
                          </button>
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      <div className="legend-block mt-4">
        <b>数据来源与口径</b>
        <br />
        新建用户的初始密码与重置后的密码都只显示一次（服务端不保存明文）；重置密码会同时撤销该用户全部会话。
        <br />
        角色变更在用户的下一次请求即生效（权限缓存在改动后立即失效）。
        <br />
        不能停用当前登录的账号；不能删除最后一个管理员账号；管理员角色必须保留全部功能点。
        <br />
        所有管理动作都会写入审计日志（用户、动作、目标、变更摘要），可在「登录与安全」页查看。
      </div>
    </>
  );
}

/* ------------------------------------------------------------------
   角色与权限
   ------------------------------------------------------------------ */

function RolesPanel() {
  const queryClient = useQueryClient();
  const toast = useToast();

  const { data, error, isPending, refetch, failureCount } = useQuery({
    queryKey: ['admin', 'permissions'],
    queryFn: fetchPermissionMatrix
  });

  const pointsMutation = useMutation({
    mutationFn: ({ roleId, codes }: { roleId: string; codes: string[] }) => updateRoleFunctionPoints(roleId, codes),
    onSuccess: async () => {
      toast.toast('功能点已更新（立即生效）', 'ok');
      await queryClient.invalidateQueries({ queryKey: ['admin', 'permissions'] });
    },
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  const quotaMutation = useMutation({
    mutationFn: ({ roleId, quotas }: { roleId: string; quotas: Record<string, number> }) => updateRoleQuotas(roleId, quotas),
    onSuccess: async () => {
      toast.toast('配额已更新', 'ok');
      await queryClient.invalidateQueries({ queryKey: ['admin', 'permissions'] });
    },
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  // 按分组组织功能点：矩阵按分组展示，阅读顺序与设计文档一致
  const grouped = useMemo(() => {
    const map = new Map<string, { code: string; name: string }[]>();
    for (const point of data?.functionPoints ?? []) {
      const list = map.get(point.group) ?? [];
      list.push({ code: point.code, name: point.name });
      map.set(point.group, list);
    }

    return [...map.entries()];
  }, [data?.functionPoints]);

  if (isPending) {
    return <div className="sa-boot">正在载入权限矩阵…</div>;
  }

  if (!data) {
    return (
      <div className="card mt-3">
        <div className="card-body">
          <ErrorState error={error} onRetry={() => void refetch()} retryCount={failureCount} />
        </div>
      </div>
    );
  }

  return (
    <>
      {/* 配额 */}
      <div className="card mt-3">
        <div className="card-head">
          <span className="card-title">角色配额</span>
          <span className="card-sub">操作级限制，超出时接口返回 3002</span>
        </div>
        <div className="card-body is-flush">
          <div className="tbl-wrap">
            <table className="tbl">
              <thead>
                <tr>
                  <th>角色</th>
                  {data.quotaKeys.map((key) => (
                    <th key={key} className="num">
                      {key}
                    </th>
                  ))}
                  <th className="num">用户数</th>
                </tr>
              </thead>
              <tbody>
                {data.roles.map((role) => (
                  <tr key={role.id}>
                    <td>
                      <span className="fs-12">{role.name}</span>
                      {role.isBuiltin ? <span className="tag tag-outline" style={{ marginLeft: 6 }}>内置</span> : null}
                    </td>
                    {data.quotaKeys.map((key) => (
                      <td key={key} className="num">
                        <input
                          className="input input-sm"
                          style={{ width: 90 }}
                          type="number"
                          min={0}
                          defaultValue={role.quotas[key] ?? 0}
                          onBlur={(event) => {
                            const value = Number(event.target.value);
                            if (value !== (role.quotas[key] ?? 0)) {
                              quotaMutation.mutate({ roleId: role.id, quotas: { [key]: value } });
                            }
                          }}
                        />
                      </td>
                    ))}
                    <td className="num">{role.userCount}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>

      {/* 功能点矩阵 */}
      <div className="card mt-4">
        <div className="card-head">
          <span className="card-title">功能点矩阵</span>
          <span className="card-sub">勾选后立即生效；管理员角色必须保留全部功能点</span>
        </div>
        <div className="card-body is-flush">
          <div className="tbl-wrap">
            <table className="tbl is-comfort">
              <thead>
                <tr>
                  <th>功能点</th>
                  {data.roles.map((role) => (
                    <th key={role.id} className="num">
                      {role.name}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {grouped.map(([group, points]) => (
                  <>
                    <tr key={group}>
                      <td colSpan={data.roles.length + 1} className="fs-11 t-3" style={{ paddingTop: 12 }}>
                        {group}
                      </td>
                    </tr>
                    {points.map((point) => (
                      <tr key={point.code}>
                        <td>
                          <span className="fs-12">{point.name}</span>
                          <span className="mono fs-11 t-3" style={{ marginLeft: 6 }}>
                            {point.code}
                          </span>
                        </td>
                        {data.roles.map((role) => {
                          const checked = role.functionPoints.includes(point.code);
                          return (
                            <td key={role.id} className="num">
                              <input
                                type="checkbox"
                                checked={checked}
                                disabled={pointsMutation.isPending}
                                onChange={() => {
                                  // 全量覆盖：勾选即加入、取消即移除
                                  const next = checked
                                    ? role.functionPoints.filter((code) => code !== point.code)
                                    : [...role.functionPoints, point.code];
                                  pointsMutation.mutate({ roleId: role.id, codes: next });
                                }}
                              />
                            </td>
                          );
                        })}
                      </tr>
                    ))}
                  </>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <div className="legend-block mt-4">
        <b>数据来源与口径</b>
        <br />
        功能点目录是权限体系的唯一来源（与需求规格 §7.1 的 28 项一致），矩阵由它直接生成，因此不会漏项。
        <br />
        数据范围由 <code>{data.dataScopes.map((scope) => scope.code).join(' / ')}</code> 控制：
        {data.dataScopes.map((scope) => ` ${scope.name}=${scope.description}；`).join('')}
        <br />
        改动会立即使该角色的权限缓存失效，用户的下一次请求即按新权限判定（无需重新登录）。
        <br />
        配额为 0 表示该操作对该角色完全关闭（例如 <code>export.daily=0</code> 即不允许导出）。
      </div>
    </>
  );
}

function StatCard({ label, value, tone }: { label: string; value: string; tone?: string }) {
  return (
    <div className="card">
      <div className="card-body is-tight">
        <div className="kpi">
          <span className="kpi-label">{label}</span>
          <span className={`kpi-value is-sm ${tone === 'down' ? 't-down' : ''}`}>{value}</span>
        </div>
      </div>
    </div>
  );
}
