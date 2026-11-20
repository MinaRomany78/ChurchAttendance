(function () {
    'use strict';

    var canvas = document.getElementById('attendanceChart');
    if (!canvas || !window.Chart) return;

    var labels = JSON.parse(canvas.dataset.labels || '[]');
    var values = JSON.parse(canvas.dataset.values || '[]');

    Chart.defaults.font.family = "'Cairo', system-ui, sans-serif";

    new Chart(canvas, {
        type: 'line',
        data: {
            labels: labels,
            datasets: [{
                label: 'نسبة الحضور',
                data: values,
                borderColor: '#12356b',
                backgroundColor: 'rgba(31, 86, 165, 0.12)',
                pointBackgroundColor: '#c8962e',
                pointRadius: 4,
                fill: true,
                tension: 0.3,
                spanGaps: false // days without sessions stay as gaps, not fake 0%
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            scales: {
                y: { min: 0, max: 100, ticks: { callback: function (v) { return v + '%'; } } }
            },
            plugins: {
                legend: { display: false },
                tooltip: {
                    callbacks: {
                        label: function (ctx) {
                            return ctx.parsed.y == null ? 'لا توجد بيانات' : 'نسبة الحضور: ' + ctx.parsed.y + '%';
                        }
                    }
                }
            }
        }
    });
})();
