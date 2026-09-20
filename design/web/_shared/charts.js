/* ============================================================
   SA · 股析 — 图表层（ECharts 6 主题与图表工厂）
   所有图表读取 CSS 令牌，主题/涨跌色切换后自动重建。
   ============================================================ */
(function () {
  "use strict";

  var D = window.SA_DATA;
  var registry = [];

  function cv(name) {
    return getComputedStyle(document.documentElement).getPropertyValue(name).trim() || "#888";
  }
  function qs(s, r) { return (r || document).querySelector(s); }
  function qsa(s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); }

  function palette() {
    return [cv("--chart-1"), cv("--chart-2"), cv("--chart-3"), cv("--chart-4"), cv("--chart-5"), cv("--chart-6"), cv("--chart-7"), cv("--chart-8")];
  }
  function upColor() { return cv("--up"); }
  function downColor() { return cv("--down"); }
  function flatColor() { return cv("--flat"); }

  /* ---------- 通用片段 ---------- */
  function tooltip(extra) {
    var base = {
      backgroundColor: cv("--chart-tooltip-bg"),
      borderColor: cv("--border"),
      borderWidth: 1,
      padding: [8, 12],
      textStyle: { color: cv("--text-1"), fontSize: 12, fontFamily: "inherit" },
      extraCssText: "backdrop-filter:blur(12px);border-radius:10px;box-shadow:0 12px 40px rgba(0,0,0,.45);"
    };
    return Object.assign(base, extra || {});
  }
  function axisLine() { return { lineStyle: { color: cv("--chart-axis") } }; }
  function axisLabel(extra) {
    return Object.assign({ color: cv("--chart-label"), fontSize: 11 }, extra || {});
  }
  function splitLine(show) {
    return { show: show !== false, lineStyle: { color: cv("--chart-split"), type: "solid" } };
  }
  function grid(extra) {
    return Object.assign({ left: 8, right: 12, top: 28, bottom: 6, containLabel: true }, extra || {});
  }
  function legend(extra) {
    return Object.assign({
      top: 0, right: 4, itemWidth: 9, itemHeight: 9, itemGap: 12,
      textStyle: { color: cv("--chart-label"), fontSize: 11 },
      icon: "roundRect"
    }, extra || {});
  }
  function titleText(text, sub) {
    return {
      text: text, subtext: sub || "", left: 0, top: 0,
      textStyle: { color: cv("--text-1"), fontSize: 13, fontWeight: 600 },
      subtextStyle: { color: cv("--text-3"), fontSize: 11 }
    };
  }
  function axisPointer() {
    return {
      type: "cross",
      crossStyle: { color: cv("--text-3"), type: "dashed" },
      lineStyle: { color: cv("--text-3"), type: "dashed" },
      label: { backgroundColor: cv("--bg-elevated"), borderColor: cv("--border"), borderWidth: 1, color: cv("--text-1"), fontSize: 11 }
    };
  }

  /* ---------- 注册与重建 ---------- */
  var HELPERS = function () {
    return { cv: cv, D: D, palette: palette, up: upColor, down: downColor, flat: flatColor, tooltip: tooltip, grid: grid, legend: legend, axisLine: axisLine, axisLabel: axisLabel, splitLine: splitLine, axisPointer: axisPointer, titleText: titleText };
  };

  /* 同一容器重复绘制时，先释放该容器上的旧实例：
     否则 ECharts 会在控制台告警，且 registry 会无限增长导致主题切换时重复重建 */
  function release(dom) {
    for (var i = registry.length - 1; i >= 0; i--) {
      if (registry[i].dom === dom) {
        try { registry[i].inst.dispose(); } catch (e) { /* 忽略 */ }
        registry.splice(i, 1);
      }
    }
    if (window.echarts.getInstanceByDom && window.echarts.getInstanceByDom(dom)) {
      try { window.echarts.dispose(dom); } catch (e) { /* 忽略 */ }
    }
  }

  function make(el, build) {
    var dom = typeof el === "string" ? qs(el) : el;
    if (!dom || !window.echarts) return null;
    release(dom);
    var inst = window.echarts.init(dom, null, { renderer: "canvas" });
    var opt = build(HELPERS());
    if (opt) inst.setOption(opt);
    registry.push({ dom: dom, inst: inst, build: build });
    return inst;
  }

  function rebuildAll() {
    registry.forEach(function (r) {
      try { r.inst.dispose(); } catch (e) { /* 忽略 */ }
      var inst = window.echarts.init(r.dom, null, { renderer: "canvas" });
      var opt = r.build(HELPERS());
      if (opt) inst.setOption(opt);
      r.inst = inst;
    });
  }

  document.addEventListener("sa:themechange", function () {
    setTimeout(rebuildAll, 30);
  });

  window.addEventListener("resize", function () {
    registry.forEach(function (r) { try { r.inst.resize(); } catch (e) { /* 忽略 */ } });
  });

  /* ============================================================
     指标计算
     ============================================================ */
  function ma(arr, n) {
    var out = [];
    for (var i = 0; i < arr.length; i++) {
      if (i < n - 1) { out.push("-"); continue; }
      var s = 0;
      for (var j = 0; j < n; j++) s += arr[i - j];
      out.push(+(s / n).toFixed(2));
    }
    return out;
  }
  function ema(arr, n) {
    var k = 2 / (n + 1), out = [], prev = arr[0];
    arr.forEach(function (v, i) {
      prev = i === 0 ? v : v * k + prev * (1 - k);
      out.push(+prev.toFixed(4));
    });
    return out;
  }
  function macd(closes) {
    var e12 = ema(closes, 12), e26 = ema(closes, 26);
    var dif = closes.map(function (_, i) { return +(e12[i] - e26[i]).toFixed(4); });
    var dea = ema(dif, 9);
    var hist = dif.map(function (v, i) { return +((v - dea[i]) * 2).toFixed(4); });
    return { dif: dif, dea: dea, hist: hist };
  }
  function kdj(kl, n, m1, m2) {
    n = n || 9; m1 = m1 || 3; m2 = m2 || 3;
    var k = [], d = [], j = [], pk = 50, pd = 50;
    kl.forEach(function (b, i) {
      var s = Math.max(0, i - n + 1);
      var win = kl.slice(s, i + 1);
      var hh = Math.max.apply(null, win.map(function (x) { return x.h; }));
      var ll = Math.min.apply(null, win.map(function (x) { return x.l; }));
      var rsv = hh === ll ? 50 : (b.c - ll) / (hh - ll) * 100;
      pk = (m1 - 1) / m1 * pk + rsv / m1;
      pd = (m2 - 1) / m2 * pd + pk / m2;
      k.push(+pk.toFixed(2)); d.push(+pd.toFixed(2)); j.push(+(3 * pk - 2 * pd).toFixed(2));
    });
    return { k: k, d: d, j: j };
  }

  /* ============================================================
     图表工厂
     ============================================================ */
  var F = {};

  /* --- 颜色加透明度（支持 #rgb / #rrggbb / rgb() / rgba()） --- */
  function withAlpha(color, a) {
    var c = (color || "").trim();
    var m = /^#([0-9a-f]{3})$/i.exec(c);
    if (m) {
      var s = m[1];
      return "rgba(" + parseInt(s[0] + s[0], 16) + "," + parseInt(s[1] + s[1], 16) + "," + parseInt(s[2] + s[2], 16) + "," + a + ")";
    }
    m = /^#([0-9a-f]{6})$/i.exec(c);
    if (m) {
      var v = m[1];
      return "rgba(" + parseInt(v.slice(0, 2), 16) + "," + parseInt(v.slice(2, 4), 16) + "," + parseInt(v.slice(4, 6), 16) + "," + a + ")";
    }
    m = /^rgba?\(([^)]+)\)$/i.exec(c);
    if (m) {
      var parts = m[1].split(",").map(function (x) { return x.trim(); }).slice(0, 3);
      return "rgba(" + parts.join(",") + "," + a + ")";
    }
    return c;
  }

  /* --- 迷你走势 sparkline --- */
  F.spark = function (el, data, opt) {
    opt = opt || {};
    var up = opt.up === undefined ? (data[data.length - 1] >= data[0]) : opt.up;
    var color = up ? upColor() : downColor();
    return make(el, function (h) {
      return {
        animation: false,
        grid: { left: 0, right: 0, top: 2, bottom: 2 },
        xAxis: { type: "category", show: false, boundaryGap: false, data: data.map(function (_, i) { return i; }) },
        yAxis: { type: "value", show: false, scale: true },
        series: [{
          type: "line", data: data, smooth: true, symbol: "none",
          lineStyle: { width: 1.4, color: color },
          areaStyle: {
            color: {
              type: "linear", x: 0, y: 0, x2: 0, y2: 1,
              colorStops: [
                { offset: 0, color: withAlpha(color, 0.32) },
                { offset: 1, color: withAlpha(color, 0) }
              ]
            }
          }
        }],
        tooltip: { show: false }
      };
    });
  };

  /* --- K 线（含均线 / 成交量 / MACD / KDJ） --- */
  F.kline = function (el, bars, opt) {
    opt = opt || {};
    var closes = bars.map(function (b) { return b.c; });
    var dates = bars.map(function (b) { return b.d; });
    var mas = (opt.ma || [5, 10, 20, 60]).map(function (n) { return { n: n, data: ma(closes, n) }; });
    var m = macd(closes);
    var kd = kdj(bars);
    var showVol = opt.volume !== false;
    var showMacd = opt.macd !== false;
    var showKdj = !!opt.kdj;

    return make(el, function (h) {
      var pal = h.palette();
      var grids = [];
      var xAxes = [];
      var yAxes = [];
      var series = [];
      var titles = [];

      var topPct = 6, mainH = showMacd || showKdj ? (showVol ? 44 : 62) : (showVol ? 62 : 84);
      var volH = showVol ? (showMacd || showKdj ? 12 : 22) : 0;
      var macdH = showMacd || showKdj ? 20 : 0;
      var cursor = topPct;

      /* 主图 */
      grids.push({ left: 8, right: 12, top: cursor + "%", height: mainH + "%", containLabel: true });
      xAxes.push({
        type: "category", gridIndex: 0, data: dates, boundaryGap: true,
        axisLine: axisLine(), axisLabel: { show: false }, splitLine: { show: false },
        axisPointer: { label: { show: false } }
      });
      yAxes.push({
        type: "value", gridIndex: 0, scale: true, position: "right",
        axisLine: { show: false }, axisLabel: axisLabel({ fontSize: 10 }),
        splitLine: splitLine()
      });
      cursor += mainH + 4;

      series.push({
        name: "K线", type: "candlestick", xAxisIndex: 0, yAxisIndex: 0,
        data: bars.map(function (b) { return [b.o, b.c, b.l, b.h]; }),
        itemStyle: {
          color: upColor(), color0: downColor(),
          borderColor: upColor(), borderColor0: downColor()
        },
        barMaxWidth: 12
      });
      mas.forEach(function (mm, i) {
        series.push({
          name: "MA" + mm.n, type: "line", xAxisIndex: 0, yAxisIndex: 0,
          data: mm.data, smooth: true, symbol: "none",
          lineStyle: { width: 1.1, color: pal[i % pal.length], opacity: 0.9 }
        });
      });

      /* 成交量 */
      if (showVol) {
        grids.push({ left: 8, right: 12, top: cursor + "%", height: volH + "%", containLabel: true });
        xAxes.push({ type: "category", gridIndex: 1, data: dates, axisLine: axisLine(), axisLabel: { show: false }, splitLine: { show: false } });
        yAxes.push({ type: "value", gridIndex: 1, position: "right", axisLine: { show: false }, axisLabel: axisLabel({ fontSize: 10 }), splitLine: splitLine() });
        series.push({
          name: "成交量", type: "bar", xAxisIndex: 1, yAxisIndex: 1,
          data: bars.map(function (b) { return { value: b.v, itemStyle: { color: b.c >= b.o ? upColor() : downColor(), opacity: 0.62 } }; }),
          barMaxWidth: 12
        });
        cursor += volH + 4;
      }

      /* MACD / KDJ */
      if (showMacd) {
        grids.push({ left: 8, right: 12, top: cursor + "%", height: macdH + "%", containLabel: true });
        xAxes.push({ type: "category", gridIndex: 2, data: dates, axisLine: axisLine(), axisLabel: axisLabel({ fontSize: 10 }), splitLine: { show: false } });
        yAxes.push({ type: "value", gridIndex: 2, position: "right", axisLine: { show: false }, axisLabel: axisLabel({ fontSize: 10 }), splitLine: splitLine() });
        series.push({
          name: "MACD", type: "bar", xAxisIndex: 2, yAxisIndex: 2,
          data: m.hist.map(function (v) { return { value: v, itemStyle: { color: v >= 0 ? upColor() : downColor(), opacity: 0.8 } }; }),
          barMaxWidth: 8
        });
        series.push({ name: "DIF", type: "line", xAxisIndex: 2, yAxisIndex: 2, data: m.dif, symbol: "none", lineStyle: { width: 1, color: pal[0] } });
        series.push({ name: "DEA", type: "line", xAxisIndex: 2, yAxisIndex: 2, data: m.dea, symbol: "none", lineStyle: { width: 1, color: pal[3] } });
      } else if (showKdj) {
        grids.push({ left: 8, right: 12, top: cursor + "%", height: macdH + "%", containLabel: true });
        xAxes.push({ type: "category", gridIndex: 2, data: dates, axisLine: axisLine(), axisLabel: axisLabel({ fontSize: 10 }), splitLine: { show: false } });
        yAxes.push({ type: "value", gridIndex: 2, position: "right", min: 0, max: 100, axisLine: { show: false }, axisLabel: axisLabel({ fontSize: 10 }), splitLine: splitLine() });
        series.push({ name: "K", type: "line", xAxisIndex: 2, yAxisIndex: 2, data: kd.k, symbol: "none", lineStyle: { width: 1, color: pal[0] } });
        series.push({ name: "D", type: "line", xAxisIndex: 2, yAxisIndex: 2, data: kd.d, symbol: "none", lineStyle: { width: 1, color: pal[3] } });
        series.push({ name: "J", type: "line", xAxisIndex: 2, yAxisIndex: 2, data: kd.j, symbol: "none", lineStyle: { width: 1, color: pal[6] } });
      }

      var allX = xAxes.map(function (a) { return Object.assign({}, a, { axisPointer: axisPointer() }); });

      return {
        animation: false,
        title: opt.title ? titleText(opt.title, opt.sub) : undefined,
        legend: legend({ data: ["K线"].concat(mas.map(function (x) { return "MA" + x.n; })), selected: { "K线": true } }),
        tooltip: tooltip({
          trigger: "axis",
          axisPointer: axisPointer(),
          formatter: function (ps) {
            if (!ps || !ps.length) return "";
            var i = ps[0].dataIndex;
            var b = bars[i];
            if (!b) return "";
            var pct = ((b.c - b.o) / b.o * 100);
            var cls = pct >= 0 ? "color:" + upColor() : "color:" + downColor();
            var lines = [
              '<div style="font-weight:600;margin-bottom:4px">' + b.d + "</div>",
              '<div style="font-family:monospace;font-size:12px;line-height:1.7">',
              "开 " + b.o.toFixed(2) + " &nbsp; 高 " + b.h.toFixed(2) + "<br>",
              "低 " + b.l.toFixed(2) + " &nbsp; 收 <b style=\"" + cls + "\">" + b.c.toFixed(2) + "</b><br>",
              '<span style="' + cls + '">涨跌 ' + (pct >= 0 ? "+" : "") + pct.toFixed(2) + "%</span><br>",
              "量 " + (b.v / 10000).toFixed(2) + " 万手",
              "</div>"
            ];
            mas.forEach(function (mm) {
              if (mm.data[i] !== "-") lines.push('<div style="font-size:11px;color:' + cv("--text-2") + '">MA' + mm.n + " " + mm.data[i] + "</div>");
            });
            return lines.join("");
          }
        }),
        axisPointer: { link: [{ xAxisIndex: "all" }] },
        grid: grids,
        xAxis: allX,
        yAxis: yAxes,
        dataZoom: opt.dataZoom === false ? undefined : [
          { type: "inside", xAxisIndex: xAxes.map(function (_, i) { return i; }), start: opt.zoomStart === undefined ? 55 : opt.zoomStart, end: 100 }
        ],
        series: series
      };
    });
  };

  /* --- 柱线组合 --- */
  F.combo = function (el, cfg) {
    return make(el, function (h) {
      var pal = h.palette();
      var series = [];
      (cfg.bars || []).forEach(function (b, i) {
        series.push({
          name: b.name, type: "bar", data: b.data, barMaxWidth: 26,
          itemStyle: {
            borderRadius: [3, 3, 0, 0],
            color: b.color ? b.color : {
              type: "linear", x: 0, y: 0, x2: 0, y2: 1,
              colorStops: [{ offset: 0, color: pal[i % pal.length] }, { offset: 1, color: pal[i % pal.length] + "33" }]
            }
          },
          yAxisIndex: b.axis === 2 ? 1 : 0
        });
      });
      (cfg.lines || []).forEach(function (l, i) {
        series.push({
          name: l.name, type: "line", data: l.data, smooth: true,
          symbol: l.symbol || "circle", symbolSize: 5,
          yAxisIndex: l.axis === 2 ? 1 : 0,
          lineStyle: { width: 2, color: l.color || pal[(i + 2) % pal.length] },
          itemStyle: { color: l.color || pal[(i + 2) % pal.length] },
          areaStyle: l.area ? { opacity: 0.12 } : undefined
        });
      });
      var yAxes = [{
        type: "value", name: cfg.unit || "", nameTextStyle: { color: cv("--text-3"), fontSize: 10 },
        axisLine: { show: false }, axisLabel: axisLabel({ fontSize: 10 }), splitLine: splitLine()
      }];
      if (cfg.lines && cfg.lines.some(function (l) { return l.axis === 2; })) {
        yAxes.push({ type: "value", name: cfg.unit2 || "", nameTextStyle: { color: cv("--text-3"), fontSize: 10 }, position: "right", axisLine: { show: false }, axisLabel: axisLabel({ fontSize: 10 }), splitLine: { show: false } });
      }
      return {
        animationDuration: 500,
        legend: legend({ data: series.map(function (s) { return s.name; }) }),
        tooltip: tooltip({ trigger: "axis", axisPointer: { type: "shadow" } }),
        grid: grid({ top: 30 }),
        xAxis: { type: "category", data: cfg.labels, axisLine: axisLine(), axisLabel: axisLabel({ fontSize: 11, interval: 0, rotate: cfg.rotate || 0 }), splitLine: { show: false } },
        yAxis: yAxes,
        series: series
      };
    });
  };

  /* --- 堆叠柱 --- */
  F.stacked = function (el, cfg) {
    return make(el, function (h) {
      var pal = h.palette();
      var series = cfg.series.map(function (s, i) {
        return {
          name: s.name, type: "bar", stack: cfg.stack || "total", data: s.data, barMaxWidth: 26,
          itemStyle: { color: s.color || pal[i % pal.length], borderRadius: i === cfg.series.length - 1 ? [3, 3, 0, 0] : 0 }
        };
      });
      return {
        legend: legend({ data: cfg.series.map(function (s) { return s.name; }) }),
        tooltip: tooltip({ trigger: "axis", axisPointer: { type: "shadow" } }),
        grid: grid({ top: 30 }),
        xAxis: { type: "category", data: cfg.labels, axisLine: axisLine(), axisLabel: axisLabel({ fontSize: 11 }), splitLine: { show: false } },
        yAxis: { type: "value", name: cfg.unit || "", nameTextStyle: { color: cv("--text-3"), fontSize: 10 }, axisLine: { show: false }, axisLabel: axisLabel({ fontSize: 10 }), splitLine: splitLine() },
        series: series
      };
    });
  };

  /* --- 正负双色柱（资金净流入） --- */
  F.posneg = function (el, cfg) {
    return make(el, function () {
      return {
        tooltip: tooltip({ trigger: "axis", axisPointer: { type: "shadow" }, formatter: function (ps) { return ps[0].axisValue + "<br><b>" + ps[0].data.value + " 亿元</b>"; } }),
        grid: grid({ top: 16 }),
        xAxis: { type: "category", data: cfg.labels, axisLine: axisLine(), axisLabel: axisLabel({ fontSize: 11 }), splitLine: { show: false } },
        yAxis: { type: "value", name: "亿元", nameTextStyle: { color: cv("--text-3"), fontSize: 10 }, axisLine: { show: false }, axisLabel: axisLabel({ fontSize: 10 }), splitLine: splitLine() },
        series: [{
          type: "bar", data: cfg.data.map(function (v) {
            return { value: v, itemStyle: { color: v >= 0 ? upColor() : downColor(), borderRadius: v >= 0 ? [3, 3, 0, 0] : [0, 0, 3, 3] } };
          }),
          barMaxWidth: 26
        }]
      };
    });
  };

  /* --- 面积图 --- */
  F.area = function (el, cfg) {
    return make(el, function (h) {
      var pal = h.palette();
      var series = (cfg.series || [{ name: cfg.name || "值", data: cfg.data }]).map(function (s, i) {
        var color = s.color || pal[i % pal.length];
        return {
          name: s.name, type: "line", data: s.data, smooth: true, symbol: "none",
          lineStyle: { width: 1.8, color: color },
          areaStyle: {
            opacity: 0.18,
            color: {
              type: "linear", x: 0, y: 0, x2: 0, y2: 1,
              colorStops: [{ offset: 0, color: color }, { offset: 1, color: "transparent" }]
            }
          }
        };
      });
      return {
        legend: cfg.series && cfg.series.length > 1 ? legend({ data: cfg.series.map(function (s) { return s.name; }) }) : undefined,
        tooltip: tooltip({ trigger: "axis" }),
        grid: grid({ top: cfg.series && cfg.series.length > 1 ? 30 : 16 }),
        xAxis: { type: "category", boundaryGap: false, data: cfg.labels, axisLine: axisLine(), axisLabel: axisLabel({ fontSize: 11 }), splitLine: { show: false } },
        yAxis: { type: "value", name: cfg.unit || "", nameTextStyle: { color: cv("--text-3"), fontSize: 10 }, scale: true, axisLine: { show: false }, axisLabel: axisLabel({ fontSize: 10 }), splitLine: splitLine() },
        series: series
      };
    });
  };

  /* --- 环形 / 饼图 --- */
  F.donut = function (el, cfg) {
    return make(el, function (h) {
      var pal = h.palette();
      return {
        tooltip: tooltip({ trigger: "item", formatter: "{b}<br>{c}（{d}%）" }),
        legend: legend({ orient: cfg.legendOrient || "vertical", right: 0, top: "center", itemGap: 8 }),
        series: [{
          type: "pie",
          radius: cfg.radius || ["52%", "74%"],
          center: cfg.center || ["36%", "52%"],
          avoidLabelOverlap: true,
          itemStyle: { borderColor: cv("--bg-panel"), borderWidth: 2, borderRadius: 4 },
          label: cfg.label === false ? { show: false } : { show: true, color: cv("--text-2"), fontSize: 11, formatter: "{b}\n{d}%" },
          labelLine: { lineStyle: { color: cv("--border") } },
          data: cfg.data.map(function (d, i) { return { name: d.name, value: d.value, itemStyle: { color: d.color || pal[i % pal.length] } }; })
        }]
      };
    });
  };

  /* --- 雷达 --- */
  F.radar = function (el, cfg) {
    return make(el, function (h) {
      var pal = h.palette();
      return {
        tooltip: tooltip({}),
        legend: legend({ data: cfg.series.map(function (s) { return s.name; }) }),
        radar: {
          indicator: cfg.indicators,
          center: cfg.center || ["50%", "56%"],
          radius: cfg.radius || "64%",
          axisName: { color: cv("--text-2"), fontSize: 11 },
          splitLine: { lineStyle: { color: cv("--chart-split") } },
          splitArea: { areaStyle: { color: [cv("--bg-sunken"), "transparent"] } },
          axisLine: { lineStyle: { color: cv("--chart-axis") } }
        },
        series: [{
          type: "radar",
          data: cfg.series.map(function (s, i) {
            return {
              name: s.name, value: s.values,
              symbolSize: 4,
              lineStyle: { width: 2, color: s.color || pal[i % pal.length] },
              itemStyle: { color: s.color || pal[i % pal.length] },
              areaStyle: { opacity: 0.16, color: s.color || pal[i % pal.length] }
            };
          })
        }]
      };
    });
  };

  /* --- 桑基图（产业链传导 / 自上而下传导） --- */
  F.sankey = function (el, cfg) {
    return make(el, function (h) {
      var pal = h.palette();
      var cats = cfg.categories || [];
      var nodes = cfg.nodes.map(function (n, i) {
        var color = n.color || pal[(n.level || i) % pal.length];
        return {
          name: n.name, depth: n.level,
          itemStyle: { color: color, borderColor: "transparent" },
          label: { color: cv("--text-1"), fontSize: 11 },
          tooltip: n.note ? { formatter: n.name + "<br><span style='font-size:11px'>" + n.note + "</span>" } : undefined
        };
      });
      return {
        tooltip: tooltip({
          trigger: "item",
          formatter: function (p) {
            if (p.dataType === "edge") {
              return p.data.source + " → " + p.data.target + "<br>强度 " + p.data.value + (p.data.note ? "<br><span style='font-size:11px;color:" + cv("--text-2") + "'>" + p.data.note + "</span>" : "");
            }
            return p.name;
          }
        }),
        series: [{
          type: "sankey",
          left: 8, right: 120, top: 12, bottom: 12,
          nodeWidth: 12, nodeGap: 10,
          draggable: true,
          emphasis: { focus: "adjacency" },
          lineStyle: { color: "gradient", opacity: 0.32, curveness: 0.5 },
          label: { color: cv("--text-1"), fontSize: 11 },
          data: nodes,
          links: cfg.links.map(function (l) { return { source: l.source, target: l.target, value: l.value, note: l.note }; }),
          levels: (cats.length ? cats : [
            { depth: 0, itemStyle: { color: pal[0] } },
            { depth: 1, itemStyle: { color: pal[2] } },
            { depth: 2, itemStyle: { color: pal[3] } }
          ])
        }]
      };
    });
  };

  /* --- 关系图（股权拓扑 / 事件因果网络） --- */
  F.graph = function (el, cfg) {
    return make(el, function (h) {
      var pal = h.palette();
      var catMap = cfg.categories || [];
      var catColor = {};
      catMap.forEach(function (c, i) { catColor[c.name] = c.color || pal[i % pal.length]; });

      var nodes = cfg.nodes.map(function (n) {
        var color = catColor[n.cat] || pal[0];
        return {
          id: n.id, name: n.name,
          symbolSize: n.size || 42,
          category: catMap.findIndex(function (c) { return c.name === n.cat; }),
          itemStyle: {
            color: n.cat === "self" ? color : cv("--bg-elevated"),
            borderColor: color,
            borderWidth: n.cat === "self" ? 3 : 2,
            shadowBlur: n.cat === "self" ? 18 : 6,
            shadowColor: color
          },
          label: {
            show: true, position: "bottom", distance: 6,
            color: cv("--text-1"), fontSize: 11,
            formatter: n.short || n.name
          },
          desc: n.desc || "",
          meta: n
        };
      });

      var links = cfg.links.map(function (l) {
        var pos = (l.strength === undefined ? l.ratio : l.strength);
        var neg = pos !== undefined && pos < 0;
        return {
          source: l.source, target: l.target,
          value: pos,
          lineStyle: {
            color: neg ? downColor() : (cfg.linkColor || cv("--chart-1")),
            width: Math.min(4, 0.9 + Math.abs(pos || 1) / 26),
            curveness: cfg.curveness === undefined ? 0.12 : cfg.curveness,
            opacity: 0.72,
            type: neg ? "dashed" : "solid"
          },
          label: cfg.showLinkLabel === false ? undefined : {
            show: true, fontSize: 10, color: cv("--text-2"),
            formatter: cfg.linkLabelFormatter ? cfg.linkLabelFormatter(l) : (l.ratio !== undefined ? l.ratio + "%" : (l.strength !== undefined ? (l.strength > 0 ? "+" : "") + l.strength : ""))
          },
          note: l.note
        };
      });

      return {
        tooltip: tooltip({
          formatter: function (p) {
            if (p.dataType === "edge") {
              return p.data.source + " → " + p.data.target +
                (p.data.value !== undefined && p.data.value !== null ? "<br>强度 / 比例：" + p.data.value : "") +
                (p.data.note ? "<br><span style='font-size:11px;color:" + cv("--text-2") + "'>" + p.data.note + "</span>" : "");
            }
            return "<b>" + p.name + "</b>" + (p.data.desc ? "<br><span style='font-size:11px;color:" + cv("--text-2") + "'>" + p.data.desc + "</span>" : "");
          }
        }),
        legend: cfg.showLegend === false ? undefined : [{
          data: catMap.map(function (c) { return c.name; }),
          top: 0, left: 0, itemWidth: 9, itemHeight: 9,
          textStyle: { color: cv("--chart-label"), fontSize: 11 },
          icon: "circle"
        }],
        series: [{
          type: "graph",
          layout: cfg.layout || "force",
          roam: true,
          draggable: true,
          force: { repulsion: cfg.repulsion || 320, edgeLength: cfg.edgeLength || [70, 160], gravity: 0.16, friction: 0.28 },
          labelLayout: { hideOverlap: true, moveOverlap: "shiftY" },
          circular: cfg.circular,
          categories: catMap.map(function (c, i) { return { name: c.name, itemStyle: { color: c.color || pal[i % pal.length] } }; }),
          edgeSymbol: ["none", "arrow"],
          edgeSymbolSize: cfg.edgeSymbolSize === undefined ? 7 : cfg.edgeSymbolSize,
          edgeLabel: { show: cfg.showLinkLabel !== false },
          emphasis: { focus: "adjacency", lineStyle: { width: 3, opacity: 1 } },
          label: { show: true },
          data: nodes,
          links: links,
          lineStyle: { opacity: 0.7 }
        }]
      };
    });
  };

  /* --- 热力图（风险矩阵） --- */
  F.heatmap = function (el, cfg) {
    return make(el, function () {
      var colors = ["#16a34a", "#65a30d", "#eab308", "#f97316", "#ef4444"];
      return {
        tooltip: tooltip({
          formatter: function (p) {
            var d = p.data;
            return "<b>" + cfg.yLabels[d.value[1]] + " × " + cfg.xLabels[d.value[0]] + "</b><br>" +
              "风险等级 " + d.value[2] + " / 5<br>" +
              (d.risks && d.risks.length ? "<span style='font-size:11px;color:" + cv("--text-2") + "'>" + d.risks.join("<br>") + "</span>" : "无已知风险项");
          }
        }),
        grid: grid({ left: 8, right: 8, top: 8, bottom: 24 }),
        xAxis: {
          type: "category", data: cfg.xLabels, splitArea: { show: true, areaStyle: { color: ["transparent"] } },
          axisLine: axisLine(), axisLabel: axisLabel({ fontSize: 11 }), splitLine: { show: false }
        },
        yAxis: {
          type: "category", data: cfg.yLabels, splitArea: { show: true, areaStyle: { color: ["transparent"] } },
          axisLine: axisLine(), axisLabel: axisLabel({ fontSize: 11 }), splitLine: { show: false }
        },
        visualMap: {
          min: 1, max: 5, show: false,
          inRange: { color: colors },
          dimension: 2
        },
        series: [{
          type: "heatmap",
          data: cfg.data.map(function (d) {
            return { value: [d.x, d.y, d.level], risks: d.risks };
          }),
          label: {
            show: true, color: "#fff", fontSize: 11, fontWeight: 600,
            formatter: function (p) { return p.data.value[2]; }
          },
          itemStyle: { borderRadius: 4, borderColor: cv("--bg-panel"), borderWidth: 2 },
          emphasis: { itemStyle: { shadowBlur: 10, shadowColor: "rgba(0,0,0,.4)" } }
        }]
      };
    });
  };

  /* --- 散点气泡（估值 × 盈利，气泡=市值） --- */
  F.bubble = function (el, cfg) {
    return make(el, function (h) {
      var pal = h.palette();
      var self = cfg.data.filter(function (d) { return d.self; });
      var others = cfg.data.filter(function (d) { return !d.self; });
      return {
        tooltip: tooltip({
          formatter: function (p) {
            var d = p.data.meta;
            return "<b>" + d.name + "</b><br>PE " + (d.pe || "亏损") + " 倍<br>ROE " + d.roe + "%<br>市值 " + d.cap + " 亿";
          }
        }),
        grid: grid({ top: 16, bottom: 28 }),
        xAxis: {
          type: "value", name: cfg.xName || "PE（倍）", nameLocation: "middle", nameGap: 24,
          nameTextStyle: { color: cv("--text-3"), fontSize: 11 },
          axisLine: axisLine(), axisLabel: axisLabel({ fontSize: 10 }), splitLine: splitLine()
        },
        yAxis: {
          type: "value", name: cfg.yName || "ROE（%）", nameLocation: "middle", nameGap: 34,
          nameTextStyle: { color: cv("--text-3"), fontSize: 11 },
          scale: true, axisLine: { show: false }, axisLabel: axisLabel({ fontSize: 10 }), splitLine: splitLine()
        },
        series: [
          {
            type: "scatter", data: others.map(function (d) {
              return { value: [d.pe || 60, d.roe], meta: d, symbolSize: Math.max(10, Math.sqrt(d.cap) / 3.2) };
            }),
            itemStyle: { color: pal[0], opacity: 0.42, borderColor: pal[0], borderWidth: 1 },
            label: {
              show: true, position: "top", fontSize: 10, color: cv("--text-2"),
              formatter: function (p) { return p.data.meta.name; }
            }
          },
          {
            type: "scatter", data: self.map(function (d) {
              return { value: [d.pe || 60, d.roe], meta: d, symbolSize: Math.max(14, Math.sqrt(d.cap) / 3) };
            }),
            itemStyle: { color: upColor(), opacity: 0.9, borderColor: "#fff", borderWidth: 2, shadowBlur: 12, shadowColor: upColor() },
            label: {
              show: true, position: "top", fontSize: 11, fontWeight: 600, color: cv("--text-1"),
              formatter: function (p) { return p.data.meta.name; }
            }
          }
        ]
      };
    });
  };

  /* --- 目标价散点带（含当前价基准线） --- */
  F.targetBand = function (el, cfg) {
    return make(el, function (h) {
      var pal = h.palette();
      return {
        tooltip: tooltip({
          formatter: function (p) {
            var d = p.data.meta;
            return "<b>" + d.org + "</b>（" + d.date + "）<br>评级：" + d.rating + "<br>目标价：" + d.target + " 元<br>距现价：" + (d.upside >= 0 ? "+" : "") + d.upside + "%";
          }
        }),
        grid: grid({ top: 20, bottom: 20, right: 60 }),
        xAxis: {
          type: "category", data: cfg.labels, axisLine: axisLine(), axisLabel: axisLabel({ fontSize: 10, rotate: 40 }), splitLine: { show: false }
        },
        yAxis: {
          type: "value", name: "元", nameTextStyle: { color: cv("--text-3"), fontSize: 10 },
          scale: true, axisLine: { show: false }, axisLabel: axisLabel({ fontSize: 10 }), splitLine: splitLine()
        },
        series: [
          {
            type: "scatter",
            data: cfg.data.map(function (d) {
              return {
                value: [d.date, d.target],
                meta: d,
                symbolSize: 11,
                itemStyle: { color: d.rating === "买入" ? upColor() : d.rating === "增持" ? pal[0] : d.rating === "中性" ? flatColor() : downColor() }
              };
            }),
            markLine: {
              silent: true, symbol: "none",
              lineStyle: { color: cv("--accent"), type: "dashed", width: 1.4 },
              label: { formatter: "现价 " + cfg.current, color: cv("--accent"), fontSize: 11, position: "end" },
              data: [{ yAxis: cfg.current }]
            }
          }
        ]
      };
    });
  };

  /* --- 筹码分布（横向） --- */
  F.chips = function (el, cfg) {
    return make(el, function () {
      var prices = cfg.data.map(function (d) { return d.price; });
      /* 类别轴上的 markLine 必须落在某个类别值上，否则不渲染；取最接近的一档 */
      var nearest = prices.reduce(function (a, b) {
        return Math.abs(b - cfg.current) < Math.abs(a - cfg.current) ? b : a;
      }, prices[0]);
      return {
        tooltip: tooltip({
          formatter: function (p) {
            return p.name + " 元<br>筹码占比 " + p.data.value + "%";
          }
        }),
        grid: grid({ left: 8, right: 56, top: 12, bottom: 8 }),
        xAxis: {
          type: "value", name: "占比 %", nameTextStyle: { color: cv("--text-3"), fontSize: 10 },
          axisLine: { show: false }, axisLabel: axisLabel({ fontSize: 10 }), splitLine: splitLine()
        },
        yAxis: {
          type: "category", data: prices, axisLine: axisLine(),
          axisLabel: axisLabel({ fontSize: 9, interval: 3 }), splitLine: { show: false }
        },
        series: [{
          type: "bar",
          data: cfg.data.map(function (d) {
            var above = d.price >= cfg.current;
            return {
              value: d.ratio,
              itemStyle: {
                color: above ? downColor() : upColor(),
                opacity: 0.72, borderRadius: [0, 3, 3, 0]
              }
            };
          }),
          barMaxWidth: 10,
          markLine: {
            silent: true, symbol: "none",
            lineStyle: { color: cv("--accent"), type: "dashed", width: 1.4 },
            label: { formatter: "现价 " + cfg.current, color: cv("--accent"), fontSize: 11 },
            data: [{ yAxis: nearest }]
          }
        }]
      };
    });
  };

  /* --- 时间轴泳道 --- */
  F.lanes = function (el, cfg) {
    return make(el, function (h) {
      var pal = h.palette();
      var dates = [];
      cfg.lanes.forEach(function (l) {
        l.items.forEach(function (it) { if (dates.indexOf(it.date) < 0) dates.push(it.date); });
      });
      dates.sort();
      var laneNames = cfg.lanes.map(function (l) { return l.name; });
      var points = [];
      cfg.lanes.forEach(function (l, li) {
        l.items.forEach(function (it) {
          points.push({
            value: [it.date, l.name],
            meta: it, lane: li,
            symbolSize: 15,
            itemStyle: { color: it.impact === "up" ? upColor() : it.impact === "down" ? downColor() : flatColor(), borderColor: cv("--bg-panel"), borderWidth: 2 }
          });
        });
      });
      return {
        tooltip: tooltip({
          formatter: function (p) {
            var m = p.data.meta;
            return "<b>" + m.date + "</b><br>" + m.title + "<br><span style='font-size:11px;color:" + (m.impact === "up" ? upColor() : m.impact === "down" ? downColor() : flatColor()) + "'>" +
              (m.impact === "up" ? "利好" : m.impact === "down" ? "利空" : "中性") + "</span>";
          }
        }),
        grid: grid({ left: 8, right: 16, top: 20, bottom: 8 }),
        xAxis: {
          type: "category", data: dates, boundaryGap: true, position: "top",
          axisLine: axisLine(), axisLabel: axisLabel({ fontSize: 10, rotate: 30 }), splitLine: { show: true, lineStyle: { color: cv("--chart-split"), type: "dashed" } }
        },
        yAxis: {
          type: "category", data: laneNames, axisLine: axisLine(),
          axisLabel: axisLabel({ fontSize: 11 }), splitLine: { show: true, lineStyle: { color: cv("--chart-split") } }
        },
        series: [{
          type: "scatter", data: points,
          label: {
            show: true, position: "right", fontSize: 10, color: cv("--text-2"),
            formatter: function (p) { return p.data.meta.title.length > 16 ? p.data.meta.title.slice(0, 16) + "…" : p.data.meta.title; }
          },
          emphasis: { scale: 1.4 }
        }]
      };
    });
  };

  /* --- 估值分位带 --- */
  F.band = function (el, cfg) {
    return make(el, function () {
      var d = cfg.band;
      var seg = function (from, to, color, name) {
        return { name: name, value: to - from, from: from, itemStyle: { color: color, borderRadius: 2 } };
      };
      return {
        tooltip: tooltip({
          formatter: function (p) {
            return p.seriesName + "：" + p.data.from + " – " + (p.data.from + p.data.value) + " 倍";
          }
        }),
        grid: grid({ left: 8, right: 8, top: 30, bottom: 8 }),
        xAxis: {
          type: "value", axisLine: { show: false }, axisLabel: axisLabel({ fontSize: 10 }), splitLine: splitLine()
        },
        yAxis: {
          type: "category", data: [cfg.name || "估值"], axisLine: axisLine(), axisLabel: axisLabel({ fontSize: 11 }), splitLine: { show: false }
        },
        series: [
          { name: "最低–25 分位", type: "bar", stack: "b", data: [seg(d.min, d.p25, cv("--chart-6"))], barMaxWidth: 26 },
          { name: "25–50 分位", type: "bar", stack: "b", data: [seg(d.p25, d.median, cv("--chart-2"))], barMaxWidth: 26 },
          { name: "50–75 分位", type: "bar", stack: "b", data: [seg(d.median, d.p75, cv("--chart-4"))], barMaxWidth: 26 },
          { name: "75 分位–最高", type: "bar", stack: "b", data: [seg(d.p75, d.max, cv("--chart-5"))], barMaxWidth: 26 },
          {
            name: "当前", type: "scatter",
            data: [{ value: [d.current, cfg.name || "估值"], itemStyle: { color: "#fff", borderColor: cv("--text-1"), borderWidth: 2 }, symbolSize: 14 }],
            label: { show: true, position: "top", formatter: "当前 " + d.current, color: cv("--text-1"), fontSize: 11, fontWeight: 600 }
          }
        ]
      };
    });
  };

  /* --- 仪表盘 --- */
  F.gauge = function (el, cfg) {
    return make(el, function () {
      var color = cfg.value <= cfg.warnAt ? cv("--ok") : cfg.value <= cfg.dangerAt ? cv("--warn") : cv("--danger");
      return {
        series: [{
          type: "gauge",
          startAngle: 200, endAngle: -20,
          min: 0, max: cfg.max || 100,
          radius: "92%", center: ["50%", "62%"],
          progress: { show: true, width: 10, roundCap: true, itemStyle: { color: color } },
          axisLine: { lineStyle: { width: 10, color: [[1, cv("--border-soft")]] } },
          pointer: { show: false },
          axisTick: { show: false },
          splitLine: { show: false },
          axisLabel: { show: false },
          anchor: { show: false },
          title: { show: true, offsetCenter: [0, "34%"], color: cv("--text-3"), fontSize: 11 },
          detail: {
            valueAnimation: true, offsetCenter: [0, "0%"],
            color: cv("--text-1"), fontSize: 22, fontWeight: 700,
            formatter: function (v) { return v + (cfg.suffix || ""); }
          },
          data: [{ value: cfg.value, name: cfg.name || "" }]
        }]
      };
    });
  };

  /* --- 树图（行业热力） --- */
  F.treemap = function (el, cfg) {
    return make(el, function (h) {
      var pal = h.palette();
      return {
        tooltip: tooltip({
          formatter: function (p) {
            var d = p.data.meta;
            if (!d) return p.name;
            return "<b>" + d.name + "</b><br>涨跌幅：" + (d.pct >= 0 ? "+" : "") + d.pct + "%<br>主力净流入：" + d.flow + " 亿<br>龙头：" + d.leader;
          }
        }),
        series: [{
          type: "treemap",
          roam: false, nodeClick: false, breadcrumb: { show: false },
          left: 0, right: 0, top: 0, bottom: 0,
          itemStyle: { borderColor: cv("--bg-root"), borderWidth: 2, gapWidth: 2 },
          label: { show: true, fontSize: 11, color: "#fff", formatter: "{b}" },
          upperLabel: { show: false },
          levels: [{ itemStyle: { borderWidth: 0, gapWidth: 2 } }],
          data: cfg.data.map(function (d) {
            var intensity = Math.min(1, Math.abs(d.pct) / 4);
            return {
              name: d.name,
              value: Math.max(4, Math.abs(d.pct) * 20 + 12),
              meta: d,
              itemStyle: { color: d.pct >= 0 ? upColor() : downColor(), opacity: 0.28 + intensity * 0.62 }
            };
          })
        }]
      };
    });
  };

  /* --- 横向条形排行 --- */
  F.hbar = function (el, cfg) {
    return make(el, function () {
      var labels = cfg.data.map(function (d) { return d.name; });
      return {
        tooltip: tooltip({
          formatter: function (p) {
            var d = cfg.data[p.dataIndex];
            return "<b>" + d.name + "</b><br>" + (d.pct >= 0 ? "+" : "") + d.pct + "%" + (d.flow !== undefined ? "<br>主力净流入 " + d.flow + " 亿" : "");
          }
        }),
        grid: grid({ left: 8, right: 60, top: 8, bottom: 8 }),
        xAxis: { type: "value", axisLine: { show: false }, axisLabel: axisLabel({ fontSize: 10 }), splitLine: splitLine() },
        yAxis: {
          type: "category", data: labels, inverse: true, axisLine: axisLine(),
          axisLabel: axisLabel({ fontSize: 11 }), splitLine: { show: false }
        },
        series: [{
          type: "bar",
          data: cfg.data.map(function (d) {
            return {
              value: d.pct,
              itemStyle: { color: d.pct >= 0 ? upColor() : downColor(), opacity: 0.82, borderRadius: d.pct >= 0 ? [0, 3, 3, 0] : [3, 0, 0, 3] }
            };
          }),
          barMaxWidth: 12,
          label: {
            show: true, position: "right", fontSize: 10, color: cv("--text-2"),
            formatter: function (p) { return (p.data.value >= 0 ? "+" : "") + p.data.value + "%"; }
          }
        }]
      };
    });
  };

  /* --- 评级历史：堆叠柱 + 平均目标价折线 --- */
  F.ratingHistory = function (el, cfg) {
    return make(el, function (h) {
      var pal = h.palette();
      var hist = D.ratings.history;
      return {
        legend: legend({ data: ["买入", "增持", "中性", "减持", "平均目标价"] }),
        tooltip: tooltip({ trigger: "axis", axisPointer: { type: "shadow" } }),
        grid: grid({ top: 30, right: 56 }),
        xAxis: { type: "category", data: hist.labels, axisLine: axisLine(), axisLabel: axisLabel({ fontSize: 10 }), splitLine: { show: false } },
        yAxis: [
          { type: "value", name: "机构数", nameTextStyle: { color: cv("--text-3"), fontSize: 10 }, axisLine: { show: false }, axisLabel: axisLabel({ fontSize: 10 }), splitLine: splitLine() },
          { type: "value", name: "目标价（元）", nameTextStyle: { color: cv("--text-3"), fontSize: 10 }, position: "right", scale: true, axisLine: { show: false }, axisLabel: axisLabel({ fontSize: 10 }), splitLine: { show: false } }
        ],
        series: [
          { name: "买入", type: "bar", stack: "r", data: hist.buy, itemStyle: { color: upColor() }, barMaxWidth: 20 },
          { name: "增持", type: "bar", stack: "r", data: hist.hold, itemStyle: { color: pal[0] }, barMaxWidth: 20 },
          { name: "中性", type: "bar", stack: "r", data: hist.neutral, itemStyle: { color: flatColor() }, barMaxWidth: 20 },
          { name: "减持", type: "bar", stack: "r", data: hist.reduce, itemStyle: { color: downColor() }, barMaxWidth: 20 },
          { name: "平均目标价", type: "line", yAxisIndex: 1, data: hist.avgTarget, smooth: true, symbolSize: 5, lineStyle: { width: 2, color: pal[3] }, itemStyle: { color: pal[3] } }
        ]
      };
    });
  };

  /* ============================================================
     导出
     ============================================================ */
  window.SA_CHARTS = {
    make: make, rebuildAll: rebuildAll, cv: cv, palette: palette,
    F: F, ma: ma, macd: macd, kdj: kdj,
    upColor: upColor, downColor: downColor, flatColor: flatColor
  };
})();
