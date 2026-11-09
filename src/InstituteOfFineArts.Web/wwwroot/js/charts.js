// Institute of Fine Arts – dashboard charts (FE2-10), drawn with Chart.js (wwwroot/lib/chart.js) from the JSON
// endpoints of ChartsController: { labels: [...], datasets: [{ label, data: [...] }] }.
//
// Markup (one per chart):
//   <figure class="chart-figure" data-chart="grouped|rating|revenue" data-chart-url="/charts/...">
//     <figcaption><h3 id="x">Title</h3><p>One sentence.</p></figcaption>
//     <div class="chart-box"><canvas role="img" aria-labelledby="x"></canvas></div>
//     <p class="chart-status" data-chart-status>Loading the chart…</p>
//     <div class="visually-hidden" data-chart-table></div>
//   </figure>
//
// Palette of the world: ink for the main series, a mist-derived blue-grey for the second one, stone for "Not graded",
// gilt only for BEST. Accessibility: every chart gets an aria-label summary and a visually hidden data table;
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
        second: "#7e9ca6",                     // mist-derived blue-grey, validated against ink (CVD ΔE 40)
        stone: token("--ifa-line-strong", "#b9b4a8"),
        gilt: token("--ifa-gilt", "#a8814a"),
        danger: token("--ifa-danger", "#a23b2a"),
        text: token("--ifa-text", "#2b2924"),
        muted: token("--ifa-muted", "#625d53"),
        line: token("--ifa-line", "#d6d2c8"),
        wall: token("--ifa-wall", "#ffffff")
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
    const shorten = (s) => (s && s.length > 18 ? s.slice(0, 17) + "…" : s);

    const tooltip = {
        backgroundColor: colors.ink,
        titleColor: "#f3f6f7",
        bodyColor: "#f3f6f7",
        padding: 10,
        cornerRadius: 4,
        boxPadding: 4
    };
    const barBase = { borderRadius: 4, borderSkipped: "start", maxBarThickness: 32, borderWidth: 0 };

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

    function topOf(labels, data) {
        let best = 0;
        data.forEach((v, i) => { if (v > data[best]) { best = i; } });
        return { label: labels[best], value: data[best] };
    }

    const builders = {
        // Submissions and evaluated submissions per competition: two bars per competition, one axis
        grouped: function (json) {
            const [subs, evals] = json.datasets;
            const top = topOf(json.labels, subs.data);
            return {
                summary: `${json.labels.length} competitions; most entries in ${top.label} (${count.format(top.value)}).`,
                format: (v) => count.format(v),
                config: {
                    type: "bar",
                    data: {
                        labels: json.labels,
                        datasets: [
                            { ...barBase, label: subs.label, data: subs.data, backgroundColor: colors.ink },
                            { ...barBase, label: evals ? evals.label : "", data: evals ? evals.data : [], backgroundColor: colors.second }
                        ]
                    },
                    options: {
                        maintainAspectRatio: false,
                        datasets: { bar: { categoryPercentage: .72, barPercentage: .86 } },
                        plugins: { legend: { position: "top", align: "start", labels: { boxWidth: 12, boxHeight: 12, useBorderRadius: true, borderRadius: 2, color: colors.text } }, tooltip },
                        scales: {
                            x: { grid: { display: false }, ticks: { callback: function (v) { return shorten(this.getLabelForValue(v)); } } },
                            y: { beginAtZero: true, ticks: { precision: 0 }, border: { display: false } }
                        }
                    }
                }
            };
        },

        // Rating distribution: one series, horizontal bars; BEST in gilt, "Not graded" in stone
        rating: function (json) {
            const d = json.datasets[0];
            const total = d.data.reduce((a, b) => a + b, 0);
            const best = d.data[json.labels.indexOf("BEST")] || 0;
            return {
                summary: `${count.format(total)} submissions; ${count.format(best)} rated BEST.`,
                format: (v) => count.format(v),
                config: {
                    type: "bar",
                    data: {
                        labels: json.labels,
                        datasets: [{
                            ...barBase, label: d.label, data: d.data,
                            backgroundColor: json.labels.map(l => l === "BEST" ? colors.gilt : l === "Not graded" ? colors.stone : l === "DISQUALIFIED" ? colors.danger : colors.ink)
                        }]
                    },
                    options: {
                        indexAxis: "y",
                        maintainAspectRatio: false,
                        plugins: { legend: { display: false }, tooltip },
                        scales: {
                            x: { beginAtZero: true, ticks: { precision: 0 }, border: { display: false } },
                            y: { grid: { display: false }, ticks: { color: colors.text } }
                        }
                    }
                }
            };
        },

        // Revenue per exhibition (money axis only); paintings sold shown in the tooltip and the table, never a second axis
        revenue: function (json) {
            const rev = json.datasets[0];
            const sold = json.datasets[1];
            const total = rev.data.reduce((a, b) => a + b, 0);
            return {
                summary: `${money.format(total)} of sales over ${json.labels.length} exhibitions.`,
                format: (v, c) => (c === 0 ? money.format(v) : count.format(v)),
                config: {
                    type: "bar",
                    data: { labels: json.labels, datasets: [{ ...barBase, label: rev.label, data: rev.data, backgroundColor: colors.ink }] },
                    options: {
                        maintainAspectRatio: false,
                        plugins: {
                            legend: { display: false },
                            tooltip: {
                                ...tooltip,
                                callbacks: {
                                    label: (ctx) => `${rev.label}: ${money.format(ctx.parsed.y)}`,
                                    afterLabel: (ctx) => (sold ? `${sold.label}: ${count.format(sold.data[ctx.dataIndex])}` : "")
                                }
                            }
                        },
                        scales: {
                            x: { grid: { display: false }, ticks: { callback: function (v) { return shorten(this.getLabelForValue(v)); } } },
                            y: { beginAtZero: true, border: { display: false }, ticks: { callback: (v) => moneyShort.format(v) } }
                        }
                    }
                }
            };
        }
    };

    figures.forEach(function (figure) {
        const build = builders[figure.dataset.chart];
        const canvas = figure.querySelector("canvas");
        const status = figure.querySelector("[data-chart-status]");
        const tableHolder = figure.querySelector("[data-chart-table]");
        const title = figure.querySelector("figcaption h3")?.textContent.trim() || "Chart";
        if (!build || !canvas) { return; }

        fetch(figure.dataset.chartUrl, { credentials: "same-origin", headers: { Accept: "application/json" } })
            .then(function (r) { if (!r.ok) { throw new Error(r.status); } return r.json(); })
            .then(function (json) {
                const labels = json.labels || [];
                const datasets = json.datasets || [];
                const hasData = labels.length > 0 && datasets.some(d => d.data.some(v => v > 0));
                if (!hasData) {
                    figure.querySelector(".chart-box").hidden = true;
                    status.textContent = "Nothing to show yet.";
                    return;
                }
                const chart = build({ labels, datasets });
                canvas.setAttribute("aria-label", `${title}: ${chart.summary} The figures are in the table that follows.`);
                renderTable(tableHolder, title, labels, datasets, chart.format);
                new Chart(canvas, chart.config);
                status.textContent = chart.summary;
            })
            .catch(function () {
                figure.querySelector(".chart-box").hidden = true;
                status.textContent = "The chart could not be loaded. Reload the page to try again.";
            });
    });
})();
