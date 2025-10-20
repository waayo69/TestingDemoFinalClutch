// Render Analytics Charts using Chart.js

document.addEventListener('DOMContentLoaded', function () {
    if (!window.analyticsData) return;
    const d = window.analyticsData;

    // Clients by Status (Pie)
    if (document.getElementById('statusChart')) {
        new Chart(document.getElementById('statusChart').getContext('2d'), {
            type: 'pie',
            data: {
                labels: d.statusLabels,
                datasets: [{
                    data: d.statusData,
                    backgroundColor: [
                        '#0d6efd', '#6c757d', '#198754', '#ffc107', '#dc3545', '#0dcaf0', '#6610f2', '#fd7e14'
                    ],
                }]
            },
            options: {
                plugins: { legend: { position: 'bottom' } },
                responsive: true
            }
        });
    }

    // Clients by Project Type (Bar)
    if (document.getElementById('typeChart')) {
        new Chart(document.getElementById('typeChart').getContext('2d'), {
            type: 'bar',
            data: {
                labels: d.typeLabels,
                datasets: [{
                    label: 'Clients',
                    data: d.typeData,
                    backgroundColor: '#0dcaf0',
                }]
            },
            options: {
                plugins: { legend: { display: false } },
                responsive: true,
                scales: { y: { beginAtZero: true } }
            }
        });
    }

    // Clients by Urgency (Pie)
    if (document.getElementById('urgencyChart')) {
        new Chart(document.getElementById('urgencyChart').getContext('2d'), {
            type: 'pie',
            data: {
                labels: d.urgencyLabels,
                datasets: [{
                    data: d.urgencyData,
                    backgroundColor: [
                        '#ffc107', '#dc3545', '#198754', '#0d6efd', '#6c757d', '#0dcaf0', '#6610f2', '#fd7e14'
                    ],
                }]
            },
            options: {
                plugins: { legend: { position: 'bottom' } },
                responsive: true
            }
        });
    }

    // Monthly Client Trends (Line)
    if (document.getElementById('monthlyChart')) {
        new Chart(document.getElementById('monthlyChart').getContext('2d'), {
            type: 'line',
            data: {
                labels: d.monthLabels,
                datasets: [{
                    label: 'Clients Created',
                    data: d.monthData,
                    fill: false,
                    borderColor: '#0d6efd',
                    backgroundColor: '#0d6efd',
                    tension: 0.3
                }]
            },
            options: {
                plugins: { legend: { display: true } },
                responsive: true,
                scales: { y: { beginAtZero: true } }
            }
        });
    }

    // Urgent Requests Trend (Line)
    if (document.getElementById('urgentTrendChart')) {
        new Chart(document.getElementById('urgentTrendChart').getContext('2d'), {
            type: 'line',
            data: {
                labels: d.urgentTrendLabels,
                datasets: [{
                    label: 'Urgent Requests',
                    data: d.urgentTrendData,
                    fill: false,
                    borderColor: '#dc3545',
                    backgroundColor: '#dc3545',
                    tension: 0.3
                }]
            },
            options: {
                plugins: { legend: { display: true } },
                responsive: true,
                scales: { y: { beginAtZero: true } }
            }
        });
    }

    // Requests Heatmap (Bar with color intensity)
    if (document.getElementById('heatmapChart')) {
        const maxCount = Math.max(...d.heatmapData, 1);
        new Chart(document.getElementById('heatmapChart').getContext('2d'), {
            type: 'bar',
            data: {
                labels: d.heatmapLabels,
                datasets: [{
                    label: 'Requests',
                    data: d.heatmapData,
                    backgroundColor: d.heatmapData.map(count => `rgba(13,110,253,${0.2 + 0.8 * (count / maxCount)})`),
                }]
            },
            options: {
                plugins: { legend: { display: false } },
                responsive: true,
                scales: {
                    y: { beginAtZero: true },
                    x: { display: false }
                },
                elements: { bar: { borderRadius: 2 } }
            }
        });
    }

    // Supplier Count by Business Type (Pie)
    if (document.getElementById('supplierBusinessTypeChart')) {
        new Chart(document.getElementById('supplierBusinessTypeChart').getContext('2d'), {
            type: 'pie',
            data: {
                labels: d.supplierBusinessTypeLabels,
                datasets: [{
                    data: d.supplierBusinessTypeData,
                    backgroundColor: [
                        '#0d6efd', '#6c757d', '#198754', '#ffc107', '#dc3545', '#0dcaf0', '#6610f2', '#fd7e14', '#6f42c1', '#20c997'
                    ],
                }]
            },
            options: {
                plugins: { legend: { position: 'bottom' } },
                responsive: true
            }
        });
    }

    // Most Common Products Offered (Bar)
    if (document.getElementById('productsOfferedChart')) {
        new Chart(document.getElementById('productsOfferedChart').getContext('2d'), {
            type: 'bar',
            data: {
                labels: d.productsOfferedLabels,
                datasets: [{
                    label: 'Vendors',
                    data: d.productsOfferedData,
                    backgroundColor: '#fd7e14',
                }]
            },
            options: {
                plugins: { legend: { display: false } },
                responsive: true,
                scales: { y: { beginAtZero: true } }
            }
        });
    }

    // External Audit Purposes (Bar)
    if (document.getElementById('externalAuditPurposeChart')) {
        new Chart(document.getElementById('externalAuditPurposeChart').getContext('2d'), {
            type: 'bar',
            data: {
                labels: d.externalAuditPurposeLabels,
                datasets: [{
                    label: 'Audits',
                    data: d.externalAuditPurposeData,
                    backgroundColor: '#198754',
                }]
            },
            options: {
                plugins: { legend: { display: false } },
                responsive: true,
                scales: { y: { beginAtZero: true } }
            }
        });
    }

    // Upcoming External Audit Deadlines (Timeline/Bar)
    if (document.getElementById('auditDeadlineTimeline')) {
        new Chart(document.getElementById('auditDeadlineTimeline').getContext('2d'), {
            type: 'bar',
            data: {
                labels: d.auditDeadlineLabels,
                datasets: [{
                    label: 'Upcoming Deadlines',
                    data: d.auditDeadlineData,
                    backgroundColor: '#0dcaf0',
                }]
            },
            options: {
                plugins: { legend: { display: false } },
                responsive: true,
                scales: {
                    y: { beginAtZero: true, display: false },
                    x: { display: true }
                },
                elements: { bar: { borderRadius: 2 } }
            }
        });
    }

    // Projects by Client Type & Status (Stacked Bar)
    if (document.getElementById('stackedClientTypeStatusChart')) {
        const datasets = d.stackedStatusLabels.map((status, i) => ({
            label: status,
            data: d.stackedClientTypeStatusData.map(row => row[i]),
            backgroundColor: [
                '#0d6efd', '#6c757d', '#198754', '#ffc107', '#dc3545', '#0dcaf0', '#6610f2', '#fd7e14', '#6f42c1', '#20c997'
            ][i % 10]
        }));
        new Chart(document.getElementById('stackedClientTypeStatusChart').getContext('2d'), {
            type: 'bar',
            data: {
                labels: d.stackedClientTypeLabels,
                datasets: datasets
            },
            options: {
                plugins: { legend: { position: 'bottom' } },
                responsive: true,
                scales: {
                    x: { stacked: true },
                    y: { stacked: true, beginAtZero: true }
                }
            }
        });
    }

    // Approval Funnel / Conversion Rate (Horizontal Bar)
    if (document.getElementById('approvalFunnelChart')) {
        new Chart(document.getElementById('approvalFunnelChart').getContext('2d'), {
            type: 'bar',
            data: {
                labels: d.approvalFunnelLabels,
                datasets: [{
                    label: 'Requests',
                    data: d.approvalFunnelData,
                    backgroundColor: ['#0d6efd', '#ffc107', '#198754'],
                }]
            },
            options: {
                indexAxis: 'y',
                plugins: { legend: { display: false } },
                responsive: true,
                scales: { x: { beginAtZero: true } }
            }
        });
    }

    // Drill-down logic
    const filteredClients = JSON.parse(document.getElementById('filteredClientsJson').textContent);
    const drilldownTableContainer = document.getElementById('drilldown-table-container');
    const drilldownFilterDiv = document.getElementById('drilldown-active-filter');
    let activeDrilldown = null;

    function renderDrilldownTable(clients) {
        let html = `<table class=\"table table-striped table-bordered mb-0\"><thead class=\"table-light\"><tr>
            <th>Client</th><th>Type</th><th>Urgency</th><th>Days Pending</th><th>Status</th><th>Supplier Name</th><th>Business Type</th><th>Created</th></tr></thead><tbody>`;
        if (!clients || clients.length === 0) {
            html += `<tr><td colspan='8' class='text-center text-muted'>No data available</td></tr>`;
        } else {
            for (const c of clients) {
                const daysPending = c.createdDate ? Math.floor((new Date() - new Date(c.createdDate)) / 86400000) : '';
                html += `<tr><td>${c.clientName || ''}</td><td>${c.typeOfProject || ''}</td><td>${c.urgencyLevel || ''}</td><td>${daysPending}</td><td>${c.status || ''}</td><td>${c.requestingParty || ''}</td><td>${c.clientType || ''}</td><td>${c.createdDate ? new Date(c.createdDate).toLocaleDateString() : ''}</td></tr>`;
            }
        }
        html += '</tbody></table>';
        drilldownTableContainer.innerHTML = html;
    }
    function showDrilldownFilter(label) {
        drilldownFilterDiv.innerHTML = `${label} <button class='btn btn-sm btn-outline-secondary ms-2' id='clearDrilldownBtn'>Clear</button>`;
        drilldownFilterDiv.classList.remove('d-none');
        document.getElementById('clearDrilldownBtn').onclick = function () {
            activeDrilldown = null;
            renderDrilldownTable(filteredClients);
            drilldownFilterDiv.classList.add('d-none');
        };
    }
    // Initial render
    renderDrilldownTable(filteredClients);

    // Chart drill-down handlers
    function addDrilldown(chart, field, labelPrefix) {
        chart.options.onClick = function (evt, elements) {
            if (elements && elements.length > 0) {
                const idx = elements[0].index;
                const value = chart.data.labels[idx];
                activeDrilldown = { field, value };
                const filtered = filteredClients.filter(c => (c[field] || '') === value);
                renderDrilldownTable(filtered);
                showDrilldownFilter(`${labelPrefix}: <b>${value}</b>`);
            }
        };
    }
    // Wait for all charts to be created, then add drilldown
    setTimeout(() => {
        if (window.statusChart) addDrilldown(window.statusChart, 'status', 'Status');
        if (window.typeChart) addDrilldown(window.typeChart, 'typeOfProject', 'Project Type');
        if (window.urgencyChart) addDrilldown(window.urgencyChart, 'urgencyLevel', 'Urgency');
        if (window.supplierBusinessTypeChart) addDrilldown(window.supplierBusinessTypeChart, 'clientType', 'Business Type');
    }, 500);

    // TODO: Add more analytics visualizations here as needed
}); 