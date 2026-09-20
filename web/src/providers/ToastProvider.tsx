import { createContext, useCallback, useContext, useMemo, useRef, useState, type ReactNode } from 'react';

/** 提示类型，对应原型 `.toast.is-<kind>` 的三个变体。 */
export type ToastKind = 'info' | 'ok' | 'warn' | 'error';

interface ToastItem {
  id: number;
  message: string;
  kind: ToastKind;
}

interface ToastContextValue {
  /** 弹出一条提示。 */
  toast: (message: string, kind?: ToastKind) => void;
}

const ToastContext = createContext<ToastContextValue | null>(null);

/**
 * 全局提示。使用原型的 `.toast-wrap` / `.toast.is-*` 类名，视觉与原型的 Toast 一致。
 */
export function ToastProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<ToastItem[]>([]);
  const nextId = useRef(1);

  const toast = useCallback((message: string, kind: ToastKind = 'info') => {
    const id = nextId.current++;
    setItems((current) => [...current, { id, message, kind }]);

    // 与原型一致：2600ms 后淡出并移除
    window.setTimeout(() => {
      setItems((current) => current.filter((item) => item.id !== id));
    }, 2600);
  }, []);

  const value = useMemo(() => ({ toast }), [toast]);

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div className="toast-wrap">
        {items.map((item) => (
          <div key={item.id} className={`toast is-${item.kind}`}>
            {item.message}
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

/** 取 Toast 能力。必须在 ToastProvider 内使用。 */
export function useToast(): ToastContextValue {
  const value = useContext(ToastContext);
  if (!value) {
    throw new Error('useToast 必须在 ToastProvider 内使用');
  }

  return value;
}
