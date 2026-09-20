import { useEffect, useState } from 'react';
import { apiGet } from '@/lib/api';
import { ApiError } from '@/lib/errors';

/** 后端健康检查返回体，对应 SA.Contracts.Common.HealthDto。 */
interface Health {
  status: string;
  version: string;
  environment: string;
  serverTime: string;
}

type State =
  | { kind: 'loading' }
  | { kind: 'ready'; health: Health }
  | { kind: 'failed'; message: string; traceId: string };

/**
 * 第 0 批骨架页：验证「原型样式已生效」与「前端能经代理打通后端」两件事。
 * 第 1 批会替换为登录页 + 应用外壳。
 */
export function App() {
  const [state, setState] = useState<State>({ kind: 'loading' });

  useEffect(() => {
    let alive = true;
    apiGet<Health>('/api/health')
      .then((health) => {
        if (alive) setState({ kind: 'ready', health });
      })
      .catch((error: unknown) => {
        if (!alive) return;
        const traceId = error instanceof ApiError ? error.traceId : '';
        const message = error instanceof Error ? error.message : '未知错误';
        setState({ kind: 'failed', message, traceId });
      });
    return () => {
      alive = false;
    };
  }, []);

  if (state.kind === 'loading') {
    return <div className="sa-boot">正在连接后端…</div>;
  }

  if (state.kind === 'failed') {
    return (
      <div className="sa-boot">
        <div className="sa-boot-error">
          <h1>后端未连通</h1>
          <p>{state.message}</p>
          <p>
            请确认 API 已启动：<code>dotnet run --project src/SA.Api</code>
          </p>
          {state.traceId ? <p><code>traceId: {state.traceId}</code></p> : null}
        </div>
      </div>
    );
  }

  return (
    <div className="sa-app">
      <div className="sa-brand">
        <div className="sa-logo">SA</div>
        <div className="sa-brand-text">
          <div className="sa-brand-name">股析</div>
          <div className="sa-brand-sub">Stock Analysis</div>
        </div>
      </div>

      <main className="sa-main">
        <div className="sa-page">
          <div className="sa-pagehead">
            <div>
              <h1>骨架就绪</h1>
              <div className="sub">
                后端 {state.health.version} · {state.health.environment} · 服务器时间 {state.health.serverTime}
              </div>
            </div>
          </div>

          <div className="grid grid-3 mt-4">
            <div className="card">
              <div className="card-head"><span className="card-title">原型样式</span></div>
              <div className="card-body">
                <div className="kpi">
                  <span className="kpi-label">令牌与组件</span>
                  <span className="kpi-value">已加载</span>
                  <span className="kpi-delta t-3">tokens / tailwind / components</span>
                </div>
              </div>
            </div>
            <div className="card">
              <div className="card-head"><span className="card-title">涨跌色</span></div>
              <div className="card-body">
                <div className="kpi">
                  <span className="kpi-label">上涨</span>
                  <span className="kpi-value t-up">+2.42%</span>
                </div>
                <div className="kpi">
                  <span className="kpi-label">下跌</span>
                  <span className="kpi-value t-down">-1.86%</span>
                </div>
              </div>
            </div>
            <div className="card">
              <div className="card-head"><span className="card-title">接口连通</span></div>
              <div className="card-body">
                <div className="kpi">
                  <span className="kpi-label">GET /api/health</span>
                  <span className="kpi-value t-up">{state.health.status}</span>
                  <span className="kpi-delta t-3">经 Vite 代理 → localhost:5180</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </main>
    </div>
  );
}
