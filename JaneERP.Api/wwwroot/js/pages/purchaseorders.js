// ── PO List ───────────────────────────────────────────────────────────────────
const PurchaseOrdersPage = (() => {
  let currentFilter = 'active';
  let searchQ = '';
  let searchTimer = null;

  async function render(container) {
    container.innerHTML = `
      <div class="page">
        <div class="page-header">
          <h1>Purchase Orders</h1>
          <button class="btn btn-primary" id="po-new-btn" style="padding:8px 14px;font-size:14px;">+ New PO</button>
        </div>
        <div class="content">
          <div id="po-create-panel" class="hidden"></div>
          <div class="search-bar">
            <div class="search-icon">
              <svg viewBox="0 0 24 24"><path d="M15.5 14h-.79l-.28-.27A6.47 6.47 0 0 0 16 9.5 6.5 6.5 0 1 0 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z"/></svg>
            </div>
            <input id="po-search" type="search" placeholder="Search supplier or PO #…" autocomplete="off" value="${searchQ}">
          </div>
          <div class="filter-row" id="po-filters">
            ${[['active','Active'],['pending','To Receive'],['','All']].map(([v,l]) => `
              <button class="filter-chip${v === currentFilter ? ' active' : ''}" data-filter="${v}">${l}</button>`).join('')}
          </div>
          <div id="po-list"></div>
        </div>
      </div>`;

    document.getElementById('po-new-btn').addEventListener('click', () => showCreatePanel());
    document.getElementById('po-search').addEventListener('input', e => {
      clearTimeout(searchTimer);
      searchQ = e.target.value.trim();
      searchTimer = setTimeout(loadPOs, 350);
    });
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
      const params = new URLSearchParams();
      if (currentFilter) params.set('status', currentFilter);
      if (searchQ)        params.set('q', searchQ);
      const url = `/api/purchase-orders${params.toString() ? '?' + params : ''}`;
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

  async function showCreatePanel() {
    const panelEl = document.getElementById('po-create-panel');
    if (!panelEl.classList.contains('hidden')) { panelEl.classList.add('hidden'); panelEl.innerHTML = ''; return; }

    panelEl.innerHTML = `<div class="card" style="margin-bottom:12px;"><p class="text-muted text-small" style="text-align:center;padding:8px 0;">Loading suppliers…</p></div>`;
    panelEl.classList.remove('hidden');

    let suppliers = [];
    try { suppliers = await Api.get('/api/purchase-orders/suppliers'); }
    catch (err) { panelEl.innerHTML = `<div class="card" style="margin-bottom:12px;"><p style="color:var(--danger);">${err.message}</p></div>`; return; }

    if (suppliers.length === 0) {
      panelEl.innerHTML = `<div class="card" style="margin-bottom:12px;"><p class="text-muted text-small">No suppliers found. Add suppliers in the desktop app first.</p></div>`;
      return;
    }

    panelEl.innerHTML = `
      <div class="card" style="margin-bottom:12px;">
        <div style="font-weight:700;margin-bottom:12px;">New Purchase Order</div>

        <div style="margin-bottom:10px;">
          <div style="font-size:12px;color:var(--text-2);margin-bottom:4px;">Supplier *</div>
          <select id="pc-supplier" style="width:100%;padding:9px;border:1.5px solid var(--border);border-radius:8px;font-size:14px;">
            <option value="">— Select —</option>
            ${suppliers.map(s => `<option value="${s.supplierID}">${s.supplierName}</option>`).join('')}
          </select>
        </div>

        <div style="display:grid;grid-template-columns:1fr 1fr;gap:8px;margin-bottom:10px;">
          <div>
            <div style="font-size:12px;color:var(--text-2);margin-bottom:4px;">Expected Date</div>
            <input id="pc-expected" type="date" style="width:100%;padding:9px;border:1.5px solid var(--border);border-radius:8px;font-size:14px;box-sizing:border-box;">
          </div>
          <div>
            <div style="font-size:12px;color:var(--text-2);margin-bottom:4px;">Shipping Cost</div>
            <input id="pc-shipping" type="number" min="0" step="0.01" value="0"
              style="width:100%;padding:9px;border:1.5px solid var(--border);border-radius:8px;font-size:14px;box-sizing:border-box;">
          </div>
        </div>

        <div style="margin-bottom:12px;">
          <div style="font-size:12px;color:var(--text-2);margin-bottom:4px;">Notes</div>
          <textarea id="pc-notes" rows="2" style="width:100%;padding:9px;border:1.5px solid var(--border);border-radius:8px;font-size:14px;resize:none;box-sizing:border-box;"></textarea>
        </div>

        <div style="font-weight:600;font-size:13px;margin-bottom:8px;">
          Line Items
          <button class="btn btn-outline" id="pc-add-row" style="margin-left:10px;padding:4px 10px;font-size:12px;">+ Add Item</button>
        </div>
        <div id="pc-items-list" style="margin-bottom:12px;"></div>

        <div style="display:flex;gap:8px;">
          <button class="btn btn-primary" id="pc-save-btn" style="flex:1;">Create PO</button>
          <button class="btn btn-outline" id="pc-cancel-btn">Cancel</button>
        </div>
      </div>`;

    let rowCount = 0;

    function addRow() {
      rowCount++;
      const id = `pc-row-${rowCount}`;
      const div = document.createElement('div');
      div.id  = id;
      div.className = 'qty-input-row';
      div.style.cssText = 'display:flex;align-items:center;gap:6px;margin-bottom:6px;';
      div.innerHTML = `
        <input type="text" placeholder="Item name *" class="pc-item-name"
          style="flex:2;padding:8px;border:1.5px solid var(--border);border-radius:8px;font-size:14px;">
        <input type="number" placeholder="Qty" class="pc-item-qty" min="1" value="1"
          style="flex:1;padding:8px;border:1.5px solid var(--border);border-radius:8px;font-size:14px;text-align:center;">
        <input type="number" placeholder="Cost" class="pc-item-cost" min="0" step="0.01" value="0"
          style="flex:1;padding:8px;border:1.5px solid var(--border);border-radius:8px;font-size:14px;text-align:center;">
        <button class="btn btn-outline pc-remove-row" data-row="${id}"
          style="padding:7px 10px;font-size:13px;flex-shrink:0;">✕</button>`;
      document.getElementById('pc-items-list').appendChild(div);
    }

    addRow();  // start with one empty row

    document.getElementById('pc-add-row').addEventListener('click', addRow);
    document.getElementById('pc-items-list').addEventListener('click', e => {
      const btn = e.target.closest('.pc-remove-row');
      if (btn) document.getElementById(btn.dataset.row)?.remove();
    });

    document.getElementById('pc-cancel-btn').addEventListener('click', () => {
      panelEl.classList.add('hidden');
      panelEl.innerHTML = '';
    });

    document.getElementById('pc-save-btn').addEventListener('click', async () => {
      const supplierID = parseInt(document.getElementById('pc-supplier').value, 10);
      if (!supplierID) { App.toast('Select a supplier', 'error'); return; }

      const rows = [...document.querySelectorAll('#pc-items-list .qty-input-row')];
      const items = rows.map(row => ({
        itemName:        row.querySelector('.pc-item-name').value.trim(),
        quantityOrdered: parseInt(row.querySelector('.pc-item-qty').value, 10) || 1,
        unitCost:        parseFloat(row.querySelector('.pc-item-cost').value) || 0,
        sku:             '',
      })).filter(i => i.itemName);

      if (items.length === 0) { App.toast('Add at least one line item', 'error'); return; }

      const expectedVal = document.getElementById('pc-expected').value;
      const btn = document.getElementById('pc-save-btn');
      btn.disabled = true; btn.textContent = 'Saving…';

      try {
        const res = await Api.post('/api/purchase-orders', {
          supplierID,
          expectedDate: expectedVal ? new Date(expectedVal).toISOString() : null,
          notes:        document.getElementById('pc-notes').value.trim() || null,
          shippingCost: parseFloat(document.getElementById('pc-shipping').value) || 0,
          items,
        });
        App.toast('Purchase order created!', 'success');
        panelEl.classList.add('hidden');
        panelEl.innerHTML = '';
        App.navigate(`po/${res.poid}`);
      } catch (err) {
        App.toast(err.message, 'error');
        btn.disabled = false; btn.textContent = 'Create PO';
      }
    });
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
