// Rich text editor (TinyMCE 7, self-hosted under /lib/tinymce, GPL licence) for every textarea marked data-rich-text
// (BR-50): competition description / rules / result remarks, an entry's description and poem / story, the judges'
// comments, an exhibition's description, an award's remarks. Loaded on every page by _Scripts.cshtml; TinyMCE itself
// is only downloaded when the page has such a textarea.
//   data-rich-text="2000"          most characters of text (tags not counted), shown as "n / 2000 characters"
//   data-rich-text-mode="simple"   short texts: no headings or tables, a smaller box
// The editor only offers what the server keeps (Helpers/RichText.cs); pasted text is cut down to the same tags and the
// server sanitizes again.
(function () {
    "use strict";
    const areas = document.querySelectorAll("textarea[data-rich-text]");
    if (areas.length === 0) { return; }

    // A Bootstrap modal keeps the focus inside itself; let the editor's own dialogs (link, table) take it
    document.addEventListener("focusin", function (e) {
        if (e.target.closest && e.target.closest(".tox-tinymce, .tox-tinymce-aux, .tox-dialog")) {
            e.stopImmediatePropagation();
        }
    }, true);

    function start() {
        areas.forEach(function (area) {
            const max = parseInt(area.getAttribute("data-rich-text"), 10) || 4000;
            const simple = area.getAttribute("data-rich-text-mode") === "simple";
            const label = document.querySelector("label[for='" + area.id + "']");
            const counter = document.createElement("div");
            counter.className = "form-text rich-text-count";
            counter.setAttribute("aria-live", "polite");
            area.insertAdjacentElement("afterend", counter);
            // The browser's own limit would count the tags
            area.removeAttribute("maxlength");

            tinymce.init({
                target: area,
                license_key: "gpl",
                base_url: "/lib/tinymce",
                suffix: ".min",
                menubar: false,
                branding: false,
                promotion: false,
                plugins: simple ? "lists advlist link autolink wordcount" : "lists advlist link autolink table wordcount",
                toolbar: simple
                    ? "bold italic underline strikethrough | bullist numlist blockquote | link | removeformat | undo redo"
                    : "blocks | bold italic underline strikethrough | bullist numlist blockquote | link table | removeformat | undo redo",
                // One toolbar row; in a narrow column the rest goes behind a "…" button instead of covering the text
                toolbar_mode: "sliding",
                block_formats: "Paragraph=p; Heading=h2; Subheading=h3; Small heading=h4",
                // Only the tags the server keeps; anything else (pasted from Word or a web page) is dropped or unwrapped
                valid_elements: "p,br,h2,h3,h4,strong/b,em/i,u,s,ul,ol,li,blockquote,a[href|title],"
                    + "table,thead,tbody,tr,th[colspan|rowspan],td[colspan|rowspan]",
                valid_styles: {},
                invalid_styles: "*",
                table_default_attributes: {},
                table_default_styles: {},
                table_resize_bars: false,
                table_appearance_options: false,
                table_advtab: false,
                table_cell_advtab: false,
                table_row_advtab: false,
                object_resizing: false,
                link_default_target: "_blank",
                link_target_list: false,
                link_title: false,
                link_assume_external_targets: "https",
                convert_urls: false,
                height: simple ? 240 : 300,
                min_height: 150,
                resize: true,
                statusbar: true,
                elementpath: false,
                iframe_aria_text: (label ? label.textContent.trim() : "Text") + " editor",
                content_style: "body { font-family: 'General Sans', 'Segoe UI', system-ui, sans-serif; font-size: 15px; "
                    + "line-height: 1.6; color: #0b2a33; margin: 12px 14px; } p { margin: 0 0 .7em; } "
                    + "table { border-collapse: collapse; } td, th { border: 1px solid #c9d3d6; padding: 4px 8px; }",
                setup: function (editor) {
                    function update() {
                        // Keep the textarea filled, so the form always posts what is on screen
                        editor.save();
                        const count = editor.plugins.wordcount.body.getCharacterCount();
                        counter.textContent = count + " / " + max + " characters";
                        counter.classList.toggle("text-danger", count > max);
                    }
                    editor.on("init", update);
                    editor.on("input change undo redo SetContent keyup", update);
                    // A field the page turns off (e.g. read-only grade) stays read-only in the editor
                    editor.on("init", function () {
                        if (area.disabled || area.readOnly) { editor.mode.set("readonly"); }
                    });
                },
            });
        });
    }

    if (window.tinymce) { start(); return; }
    const script = document.createElement("script");
    script.src = "/lib/tinymce/tinymce.min.js";
    script.onload = start;
    document.head.appendChild(script);
})();
