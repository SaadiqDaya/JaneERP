// ── PO List ───────────────────────────────────────────────────────────────────
const PurchaseOrdersPage = (() => {
  let currentFilter = 'active';

  async function render(container) {
    container.innerHTML = `
      <div class="page">
        <div class="page-header">
          <h1>Purchase Orders</h1>
        </div>
        <div class="content">
          <div class="filter-row" id="po-filters">
            ${[['active','Active'],['pending','To Receive'],['','All']].map(([v,l]) => `
              <button class="filter-chip${v === currentFilter ? ' active' : ''}" data-filter="${v}">${l}</button>`).join('')}
          </div>
          <div id="po-list"></div>
        </div>
      </div>`;

    document.getElementById('po-filters').addEventListener('click', async e => {
      const chip = e.target.closest('.filter-chip');
      if (!chip) return;
      document.querySelectorAll('#po-filters .filter-chip').forEach(c => c.classList.remove('active'));
      chip.classList.add('active');
      currentFilter = chip.dataset.filter;
      await loadPOs();
    });

    await loadPOs();
  }

  async function loadPOs() {
    const listEl = document.getElementById('po-list');
    listEl.innerHTML = App.skeletonCards(4);
    try {
      const url = `/api/purchase-orders${currentFilter ? '?status=' + currentFilter : ''}`;
      const pos = await Api.get(url);

      if (pos.length === 0) {
        listEl.innerHTML = `<div class="empty-state">
          <svg viewBox="0 0 24 24"><path d="M20 8h-3V4H3c-1.1 0-2 .9-2 2v11h2c0 1.66 1.34 3 3 3s3-1.34 3-3h6c0 1.66 1.34 3 3 3s3-1.34 3-3h2v-5l-3-4z"/></svg>
          <p>No purchase orders found</p></div>`;
        return;
      }

      listEl.innerHTML = `<div class="list-card">
        ${pos.map(po => `
          <div class="list-item" data-id="${po.poid}">
            <div class="li-main">
              <div class="li-title">${po.supplierName} — ${po.poNumber}</div>
              <div class="li-sub">
                ${App.fmtDate(po.orderDate)}
                ${po.expectedDate ? ' · Due ' + App.fmtDateShort(po.expectedDate) : ''}
              </div>
            </div>
            <div class="li-right">
              <div class="li-val">${App.fmt$(po.totalCost)}</div>
              <div class="li-badge">
                ${po.isOverdue ? '<span class="badge badge-overdue">Overdue</span>' : App.statusBadge(po.status)}
              </div>
            </div>
            <div class="chevron">${App.chevronSvg()}</div>
          </div>`).join('')}
      </div>`;

      listEl.querySelectorAll('.list-item').forEach(el => {
        el.addEventListener('click', () => App.navigate(`po/${el.dataset.id}`));
      });
    } catch (err) {
      listEl.innerHTML = `<div class="empty-state"><p>${err.message}</p></div>`;
    }
  }

  return { render };
})();

// ── PO Detail & Receive ───────────────────────────────────────────────────────
const PoDetailPage = (() => {
  async function render(container, poId) {
    container.innerHTML = `
      <div class="page">
        <div class="page-header">
          <button class="back-btn" id="po-back">
            <svg viewBox="0 0 24 24"><path d="M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z"/></svg>
          </button>
          <h1>PO Detail</h1>
        </div>
        <div class="content" id="po-detail-content">
          ${App.skeletonCards(3)}
        </div>
      </div>`;

    document.getElementById('po-back').addEventListener('click', () => history.back());
    await loadDetail(poId);
  }

  async function loadDetail(poId) {
    const contentEl = document.getElementById('po-detail-content');
    try {
      const po = await Api.get(`/api/purchase-orders/${poId}`);
      const canReceive = ['Sent', 'PartiallyReceived'].includes(po.status);

      const outstandingItems = po.items.filter(i => i.quantityOrdered > i.quantityReceived);

      contentEl.innerHTML = `
        <!-- Header -->
        <div class="card">
          <div style="display:flex;justify-content:space-between;align-items:flex-start;margin-bottom:12px;">
            <div>
              <div style="font-size:17px;font-weight:800;">${po.poNumber}</div>
              <div class="text-muted text-small">${po.supplierName}</div>
            </div>
            ${po.isOverdue
              ? '<span class="badge badge-overdue">Overdue</span>'
              : App.statusBadge(po.status)}
          </div>
          <div class="divider"></div>
          <div style="display:grid;grid-template-columns:1fr 1fr;gap:8px;margin-top:12px;">
            <div>
              <div class="text-muted text-small">Order Date</div>
              <div style="font-weight:600;">${App.fmtDate(po.orderDate)}</div>
            </div>
            ${po.expectedDate ? `<div>
              <div class="text-muted text-small">Expected</div>
              <div style="font-weight:600;${po.isOverdue ? 'color:var(--danger)' : ''}">${App.fmtDate(po.expectedDate)}</div>
            </div>` : ''}
            <div>
              <div class="text-muted text-small">Total</div>
              <div style="font-weight:700;">${App.fmt$(po.totalCost)}</div>
            </div>
            <div>
              <div class="text-muted text-small">Created By</div>
              <div style="font-weight:600;">${po.createdBy || '—'}</div>
            </div>
          </div>
          ${po.notes ? `<div class="text-small mt-8" style="color:var(--text-2)">${po.notes}</div>` : ''}
        </div>

        <!-- Items -->
        <div class="card">
          <div style="font-weight:700;font-size:14px;margin-bottom:8px;">Items (${po.items.length})</div>
          ${po.items.map(item => `
            <div class="qty-input-row" data-item-id="${item.poItemID}">
              <div style="flex:1;min-width:0;">
                <div class="qi-name">${item.itemName}</div>
                <div class="qi-ordered">${item.sku ? item.sku + ' · ' : ''}${App.fmt$(item.unitCost)} each</div>
                <div style="font-size:12px;margin-top:3px;">
                  <span style="color:var(--text-2)">Ordered:</span> <b>${item.quantityOrdered}</b>
                  &nbsp;
                  <span style="color:var(--text-2)">Received:</span>
                  <b style="color:${item.quantityReceived >= item.quantityOrdered ? 'var(--success)' : 'var(--text)'}">${item.quantityReceived}</b>
                </div>
              </div>
              ${canReceive && item.quantityReceived < item.quantityOrdered ? `
                <input type="number" class="receive-qty"
                       min="0" max="${item.quantityOrdered - item.quantityReceived}"
                       placeholder="0"
                       style="width:72px;padding:8px;text-align:center;border:1.5px solid var(--border);border-radius:8px;font-size:16px;font-weight:700;outline:none;"
                       data-item-id="${item.poItemID}"
                       data-max="${item.quantityOrdered - item.quantityReceived}">` : `
                <span class="badge ${item.quantityReceived >= item.quantityOrdered ? 'badge-received' : 'badge-partial'}">
                  ${item.quantityReceived >= item.quantityOrdered ? 'Complete' : 'Partial'}
                </span>`}
            </div>`).join('')}
        </div>

        ${canReceive ? `
          <button class="btn btn-success btn-full" id="receive-btn">
            <svg viewBox="0 0 24 24" style="width:18px;height:18px;fill:white;"><path d="M20 6h-2.18c.07-.44.18-.87.18-1.33C18 2.54 15.96.5 13.5.5c-1.39 0-2.58.72-3.5 1.77C9.08 1.22 7.89.5 6.5.5 4.04.5 2 2.54 2 4.67c0 .46.11.89.18 1.33H0v14h24V6h-4z"/></svg>
            Receive Items
          </button>
          <button class="btn btn-outline btn-full mt-8" id="receive-all-btn">Set All to Max</button>
        ` : ''}
        <button class="btn btn-outline btn-full mt-8" id="duplicate-po-btn">
          <svg viewBox="0 0 24 24" style="width:18px;height:18px;fill:currentColor;"><path d="M16 1H4c-1.1 0-2 .9-2 2v14h2V3h12V1zm3 4H8c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h11c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2zm0 16H8V7h11v14z"/></svg>
          Duplicate as New Draft PO
        </button>`;

      if (canReceive) {
        document.getElementById('receive-all-btn')?.addEventListener('click', () => {
          document.querySelectorAll('.receive-qty').forEach(inp => {
            inp.value = inp.dataset.max;
            inp.dispatchEvent(new Event('input'));
          });
        });

        document.getElementById('receive-btn')?.addEventListener('click', async () => {
          const inputs = [...document.querySelectorAll('.receive-qty')];
          const items  = inputs
            .map(inp => ({ poItemId: parseInt(inp.dataset.itemId, 10), qtyReceived: parseInt(inp.value || 0, 10) }))
            .filter(i => i.qtyReceived > 0);

          if (items.length === 0) { App.toast('Enter at least one quantity to receive', 'error'); return; }

          const btn = document.getElementById('receive-btn');
          btn.disabled = true; btn.textContent = 'Saving…';

          try {
            await Api.post(`/api/purchase-orders/${poId}/receive`, { items });
            App.toast('Items received!', 'success');
            await loadDetail(poId);
          } catch (err) {
            App.toast(err.message, 'error');
            btn.disabled = false; btn.textContent = 'Receive Items';
          }
        });
      }
      document.getElementById('duplicate-po-btn')?.addEventListener('click', async () => {
        const btn = document.getElementById('duplicate-po-btn');
        btn.disabled = true; btn.textContent = 'Duplicating…';
        try {
          const res = await Api.post(`/api/purchase-orders/${poId}/duplicate`, {});
          App.toast('New draft PO created!', 'success');
          App.navigate(`po/${res.poid}`);
        } catch (err) {
          App.toast(err.message, 'error');
          btn.disabled = false; btn.textContent = 'Duplicate as New Draft PO';
        }
      });
    } catch (err) {
      contentEl.innerHTML = `<div class="empty-state"><p>${err.message} [id=${poId}]</p></div>`;
    }
  }

  return { render };
})();
