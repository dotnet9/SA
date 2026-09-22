import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { area, useChart } from '@/components/charts';
import { ErrorState } from '@/components/ui/States';
import {
  fetchValueResearch,
  statusText,
  type ValueResearch,
  type ValueResearchGroup,
  type ValueResearchItem
} from '@/features/research/api';

/**
 * 个股「价值研究」页（实施计划 §6.1）。
 *
 * 结构严格遵循 §1.4 简洁优先：
 *   结论区（一行客观事实 + 关键数字，**不给判断**）
 *   七组检查清单（每行：指标名 | 当前值 | 历史对比 | 数据状态）
 *   默认只展开第 1、2 组，其余折叠
 *
 * 唯一允许的图表是「周期位置」组的趋势线：只有它需要历史对照才能回答
 * 「这是周期高点还是起点」。其余各组的数字单点即可判断。
 */
export function ValueResearchPage() {
  const { code = '' } = useParams();
  const { data, error, isPending, refetch, failureCount } = useQuery({
    queryKey: ['stock', code, 'value-research'],
    queryFn: () => fetchValueResearch(code)
  });

  if (isPending) {
    return <div className="sa-boot">正在载入 {code} 的价值研究…</div>;
  }

  if (!data) {
    return (
      <>
        <Head code={code} name={code} />
        <div className="card">
          <div className="card-body">
            <ErrorState error={error} onRetry={() => void refetch()} retryCount={failureCount} />
          </div>
        </div>
      </>
    );
  }

  return <Content data={data} />;
}

function Head({ code, name, data }: { code: string; name: string; data?: ValueResearch }) {
  return (
    <div className="sa-pagehead">
      <div>
        <div className="breadcrumb">
          <Link to="/search">股票搜索</Link>
          <span className="sep">/</span>
          <span>价值研究</span>
        </div>
        <h1>
          {name}
          <span className="mono fs-14 t-3" style={{ marginLeft: 8 }}>
            {code}
          </span>
        </h1>
        <div className="sub">
          {data?.industry ?? '行业待采集'}
          {data?.board ? ` · ${data.board}` : ''}
          {data?.fundamentalAsOf ? ` · 基本面口径 ${data.fundamentalAsOf}` : ''}
        </div>
      </div>
    </div>
  );
}

function Content({ data }: { data: ValueResearch }) {
  return (
    <>
      <Head code={data.code} name={data.name} data={data} />

      {/* 结论区：只陈述客观数值，不给买入/卖出/持有判断，不给评分 */}
      <div className="card is-accent">
        <div className="card-head">
          <span className="card-title">结论</span>
          <span className="card-sub">
            {data.conclusion.reportDate ? `口径 ${data.conclusion.reportDate}` : '暂无财报数据'} · 只列事实，不含判断
          </span>
        </div>
        <div className="card-body is-tight">
          <div className="fs-14" style={{ lineHeight: 1.7 }}>
            {data.conclusion.summary}
          </div>
          {data.conclusion.metrics.length > 0 ? (
            <div className="row gap-4 wrap mt-3">
              {data.conclusion.metrics.map((metric) => (
                <div key={metric.name}>
                  <div className="fs-11 t-3">{metric.name}</div>
                  <div className="mono fs-14 fw-600">{metric.value}</div>
                </div>
              ))}
            </div>
          ) : null}
        </div>
      </div>

      {/* 七组检查清单 */}
      {data.groups.map((group) => (
        <GroupCard key={group.key} group={group} />
      ))}
    </>
  );
}

/** 一组检查清单。默认展开由后端下发（第 1、2 组展开）。 */
function GroupCard({ group }: { group: ValueResearchGroup }) {
  const [open, setOpen] = useState(group.defaultExpanded);

  return (
    <div className="card mt-4">
      <div
        className="card-head"
        style={{ cursor: 'pointer' }}
        onClick={() => setOpen((previous) => !previous)}
      >
        <span className="card-title">
          {open ? '▾' : '▸'} {group.name}
        </span>
        <span className="card-sub">{group.question}</span>
      </div>
      {open ? (
        <div className="card-body is-flush">
          <div className="tbl-wrap">
            <table className="tbl">
              <thead>
                <tr>
                  <th>指标</th>
                  <th>当前值</th>
                  <th className="hide-mobile">历史对比</th>
                  <th>数据状态</th>
                  <th className="hide-mobile">来源</th>
                </tr>
              </thead>
              <tbody>
                {group.items.map((item) => (
                  <tr key={item.key}>
                    <td>{item.name}</td>
                    <td className="mono">
                      {item.value ?? '—'}
                      {item.value && item.unit ? <span className="t-3 fs-11"> {item.unit}</span> : null}
                    </td>
                    <td className="hide-mobile">
                      <HistoryCell item={item} />
                    </td>
                    <td className="fs-11">
                      <StatusTag item={item} />
                    </td>
                    <td className="fs-11 t-3 hide-mobile">{item.source ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ) : null}
    </div>
  );
}

/** 历史对比列：有序列时画趋势线（唯一允许的图表），否则给出口径日期。 */
function HistoryCell({ item }: { item: ValueResearchItem }) {
  if (item.history && item.history.length > 1) {
    return <HistoryChart item={item} />;
  }

  return <span className="fs-11 t-3">{item.asOf ? `口径 ${item.asOf}` : '—'}</span>;
}

function HistoryChart({ item }: { item: ValueResearchItem }) {
  const history = item.history ?? [];
  const labels = history.map((point) => point.period.slice(0, 4));
  const values = history.map((point) => point.value ?? 0);

  const { ref } = useChart(area, {
    labels,
    data: values,
    unit: item.unit ? ` ${item.unit}` : ''
  });

  return <div ref={ref} style={{ width: 220, height: 48 }} />;
}

/**
 * 数据状态标签。
 *
 * 四态必须区分：把「不适用」显示成「暂无数据」会误导用户去等一个永远不会来的数据；
 * 「暂无待解禁」是「确实没有」而不是「没查到」。
 */
function StatusTag({ item }: { item: ValueResearchItem }) {
  const text = statusText(item.status);

  if (text === null) {
    return <span className="t-3">—</span>;
  }

  // 四态用不同色调：ok 不显示（有值即正常），其余按语义着色
  const tone =
    item.status === 'notApplicable'
      ? 'tag-outline'
      : item.status === 'noUpcoming'
        ? 'tag-outline'
        : 'tag-outline';

  return <span className={`tag ${tone}`}>{text}</span>;
}
