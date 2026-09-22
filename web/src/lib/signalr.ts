/**
 * 极简 SignalR 客户端（JSON 协议 + WebSocket 传输）。
 *
 * 为什么不用官方的 `@microsoft/signalr`：本机 npm 11 与 node 25 组合下任何需要访问注册表的
 * 命令都直接失败（`npm view` 同样报 `Class extends value undefined is not a constructor`），
 * 因此无法把官方客户端装进依赖树。这里按 SignalR 的公开协议实现本项目实际用到的子集：
 * 握手、调用（invoke/send）、服务端回调、心跳忽略、断线重连。
 *
 * 覆盖范围有限，刻意如此：
 * - 只支持 JSON 协议与 WebSocket 传输（跳过协商，直连 `/hubs/*`）；
 * - 只支持一个中心（本项目也只有 `/hubs/quotes`）；
 * - 不支持流式调用与二进制（本项目的推送都是对象数组）。
 *
 * 协议参考：SignalR 的 JSON 帧以 `\u001e` 分隔，消息形如
 * `{ type: 1 | 3 | 6 | 7, target, invocationId, arguments, result }`。
 */

/** 连接状态。 */
export type SignalRStatus = 'idle' | 'connecting' | 'connected' | 'reconnecting' | 'closed';

/** 服务端主动调用的处理函数。 */
export type SignalRHandler = (args: unknown[]) => void;

/** 构造参数。 */
export interface SignalROptions {
  /** 中心地址，如 `/hubs/quotes`。 */
  hubUrl: string;
  /**
   * 取访问令牌。
   *
   * 本应用没有登录，因此通常不传；保留这个口子是因为 SignalR 在需要鉴权的部署下
   * 仍要把令牌放进查询串（浏览器 WebSocket 不能带自定义头）。
   */
  accessToken?: () => string | null;
  /** 状态变化回调。 */
  onStatus?: (status: SignalRStatus) => void;
  /** 连接成功（含重连成功）后回调，用于重新订阅。 */
  onConnected?: () => void;
  /** 日志（调试用）。 */
  onLog?: (message: string) => void;
}

/** 帧类型。 */
const FrameType = {
  /** 服务端调用客户端方法。 */
  Invocation: 1,
  /** 调用完成（响应 invoke）。 */
  Completion: 3,
  /** 心跳。 */
  Ping: 6,
  /** 关闭。 */
  Close: 7
} as const;

/** 记录分隔符（SignalR 文本协议）。 */
const RecordSeparator = '\u001e';

/**
 * 连接实例。
 */
export class SignalRConnection {
  private readonly options: SignalROptions;
  private readonly handlers = new Map<string, SignalRHandler>();
  private readonly pending = new Map<string, { resolve: (value: unknown) => void; reject: (error: Error) => void }>();

  private socket: WebSocket | null = null;
  private status: SignalRStatus = 'idle';
  private attempt = 0;
  private reconnectTimer: number | null = null;
  private invocationSeq = 0;
  private stopped = false;

  constructor(options: SignalROptions) {
    this.options = options;
  }

  /** 当前状态。 */
  get currentStatus(): SignalRStatus {
    return this.status;
  }

  /** 注册服务端方法处理器。 */
  on(target: string, handler: SignalRHandler): void {
    this.handlers.set(target, handler);
  }

  /** 建立连接。可重复调用（已连接时忽略）。 */
  start(): void {
    if (this.status === 'connected' || this.status === 'connecting') {
      return;
    }

    this.stopped = false;
    this.open();
  }

  /** 主动断开，不再重连。 */
  stop(): void {
    this.stopped = true;
    this.clearReconnect();
    this.socket?.close();
    this.socket = null;
    this.setStatus('closed');
  }

  /**
   * 调用服务端方法并等待结果。
   */
  invoke<T = unknown>(target: string, ...args: unknown[]): Promise<T> {
    const socket = this.socket;
    if (!socket || socket.readyState !== WebSocket.OPEN) {
      return Promise.reject(new Error('SignalR 连接未就绪'));
    }

    const invocationId = String(++this.invocationSeq);
    return new Promise<T>((resolve, reject) => {
      this.pending.set(invocationId, { resolve: resolve as (value: unknown) => void, reject });
      socket.send(
        JSON.stringify({ type: FrameType.Invocation, invocationId, target, arguments: args }) + RecordSeparator
      );
    });
  }

  /** 发送不关心结果的消息。 */
  send(target: string, ...args: unknown[]): void {
    const socket = this.socket;
    if (!socket || socket.readyState !== WebSocket.OPEN) {
      return;
    }

    socket.send(JSON.stringify({ type: FrameType.Invocation, target, arguments: args }) + RecordSeparator);
  }

  /* ------------------------------------------------------------------ */

  private open(): void {
    this.setStatus(this.attempt === 0 ? 'connecting' : 'reconnecting');

    const token = this.options.accessToken?.() ?? null;
    const scheme = window.location.protocol === 'https:' ? 'wss' : 'ws';
    const query = token ? `?access_token=${encodeURIComponent(token)}` : '';
    const url = `${scheme}://${window.location.host}${this.options.hubUrl}${query}`;

    let socket: WebSocket;
    try {
      socket = new WebSocket(url);
    } catch (error) {
      this.log(`WebSocket 创建失败：${String(error)}`);
      this.scheduleReconnect();
      return;
    }

    this.socket = socket;
    socket.onopen = () => {
      // 握手：声明协议与版本，服务端回一个空对象表示接受
      socket.send(JSON.stringify({ protocol: 'json', version: 1 }) + RecordSeparator);
    };

    socket.onmessage = (event) => this.handleFrame(String(event.data));

    socket.onerror = () => {
      this.log('WebSocket 出错');
    };

    socket.onclose = (event) => {
      this.socket = null;

      // 未解决的调用一律失败，否则调用方会永久挂起
      for (const [, entry] of this.pending) {
        entry.reject(new Error('连接已关闭'));
      }

      this.pending.clear();

      if (this.stopped) {
        this.setStatus('closed');
        return;
      }

      this.log(`连接关闭（code=${event.code}），准备重连`);
      this.scheduleReconnect();
    };
  }

  private handleFrame(raw: string): void {
    for (const record of raw.split(RecordSeparator)) {
      if (record.length === 0) {
        continue;
      }

      let frame: {
        type: number;
        target?: string;
        invocationId?: string;
        arguments?: unknown[];
        result?: unknown;
        error?: string;
      };

      try {
        frame = JSON.parse(record);
      } catch {
        this.log(`无法解析的帧：${record.slice(0, 120)}`);
        continue;
      }

      switch (frame.type) {
        case FrameType.Invocation:
          if (frame.target) {
            this.handlers.get(frame.target)?.(frame.arguments ?? []);
          }
          break;

        case FrameType.Completion: {
          const id = frame.invocationId;
          if (!id) {
            break;
          }

          const entry = this.pending.get(id);
          this.pending.delete(id);

          if (!entry) {
            break;
          }

          if (frame.error) {
            entry.reject(new Error(frame.error));
          } else {
            entry.resolve(frame.result);
          }
          break;
        }

        case FrameType.Ping:
          // 心跳：服务端保活，不需要回应
          break;

        case FrameType.Close:
          this.socket?.close();
          break;

        default:
          // 握手响应是 `{}`（无 type），到这里说明是已知之外的控制帧，忽略即可
          if (frame.type === undefined) {
            this.attempt = 0;
            this.setStatus('connected');
            this.options.onConnected?.();
          }
          break;
      }
    }
  }

  private scheduleReconnect(): void {
    if (this.stopped || this.reconnectTimer !== null) {
      return;
    }

    // 指数退避 + 抖动，上限 10 秒：网络恢复后能自动接回，也不会形成重试风暴
    const base = Math.min(10_000, 500 * 2 ** this.attempt);
    const delay = base + Math.random() * 300;
    this.attempt++;

    this.setStatus('reconnecting');
    this.reconnectTimer = window.setTimeout(() => {
      this.reconnectTimer = null;
      this.open();
    }, delay);
  }

  private clearReconnect(): void {
    if (this.reconnectTimer !== null) {
      window.clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
  }

  private setStatus(status: SignalRStatus): void {
    if (this.status === status) {
      return;
    }

    this.status = status;
    this.options.onStatus?.(status);
  }

  private log(message: string): void {
    this.options.onLog?.(message);
  }
}
