// Autocomplete for choosing one student. Markup: Views/Shared/_StudentPicker.cshtml
// Selecting a student fills the hidden id input; typing again clears it so a stale id is never posted.
(function () {
    'use strict';

    document.querySelectorAll('[data-student-picker]').forEach(function (root) {
        var url = root.getAttribute('data-lookup-url');
        var idInput = root.querySelector('[data-picker-id]');
        var textInput = root.querySelector('[data-picker-text]');
        var results = root.querySelector('[data-picker-results]');
        var balanceEl = root.querySelector('[data-picker-balance]');
        var timer = null;

        function clearResults() { results.textContent = ''; }

        function select(s) {
            idInput.value = s.id;
            textInput.value = s.name + ' — ' + s.code;
            balanceEl.textContent = Number(s.balance).toLocaleString('en-US');
            clearResults();
            root.dispatchEvent(new CustomEvent('student-selected', { bubbles: true, detail: s }));
        }

        function render(items) {
            clearResults();
            if (!items.length) {
                var empty = document.createElement('div');
                empty.className = 'list-group-item text-muted small';
                empty.textContent = 'لا توجد نتائج.';
                results.appendChild(empty);
                return;
            }

            items.forEach(function (s) {
                var btn = document.createElement('button');
                btn.type = 'button';
                btn.className = 'list-group-item list-group-item-action d-flex justify-content-between align-items-center';

                var left = document.createElement('span');
                var name = document.createElement('strong');
                name.textContent = s.name;
                var meta = document.createElement('span');
                meta.className = 'text-muted small ms-2';
                meta.textContent = s.code + ' — ' + s.stage;
                left.appendChild(name);
                left.appendChild(meta);

                var pts = document.createElement('span');
                pts.className = 'points-chip';
                pts.textContent = Number(s.balance).toLocaleString('en-US');

                btn.appendChild(left);
                btn.appendChild(pts);
                btn.addEventListener('click', function () { select(s); });
                results.appendChild(btn);
            });
        }

        textInput.addEventListener('input', function () {
            idInput.value = '';
            balanceEl.textContent = '—';
            clearTimeout(timer);

            var term = textInput.value.trim();
            if (!term) { clearResults(); return; }

            timer = setTimeout(function () {
                fetch(url + '?term=' + encodeURIComponent(term), { credentials: 'same-origin' })
                    .then(function (r) { if (!r.ok) throw new Error(); return r.json(); })
                    .then(render)
                    .catch(function () {
                        clearResults();
                        var err = document.createElement('div');
                        err.className = 'list-group-item text-danger small';
                        err.textContent = 'تعذر البحث. حاول مرة أخرى.';
                        results.appendChild(err);
                    });
            }, 300);
        });

        document.addEventListener('click', function (e) { if (!root.contains(e.target)) clearResults(); });
    });
})();
