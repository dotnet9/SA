/**
 * 本地存储。键名沿用原型的 <c>sa.</c> 前缀，便于从原型迁移习惯与状态。
 */

const prefix = 'sa.';

/** 读取字符串值。 */
export function readString(key: string, fallback: string): string {
  try {
    const value = localStorage.getItem(prefix + key);
    return value === null ? fallback : value;
  } catch {
    return fallback;
  }
}

/** 写入字符串值。 */
export function writeString(key: string, value: string): void {
  try {
    localStorage.setItem(prefix + key, value);
  } catch {
    // 隐私模式下 localStorage 可能不可写，忽略即可，不影响功能
  }
}

/** 读取布尔值。 */
export function readBool(key: string, fallback: boolean): boolean {
  const value = readString(key, fallback ? 'true' : 'false');
  return value === 'true';
}

/** 写入布尔值。 */
export function writeBool(key: string, value: boolean): void {
  writeString(key, value ? 'true' : 'false');
}

/** 读取 JSON 值。解析失败返回 fallback。 */
export function readJson<T>(key: string, fallback: T): T {
  const raw = readString(key, '');
  if (!raw) return fallback;

  try {
    return JSON.parse(raw) as T;
  } catch {
    return fallback;
  }
}

/** 写入 JSON 值。 */
export function writeJson(key: string, value: unknown): void {
  try {
    writeString(key, JSON.stringify(value));
  } catch {
    // 序列化失败（循环引用等）不应影响界面
  }
}

/** 存储键。 */
export const StorageKeys = {
  Theme: 'theme',
  UpDown: 'updown',
  Density: 'density',
  CardOrder: 'cardorder.',
  SettingsCache: 'settings',
  /** 数字字体：mono（等宽）或 ui（跟随界面字体）。 */
  NumFont: 'numfont',
  /** 默认首页路径。 */
  HomePath: 'home',
  /** 推送到达时高亮数字。 */
  FlashOnPush: 'flash',
  /** 顶栏常驻显示行情时间戳。 */
  ShowMarketTime: 'markettime',
  /** 均线周期（逗号分隔）。 */
  MaPeriods: 'ma',
  /** 复权方式：qfq / hfq / none。 */
  Adjust: 'adjust',
  /** MACD 参数（逗号分隔的三段）。 */
  MacdParams: 'macd',
  /** 主力资金口径。 */
  FundFlowCaliber: 'fundflow',
  /** 通知渠道开关（JSON）。 */
  NotifyChannels: 'notifychannels',
  /** 免打扰时段（JSON）。 */
  DoNotDisturb: 'dnd'
} as const;
