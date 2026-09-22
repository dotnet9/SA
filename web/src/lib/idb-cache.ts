/**
 * 极简 IndexedDB 缓存。
 *
 * 用途：把接口响应按 URL 缓存到浏览器，刷新页面时**先出缓存再更新**，
 * 断网或后端未启动时也能看到上次的数据（用户要求「数据缓存到客户端」）。
 *
 * 为什么用 IndexedDB 而不是 localStorage：行情与个股响应单条可达几十 KB，
 * 全市场列表与多个个股模块叠加会迅速逼近 localStorage 的 5–10 MB 上限，
 * 而且 localStorage 是同步 API，写入会阻塞主线程。IndexedDB 没有这两个问题。
 *
 * 只做两件事：`get`（过期即视为未命中）与 `put`（带 TTL）。没有索引、没有版本迁移复杂度。
 */

const DbName = 'sa-cache';
const StoreName = 'responses';
const DbVersion = 1;

/** 一条缓存记录。 */
interface CacheRecord {
  key: string;
  value: unknown;
  /** 过期时刻（毫秒时间戳）。 */
  expiresAt: number;
  /** 写入时刻，用于界面显示「数据来自 x 分钟前」。 */
  savedAt: number;
}

/** 缓存命中结果。 */
export interface CacheHit<T> {
  value: T;
  savedAt: number;
}

let dbPromise: Promise<IDBDatabase | null> | null = null;

/** 打开数据库；不可用（隐私模式、旧浏览器）时返回 null，调用方静默跳过缓存。 */
function openDb(): Promise<IDBDatabase | null> {
  if (dbPromise) {
    return dbPromise;
  }

  dbPromise = new Promise<IDBDatabase | null>((resolve) => {
    if (typeof indexedDB === 'undefined') {
      resolve(null);
      return;
    }

    let request: IDBOpenDBRequest;
    try {
      request = indexedDB.open(DbName, DbVersion);
    } catch {
      resolve(null);
      return;
    }

    request.onupgradeneeded = () => {
      const db = request.result;
      if (!db.objectStoreNames.contains(StoreName)) {
        db.createObjectStore(StoreName, { keyPath: 'key' });
      }
    };

    request.onsuccess = () => resolve(request.result);
    request.onerror = () => resolve(null);
    request.onblocked = () => resolve(null);
  });

  return dbPromise;
}

/** 读缓存；未命中或已过期返回 null。 */
export async function cacheGet<T>(key: string): Promise<CacheHit<T> | null> {
  const db = await openDb();
  if (!db) {
    return null;
  }

  return new Promise<CacheHit<T> | null>((resolve) => {
    try {
      const tx = db.transaction(StoreName, 'readonly');
      const request = tx.objectStore(StoreName).get(key);

      request.onsuccess = () => {
        const record = request.result as CacheRecord | undefined;
        if (!record || record.expiresAt < Date.now()) {
          resolve(null);
          return;
        }

        resolve({ value: record.value as T, savedAt: record.savedAt });
      };
      request.onerror = () => resolve(null);
    } catch {
      resolve(null);
    }
  });
}

/** 写缓存。失败（配额、隐私模式）时静默忽略——缓存不该让业务失败。 */
export async function cachePut(key: string, value: unknown, ttlMs: number): Promise<void> {
  const db = await openDb();
  if (!db) {
    return;
  }

  return new Promise<void>((resolve) => {
    try {
      const tx = db.transaction(StoreName, 'readwrite');
      const record: CacheRecord = {
        key,
        value,
        expiresAt: Date.now() + ttlMs,
        savedAt: Date.now()
      };

      tx.objectStore(StoreName).put(record);
      tx.oncomplete = () => resolve();
      tx.onerror = () => resolve();
      tx.onabort = () => resolve();
    } catch {
      resolve();
    }
  });
}

/** 清空缓存（设置页的「清除本地缓存」用）。 */
export async function cacheClear(): Promise<void> {
  const db = await openDb();
  if (!db) {
    return;
  }

  return new Promise<void>((resolve) => {
    try {
      const tx = db.transaction(StoreName, 'readwrite');
      tx.objectStore(StoreName).clear();
      tx.oncomplete = () => resolve();
      tx.onerror = () => resolve();
    } catch {
      resolve();
    }
  });
}

/** 当前缓存条数与占用估算（设置页展示用）。 */
export async function cacheStats(): Promise<{ count: number; bytes: number }> {
  const db = await openDb();
  if (!db) {
    return { count: 0, bytes: 0 };
  }

  return new Promise((resolve) => {
    try {
      const tx = db.transaction(StoreName, 'readonly');
      const request = tx.objectStore(StoreName).getAll();

      request.onsuccess = () => {
        const records = (request.result as CacheRecord[]) ?? [];
        let bytes = 0;
        for (const record of records) {
          try {
            bytes += JSON.stringify(record.value).length;
          } catch {
            // 循环引用不可能出现（都是接口响应），忽略
          }
        }

        resolve({ count: records.length, bytes });
      };
      request.onerror = () => resolve({ count: 0, bytes: 0 });
    } catch {
      resolve({ count: 0, bytes: 0 });
    }
  });
}
