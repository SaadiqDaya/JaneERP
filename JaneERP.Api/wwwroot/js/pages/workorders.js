// ── Work Orders ───────────────────────────────────────────────────────────────

const WorkOrdersPage = (() => {
  let currentFilter = 'open';

  async function render(container) {
    container.innerHTML = `
      <div class="page">
        <div class="page-header">
          <h1>Work Orders</h1>
        </div>
        <div class="content">
          <div class="filter-row" id="wo-filters">
            ${[['open','Open'],['progress','In Progress'],['done','Complete'],['','All']].map(([v,l]) => `
              <button class="filter-chip${v === currentFilter ? ' active' : ''}" data-filter="${v}">${l}</button>`).join('')}
          </div>
          <div id="wo-list"></div>
        </div>
      </div>`;

    document.getElementById('wo-filters').addEventListener('click', async e => {
      const chip = e.target.closest('.filter-chip');
      if (!chip) return;
      document.querySelectorAll('#wo-filters .filter-chip').forEach(c => c.classList.remove('active'));
      chip.classList.add('active');
      currentFilter = chip.dataset.filter;
      await loadWOs();
    });

    await loadWOs();
  }

  async function loadWOs() {
    const listEl = document.getElementById('wo-list');
    listEl.innerHTML = App.skeletonCards(4);
    try {
      const url = `/api/work-orders${currentFilter ? '?status=' + currentFilter : ''}`;
      const wos = await Api.get(url);

      if (wos.length === 0) {
        listEl.innerHTML = `<div class="empty-state">
          <svg viewBox="0 0 24 24"><path d="M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-7 3c1.93 0 3.5 1.57 3.5 3.5S13.93 13 12 13s-3.5-1.57-3.5-3.5S10.07 6 12 6zm7 13H5v-.23c0-.62.28-1.2.76-1.58C7.47 15.82 9.64 15 12 15s4.53.82 6.24 2.19c.48.38.76.97.76 1.58V19z"/></svg>
          <p>No work orders found</p></div>`;
        return;
      }

      listEl.innerHTML = `<div class="list-card">
        ${wos.map(wo => {
          const progress = wo.quantity > 0
            ? Math.round((wo.completedQty / wo.quantity) * 100) : 0;
          return `
          <div class="list-item" data-id="${wo.workOrderID}">
            <div class="li-main">
              <div class="li-title">${wo.productName}</div>
              <div class="li-sub">
                MO: ${wo.moNumber}
                ${wo.assignedTo ? ' · ' + wo.assignedTo : ''}
              </div>
              ${(wo.status === 'InProgress' || wo.completedQty > 0) ? `
              <div class="wo-progress-bar">
                <div class="wo-progress-fill" style="width:${progress}%;"></div>
              </div>` : ''}
            </div>
            <div class="li-right">
              <div class="li-val" style="font-size:16px;font-weight:800;">${wo.completedQty}/${wo.quantity}</div>
              <div class="li-badge">${woStatusBadge(wo.status)}</div>
            </div>
            <div class="chevron">${App.chevronSvg()}</div>
          </div>`;
        }).join('')}
      </div>`;

      listEl.querySelectorAll('.list-item').forEach(el => {
        el.addEventListener('click', () => App.navigate(`work-orders/${el.dataset.id}`));
      });
    } catch (err) {
      listEl.innerHTML = `<div class="empty-state"><p>${err.message}</p></div>`;
    }
  }

  return { render };
})();

// ── Work Order Detail ─────────────────────────────────────────────────────────

const WorkOrderDetailPage = (() => {
  async function render(container, woId) {
    container.innerHTML = `
      <div class="page">
        <div class="page-header">
          <button class="back-btn" id="wo-back">
            <svg viewBox="0 0 24 24"><path d="M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z"/></svg>
          </button>
          <h1>Work Order</h1>
        </div>
        <div class="content" id="wo-detail-content">
          ${App.skeletonCards(3)}
        </div>
      </div>`;

    document.getElementById('wo-back').addEventListener('click', () => history.back());
    await loadDetail(woId);
  }

  async function loadDetail(woId) {
    const content = document.getElementById('wo-detail-content');
    try {
      const wo = await Api.get(`/api/work-orders/${woId}`);
      const progress = wo.quantity > 0
        ? Math.round((wo.completedQty / wo.quantity) * 100) : 0;

      content.innerHTML = `
        <div class="card">
          <div style="display:flex;justify-content:space-between;align-items:flex-start;margin-bottom:12px;">
            <div>
              <div style="font-size:17px;font-weight:800;">${wo.productName}</div>
              <div class="text-muted text-small">${wo.sku}</div>
            </div>
            ${woStatusBadge(wo.status)}
          </div>
          <div class="divider"></div>
          <div style="display:grid;grid-template-columns:1fr 1fr;gap:12px;margin-top:12px;">
            <div>
              <div class="text-muted text-small">Manufacturing Order</div>
              <div style="font-weight:700;">${wo.moNumber}</div>
            </div>
            <div>
              <div class="text-muted text-small">Assigned To</div>
              <div style="font-weight:700;">${wo.assignedTo || '—'}</div>
            </div>
            <div>
              <div class="text-muted text-small">Qty Ordered</div>
              <div style="font-size:22px;font-weight:800;">${wo.quantity}</div>
            </div>
            <div>
              <div class="text-muted text-small">Qty Completed</div>
              <div style="font-size:22px;font-weight:800;color:${wo.completedQty >= wo.quantity ? 'var(--success)' : 'var(--text)'}">
                ${wo.completedQty}
              </div>
            </div>
          </div>

          ${wo.quantity > 0 ? `
          <div style="margin-top:14px;">
            <div style="display:flex;justify-content:space-between;margin-bottom:6px;">
              <span class="text-muted text-small">Progress</span>
              <span class="text-small" style="font-weight:700;">${progress}%</span>
            </div>
            <div style="background:var(--bg);border-radius:4px;height:10px;overflow:hidden;">
              <div style="height:100%;width:${progress}%;background:${progress >= 100 ? 'var(--success)' : 'var(--primary)'};border-radius:4px;transition:width 0.3s;"></div>
            </div>
          </div>` : ''}

          ${wo.notes ? `
          <div class="divider" style="margin-top:12px;"></div>
          <div class="text-small mt-8" style="color:var(--text-2);">${wo.notes}</div>` : ''}

          ${wo.completedAt ? `
          <div class="divider" style="margin-top:12px;"></div>
          <div style="margin-top:8px;">
            <div class="text-muted text-small">Completed</div>
            <div style="font-weight:600;">${App.fmtDate(wo.completedAt)}</div>
          </div>` : ''}
        </div>

        <div class="card" style="background:var(--primary-lt);border-color:var(--primary);">
          <div style="font-size:13px;color:var(--primary-dk);">
            <svg viewBox="0 0 24 24" style="width:16px;height:16px;fill:currentColor;vertical-align:middle;margin-right:4px;"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z"/></svg>
            Work orders are managed in the desktop app. This is a read-only view.
          </div>
        </div>`;
    } catch (err) {
      content.innerHTML = `<div class="empty-state"><p>${err.message}</p></div>`;
    }
  }

  return { render };
})();

// Work order status badge helper (module-scoped so both pages can use it)
function woStatusBadge(status) {
  const map = {
    'Pending':    'badge-draft',
    'InProgress': 'badge-wip',
    'Complete':   'badge-complete',
    'Completed':  'badge-complete',
    'Cancelled':  'badge-draft',
  };
  return `<span class="badge ${map[status] || 'badge-draft'}">${status}</span>`;
}
