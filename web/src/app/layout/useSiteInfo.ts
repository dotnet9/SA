import { useQuery } from '@tanstack/react-query';
import { apiGet } from '@/lib/api';

/** 站点信息（页头名称与全局公告）。 */
export interface SiteInfo {
  name: string;
  /** 全局公告；为空表示不显示横幅。 */
  notice: string | null;
}

/**
 * 取站点信息。
 *
 * 全站共用一份缓存（5 分钟），因为页头每个页面都要渲染它；
 * 失败时静默回落到默认名称，不让页头因为一个装饰性接口而报错。
 */
export function useSiteInfo(): SiteInfo {
  const { data } = useQuery({
    queryKey: ['system', 'site'],
    queryFn: () => apiGet<SiteInfo>('/api/system/site'),
    staleTime: 5 * 60_000,
    retry: false
  });

  return { name: data?.name ?? '股析', notice: data?.notice ?? null };
}
