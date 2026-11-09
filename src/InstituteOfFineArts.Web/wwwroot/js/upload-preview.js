// Institute of Fine Arts – submit / edit entry form helpers (FE1-05).
// 1. Image preview: <input type="file" data-upload-preview="preview-id"> shows the chosen file in the
//    <div id="preview-id" hidden> block (an .artwork-frame with an <img>, [data-preview-name], [data-preview-meta],
//    [data-preview-warn]) with its size and pixel dimensions. Warnings (over 5 MB, not a JPEG, small image) only
//    inform: nothing is blocked here, the server still decides.
// 2. Text type pills: <select data-seg-pills> gets a segmented pill group that drives the same select,
//    so the form still posts the select's name and value (and works without JavaScript).
(function () {
    "use strict";

    const MAX_BYTES = 5 * 1024 * 1024;
    const MIN_SIDE = 600;

    function formatSize(bytes) {
        return bytes >= 1024 * 1024
            ? (bytes / (1024 * 1024)).toFixed(1) + " MB"
            : Math.max(1, Math.round(bytes / 1024)).toLocaleString("en-US") + " KB";
    }

    // ---- 1. Image preview ----
    document.querySelectorAll("input[type=file][data-upload-preview]").forEach(function (input) {
        const box = document.getElementById(input.dataset.uploadPreview);
        if (!box) { return; }
        const img = box.querySelector("img");
        const name = box.querySelector("[data-preview-name]");
        const meta = box.querySelector("[data-preview-meta]");
        const warn = box.querySelector("[data-preview-warn]");
        let url = null;

        input.addEventListener("change", function () {
            if (url) { URL.revokeObjectURL(url); url = null; }
            const file = input.files && input.files[0];
            if (!file) { box.hidden = true; return; }

            const warnings = [];
            const isJpeg = /\.(jpe?g)$/i.test(file.name) && (file.type === "" || file.type === "image/jpeg");
            if (!isJpeg) { warnings.push("This does not look like a JPEG file (.jpg, .jpeg); it will be refused."); }
            if (file.size > MAX_BYTES) { warnings.push("This file is larger than 5 MB; it will be refused."); }

            name.textContent = file.name;
            meta.textContent = formatSize(file.size);
            warn.textContent = warnings.join(" ");
            warn.hidden = warnings.length === 0;
            box.hidden = false;

            if (!file.type.startsWith("image/")) { img.removeAttribute("src"); return; }
            url = URL.createObjectURL(file);
            img.onload = function () {
                meta.textContent = formatSize(file.size) + ", " + img.naturalWidth + " × " + img.naturalHeight + " px";
                if (Math.min(img.naturalWidth, img.naturalHeight) < MIN_SIDE) {
                    warnings.push("The image is small; at least " + MIN_SIDE + " px on the shortest side looks better.");
                    warn.textContent = warnings.join(" ");
                    warn.hidden = false;
                }
            };
            img.alt = "Preview of " + file.name;
            img.src = url;
        });
    });

    // ---- 2. Text type pills ----
    document.querySelectorAll("select[data-seg-pills]").forEach(function (select) {
        const group = document.createElement("div");
        group.className = "seg seg-pills";
        group.setAttribute("role", "radiogroup");
        const label = document.querySelector("label[for='" + select.id + "']");
        if (label) {
            label.id = label.id || select.id + "-label";
            group.setAttribute("aria-labelledby", label.id);
        }
        Array.from(select.options).filter(function (o) { return o.value !== ""; }).forEach(function (option, i) {
            const id = select.id + "-pill-" + i;
            const radio = document.createElement("input");
            radio.type = "radio";
            radio.id = id;
            radio.value = option.value;
            // Grouped by name for arrow keys, but owned by no form (form="" points nowhere): only the select is posted
            radio.name = select.id + "-pills";
            radio.setAttribute("form", select.id + "-pills-none");
            radio.checked = option.selected;
            radio.addEventListener("change", function () {
                select.value = option.value;
                select.dispatchEvent(new Event("change", { bubbles: true }));
            });
            const pill = document.createElement("label");
            pill.htmlFor = id;
            pill.textContent = option.textContent;
            group.append(radio, pill);
        });
        select.classList.add("visually-hidden");
        select.setAttribute("tabindex", "-1");
        select.setAttribute("aria-hidden", "true");
        select.insertAdjacentElement("afterend", group);
    });
})();
