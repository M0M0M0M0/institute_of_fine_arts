// Grading criteria rows of the competition create / edit form (Areas/Staff/Views/Competitions/_JuryCriteria.cshtml).
// "Add criterion" copies the <template>; "Remove" drops a row; the rows are renumbered Criteria[0..n] so MVC binds
// them as a list; the total of the weights is shown live (it must be exactly 100 – checked again by the server).
(function () {
    "use strict";
    document.querySelectorAll("[data-criteria-rows]").forEach(function (box) {
        const list = box.querySelector("[data-criteria-list]");
        const template = box.querySelector("[data-criterion-template]");
        const total = box.querySelector("[data-criteria-total]");
        const add = box.querySelector("[data-criterion-add]");

        function renumber() {
            list.querySelectorAll("[data-criterion-row]").forEach(function (row, i) {
                row.querySelectorAll("[name^='Criteria[']").forEach(function (input) {
                    input.name = input.name.replace(/^Criteria\[[^\]]*\]/, "Criteria[" + i + "]");
                });
            });
        }

        function updateTotal() {
            let sum = 0;
            list.querySelectorAll("[data-criterion-weight]").forEach(function (input) {
                const v = parseFloat(input.value);
                if (!isNaN(v)) { sum += v; }
            });
            sum = Math.round(sum * 100) / 100;
            total.textContent = sum.toLocaleString("en-US", { maximumFractionDigits: 2 });
            total.classList.toggle("text-danger", sum !== 100 && list.querySelector("[data-criterion-row]") !== null);
            total.classList.toggle("text-success", sum === 100);
        }

        if (add) {
            add.addEventListener("click", function () {
                list.insertAdjacentHTML("beforeend", template.innerHTML.replaceAll("__i__", "x"));
                renumber();
                list.lastElementChild.querySelector("select").focus();
                updateTotal();
            });
        }
        list.addEventListener("click", function (e) {
            const button = e.target.closest("[data-criterion-remove]");
            if (!button) { return; }
            button.closest("[data-criterion-row]").remove();
            renumber();
            updateTotal();
        });
        list.addEventListener("input", updateTotal);
        updateTotal();
    });
})();
