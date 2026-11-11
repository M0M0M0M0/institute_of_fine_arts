// Institute of Fine Arts – shared behaviour of every page (FE1-01, FE2-01).
// 1. Menus on small screens (navy rail of the working screens, public top bar).
// 2. Confirmations with SweetAlert2: <form data-confirm="Delete this competition?" data-confirm-ok="Delete"> asks before submitting.
// 3. Image fallback: a painting that cannot be loaded shows a quiet "Image not available" note instead of a broken icon.
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
})();
