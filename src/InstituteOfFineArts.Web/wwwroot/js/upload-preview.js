// Institute of Fine Arts – submit / edit entry form helpers (FE1-05).
// 1. Image preview: <input type="file" data-upload-preview="preview-id"> shows the chosen file in the
//    <div id="preview-id" hidden> block (an .artwork-frame with an <img>, [data-preview-name], [data-preview-meta],
//    [data-preview-warn]) with its size and pixel dimensions.
// 1b. Several pictures (BR-18): <input type="file" multiple data-upload-multi="list-id" [data-cover-name="CoverIndex"]
//    [data-max-files="5"]> lists every chosen file in <ul id="list-id"> (thumbnail, size, pixels, warnings) and, with
//    data-cover-name, a "Cover" radio posting the file's index. On the edit form, ticking "Remove" on a current picture
//    frees a place for a new one.
//    Warnings (over 10 MB, not JPG / PNG / WebP, small image, too many pictures) only inform: nothing is blocked here,
//    the server still decides.
// 2. Text type pills: <select data-seg-pills> gets a segmented pill group that drives the same select,
//    so the form still posts the select's name and value (and works without JavaScript).
(function () {
    "use strict";

    const MAX_BYTES = 10 * 1024 * 1024;
    const ACCEPTED = /\.(jpe?g|png|webp)$/i;
    const ACCEPTED_TYPES = ["", "image/jpeg", "image/png", "image/webp"];

    function fileWarnings(file) {
        const warnings = [];
        if (!ACCEPTED.test(file.name) || ACCEPTED_TYPES.indexOf(file.type) < 0) {
            warnings.push("Only JPG, PNG or WebP pictures are accepted; this one will be refused.");
        }
        if (file.size > MAX_BYTES) { warnings.push("This file is larger than 10 MB; it will be refused."); }
        return warnings;
    }
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

            const warnings = fileWarnings(file);

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

    // ---- 1b. Several pictures ----
    document.querySelectorAll("input[type=file][data-upload-multi]").forEach(function (input) {
        const list = document.getElementById(input.dataset.uploadMulti);
        if (!list) { return; }
        const coverName = input.dataset.coverName || null;
        const form = input.form;
        let urls = [];

        // Places left: data-max-files, plus current pictures ticked "Remove" on the edit form
        function placesLeft() {
            const base = parseInt(input.dataset.maxFiles || "5", 10);
            const freed = form ? form.querySelectorAll("[data-remove-image]:checked").length : 0;
            return base + freed;
        }

        function render() {
            urls.forEach(function (u) { URL.revokeObjectURL(u); });
            urls = [];
            list.replaceChildren();
            const files = Array.from(input.files || []);
            const old = form ? form.querySelector("[data-upload-summary]") : null;
            if (old) { old.remove(); }
            if (files.length === 0) { list.hidden = true; return; }

            files.forEach(function (file, i) {
                const li = document.createElement("li");
                li.className = "upload-item";
                const thumb = document.createElement("div");
                thumb.className = "upload-thumb";
                const info = document.createElement("div");
                info.className = "upload-info";
                const name = document.createElement("span");
                name.className = "upload-name";
                name.textContent = file.name;
                const meta = document.createElement("span");
                meta.className = "upload-meta";
                meta.textContent = formatSize(file.size);
                info.append(name, meta);

                const warnings = fileWarnings(file);
                const note = document.createElement("span");
                note.className = "upload-note";
                note.textContent = warnings.join(" ");
                note.hidden = warnings.length === 0;
                info.append(note);

                if (coverName) {
                    const choices = document.createElement("div");
                    choices.className = "upload-choices";
                    const label = document.createElement("label");
                    label.className = "form-check-label";
                    const radio = document.createElement("input");
                    radio.type = "radio";
                    radio.className = "form-check-input me-1";
                    radio.name = coverName;
                    radio.value = String(i);
                    radio.checked = i === 0;
                    label.append(radio, document.createTextNode("Cover"));
                    choices.append(label);
                    info.append(choices);
                }

                if (file.type.startsWith("image/")) {
                    const img = document.createElement("img");
                    const url = URL.createObjectURL(file);
                    urls.push(url);
                    img.alt = "";
                    img.onload = function () {
                        meta.textContent = formatSize(file.size) + ", " + img.naturalWidth + " × " + img.naturalHeight + " px";
                        if (Math.min(img.naturalWidth, img.naturalHeight) < MIN_SIDE) {
                            warnings.push("The picture is small; at least " + MIN_SIDE + " px on the shortest side looks better.");
                            note.textContent = warnings.join(" ");
                            note.hidden = false;
                        }
                    };
                    img.src = url;
                    thumb.append(img);
                }
                li.append(thumb, info);
                list.append(li);
            });
            list.hidden = false;

            const left = placesLeft();
            if (files.length > left) {
                const p = document.createElement("p");
                p.className = "upload-summary";
                p.setAttribute("data-upload-summary", "");
                p.textContent = left <= 0
                    ? "This entry already has the most pictures allowed; remove one first, or the save will be refused."
                    : "You chose " + files.length + " pictures but only " + left + " more can be added; the save will be refused.";
                list.insertAdjacentElement("afterend", p);
            }
        }

        input.addEventListener("change", render);
        if (form) {
            form.querySelectorAll("[data-remove-image]").forEach(function (box) {
                box.addEventListener("change", function () {
                    const item = box.closest(".upload-item");
                    if (item) { item.classList.toggle("is-removed", box.checked); }
                    render();
                });
            });
        }
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
