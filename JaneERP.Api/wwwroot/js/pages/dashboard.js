const DashboardPage = (() => {
  let currentDays = 30;

  async function render(container) {
    const session = Api.getSession();
    container.innerHTML = `
      <div class="page">
        <div class="page-header">
          <h1>Dashboard</h1>
          <div class="header-actions">
            <button class="btn-icon" id="dash-logout" title="Sign out">
              <svg viewBox="0 0 24 24"><path d="M17 7l-1.41 1.41L18.17 11H8v2h10.17l-2.58 2.58L17 17l5-5-5-5zM4 5h8V3H4c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h8v-2H4V5z"/></svg>
            </button>
          </div>
        </div>
        <div class="content" id="dash-content">
          <div class="kpi-grid">${App.skeletonCards(6)}</div>
        </div>
      </div>`;

    document.getElementById('dash-logout').addEventListener('click', () => {
      Api.clearSession();
      App.navigate('login');
    });

    await loadDashboard(currentDays, container);
  }

  async function loadDashboard(days, container) {
    try {
      const d = await Api.get(`/api/dashboard?days=${days}`);
      document.getElementById('dash-content').innerHTML = buildDashboard(d, days);
      bindDays(container);
      bindLists();
    } catch (err) {
      document.getElementById('dash-content').innerHTML =
        `<div class="empty-state"><p>Could not load dashboard.<br>${err.message}</p></div>`;
    }
  }

  function buildDashboard(d, days) {
    const salesClass   = d.salesTotal > 0    ? 'success' : '';
    const lowClass     = d.lowStockItems > 0  ? 'danger'  : 'success';
    const packClass    = d.ordersToPack > 0   ? 'warn'    : '';
    const recvClass    = d.itemsToReceive > 0 ? 'warn'    : '';
    const overdueClass = d.overduePOs > 0     ? 'danger'  : '';
    const ccClass      = d.overdueCycleCount > 0 ? 'warn' : '';

    return `
      <div class="days-toggle">
        ${[7,14,30,90].map(n => `
          <button class="days-btn${n === days ? ' active' : ''}" data-days="${n}">${n}d</button>
        `).join('')}
      </div>

      <div class="kpi-grid">
        <div class="kpi-card ${salesClass}">
          <div class="kpi-val">${App.fmt$(d.salesTotal)}</div>
          <div class="kpi-label">Sales (${days}d)</div>
        </div>
        <div class="kpi-card">
          <div class="kpi-val">${d.totalProducts}</div>
          <div class="kpi-label">Active Products</div>
        </div>
        <div class="kpi-card ${lowClass}">
          <div class="kpi-val">${d.lowStockItems}</div>
          <div class="kpi-label">Low Stock Items</div>
        </div>
        <div class="kpi-card ${packClass}">
          <div class="kpi-val">${d.ordersToPack}</div>
          <div class="kpi-label">Orders to Pack</div>
        </div>
        <div class="kpi-card ${recvClass}">
          <div class="kpi-val">${d.itemsToReceive}</div>
          <div class="kpi-label">Items to Receive</div>
        </div>
        <div class="kpi-card ${overdueClass}">
          <div class="kpi-val">${d.overduePOs}</div>
          <div class="kpi-label">Overdue POs</div>
        </div>
      </div>

      <!-- Orders to Pack -->
      <div class="section-header">
        <h2>Orders to Pack</h2>
        <a href="#/orders">View all</a>
      </div>
      ${d.sosToPack.length === 0
        ? `<div class="card card-sm text-muted text-small">No orders to pack</div>`
        : `<div class="list-card" id="dash-so-list">
            ${d.sosToPack.slice(0, 5).map(so => `
              <div class="list-item" data-so="${so.salesOrderID}">
                <div class="li-main">
                  <div class="li-title">${so.customerName} — #${so.orderNumber}</div>
                  <div class="li-sub">${so.totalQty} item${so.totalQty !== 1 ? 's' : ''} · ${App.fmtDateShort(so.orderDate)}</div>
                </div>
                <div class="li-right">
                  <div class="li-val">${App.statusBadge(so.status)}</div>
                </div>
                <div class="chevron">${App.chevronSvg()}</div>
              </div>`).join('')}
          </div>`}

      <!-- POs to Receive -->
      <div class="section-header mt-16">
        <h2>POs to Receive</h2>
        <a href="#/po">View all</a>
      </div>
      ${d.posToReceive.length === 0
        ? `<div class="card card-sm text-muted text-small">No outstanding POs</div>`
        : `<div class="list-card" id="dash-po-list">
            ${d.posToReceive.slice(0, 5).map(po => `
              <div class="list-item" data-po="${po.pOID}">
                <div class="li-main">
                  <div class="li-title">${po.supplierName} — ${po.pONumber}</div>
                  <div class="li-sub">${po.itemsOutstanding} item${po.itemsOutstanding !== 1 ? 's' : ''} outstanding${po.expectedDate ? ' · Due ' + App.fmtDateShort(po.expectedDate) : ''}</div>
                </div>
                <div class="li-right">
                  <div class="li-val">${App.statusBadge(po.status)}</div>
                </div>
                <div class="chevron">${App.chevronSvg()}</div>
              </div>`).join('')}
          </div>`}`;
  }

  function bindDays(container) {
    document.querySelectorAll('.days-btn').forEach(btn => {
      btn.addEventListener('click', async () => {
        currentDays = parseInt(btn.dataset.days, 10);
        document.getElementById('dash-content').innerHTML =
          `<div class="kpi-grid">${App.skeletonCards(6)}</div>`;
        await loadDashboard(currentDays, container);
      });
    });
  }

  function bindLists() {
    document.querySelectorAll('#dash-so-list .list-item').forEach(el => {
      el.addEventListener('click', () => App.navigate(`orders/${el.dataset.so}`));
    });
    document.querySelectorAll('#dash-po-list .list-item').forEach(el => {
      el.addEventListener('click', () => App.navigate(`po/${el.dataset.po}`));
    });
  }

  return { render };
})();
