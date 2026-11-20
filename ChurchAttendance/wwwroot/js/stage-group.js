// Filters a group <select> by the stage chosen in another <select>.
// Usage: <select data-group-for="#StageId"> with <option data-stage="{stageId}">.
(function () {
    'use strict';

    document.querySelectorAll('select[data-group-for]').forEach(function (groupSel) {
        var stageSel = document.querySelector(groupSel.getAttribute('data-group-for'));
        if (!stageSel) return;

        function apply() {
            var stage = stageSel.value;
            Array.prototype.forEach.call(groupSel.options, function (opt) {
                var optStage = opt.getAttribute('data-stage');
                var show = !opt.value || !optStage || !stage || optStage === stage;
                opt.hidden = !show;
                opt.disabled = !show;
            });

            var current = groupSel.options[groupSel.selectedIndex];
            if (current && current.disabled) groupSel.value = '';
        }

        stageSel.addEventListener('change', apply);
        apply();
    });
})();
