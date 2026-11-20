// Attendance registration: barcode/code input, camera QR scanning (jsQR) and manual search.
// Every result shown comes from the server response, so "success" only appears after the record is saved.
(function () {
    'use strict';

    var app = document.getElementById('scanApp');
    if (!app) return;

    var METHOD = { QR_CAMERA: 1, BARCODE: 2, CODE: 3, MANUAL: 4 };
    var sessionId = app.dataset.sessionId;
    var registerUrl = app.dataset.registerUrl;
    var searchUrl = app.dataset.searchUrl;
    var tokenInput = app.querySelector('input[name="__RequestVerificationToken"]');

    var resultBox = document.getElementById('scanResult');
    var recentBox = document.getElementById('recentScans');
    var counter = document.getElementById('registeredCount');

    // ---------- helpers ----------
    function showResult(res) {
        var kind = res.success ? 'alert-success' : (res.duplicate ? 'alert-warning' : 'alert-danger');
        resultBox.className = 'alert mb-3 ' + kind;
        resultBox.textContent = res.message || 'حدث خطأ غير متوقع.';

        if (typeof res.registered === 'number' && counter) counter.textContent = res.registered;

        if (res.success) {
            if (navigator.vibrate) navigator.vibrate(80);
            addRecent(res);
        } else if (navigator.vibrate) {
            navigator.vibrate([60, 40, 60]);
        }
    }

    function addRecent(res) {
        var row = document.createElement('div');
        row.className = 'recent-item';

        var name = document.createElement('strong');
        name.textContent = res.studentName || '';

        var meta = document.createElement('span');
        meta.className = 'text-muted';
        meta.textContent = ' — ' + (res.studentCode || '') + ' — ' + (res.status || '') + ' — ' + (res.time || '');

        row.appendChild(name);
        row.appendChild(meta);
        recentBox.insertBefore(row, recentBox.firstChild);
        while (recentBox.children.length > 8) recentBox.removeChild(recentBox.lastChild);
    }

    function post(params) {
        return fetch(registerUrl, {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': tokenInput ? tokenInput.value : ''
            },
            body: new URLSearchParams(params)
        }).then(function (r) {
            if (!r.ok) throw new Error('HTTP ' + r.status);
            return r.json();
        });
    }

    function register(params) {
        params.sessionId = sessionId;
        return post(params).then(function (res) { showResult(res); return res; })
            .catch(function () {
                showResult({ success: false, message: 'تعذر الاتصال بالخادم أو انتهت الجلسة. أعد تحميل الصفحة وحاول مرة أخرى.' });
            });
    }

    // ---------- 1. barcode scanner / student code ----------
    var codeForm = document.getElementById('codeForm');
    var codeInput = document.getElementById('codeInput');
    var codeMethod = document.getElementById('codeMethod');

    if (codeForm) {
        codeForm.addEventListener('submit', function (e) {
            e.preventDefault();
            var value = codeInput.value.trim();
            if (!value) return;
            codeInput.value = '';
            register({ identifier: value, method: codeMethod.value }).then(function () { codeInput.focus(); });
        });
        codeInput.focus();
    }

    // ---------- 2. camera QR ----------
    var video = document.getElementById('cameraVideo');
    var hint = document.getElementById('cameraHint');
    var startBtn = document.getElementById('cameraStart');
    var stopBtn = document.getElementById('cameraStop');
    var canvas = document.createElement('canvas');
    var ctx = canvas.getContext('2d', { willReadFrequently: true });
    var stream = null, scanning = false, lastCode = '', lastAt = 0, busy = false;

    function setHint(text) { hint.textContent = text; hint.style.display = text ? 'block' : 'none'; }

    function stopCamera() {
        scanning = false;
        if (stream) { stream.getTracks().forEach(function (t) { t.stop(); }); stream = null; }
        video.srcObject = null;
        startBtn.disabled = false;
        stopBtn.disabled = true;
        setHint('اضغط تشغيل الكاميرا ووجّه الرمز نحوها');
    }

    function tick() {
        if (!scanning) return;

        if (!busy && video.readyState === video.HAVE_ENOUGH_DATA && window.jsQR) {
            canvas.width = video.videoWidth;
            canvas.height = video.videoHeight;
            ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
            var img = ctx.getImageData(0, 0, canvas.width, canvas.height);
            var code = window.jsQR(img.data, img.width, img.height, { inversionAttempts: 'dontInvert' });

            if (code && code.data) {
                var now = Date.now();
                // Ignore the same code for 3 seconds so one badge is not submitted repeatedly.
                if (code.data !== lastCode || now - lastAt > 3000) {
                    lastCode = code.data;
                    lastAt = now;
                    busy = true;
                    register({ identifier: code.data, method: METHOD.QR_CAMERA }).then(function () { busy = false; });
                }
            }
        }
        requestAnimationFrame(tick);
    }

    if (startBtn) {
        startBtn.addEventListener('click', function () {
            if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
                setHint('الكاميرا غير مدعومة في هذا المتصفح، أو أن الصفحة ليست عبر HTTPS.');
                return;
            }
            if (!window.jsQR) {
                setHint('تعذر تحميل مكتبة قراءة QR. تحقق من الاتصال بالإنترنت.');
                return;
            }

            navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' }, audio: false })
                .then(function (s) {
                    stream = s;
                    video.srcObject = s;
                    return video.play();
                })
                .then(function () {
                    scanning = true;
                    startBtn.disabled = true;
                    stopBtn.disabled = false;
                    setHint('');
                    requestAnimationFrame(tick);
                })
                .catch(function () {
                    stopCamera();
                    setHint('لم يتم السماح باستخدام الكاميرا. اسمح بالوصول للكاميرا من إعدادات المتصفح أو استخدم طريقة أخرى.');
                });
        });

        stopBtn.addEventListener('click', stopCamera);
        document.addEventListener('visibilitychange', function () { if (document.hidden) stopCamera(); });
        window.addEventListener('pagehide', stopCamera);
    }

    // ---------- 3. manual search ----------
    var manualInput = document.getElementById('manualSearch');
    var manualResults = document.getElementById('manualResults');
    var timer = null;

    function renderResults(items) {
        manualResults.textContent = '';
        if (!items.length) {
            var empty = document.createElement('div');
            empty.className = 'text-muted small p-2';
            empty.textContent = 'لا توجد نتائج ضمن نطاق هذه الجلسة.';
            manualResults.appendChild(empty);
            return;
        }

        items.forEach(function (s) {
            var row = document.createElement('div');
            row.className = 'list-group-item d-flex justify-content-between align-items-center gap-2';

            var info = document.createElement('div');
            var nm = document.createElement('div');
            nm.className = 'fw-semibold';
            nm.textContent = s.name;
            var sub = document.createElement('div');
            sub.className = 'small text-muted';
            sub.textContent = s.code + ' — ' + s.stage;
            info.appendChild(nm);
            info.appendChild(sub);

            var btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'btn btn-sm ' + (s.registered ? 'btn-outline-secondary' : 'btn-primary');
            btn.textContent = s.registered ? 'مسجَّل' : 'تسجيل';
            btn.disabled = s.registered;
            btn.addEventListener('click', function () {
                btn.disabled = true;
                register({ studentId: s.id, method: METHOD.MANUAL }).then(function (res) {
                    if (res && (res.success || res.duplicate)) {
                        btn.textContent = 'مسجَّل';
                        btn.className = 'btn btn-sm btn-outline-secondary';
                    } else {
                        btn.disabled = false;
                    }
                });
            });

            row.appendChild(info);
            row.appendChild(btn);
            manualResults.appendChild(row);
        });
    }

    if (manualInput) {
        manualInput.addEventListener('input', function () {
            clearTimeout(timer);
            var term = manualInput.value.trim();
            if (!term) { manualResults.textContent = ''; return; }

            timer = setTimeout(function () {
                var url = searchUrl + '?sessionId=' + encodeURIComponent(sessionId) + '&term=' + encodeURIComponent(term);
                fetch(url, { credentials: 'same-origin' })
                    .then(function (r) { if (!r.ok) throw new Error(); return r.json(); })
                    .then(renderResults)
                    .catch(function () {
                        manualResults.textContent = '';
                        var err = document.createElement('div');
                        err.className = 'text-danger small p-2';
                        err.textContent = 'تعذر تنفيذ البحث. حاول مرة أخرى.';
                        manualResults.appendChild(err);
                    });
            }, 300);
        });
    }
})();
