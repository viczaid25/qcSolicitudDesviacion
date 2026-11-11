/* ══════════════════════════════════════════════════════════════
   qcDesvCharts — envoltura delgada sobre Chart.js para el dashboard
   de estadísticas (Admin/Estadisticas.razor). Cada gráfica se crea
   una sola vez por canvas; si Blazor vuelve a llamar render() sobre
   el mismo id (p.ej. tras la reconexión del circuito), se destruye
   la instancia previa antes de crear la nueva para no duplicar.
══════════════════════════════════════════════════════════════ */
(function () {
    'use strict';

    var instancias = {};

    var PALETA = [
        '#2563eb', '#10b981', '#f59e0b', '#ef4444',
        '#7c3aed', '#06b6d4', '#ec4899', '#71717a', '#1e3a8a'
    ];

    function colorFondo(n, alpha) {
        var colores = [];
        for (var i = 0; i < n; i++) {
            colores.push(hexConAlpha(PALETA[i % PALETA.length], alpha));
        }
        return colores;
    }

    function hexConAlpha(hex, alpha) {
        var r = parseInt(hex.slice(1, 3), 16);
        var g = parseInt(hex.slice(3, 5), 16);
        var b = parseInt(hex.slice(5, 7), 16);
        return 'rgba(' + r + ',' + g + ',' + b + ',' + alpha + ')';
    }

    function destroy(canvasId) {
        if (instancias[canvasId]) {
            instancias[canvasId].destroy();
            delete instancias[canvasId];
        }
    }

    function donut(canvasId, labels, data) {
        destroy(canvasId);
        var ctx = document.getElementById(canvasId);
        if (!ctx || typeof Chart === 'undefined') return;

        instancias[canvasId] = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: labels,
                datasets: [{
                    data: data,
                    backgroundColor: colorFondo(labels.length, 0.85),
                    borderColor: '#fff',
                    borderWidth: 2
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '62%',
                plugins: {
                    legend: { position: 'bottom', labels: { boxWidth: 12, font: { size: 11 } } }
                }
            }
        });
    }

    function barVertical(canvasId, labels, data) {
        destroy(canvasId);
        var ctx = document.getElementById(canvasId);
        if (!ctx || typeof Chart === 'undefined') return;

        instancias[canvasId] = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    data: data,
                    backgroundColor: colorFondo(labels.length, 0.85),
                    borderRadius: 6,
                    maxBarThickness: 46
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: false } },
                scales: {
                    x: { grid: { display: false }, ticks: { font: { size: 11 } } },
                    y: { beginAtZero: true, ticks: { precision: 0, font: { size: 11 } } }
                }
            }
        });
    }

    function barHorizontal(canvasId, labels, data) {
        destroy(canvasId);
        var ctx = document.getElementById(canvasId);
        if (!ctx || typeof Chart === 'undefined') return;

        instancias[canvasId] = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    data: data,
                    backgroundColor: colorFondo(labels.length, 0.85),
                    borderRadius: 6
                }]
            },
            options: {
                indexAxis: 'y',
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: false } },
                scales: {
                    x: { beginAtZero: true, ticks: { precision: 0, font: { size: 11 } } },
                    y: { grid: { display: false }, ticks: { font: { size: 11 } } }
                }
            }
        });
    }

    function linea(canvasId, labels, data) {
        destroy(canvasId);
        var ctx = document.getElementById(canvasId);
        if (!ctx || typeof Chart === 'undefined') return;

        instancias[canvasId] = new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [{
                    data: data,
                    borderColor: '#2563eb',
                    backgroundColor: hexConAlpha('#2563eb', 0.12),
                    fill: true,
                    tension: 0.35,
                    pointRadius: 3,
                    pointBackgroundColor: '#2563eb'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: false } },
                scales: {
                    x: { grid: { display: false }, ticks: { font: { size: 11 } } },
                    y: { beginAtZero: true, ticks: { precision: 0, font: { size: 11 } } }
                }
            }
        });
    }

    window.qcDesvCharts = {
        donut: donut,
        barVertical: barVertical,
        barHorizontal: barHorizontal,
        linea: linea
    };
})();
