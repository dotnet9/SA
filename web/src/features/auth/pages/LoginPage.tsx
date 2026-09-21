import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate, useSearchParams } from 'react-router';
import { landingPath } from '@/app/nav';
import { ApiError } from '@/lib/errors';
import { useAuth } from '@/providers/AuthProvider';
import { useTheme } from '@/providers/ThemeProvider';
import { useToast } from '@/providers/ToastProvider';

/**
 * 取出 `?next=` 并只接受站内路径。
 *
 * 导航里点「需登录」的菜单会带上目标地址，登录后直接回到那一项，而不是被丢到落地页。
 * 必须挡掉 `//evil.com` 与绝对地址：那会变成开放重定向（登录页被人拿去当跳板）。
 */
function safeNextPath(raw: string | null): string | null {
  if (!raw || !raw.startsWith('/') || raw.startsWith('//')) {
    return null;
  }

  return raw;
}

/**
 * 登录页。版式与文案沿用原型 `design/web/index.html` 的左右分栏（hero + panel）。
 *
 * 与原型唯一的差异：原型用「选择演示角色」直接进入，正式版按真实账号登录，
 * 角色由服务端下发，因此角色卡片换成了用户名 / 密码表单（并在服务端强制
 * 二次验证时多一个验证码输入框）。
 */
export function LoginPage() {
  const { status, me, signIn } = useAuth();
  const { toggleTheme, toggleUpdown } = useTheme();
  const { toast } = useToast();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const nextPath = safeNextPath(searchParams.get('next'));

  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [totpCode, setTotpCode] = useState('');
  const [rememberMe, setRememberMe] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  // 已登录（含刷新页面后自动恢复）时直接进目标页；带 ?next= 时优先回到原目标
  useEffect(() => {
    if (status === 'authenticated' && me) {
      navigate(nextPath ?? landingPath(me.functionPoints), { replace: true });
    }
  }, [status, me, navigate, nextPath]);

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      const signedIn = await signIn(username, password, { totpCode: totpCode || undefined, rememberMe });
      toast(`已登录：${signedIn.nickname}（${signedIn.roleName}）`, 'ok');
      navigate(nextPath ?? landingPath(signedIn.functionPoints), { replace: true });
    } catch (caught) {
      const message = caught instanceof ApiError ? caught.message : '登录失败，请稍后重试';
      setError(message);
    } finally {
      setSubmitting(false);
    }
  }

  const needTotpHint = error?.includes('二次验证');

  return (
    <div className="login-stage">
      <section className="login-hero">
        <div className="row gap-3">
          <div className="sa-logo" style={{ width: 36, height: 36, fontSize: 14 }}>
            SA
          </div>
          <div>
            <div className="sa-brand-name" style={{ fontSize: 18 }}>
              股析
            </div>
            <div className="sa-brand-sub">Stock Analysis</div>
          </div>
        </div>

        <div>
          <h1 className="hero-title">
            把一家公司<em>拆成一张矩阵</em>，
            <br />
            一眼看清趋势。
          </h1>
          <p className="hero-sub">
            输入股票名称或代码，聚合公开披露资料，形成 8 个维度的分析矩阵：
            趋势与价格结构、盈利与财务、投资与股权、资金与筹码、行业与同业、
            事件与影响、风险与舆情、机构评级与预测。四种拓扑图把「谁影响谁」画出来。
          </p>
          <div className="hero-matrix">
            <div className="hm">
              <div className="hm-k">覆盖市场</div>
              <div className="hm-v">A 股全市场</div>
            </div>
            <div className="hm">
              <div className="hm-k">分析模块</div>
              <div className="hm-v">8 个</div>
            </div>
            <div className="hm">
              <div className="hm-k">拓扑图</div>
              <div className="hm-v">4 种</div>
            </div>
            <div className="hm">
              <div className="hm-k">自选股推送</div>
              <div className="hm-v">3 秒</div>
            </div>
          </div>
        </div>

        <div className="hero-foot">
          数据来源：公开接口（东方财富 / 腾讯 / 新浪 / 交易所公开数据 / 巨潮资讯）
          <br />
          仅使用公开数据，不做数据再分发，不构成投资建议。
          <br />
          账号由管理员创建，不开放注册。
        </div>
      </section>

      <section className="login-panel">
        <div className="login-box">
          <h2 style={{ fontSize: 22, fontWeight: 700 }}>登录</h2>
          <p className="hint mt-2">使用管理员分配的账号登录</p>
          <p className="hint mt-1">
            行情与个股资料<b>无需登录</b>即可查看；自选、条件选股、提醒与后台需登录。
          </p>

          <form className="col gap-4" style={{ marginTop: 20 }} onSubmit={onSubmit}>
            <div className="field">
              <label className="label" htmlFor="login-user">
                用户名
              </label>
              <input
                id="login-user"
                className="input is-mono"
                autoComplete="username"
                value={username}
                onChange={(event) => setUsername(event.target.value)}
                required
              />
            </div>

            <div className="field">
              <label className="label" htmlFor="login-pass">
                密码
              </label>
              <input
                id="login-pass"
                className="input is-mono"
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                required
              />
            </div>

            {needTotpHint ? (
              <div className="field">
                <label className="label" htmlFor="login-totp">
                  二次验证码
                </label>
                <input
                  id="login-totp"
                  className="input is-mono"
                  inputMode="numeric"
                  maxLength={6}
                  value={totpCode}
                  onChange={(event) => setTotpCode(event.target.value)}
                />
              </div>
            ) : null}

            <div className="row-between">
              <label className="check">
                <input
                  type="checkbox"
                  checked={rememberMe}
                  onChange={(event) => setRememberMe(event.target.checked)}
                />
                记住我
              </label>
              <span className="hint">忘记密码请联系管理员</span>
            </div>

            {error ? <div className="field-error">{error}</div> : null}

            <button className="btn btn-primary btn-lg btn-block" type="submit" disabled={submitting}>
              {submitting ? '登录中…' : '登录'}
            </button>
          </form>

          <div className="row gap-2 center mt-6">
            <button type="button" className="btn btn-ghost btn-sm" onClick={toggleTheme}>
              切换主题
            </button>
            <button type="button" className="btn btn-ghost btn-sm" onClick={toggleUpdown}>
              切换涨跌色
            </button>
          </div>

          <div className="hint mt-4" style={{ lineHeight: 1.9 }}>
            首次启动会创建管理员账号 <code>admin</code>：初始密码取配置
            <code> Sa:Auth:AdminInitialPassword</code>，未配置时随机生成并打印在服务端启动日志中；
            首次登录后需立即改密。
          </div>
        </div>
      </section>
    </div>
  );
}
