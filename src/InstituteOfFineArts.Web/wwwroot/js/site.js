// Institute of Fine Arts – shared behaviour of every page (FE1-01, FE2-01).
// 1. Menus on small screens (navy rail of the working screens, public top bar).
// 2. Confirmations with SweetAlert2: <form data-confirm="Delete this competition?" data-confirm-ok="Delete"> asks before submitting.
// 3. Image fallback: a painting that cannot be loaded shows a quiet "Image not available" note instead of a broken icon.
// 4. Painting viewer: <div class="artwork-frame" data-zoom data-zoom-download="...?download=true"><img ...></div> opens the
//    painting full screen at its real resolution: wheel / pinch / +/- to zoom, drag or arrow keys to move, double click
//    for 100 %, Esc to close, so teachers can study line work and colour while grading.
(function () {
    "use strict";

    // ---- 1. Menus ----
    const app = document.getElementById("app");
    document.addEventListener("click", function (e) {
        const railToggle = e.target.closest("[data-rail-toggle]");
        if (railToggle && app) {
            const open = app.classList.toggle("is-rail-open");
            railToggle.setAttribute("aria-expanded", String(open));
            return;
        }
        if (e.target.closest("[data-rail-close]") && app) {
            app.classList.remove("is-rail-open");
            document.querySelector("[data-rail-toggle]")?.setAttribute("aria-expanded", "false");
            return;
        }
        const siteToggle = e.target.closest("[data-site-menu]");
        if (siteToggle) {
            const header = document.getElementById("site-header");
            const open = header.classList.toggle("is-open");
            siteToggle.setAttribute("aria-expanded", String(open));
        }
    });
    // Escape closes the rail
    document.addEventListener("keydown", function (e) {
        if (e.key === "Escape" && app && app.classList.contains("is-rail-open")) {
            app.classList.remove("is-rail-open");
            document.querySelector("[data-rail-toggle]")?.focus();
        }
    });

    // ---- 2. Confirmations ----
    // Forms keep working without JavaScript (the server checks everything); with it, a styled dialog asks first.
    document.addEventListener("submit", function (e) {
        const form = e.target;
        if (!(form instanceof HTMLFormElement) || !form.dataset.confirm || form.dataset.confirmed === "1") {
            return;
        }
        // Client-side validation already stopped the submit (missing field…): no dialog for a form that will not be sent
        if (e.defaultPrevented) { return; }
        if (window.jQuery && jQuery.fn.valid && jQuery(form).data("validator") && !jQuery(form).valid()) { return; }
        if (typeof Swal === "undefined") {
            if (!window.confirm(form.dataset.confirm)) { e.preventDefault(); }
            return;
        }
        e.preventDefault();
        const submitter = e.submitter;
        const danger = form.dataset.confirmTone !== "neutral";
        Swal.fire({
            title: form.dataset.confirmTitle || "Please confirm",
            text: form.dataset.confirm,
            showCancelButton: true,
            confirmButtonText: form.dataset.confirmOk || "Confirm",
            cancelButtonText: form.dataset.confirmCancel || "Cancel",
            buttonsStyling: false,
            reverseButtons: true,
            focusCancel: danger,
            customClass: {
                confirmButton: danger ? "btn btn-danger" : "btn btn-primary",
                cancelButton: "btn btn-outline-primary"
            }
        }).then(function (result) {
            if (!result.isConfirmed) { return; }
            form.dataset.confirmed = "1";
            // Keep the clicked button's name/value (e.g. culture=vi-VN, Rating=BEST)
            if (submitter && submitter.name) {
                const hidden = document.createElement("input");
                hidden.type = "hidden";
                hidden.name = submitter.name;
                hidden.value = submitter.value;
                form.appendChild(hidden);
            }
            form.submit();
        });
    });

    // ---- 3. Image fallback ----
    function markMissing(img) {
        if (!(img instanceof HTMLImageElement) || img.dataset.fallbackDone) { return; }
        const frame = img.closest(".artwork-frame");
        if (!frame) { return; }
        img.dataset.fallbackDone = "1";
        img.style.visibility = "hidden";
        const note = document.createElement("span");
        note.className = "artwork-missing";
        note.textContent = "Image not available";
        frame.appendChild(note);
    }
    // Images that fail after this script runs
    document.addEventListener("error", function (e) { markMissing(e.target); }, true);
    // Images that already failed before it ran (scripts load at the end of the page)
    document.querySelectorAll(".artwork-frame img").forEach(function (img) {
        if (img.complete && img.naturalWidth === 0 && img.getAttribute("src")) { markMissing(img); }
    });
    // ---- 4. Painting viewer ----
    const MAX_ZOOM = 8; // ×8 of the real pixels at most
    let viewer = null;

    function buildViewer() {
        const dlg = document.createElement("dialog");
        dlg.className = "zoom-viewer";
        dlg.setAttribute("aria-label", "Painting viewer");
        dlg.innerHTML =
            '<div class="zoom-toolbar">' +
            '<span class="zoom-title"></span>' +
            '<div class="zoom-tools" role="toolbar" aria-label="Zoom">' +
            '<button type="button" data-zv="out" aria-label="Zoom out" title="Zoom out (-)"><i class="bi bi-zoom-out" aria-hidden="true"></i></button>' +
            '<output class="zoom-level num" aria-live="polite">100%</output>' +
            '<button type="button" data-zv="in" aria-label="Zoom in" title="Zoom in (+)"><i class="bi bi-zoom-in" aria-hidden="true"></i></button>' +
            '<button type="button" data-zv="fit" title="Fit to screen (0)"><i class="bi bi-arrows-angle-contract" aria-hidden="true"></i><span>Fit</span></button>' +
            '<button type="button" data-zv="actual" title="Real size: one image pixel per screen pixel (1)"><span>100%</span></button>' +
            '<a data-zv="download" title="Download original"><i class="bi bi-download" aria-hidden="true"></i><span>Original</span></a>' +
            '<button type="button" data-zv="close" aria-label="Close" title="Close (Esc)"><i class="bi bi-x-lg" aria-hidden="true"></i></button>' +
            '</div>' +
            '</div>' +
            '<div class="zoom-stage"><img alt="" draggable="false" /></div>' +
            '<p class="zoom-hint">Scroll or pinch to zoom · drag to move · double click for 100%</p>';
        document.body.appendChild(dlg);

        const v = {
            dlg: dlg, stage: dlg.querySelector(".zoom-stage"), img: dlg.querySelector(".zoom-stage img"),
            level: dlg.querySelector(".zoom-level"), title: dlg.querySelector(".zoom-title"),
            download: dlg.querySelector('[data-zv="download"]'),
            scale: 1, fit: 1, x: 0, y: 0, pointers: new Map(), pinch: null, opener: null
        };

        function apply() {
            v.img.style.transform = "translate(" + v.x + "px," + v.y + "px) scale(" + v.scale + ")";
            v.level.textContent = Math.round(v.scale * 100) + "%";
            v.stage.classList.toggle("is-pixels", v.scale > 1.001);
        }
        // Keeps the picture on screen: centred while smaller than the stage, edges never pulled inside otherwise
        function clamp() {
            const sw = v.stage.clientWidth, sh = v.stage.clientHeight;
            const w = v.img.naturalWidth * v.scale, h = v.img.naturalHeight * v.scale;
            v.x = w <= sw ? (sw - w) / 2 : Math.min(0, Math.max(sw - w, v.x));
            v.y = h <= sh ? (sh - h) / 2 : Math.min(0, Math.max(sh - h, v.y));
        }
        // Zoom to a scale keeping the stage point (px, py) still
        function zoomTo(scale, px, py) {
            const s = Math.max(v.fit, Math.min(MAX_ZOOM, scale));
            if (px === undefined) { px = v.stage.clientWidth / 2; py = v.stage.clientHeight / 2; }
            v.x = px - (px - v.x) * (s / v.scale);
            v.y = py - (py - v.y) * (s / v.scale);
            v.scale = s;
            clamp(); apply();
        }
        function fit() {
            if (!v.img.naturalWidth) { return; }
            v.fit = Math.min(1, v.stage.clientWidth / v.img.naturalWidth, v.stage.clientHeight / v.img.naturalHeight);
            v.scale = v.fit; v.x = 0; v.y = 0;
            clamp(); apply();
        }
        function point(e) {
            const r = v.stage.getBoundingClientRect();
            return { x: e.clientX - r.left, y: e.clientY - r.top };
        }
        v.fitNow = fit;

        v.img.addEventListener("load", function () { v.stage.classList.remove("is-loading"); fit(); });
        v.img.addEventListener("error", function () { v.stage.classList.remove("is-loading"); v.stage.classList.add("is-failed"); });
        window.addEventListener("resize", function () { if (dlg.open) { fit(); } });

        dlg.addEventListener("click", function (e) {
            const tool = e.target.closest("[data-zv]");
            if (!tool) { return; }
            switch (tool.dataset.zv) {
                case "in": zoomTo(v.scale * 1.5); break;
                case "out": zoomTo(v.scale / 1.5); break;
                case "fit": fit(); break;
                case "actual": zoomTo(1); break;
                case "close": dlg.close(); break;
            }
        });
        v.stage.addEventListener("wheel", function (e) {
            e.preventDefault();
            const p = point(e);
            zoomTo(v.scale * Math.exp(-e.deltaY * (e.deltaMode === 1 ? 0.05 : 0.0015)), p.x, p.y);
        }, { passive: false });
        v.stage.addEventListener("dblclick", function (e) {
            const p = point(e);
            if (v.scale < 0.999) { zoomTo(1, p.x, p.y); } else { fit(); }
        });

        // Drag with one pointer, pinch with two (touch screens)
        v.stage.addEventListener("pointerdown", function (e) {
            v.stage.setPointerCapture(e.pointerId);
            v.pointers.set(e.pointerId, point(e));
            v.pinch = null;
            v.stage.classList.add("is-dragging");
        });
        v.stage.addEventListener("pointermove", function (e) {
            if (!v.pointers.has(e.pointerId)) { return; }
            const prev = v.pointers.get(e.pointerId), now = point(e);
            v.pointers.set(e.pointerId, now);
            if (v.pointers.size === 1) {
                v.x += now.x - prev.x; v.y += now.y - prev.y;
                clamp(); apply();
            } else if (v.pointers.size === 2) {
                const pts = Array.from(v.pointers.values());
                const dist = Math.hypot(pts[0].x - pts[1].x, pts[0].y - pts[1].y);
                if (v.pinch) { zoomTo(v.scale * dist / v.pinch, (pts[0].x + pts[1].x) / 2, (pts[0].y + pts[1].y) / 2); }
                v.pinch = dist;
            }
        });
        function release(e) {
            v.pointers.delete(e.pointerId);
            v.pinch = null;
            if (v.pointers.size === 0) { v.stage.classList.remove("is-dragging"); }
        }
        v.stage.addEventListener("pointerup", release);
        v.stage.addEventListener("pointercancel", release);

        dlg.addEventListener("keydown", function (e) {
            const step = 80;
            switch (e.key) {
                case "+": case "=": zoomTo(v.scale * 1.5); break;
                case "-": case "_": zoomTo(v.scale / 1.5); break;
                case "0": fit(); break;
                case "1": zoomTo(1); break;
                case "ArrowLeft": v.x += step; clamp(); apply(); break;
                case "ArrowRight": v.x -= step; clamp(); apply(); break;
                case "ArrowUp": v.y += step; clamp(); apply(); break;
                case "ArrowDown": v.y -= step; clamp(); apply(); break;
                default: return;
            }
            e.preventDefault();
        });
        dlg.addEventListener("close", function () {
            document.documentElement.classList.remove("has-zoom-viewer");
            v.img.removeAttribute("src");
            if (v.opener) { v.opener.focus(); }
        });
        return v;
    }

    function openViewer(frame) {
        const source = frame.querySelector("img");
        if (!source || source.dataset.fallbackDone) { return; }
        viewer = viewer || buildViewer();
        if (typeof viewer.dlg.showModal !== "function") { window.open(source.currentSrc || source.src, "_blank"); return; }
        viewer.opener = frame;
        viewer.title.textContent = source.alt || "";
        viewer.img.alt = source.alt || "";
        const download = frame.dataset.zoomDownload;
        viewer.download.hidden = !download;
        if (download) { viewer.download.href = download; } else { viewer.download.removeAttribute("href"); }
        document.documentElement.classList.add("has-zoom-viewer");
        viewer.dlg.showModal();
        // Hide the previous painting's position while the original loads
        viewer.img.style.transform = "scale(0)";
        viewer.stage.classList.remove("is-failed");
        viewer.stage.classList.add("is-loading");
        viewer.img.src = frame.dataset.zoom || source.currentSrc || source.src;
        if (viewer.img.complete && viewer.img.naturalWidth) { viewer.stage.classList.remove("is-loading"); viewer.fitNow(); }
        viewer.dlg.querySelector('[data-zv="in"]').focus();
    }

    document.querySelectorAll(".artwork-frame[data-zoom]").forEach(function (frame) {
        frame.classList.add("is-zoomable");
        frame.setAttribute("role", "button");
        frame.setAttribute("tabindex", "0");
        frame.setAttribute("aria-label", "View the painting full size and zoom in");
        const badge = document.createElement("span");
        badge.className = "zoom-badge";
        badge.setAttribute("aria-hidden", "true");
        badge.innerHTML = '<i class="bi bi-arrows-fullscreen"></i> Zoom';
        frame.appendChild(badge);
        frame.addEventListener("click", function () { openViewer(frame); });
        frame.addEventListener("keydown", function (e) {
            if (e.key === "Enter" || e.key === " ") { e.preventDefault(); openViewer(frame); }
        });
    });
})();
