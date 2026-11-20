(function () {
    'use strict';

    // Mobile sidebar
    var sidebar = document.getElementById('sidebar');
    var backdrop = document.getElementById('sidebarBackdrop');
    var toggle = document.getElementById('sidebarToggle');

    function setSidebar(open) {
        if (!sidebar) return;
        sidebar.classList.toggle('open', open);
        if (backdrop) backdrop.classList.toggle('show', open);
    }

    if (toggle) toggle.addEventListener('click', function () { setSidebar(!sidebar.classList.contains('open')); });
    if (backdrop) backdrop.addEventListener('click', function () { setSidebar(false); });

    // Toasts rendered from TempData
    document.querySelectorAll('.toast[data-auto-show]').forEach(function (el) {
        new bootstrap.Toast(el, { delay: 5000 }).show();
    });

    // Confirmation for destructive actions: <form data-confirm="message">
    document.addEventListener('submit', function (e) {
        var msg = e.target.getAttribute && e.target.getAttribute('data-confirm');
        if (msg && !window.confirm(msg)) e.preventDefault();
    });

    // Print buttons: <button data-print>
    document.querySelectorAll('[data-print]').forEach(function (btn) {
        btn.addEventListener('click', function () { window.print(); });
    });
})();
