// Timeline milestone rows of the competition create / edit form (Areas/Staff/Views/Competitions/_MilestoneRows.cshtml).
// "Add milestone" copies the <template> with a fresh key (bound through Milestones.Index, so keys need not be
// consecutive). "Remove" drops a new row, or marks an existing milestone Remove=true and greys it out (click again
// to keep it).
(function () {
    "use strict";
    document.querySelectorAll("[data-milestone-rows]").forEach(function (box) {
        const list = box.querySelector("[data-milestone-list]");
        const template = box.querySelector("[data-milestone-template]");
        let next = 0;

        box.querySelector("[data-milestone-add]").addEventListener("click", function () {
            const key = "n" + (next++);
            list.insertAdjacentHTML("beforeend", template.innerHTML.replaceAll("__i__", key));
            list.lastElementChild.querySelector("input[type=date]").focus();
        });

        list.addEventListener("click", function (e) {
            const button = e.target.closest("[data-milestone-remove]");
            if (!button) { return; }
            const row = button.closest("[data-milestone-row]");
            const flag = row.querySelector("[data-milestone-remove-flag]");
            if (!flag) { row.remove(); return; }
            const removed = flag.value !== "true";
            flag.value = removed ? "true" : "false";
            row.classList.toggle("is-removed", removed);
            button.setAttribute("aria-label", removed ? "Keep this milestone" : "Remove this milestone");
            button.querySelector(".bi").className = removed ? "bi bi-arrow-counterclockwise" : "bi bi-x-lg";
        });
    });
})();
