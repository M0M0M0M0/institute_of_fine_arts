// Institute of Fine Arts – dashboard charts (FE2-10), drawn with Chart.js (wwwroot/lib/chart.js) from the JSON
// endpoints of ChartsController: { labels: [...], datasets: [{ label, data: [...] }], ids?: [...], details?: [...] }.
//
// Markup (one per chart):
//   <figure class="chart-figure" data-chart="submissions|rating|revenue" data-chart-url="/charts/..."
//           data-chart-link="/manager/competitions/{id}">            ← optional: a bar opens the record's page
//     <figcaption><h3>Title</h3><p>One sentence.</p>
//       <select data-chart-param="year">…</select></figcaption>       ← optional filters, sent as query parameters
//     <div class="chart-box"><canvas role="img"></canvas></div>
//     <p class="chart-status" data-chart-status>Loading the chart…</p>
//     <div class="visually-hidden" data-chart-table></div>
//   </figure>
//
// Palette of the world: ink for graded / revenue, the attention amber for work not done ("Not graded"), gilt only for
// BEST, danger for DISQUALIFIED. Accessibility: every chart gets an aria-label summary and a visually hidden data table;
// motion follows Chart.js' default ease and is switched off for prefers-reduced-motion.
(function () {
    "use strict";

    if (typeof Chart === "undefined") { return; }
    const figures = document.querySelectorAll("[data-chart]");
    if (figures.length === 0) { return; }

    const css = getComputedStyle(document.documentElement);
    const token = (name, fallback) => (css.getPropertyValue(name) || "").trim() || fallback;
    const colors = {
        ink: token("--ifa-ink", "#0b2a33"),
        attention: "#e07b39",                  // orange for work not done ("Not graded"): clearly apart from the BEST gilt
        stone: token("--ifa-line-strong", "#b9b4a8"),
        gilt: token("--ifa-gilt", "#a8814a"),
        danger: token("--ifa-danger", "#a23b2a"),
        text: token("--ifa-text", "#2b2924"),
        muted: token("--ifa-muted", "#625d53"),
        line: token("--ifa-line", "#d6d2c8")
    };

    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    Chart.defaults.font.family = token("--ifa-font-ui", "system-ui, sans-serif");
    Chart.defaults.font.size = 13;
    Chart.defaults.color = colors.muted;
    Chart.defaults.borderColor = colors.line;
    if (reduceMotion) { Chart.defaults.animation = false; }

    const money = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });
    const moneyShort = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD", maximumFractionDigits: 0 });
    const count = new Intl.NumberFormat("en-US");
    const percent = new Intl.NumberFormat("en-US", { style: "percent", maximumFractionDigits: 0 });
    const sentence = (s) => (s && /^[A-Z_]+$/.test(s) ? s.charAt(0) + s.slice(1).toLowerCase() : s); // BEST → Best

    const tooltip = {
        backgroundColor: colors.ink,
        titleColor: "#f3f6f7",
        bodyColor: "#f3f6f7",
        padding: 10,
        cornerRadius: 4,
        boxPadding: 4
    };
    const barBase = { borderRadius: 3, borderSkipped: "start", maxBarThickness: 28, borderWidth: 0 };

    // Text at the end of each horizontal bar ("12 (34%)", "$1,250 · 3 / 8 sold"): options.plugins.endLabels.text(i)
    const endLabels = {
        id: "endLabels",
        afterDatasetsDraw(chart, args, opts) {
            if (!opts || typeof opts.text !== "function") { return; }
            const meta = chart.getDatasetMeta(0);
            const ctx = chart.ctx;
            ctx.save();
            ctx.font = `500 12.5px ${Chart.defaults.font.family}`;
            ctx.fillStyle = colors.text;
            ctx.textBaseline = "middle";
            meta.data.forEach(function (bar, i) {
                const text = opts.text(i);
                if (text) { ctx.fillText(text, bar.x + 6, bar.y); }
            });
            ctx.restore();
        }
    };

    // Visually hidden table: the same numbers for screen readers and as a fallback
    function renderTable(holder, caption, labels, datasets, format) {
        const table = document.createElement("table");
        const cap = document.createElement("caption");
        cap.textContent = caption;
        table.appendChild(cap);
        const head = table.createTHead().insertRow();
        ["", ...datasets.map(d => d.label)].forEach(function (text, i) {
            const th = document.createElement("th");
            th.scope = "col";
            th.textContent = i === 0 ? "Item" : text;
            head.appendChild(th);
        });
        const body = table.createTBody();
        labels.forEach(function (label, r) {
            const row = body.insertRow();
            const th = document.createElement("th");
            th.scope = "row";
            th.textContent = label;
            row.appendChild(th);
            datasets.forEach(function (d, c) { row.insertCell().textContent = format(d.data[r], c, d); });
        });
        holder.replaceChildren(table);
    }

    // Longest end label in px, so the x axis leaves room for it after the longest bar
    function labelRoom(texts) {
        const ctx = document.createElement("canvas").getContext("2d");
        ctx.font = `500 12.5px ${Chart.defaults.font.family}`;
        return Math.max(0, ...texts.map(t => ctx.measureText(t).width)) + 12;
    }

    const builders = {
        // One stacked bar per competition: graded (ink) + not graded yet (amber); oldest competition on the left
        submissions: function (json, figure) {
            const [subs, evals] = json.datasets;
            const graded = evals ? evals.data : subs.data.map(() => 0);
            const notGraded = subs.data.map((v, i) => Math.max(0, v - graded[i]));
            const total = subs.data.reduce((a, b) => a + b, 0);
            const waiting = notGraded.reduce((a, b) => a + b, 0);
            const link = figure.dataset.chartLink;
            const ids = json.ids || [];
            const details = json.details || [];
            return {
                summary: `${json.labels.length} competitions, ${count.format(total)} entries; ${count.format(waiting)} not graded yet.`,
                tableLabels: json.labels.map((l, i) => details[i] ? `${l} – ${details[i]}` : l),
                tableSets: [subs, { label: "Graded", data: graded }, { label: "Not graded", data: notGraded }],
                format: (v) => count.format(v),
                config: {
                    type: "bar",
                    data: {
                        labels: json.labels,
                        datasets: [
                            { ...barBase, label: "Graded", data: graded, backgroundColor: colors.ink, stack: "s" },
                            { ...barBase, label: "Not graded", data: notGraded, backgroundColor: colors.attention, stack: "s", borderSkipped: false }
                        ]
                    },
                    options: {
                        maintainAspectRatio: false,
                        onClick: function (evt, elements) {
                            if (!link || elements.length === 0) { return; }
                            const id = ids[elements[0].index];
                            if (id) { window.location.href = link.replace("{id}", id); }
                        },
                        onHover: function (evt, elements) {
                            evt.native.target.style.cursor = link && elements.length ? "pointer" : "default";
                        },
                        plugins: {
                            legend: { position: "top", align: "start", labels: { boxWidth: 12, boxHeight: 12, useBorderRadius: true, borderRadius: 2, color: colors.text } },
                            tooltip: {
                                ...tooltip,
                                mode: "index",
                                intersect: false,
                                callbacks: {
                                    title: (items) => items.length ? `${json.labels[items[0].dataIndex]} · ${details[items[0].dataIndex] || ""}` : "",
                                    label: (ctx) => `${ctx.dataset.label}: ${count.format(ctx.parsed.y)}`,
                                    footer: (items) => items.length ? `Total: ${count.format(subs.data[items[0].dataIndex])} entries${link ? " · click to open" : ""}` : ""
                                }
                            }
                        },
                        scales: {
                            // Only the start date ("31/10") on the axis: short, and the order by date reads at a glance.
                            // The code, title and full dates are in the tooltip.
                            // Without the dates (older server), the codes are long: let Chart.js skip some instead of overlapping
                            x: { stacked: true, grid: { display: false }, ticks: { color: colors.text, autoSkip: !json.subLabels, maxRotation: 0,
                                callback: function (v, i) { return json.subLabels ? json.subLabels[i] : this.getLabelForValue(v); } } },
                            y: { stacked: true, beginAtZero: true, ticks: { precision: 0 }, border: { display: false } }
                        }
                    }
                }
            };
        },

        // Rating distribution: horizontal bars with "count (share)" at the end; BEST gilt, DQ danger, "Not graded" amber
        rating: function (json) {
            const d = json.datasets[0];
            const total = d.data.reduce((a, b) => a + b, 0);
            const best = d.data[json.labels.indexOf("BEST")] || 0;
            const texts = d.data.map(v => `${count.format(v)} (${percent.format(total ? v / total : 0)})`);
            const max = Math.max(...d.data);
            return {
                summary: `${count.format(total)} submissions; ${count.format(best)} rated Best.`,
                tableLabels: json.labels.map(sentence),
                tableSets: [d],
                format: (v) => `${count.format(v)} (${percent.format(total ? v / total : 0)})`,
                config: {
                    type: "bar",
                    plugins: [endLabels],
                    data: {
                        labels: json.labels.map(sentence),
                        datasets: [{
                            ...barBase, label: d.label, data: d.data,
                            backgroundColor: json.labels.map(l => l === "BEST" ? colors.gilt : l === "Not graded" ? colors.attention : l === "DISQUALIFIED" ? colors.danger : colors.ink)
                        }]
                    },
                    options: {
                        indexAxis: "y",
                        maintainAspectRatio: false,
                        layout: { padding: { right: labelRoom(texts) } },
                        plugins: { legend: { display: false }, tooltip: { ...tooltip, callbacks: { label: (ctx) => texts[ctx.dataIndex] } }, endLabels: { text: (i) => texts[i] } },
                        scales: {
                            x: { beginAtZero: true, suggestedMax: max, ticks: { precision: 0 }, border: { display: false } },
                            y: { grid: { display: false }, ticks: { color: colors.text } }
                        }
                    }
                }
            };
        },

        // Revenue per exhibition: horizontal bars, full names, highest revenue first; revenue and "sold / shown" in the tooltip.
        // Every exhibition is listed, also those with no sale yet, so the caption count and the bars always match.
        revenue: function (json) {
            const rev = json.datasets[0];
            const sold = json.datasets[1] || { data: rev.data.map(() => 0) };
            const shown = json.datasets[2] || { data: sold.data };
            const order = json.labels.map((_, i) => i).sort((a, b) => rev.data[b] - rev.data[a] || json.labels[a].localeCompare(json.labels[b]));
            const labels = order.map(i => json.labels[i]);
            const revenue = order.map(i => rev.data[i]);
            const texts = order.map(i => `${moneyShort.format(rev.data[i])} · ${count.format(sold.data[i])} / ${count.format(shown.data[i])} sold`);
            const total = rev.data.reduce((a, b) => a + b, 0);
            const withSales = rev.data.filter(v => v > 0).length;
            return {
                summary: `${money.format(total)} of sales; ${withSales} of ${json.labels.length} exhibitions sold at least one painting.`,
                tableLabels: labels,
                tableSets: [
                    { label: rev.label, data: revenue },
                    { label: "Paintings sold", data: order.map(i => sold.data[i]) },
                    { label: "Paintings shown", data: order.map(i => shown.data[i]) }
                ],
                format: (v, c) => (c === 0 ? money.format(v) : count.format(v)),
                height: Math.max(12, labels.length * 2.2 + 3) + "rem",
                config: {
                    type: "bar",
                    data: { labels, datasets: [{ ...barBase, label: rev.label, data: revenue, backgroundColor: colors.ink, minBarLength: 2 }] },
                    options: {
                        indexAxis: "y",
                        maintainAspectRatio: false,
                        plugins: {
                            legend: { display: false },
                            tooltip: { ...tooltip, callbacks: { title: (items) => items.length ? labels[items[0].dataIndex] : "", label: (ctx) => texts[ctx.dataIndex] } }
                        },
                        scales: {
                            x: { beginAtZero: true, border: { display: false }, ticks: { callback: (v) => moneyShort.format(v) } },
                            // Full names, wrapped on two lines when long (never cut)
                            // One line per exhibition: the start of the name, "…" when it is longer than the name column
                            // (Chart.js gives it about a third of the chart width, ~6.5 px per character at 13 px).
                            // The full name is the title of the tooltip.
                            y: { grid: { display: false }, ticks: { color: colors.text, autoSkip: false, callback: function (v) {
                                const chars = Math.max(14, Math.floor(this.chart.width * 0.3 / 6.5));
                                const name = this.getLabelForValue(v);
                                return name.length > chars ? name.slice(0, chars - 1).trimEnd() + "…" : name;
                            } } }
                        }
                    }
                }
            };
        }
    };
    builders.grouped = builders.submissions; // old name of the submissions chart

    figures.forEach(function (figure) {
        const build = builders[figure.dataset.chart];
        const canvas = figure.querySelector("canvas");
        const box = figure.querySelector(".chart-box");
        const status = figure.querySelector("[data-chart-status]");
        const tableHolder = figure.querySelector("[data-chart-table]");
        const title = figure.querySelector("figcaption h3")?.textContent.trim() || "Chart";
        const params = figure.querySelectorAll("[data-chart-param]");
        if (!build || !canvas) { return; }
        let chart = null;

        function load() {
            const url = new URL(figure.dataset.chartUrl, window.location.origin);
            params.forEach(function (p) { if (p.value) { url.searchParams.set(p.dataset.chartParam, p.value); } });
            status.textContent = "Loading the chart…";
            fetch(url, { credentials: "same-origin", headers: { Accept: "application/json" } })
                .then(function (r) { if (!r.ok) { throw new Error(r.status); } return r.json(); })
                .then(function (json) {
                    if (chart) { chart.destroy(); chart = null; }
                    const labels = json.labels || [];
                    const datasets = json.datasets || [];
                    const hasData = labels.length > 0 && datasets.some(d => d.data.some(v => v > 0));
                    if (!hasData) {
                        box.hidden = true;
                        tableHolder.replaceChildren();
                        status.textContent = "Nothing to show for this choice.";
                        return;
                    }
                    box.hidden = false;
                    const built = build({ ...json, labels, datasets }, figure);
                    if (built.height) { box.style.height = built.height; }
                    canvas.setAttribute("aria-label", `${title}: ${built.summary} The figures are in the table that follows.`);
                    renderTable(tableHolder, title, built.tableLabels, built.tableSets, built.format);
                    chart = new Chart(canvas, built.config);
                    status.textContent = built.summary;
                })
                .catch(function () {
                    box.hidden = true;
                    status.textContent = "The chart could not be loaded. Reload the page to try again.";
                });
        }

        params.forEach(function (p) { p.addEventListener("change", load); });
        load();
    });
})();
