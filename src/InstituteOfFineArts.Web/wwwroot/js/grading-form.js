// Grading form of a judge (Areas/Teacher/Views/Evaluations/Index.cshtml): shows the weighted total while typing
// (Σ score × weight / 100 – the server calculates it again) and hides the scores when "Disqualify" is ticked.
(function () {
    "use strict";
    const form = document.querySelector("[data-grading-form]");
    if (!form) { return; }
    const scores = form.querySelector("[data-scores]");
    const total = form.querySelector("[data-score-total]");
    const disqualify = form.querySelector("[data-disqualify]");

    function update() {
        let sum = 0, missing = false;
        form.querySelectorAll("input[data-weight]").forEach(function (input) {
            const v = parseFloat(input.value);
            if (isNaN(v)) { missing = true; return; }
            sum += Math.min(100, Math.max(0, v)) * parseFloat(input.dataset.weight) / 100;
        });
        total.textContent = missing ? "–" : (Math.round(sum * 100) / 100).toFixed(2);
    }

    function toggle() {
        const off = disqualify && disqualify.checked;
        scores.hidden = !!off;
        scores.querySelectorAll("input, textarea").forEach(function (el) { el.disabled = !!off; });
    }

    form.addEventListener("input", update);
    if (disqualify) { disqualify.addEventListener("change", toggle); }
    if (form.querySelector("input[name='Form.Disqualified'][type=hidden][value=true]")) {
        scores.hidden = true;
    }
    toggle();
    update();
})();
