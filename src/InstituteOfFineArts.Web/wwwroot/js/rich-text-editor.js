// Rich text editor (TinyMCE 7, self-hosted under /lib/tinymce, GPL licence) for the textareas marked
// data-rich-text: Description and Rules of the competition form (TODO step 8, BR-30).
// The editor only offers what the server keeps (Helpers/RichText.cs): paragraphs, headings, bold / italic / underline /
// strike, lists, quotes, links and tables. Pasted text is cut down to the same tags. The server sanitizes again.
// Under each editor: "n / 4000 characters" (text without the formatting), red when over the limit.
(function () {
    "use strict";
    if (!window.tinymce) { return; }
    const MAX = 4000;

    document.querySelectorAll("textarea[data-rich-text]").forEach(function (area) {
        const label = document.querySelector("label[for='" + area.id + "']");
        const counter = document.createElement("div");
        counter.className = "form-text rich-text-count";
        counter.setAttribute("aria-live", "polite");
        area.insertAdjacentElement("afterend", counter);

        tinymce.init({
            target: area,
            license_key: "gpl",
            base_url: "/lib/tinymce",
            suffix: ".min",
            menubar: false,
            branding: false,
            promotion: false,
            plugins: "lists advlist link autolink table wordcount",
            toolbar: "blocks | bold italic underline strikethrough | bullist numlist blockquote | link table | removeformat | undo redo",
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
            height: 300,
            min_height: 200,
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
                    counter.textContent = count + " / " + MAX + " characters";
                    counter.classList.toggle("text-danger", count > MAX);
                }
                editor.on("init", update);
                editor.on("input change undo redo SetContent keyup", update);
            },
        });
    });
})();
