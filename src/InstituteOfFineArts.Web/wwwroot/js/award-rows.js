// Award rows of the competition create / edit form (Areas/Staff/Views/Competitions/_AwardRows.cshtml).
// "Add award" copies the <template> with a fresh key (MVC binds the list through the Awards.Index fields, so keys
// need not be consecutive) and suggests the next rank. "Remove" drops a new row, or marks an existing award
// Remove=true and greys it out (click again to keep it); the server refuses to remove an award already given.
(function () {
    "use strict";
    document.querySelectorAll("[data-award-rows]").forEach(function (box) {
        const list = box.querySelector("[data-award-list]");
        const template = box.querySelector("[data-award-template]");
        let next = 0;

        box.querySelector("[data-award-add]").addEventListener("click", function () {
            const key = "n" + (next++);
            list.insertAdjacentHTML("beforeend", template.innerHTML.replaceAll("__i__", key));
            const row = list.lastElementChild;
            // Next rank after the highest one on the form
            const ranks = [...list.querySelectorAll("input[name$='.Rank']")]
                .map(function (i) { return parseInt(i.value, 10); })
                .filter(function (n) { return n > 0; });
            row.querySelector("input[name$='.Rank']").value = ranks.length ? Math.max.apply(null, ranks) + 1 : 1;
            row.querySelector("input[name$='.AwardName']").focus();
        });

        list.addEventListener("click", function (e) {
            const button = e.target.closest("[data-award-remove]");
            if (!button) { return; }
            const row = button.closest("[data-award-row]");
            const flag = row.querySelector("[data-award-remove-flag]");
            if (!flag) { row.remove(); return; }            // new row: just drop it
            const removed = flag.value !== "true";          // existing award: toggle the Remove flag
            flag.value = removed ? "true" : "false";
            row.classList.toggle("is-removed", removed);
            button.setAttribute("aria-label", removed ? "Keep this award" : "Remove this award");
            const icon = button.querySelector(".bi");
            icon.className = removed ? "bi bi-arrow-counterclockwise" : "bi bi-x-lg";
        });
    });
})();
