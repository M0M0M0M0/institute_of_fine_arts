// Institute of Fine Arts – submission deadline countdown (FE1-05).
// Any element with data-deadline="2026-11-14T23:59:59" (ISO 8601, institute local time, written by the server)
// shows the time left: "5 days 03:12 left", "03:12 left" (class is-soon under 24 hours), "Deadline passed" (is-over).
// The text goes into the element's [data-countdown-text] child, or the element itself when it has none.
// Without JavaScript the server text (the deadline date) stays; the server always checks the deadline itself.
(function () {
    "use strict";

    const DAY = 24 * 60 * 60 * 1000;
    const items = Array.from(document.querySelectorAll("[data-deadline]"))
        .map(function (el) {
            // "2026-11-14T23:59:59" without a zone is read as local time by the browser
            const deadline = new Date(el.dataset.deadline);
            return { el: el, out: el.querySelector("[data-countdown-text]") || el, deadline: deadline };
        })
        .filter(function (x) { return !isNaN(x.deadline.getTime()); });

    if (items.length === 0) { return; }

    function pad(n) { return String(n).padStart(2, "0"); }

    function render() {
        const now = Date.now();
        items.forEach(function (x) {
            const left = x.deadline.getTime() - now;
            x.el.classList.add("deadline");
            x.el.classList.toggle("is-over", left <= 0);
            x.el.classList.toggle("is-soon", left > 0 && left < DAY);
            if (left <= 0) {
                x.out.textContent = "Deadline passed";
                return;
            }
            const days = Math.floor(left / DAY);
            const hours = Math.floor((left % DAY) / 3600000);
            const minutes = Math.floor((left % 3600000) / 60000);
            const time = pad(hours) + ":" + pad(minutes);
            x.out.textContent = days > 0
                ? days + (days === 1 ? " day " : " days ") + time + " left"
                : time + " left";
        });
    }

    render();
    // Minutes are the smallest unit shown, so a refresh every 20 s is enough
    window.setInterval(render, 20000);
})();
