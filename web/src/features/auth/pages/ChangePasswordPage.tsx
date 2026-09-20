import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router';
import { ApiError } from '@/lib/errors';
import { changePassword } from '@/lib/session';
import { useAuth } from '@/providers/AuthProvider';
import { useToast } from '@/providers/ToastProvider';

/**
 * 修改密码。两种入口共用：
 * 1. 首登强制改密（`me.mustChangePwd`，路由守卫会把人锁在这一页）；
 * 2. 主动进入（`/settings` 里的入口，第 11 批接上）。
 *
 * 服务端改密成功后会吊销该账号全部会话，因此这里必须回到登录页重新登录。
 */
export function ChangePasswordPage() {
  const { me, reload, signOut } = useAuth();
  const { toast } = useToast();
  const navigate = useNavigate();

  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [repeat, setRepeat] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const forced = me?.mustChangePwd === true;

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);

    if (next !== repeat) {
      setError('两次输入的新密码不一致');
      return;
    }

    setSubmitting(true);
    try {
      await changePassword(current, next);
      toast('密码已修改，请用新密码重新登录', 'ok');
      await signOut();
      navigate('/login', { replace: true });
    } catch (caught) {
      const message = caught instanceof ApiError ? caught.message : '修改失败，请稍后重试';
      setError(message);
      setSubmitting(false);
    }
  }

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
            {forced ? (
              <>
                首次登录
                <em>请先修改密码</em>
              </>
            ) : (
              <>
                修改
                <em>登录密码</em>
              </>
            )}
          </h1>
          <p className="hero-sub">
            密码要求：至少 10 位，同时包含大写字母、小写字母、数字与符号，
            且不能与最近 5 次使用过的密码相同。修改成功后该账号的全部登录会话将被吊销。
          </p>
        </div>

        <div className="hero-foot">
          账号与密码策略由管理员在「登录与安全」中配置
          <br />
          密码使用 PBKDF2-HMAC-SHA256 加盐存储，服务端不保存明文
        </div>
      </section>

      <section className="login-panel">
        <div className="login-box">
          <h2 style={{ fontSize: 22, fontWeight: 700 }}>修改密码</h2>
          <p className="hint mt-2">
            当前账号：{me?.username}（{me?.roleName}）
          </p>

          <form className="col gap-4" style={{ marginTop: 20 }} onSubmit={onSubmit}>
            <div className="field">
              <label className="label" htmlFor="cp-current">
                当前密码
              </label>
              <input
                id="cp-current"
                className="input is-mono"
                type="password"
                autoComplete="current-password"
                value={current}
                onChange={(event) => setCurrent(event.target.value)}
                required
              />
            </div>

            <div className="field">
              <label className="label" htmlFor="cp-new">
                新密码
              </label>
              <input
                id="cp-new"
                className="input is-mono"
                type="password"
                autoComplete="new-password"
                value={next}
                onChange={(event) => setNext(event.target.value)}
                required
              />
            </div>

            <div className="field">
              <label className="label" htmlFor="cp-repeat">
                重复新密码
              </label>
              <input
                id="cp-repeat"
                className="input is-mono"
                type="password"
                autoComplete="new-password"
                value={repeat}
                onChange={(event) => setRepeat(event.target.value)}
                required
              />
            </div>

            {error ? <div className="field-error">{error}</div> : null}

            <button className="btn btn-primary btn-lg btn-block" type="submit" disabled={submitting}>
              {submitting ? '提交中…' : '确认修改'}
            </button>

            {!forced ? (
              <button
                type="button"
                className="btn btn-ghost btn-block"
                onClick={() => {
                  void reload().then(() => navigate(-1));
                }}
              >
                返回
              </button>
            ) : null}
          </form>
        </div>
      </section>
    </div>
  );
}
