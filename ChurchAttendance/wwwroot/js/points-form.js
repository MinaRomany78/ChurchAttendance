// Fills the points amount with the reason's default when the field is still empty (or was auto-filled).
(function () {
    'use strict';

    var reason = document.getElementById('PointReasonId');
    var points = document.getElementById('Points');
    if (!reason || !points) return;

    var autoFilled = false;

    points.addEventListener('input', function () { autoFilled = false; });

    reason.addEventListener('change', function () {
        var opt = reason.options[reason.selectedIndex];
        var def = opt ? opt.getAttribute('data-points') : null;
        if (def && (points.value === '' || autoFilled)) {
            points.value = def;
            autoFilled = true;
        }
    });
})();
