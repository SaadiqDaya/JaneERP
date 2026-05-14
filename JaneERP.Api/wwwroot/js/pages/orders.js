// ── Orders list ───────────────────────────────────────────────────────────────
const OrdersPage = (() => {
  let currentStatus = '';
  let searchQ = '';
  let searchTimer = null;

  async function render(container) {
    container.innerHTML = `
      <div class="page">
        <div class="page-header">
          <h1>Sales Orders</h1>
          <div class="header-actions">
            <button class="btn-icon" id="new-order-btn" title="New order">
              <svg viewBox="0 0 24 24"><path d="M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z"/></svg>
            </button>
          </div>
        </div>
        <div class="content">
          <div class="search-bar">
            <div class="search-icon">
              <svg viewBox="0 0 24 24"><path d="M15.5 14h-.79l-.28-.27A6.47 6.47 0 0 0 16 9.5 6.5 6.5 0 1 0 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z"/></svg>
            </div>
            <input id="orders-search" type="search" placeholder="Search by customer or order #…" autocomplete="off" value="${searchQ}">
          </div>
          <div class="filter-row" id="status-filters">
            ${['', 'Draft', 'Live', 'WIP', 'Packed', 'Shipped', 'Complete'].map(s => `
              <button class="filter-chip${s === currentStatus ? ' active' : ''}" data-status="${s}">
                ${s || 'All'}
              </button>`).join('')}
          </div>
          <div id="orders-list"></div>
        </div>
      </div>`;

    document.getElementById('new-order-btn').addEventListener('click', () => App.navigate('orders/new'));
    document.getElementById('orders-search').addEventListener('input', e => {
      clearTimeout(searchTimer);
      searchQ = e.target.value.trim();
      searchTimer = setTimeout(loadOrders, 350);
    });
    document.getElementById('status-filters').addEventListener('click', async e => {
      const chip = e.target.closest('.filter-chip');
      if (!chip) return;
      document.querySelectorAll('#status-filters .filter-chip').forEach(c => c.classList.remove('active'));
      chip.classList.add('active');
      currentStatus = chip.dataset.status;
      await loadOrders();
    });

    await loadOrders();
  }

  async function loadOrders() {
    const listEl = document.getElementById('orders-list');
    listEl.innerHTML = App.skeletonCards(5);
    try {
      const params = new URLSearchParams();
      if (currentStatus) params.set('status', currentStatus);
      if (searchQ)        params.set('q', searchQ);
      const url = `/api/orders${params.toString() ? '?' + params : ''}`;
      const data = await Api.get(url);
      const orders = data.items;

      if (orders.length === 0) {
        listEl.innerHTML = `<div class="empty-state">
          <svg viewBox="0 0 24 24"><path d="M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2z"/></svg>
          <p>No orders found</p></div>`;
        return;
      }

      listEl.innerHTML = `<div class="list-card">
        ${orders.map(o => `
          <div class="list-item" data-id="${o.salesOrderID}">
            <div class="li-main">
              <div class="li-title">${o.customerName || 'Unknown'} — #${o.orderNumber}</div>
              <div class="li-sub">${App.fmtDate(o.orderDate)} · ${o.orderType}</div>
            </div>
            <div class="li-right">
              <div class="li-val">${App.fmt$(o.totalPrice)}</div>
              <div class="li-badge">${App.statusBadge(o.status)}</div>
            </div>
            <div class="chevron">${App.chevronSvg()}</div>
          </div>`).join('')}
      </div>`;

      listEl.querySelectorAll('.list-item').forEach(el => {
        el.addEventListener('click', () => App.navigate(`orders/${el.dataset.id}`));
      });
    } catch (err) {
      listEl.innerHTML = `<div class="empty-state"><p>${err.message}</p></div>`;
    }
  }

  return { render };
})();

// ── Order detail ──────────────────────────────────────────────────────────────
const OrderDetailPage = (() => {
  const STATUSES = ['Draft', 'Live', 'WIP', 'Packed', 'Shipped', 'Complete'];

  async function render(container, orderId) {
    container.innerHTML = `
      <div class="page">
        <div class="page-header">
          <button class="back-btn" id="orders-back">
            <svg viewBox="0 0 24 24"><path d="M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z"/></svg>
          </button>
          <h1>Order Detail</h1>
          <div class="header-actions">
            <button class="btn-icon" id="edit-notes-btn" title="Edit notes">
              <svg viewBox="0 0 24 24"><path d="M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z"/></svg>
            </button>
            <button class="btn-icon" id="print-btn" title="Print packing slip">
              <svg viewBox="0 0 24 24"><path d="M19 8H5c-1.66 0-3 1.34-3 3v6h4v4h12v-4h4v-6c0-1.66-1.34-3-3-3zm-3 11H8v-5h8v5zm3-7c-.55 0-1-.45-1-1s.45-1 1-1 1 .45 1 1-.45 1-1 1zm-1-9H6v4h12V3z"/></svg>
            </button>
          </div>
        </div>
        <div class="content" id="order-detail-content">
          ${App.skeletonCards(3)}
        </div>
      </div>`;

    document.getElementById('orders-back').addEventListener('click', () => history.back());
    await loadDetail(orderId);
  }

  async function loadDetail(orderId) {
    const contentEl = document.getElementById('order-detail-content');
    try {
      const o = await Api.get(`/api/orders/${orderId}`);
      const subtotal = o.items.reduce((s, i) => s + i.quantity * i.unitPrice, 0);
      const canPick    = ['Live', 'WIP'].includes(o.status);
      const isPacked   = o.status === 'Packed';

      contentEl.innerHTML = `
        <!-- Status & Customer -->
        <div class="card">
          <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:12px;">
            <div>
              <div style="font-size:17px;font-weight:800;">#${o.orderNumber}</div>
              <div class="text-muted text-small">${o.orderType} · ${App.fmtDate(o.orderDate)}</div>
            </div>
            ${App.statusBadge(o.status)}
          </div>
          <div class="divider"></div>
          <div style="margin-top:12px;">
            <div style="font-weight:600;">${o.customerName}</div>
            <div class="text-muted text-small">${o.customerEmail}</div>
          </div>
              <div id="order-notes-display">
            ${o.notes ? `<div class="text-small mt-8" style="color:var(--text-2)">${o.notes}</div>` : ''}
          </div>
        </div>

        <!-- Line Items -->
        <div class="card">
          <div style="font-weight:700;font-size:14px;margin-bottom:8px;">Items (${o.items.length})</div>
          ${o.items.map(item => `
            <div class="order-item-row">
              <div class="oi-main">
                <div class="oi-title">${item.title || item.sku}</div>
                <div class="oi-sku">${item.sku}</div>
              </div>
              <div class="oi-right">
                <div class="oi-total">${App.fmt$(item.quantity * item.unitPrice)}</div>
                <div class="oi-qty">${item.quantity} × ${App.fmt$(item.unitPrice)}</div>
              </div>
            </div>`).join('')}
          <div style="margin-top:12px;">
            <div class="total-row"><span>Subtotal</span><span>${App.fmt$(subtotal)}</span></div>
            ${o.shippingCost > 0 ? `<div class="total-row"><span>Shipping</span><span>${App.fmt$(o.shippingCost)}</span></div>` : ''}
            <div class="total-row grand"><span>Total</span><span>${App.fmt$(o.totalPrice)}</span></div>
          </div>
        </div>

        <!-- Pick (Live / WIP orders) -->
        ${canPick ? `
        <div class="card" id="pick-section">
          <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:12px;">
            <div style="font-weight:700;font-size:14px;">
              ${o.status === 'Live' ? 'Ready to Pick' : 'Picking in Progress'}
            </div>
            ${o.status === 'Live' ? `
              <button class="btn btn-outline" style="padding:7px 14px;font-size:13px;" id="begin-picking-btn">
                Start Picking
              </button>` : ''}
          </div>
          <div id="pick-list-content" style="color:var(--text-2);font-size:13px;">Loading pick list…</div>
          <button class="btn btn-primary btn-full mt-12" id="complete-order-btn" disabled>
            ✓ Mark as Packed
          </button>
        </div>` : ''}

        <!-- Pack → Ship (Packed orders) -->
        ${isPacked ? `
        <div class="card" style="background:var(--primary-lt);border-color:var(--primary);">
          <div style="font-weight:700;font-size:14px;margin-bottom:6px;">Ready to Ship</div>
          <div class="text-muted text-small" style="margin-bottom:12px;">
            All items packed. Mark as shipped when dispatched.
          </div>
          <button class="btn btn-success btn-full" id="ship-order-btn">Mark as Shipped</button>
        </div>` : ''}

        <!-- Status buttons (Draft / Shipped / Complete and other non-pick statuses) -->
        ${!canPick && !isPacked ? `
        <div class="card">
          <div style="font-weight:700;font-size:14px;margin-bottom:10px;">Update Status</div>
          <div style="display:flex;gap:8px;flex-wrap:wrap;" id="status-btns">
            ${STATUSES.map(s => `
              <button class="btn ${s === o.status ? 'btn-primary' : 'btn-outline'}"
                      style="flex:1;min-width:80px;padding:10px 8px;font-size:13px;"
                      data-status="${s}" ${s === o.status ? 'disabled' : ''}>
                ${s}
              </button>`).join('')}
          </div>
        </div>` : ''}`;

      // Edit notes button
      document.getElementById('edit-notes-btn')?.addEventListener('click', () => openNotesEditor(orderId, o.notes));

      // Print packing slip
      document.getElementById('print-btn')?.addEventListener('click', () => printPackingSlip(o));

      // Bind non-pick status buttons
      document.querySelectorAll('#status-btns button:not([disabled])')?.forEach(btn => {
        btn.addEventListener('click', async () => {
          try {
            await Api.patch(`/api/orders/${orderId}/status`, { status: btn.dataset.status });
            App.toast('Status updated', 'success');
            await loadDetail(orderId);
          } catch (err) {
            App.toast(err.message, 'error');
          }
        });
      });

      // Ship button (Packed → Shipped)
      document.getElementById('ship-order-btn')?.addEventListener('click', async () => {
        const btn = document.getElementById('ship-order-btn');
        btn.disabled = true; btn.textContent = 'Saving…';
        try {
          await Api.patch(`/api/orders/${orderId}/status`, { status: 'Shipped' });
          App.toast('Order shipped! Inventory updated.', 'success');
          await loadDetail(orderId);
        } catch (err) {
          App.toast(err.message, 'error');
          btn.disabled = false; btn.textContent = 'Mark as Shipped';
        }
      });

      // Load pick list for Live / WIP orders
      if (canPick) {
        document.getElementById('begin-picking-btn')?.addEventListener('click', async () => {
          try {
            await Api.patch(`/api/orders/${orderId}/status`, { status: 'WIP' });
            App.toast('Picking started', 'success');
            await loadDetail(orderId);
          } catch (err) { App.toast(err.message, 'error'); }
        });

        await loadPickList(orderId);
      }
    } catch (err) {
      contentEl.innerHTML = `<div class="empty-state"><p>${err.message}</p></div>`;
    }
  }

  async function loadPickList(orderId) {
    const listEl   = document.getElementById('pick-list-content');
    const complBtn = document.getElementById('complete-order-btn');
    if (!listEl) return;

    try {
      const items = await Api.get(`/api/orders/${orderId}/pick-list`);

      if (items.length === 0) {
        listEl.innerHTML = '<p class="text-muted text-small">No items on this order.</p>';
        return;
      }

      // Normalise totalStock — API may return null for products with no transactions
      items.forEach(item => { item.totalStock = item.totalStock ?? 0; });

      const shortfalls = items.filter(i => i.totalStock < i.quantityNeeded);
      const noStock    = items.filter(i => i.totalStock <= 0);

      // Stock warning banner
      let warningHtml = '';
      if (noStock.length > 0) {
        warningHtml = `
          <div style="background:var(--danger-lt);border:1.5px solid var(--danger);border-radius:8px;
                      padding:10px 12px;margin-bottom:12px;font-size:12px;color:var(--danger);font-weight:600;">
            ⚠ ${noStock.length} item${noStock.length !== 1 ? 's' : ''} have no stock recorded.
            Verify before packing.
          </div>`;
      } else if (shortfalls.length > 0) {
        warningHtml = `
          <div style="background:#fff8e1;border:1.5px solid #f59e0b;border-radius:8px;
                      padding:10px 12px;margin-bottom:12px;font-size:12px;color:#92400e;font-weight:600;">
            ⚠ ${shortfalls.length} item${shortfalls.length !== 1 ? 's' : ''} may have insufficient stock.
          </div>`;
      }

      listEl.innerHTML = warningHtml + items.map((item, idx) => {
        const isShort   = item.totalStock < item.quantityNeeded;
        const isNoStock = item.totalStock <= 0;
        let stockBadge  = '';
        if (isNoStock) {
          stockBadge = `<span style="font-size:12px;background:var(--danger-lt);color:var(--danger);
                              padding:2px 8px;border-radius:6px;font-weight:600;">
            ⚠ No stock on file
          </span>`;
        } else if (isShort) {
          stockBadge = `<span style="font-size:12px;background:var(--danger-lt);color:var(--danger);
                              padding:2px 8px;border-radius:6px;font-weight:600;">
            ⚠ Only ${item.totalStock} avail
          </span>`;
        }
        return `
        <label style="display:flex;align-items:flex-start;gap:12px;padding:12px 0;
                      border-bottom:1px solid var(--border);cursor:pointer;"
               for="pick-cb-${idx}">
          <input type="checkbox" id="pick-cb-${idx}" class="pick-cb"
                 style="width:20px;height:20px;margin-top:2px;flex-shrink:0;cursor:pointer;">
          <div style="flex:1;min-width:0;">
            <div style="font-size:14px;font-weight:600;">${item.title || item.sku}</div>
            <div style="font-size:12px;color:var(--text-2);">${item.sku}</div>
            <div style="display:flex;gap:10px;margin-top:4px;flex-wrap:wrap;">
              <span style="font-size:12px;background:var(--primary-lt);color:var(--primary-dk);
                           padding:2px 8px;border-radius:6px;font-weight:600;">
                Qty: ${item.quantityNeeded}
              </span>
              ${item.primaryLocation ? `
              <span style="font-size:12px;background:var(--bg);padding:2px 8px;
                           border-radius:6px;color:var(--text-2);font-weight:500;">
                📍 ${item.primaryLocation}
              </span>` : ''}
              ${stockBadge}
            </div>
          </div>
        </label>`;
      }).join('');

      // Hard block: cannot pack if any items have insufficient stock
      if (shortfalls.length > 0) {
        listEl.insertAdjacentHTML('beforeend', `
          <div style="background:var(--danger-lt);border:1.5px solid var(--danger);border-radius:8px;
                      padding:12px 14px;margin-top:12px;font-size:13px;color:var(--danger);font-weight:600;">
            Cannot pack — ${shortfalls.length} item${shortfalls.length !== 1 ? 's' : ''} have insufficient stock.
            Receive the missing stock before packing this order.
          </div>`);
        if (complBtn) {
          complBtn.disabled = true;
          complBtn.title    = 'Insufficient stock — cannot pack';
        }
        return;
      }

      // Enable Complete button when all items are checked
      function updateCompleteBtn() {
        const all = document.querySelectorAll('.pick-cb');
        const allChecked = all.length > 0 && [...all].every(cb => cb.checked);
        if (complBtn) complBtn.disabled = !allChecked;
      }

      document.querySelectorAll('.pick-cb').forEach(cb => {
        cb.addEventListener('change', updateCompleteBtn);
      });

      complBtn?.addEventListener('click', async () => {
        complBtn.disabled    = true;
        complBtn.textContent = 'Saving…';
        try {
          await Api.patch(`/api/orders/${orderId}/status`, { status: 'Packed' });
          App.toast('Order packed — ready to ship!', 'success');
          await loadDetail(orderId);
        } catch (err) {
          App.toast(err.message, 'error');
          complBtn.disabled    = false;
          complBtn.textContent = '✓ Mark as Packed';
        }
      });
    } catch (err) {
      if (listEl) listEl.innerHTML = `<p class="text-danger text-small">${err.message}</p>`;
    }
  }

  function openNotesEditor(orderId, currentNotes) {
    const overlay = document.createElement('div');
    overlay.className = 'sheet-overlay';
    overlay.innerHTML = `
      <div class="sheet">
        <div class="sheet-handle"></div>
        <h2 style="font-size:16px;font-weight:700;margin-bottom:14px;">Order Notes</h2>
        <div class="form-group">
          <textarea id="notes-input" class="form-control" rows="5"
                    placeholder="Add notes…">${currentNotes || ''}</textarea>
        </div>
        <button class="btn btn-primary btn-full" id="save-notes-btn">Save Notes</button>
        <button class="btn btn-outline btn-full mt-8" id="cancel-notes-btn">Cancel</button>
      </div>`;
    document.body.appendChild(overlay);
    overlay.addEventListener('click', e => { if (e.target === overlay) overlay.remove(); });
    document.getElementById('cancel-notes-btn').addEventListener('click', () => overlay.remove());
    document.getElementById('save-notes-btn').addEventListener('click', async () => {
      const notes = document.getElementById('notes-input').value.trim() || null;
      const btn   = document.getElementById('save-notes-btn');
      btn.disabled = true; btn.textContent = 'Saving…';
      try {
        await Api.patch(`/api/orders/${orderId}/notes`, { notes });
        overlay.remove();
        App.toast('Notes saved', 'success');
        // Refresh notes display without full reload
        const display = document.getElementById('order-notes-display');
        if (display) display.innerHTML = notes
          ? `<div class="text-small mt-8" style="color:var(--text-2)">${notes}</div>`
          : '';
      } catch (err) {
        App.toast(err.message, 'error');
        btn.disabled = false; btn.textContent = 'Save Notes';
      }
    });
  }

  function printPackingSlip(o) {
    const subtotal = o.items.reduce((s, i) => s + i.quantity * i.unitPrice, 0);
    const win = window.open('', '_blank', 'width=800,height=600');
    win.document.write(`<!DOCTYPE html>
<html>
<head>
  <title>Packing Slip #${o.orderNumber}</title>
  <style>
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body { font-family: Arial, sans-serif; padding: 24px; font-size: 13px; color: #111; }
    .header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 20px; }
    .header h1 { font-size: 22px; }
    .header .meta { text-align: right; font-size: 12px; color: #555; }
    .info-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; margin-bottom: 20px; padding: 14px; background: #f5f5f5; border-radius: 6px; }
    .info-label { font-size: 11px; color: #666; margin-bottom: 2px; }
    .info-val { font-weight: 700; font-size: 13px; }
    table { width: 100%; border-collapse: collapse; margin-bottom: 16px; }
    th { text-align: left; padding: 8px 10px; border-bottom: 2px solid #222; font-size: 12px; text-transform: uppercase; }
    td { padding: 9px 10px; border-bottom: 1px solid #e0e0e0; font-size: 13px; }
    tr:last-child td { border-bottom: none; }
    .totals { margin-left: auto; width: 220px; }
    .total-row { display: flex; justify-content: space-between; padding: 4px 0; font-size: 13px; }
    .grand { font-weight: 800; font-size: 15px; border-top: 2px solid #222; padding-top: 8px; margin-top: 4px; }
    .notes { margin-top: 20px; padding: 12px; background: #f9f9f9; border-left: 3px solid #2563eb; font-size: 12px; color: #444; }
    @media print { body { padding: 12px; } }
  </style>
</head>
<body>
  <div class="header">
    <div>
      <h1>Packing Slip</h1>
      <div style="font-size:15px;font-weight:700;margin-top:4px;">Order #${o.orderNumber}</div>
    </div>
    <div class="meta">
      <div>${new Date(o.orderDate).toLocaleDateString('en-CA', { year:'numeric', month:'long', day:'numeric' })}</div>
      <div style="margin-top:4px;">${App.statusBadge ? '' : o.status}</div>
    </div>
  </div>

  <div class="info-grid">
    <div>
      <div class="info-label">Customer</div>
      <div class="info-val">${o.customerName}</div>
      <div style="font-size:12px;color:#555;margin-top:2px;">${o.customerEmail}</div>
    </div>
    <div>
      <div class="info-label">Order Type</div>
      <div class="info-val">${o.orderType}</div>
    </div>
  </div>

  <table>
    <thead>
      <tr>
        <th>Item</th>
        <th>SKU</th>
        <th style="text-align:center;">Qty</th>
        <th style="text-align:right;">Unit Price</th>
        <th style="text-align:right;">Total</th>
      </tr>
    </thead>
    <tbody>
      ${o.items.map(item => `
      <tr>
        <td>${item.title || item.sku}</td>
        <td style="color:#666;">${item.sku}</td>
        <td style="text-align:center;font-weight:700;">${item.quantity}</td>
        <td style="text-align:right;">\$${item.unitPrice.toFixed(2)}</td>
        <td style="text-align:right;font-weight:700;">\$${(item.quantity * item.unitPrice).toFixed(2)}</td>
      </tr>`).join('')}
    </tbody>
  </table>

  <div class="totals">
    <div class="total-row"><span>Subtotal</span><span>\$${subtotal.toFixed(2)}</span></div>
    ${o.shippingCost > 0 ? `<div class="total-row"><span>Shipping</span><span>\$${o.shippingCost.toFixed(2)}</span></div>` : ''}
    <div class="total-row grand"><span>Total</span><span>\$${o.totalPrice.toFixed(2)}</span></div>
  </div>

  ${o.notes ? `<div class="notes"><strong>Notes:</strong> ${o.notes}</div>` : ''}

  <script>window.onload = () => { window.print(); }<\/script>
</body>
</html>`);
    win.document.close();
  }

  return { render };
})();

// ── New manual order ──────────────────────────────────────────────────────────
const NewOrderPage = (() => {
  let cart     = [];
  let products = [];

  async function render(container) {
    container.innerHTML = `
      <div class="page">
        <div class="page-header">
          <button class="back-btn" id="order-back">
            <svg viewBox="0 0 24 24"><path d="M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z"/></svg>
          </button>
          <h1>New Order</h1>
        </div>
        <div class="content">
          <!-- Customer -->
          <div class="card">
            <div style="font-weight:700;font-size:14px;margin-bottom:12px;">Customer</div>
            <div class="form-group">
              <label>Email *</label>
              <input id="o-email" type="email" class="form-control" placeholder="customer@example.com">
            </div>
            <div class="form-group">
              <label>Full Name</label>
              <input id="o-name" type="text" class="form-control" placeholder="Customer name">
            </div>
          </div>

          <!-- Product search -->
          <div class="card">
            <div style="font-weight:700;font-size:14px;margin-bottom:10px;">Add Products</div>
            <div class="search-bar" style="margin-bottom:0;">
              <div class="search-icon">
                <svg viewBox="0 0 24 24"><path d="M15.5 14h-.79l-.28-.27A6.47 6.47 0 0 0 16 9.5 6.5 6.5 0 1 0 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z"/></svg>
              </div>
              <input id="prod-search" type="search" placeholder="Search products…" autocomplete="off">
            </div>
            <div id="prod-results"></div>
          </div>

          <!-- Cart -->
          <div class="card" id="cart-card" style="display:none;">
            <div style="font-weight:700;font-size:14px;margin-bottom:10px;">Cart</div>
            <div id="cart-items"></div>
          </div>

          <!-- Order details -->
          <div class="card">
            <div style="font-weight:700;font-size:14px;margin-bottom:12px;">Details</div>
            <div class="form-group">
              <label>Discount ($)</label>
              <input id="o-discount" type="number" class="form-control" placeholder="0.00" min="0" step="0.01" value="0">
            </div>
            <div class="form-group">
              <label>Shipping Cost ($)</label>
              <input id="o-shipping" type="number" class="form-control" placeholder="0.00" min="0" step="0.01" value="0">
            </div>
            <div class="form-group">
              <label>Notes</label>
              <textarea id="o-notes" class="form-control" rows="2" placeholder="Optional notes…"></textarea>
            </div>
          </div>

          <!-- Total & Submit -->
          <div class="card" id="order-total-card">
            <div id="order-total-display" class="text-muted text-small">Add items to see total</div>
            <button id="submit-order" class="btn btn-primary btn-full mt-12">Create Order</button>
          </div>
        </div>
      </div>`;

    document.getElementById('order-back').addEventListener('click', () => history.back());
    bindProductSearch();
    bindOrderInputs();
    document.getElementById('submit-order').addEventListener('click', submitOrder);
  }

  function bindProductSearch() {
    let timer = null;
    document.getElementById('prod-search').addEventListener('input', e => {
      clearTimeout(timer);
      const q = e.target.value.trim();
      if (!q) { document.getElementById('prod-results').innerHTML = ''; return; }
      timer = setTimeout(() => searchProducts(q), 350);
    });
  }

  async function searchProducts(q) {
    const resultsEl = document.getElementById('prod-results');
    resultsEl.innerHTML = '<div class="skeleton skeleton-line" style="margin-top:8px;"></div>';
    try {
      const data = await Api.get(`/api/inventory?q=${encodeURIComponent(q)}&page=1`);
      products = data.items;
      if (products.length === 0) {
        resultsEl.innerHTML = '<p class="text-muted text-small" style="margin-top:8px;">No products found</p>';
        return;
      }
      resultsEl.innerHTML = `<div class="search-results" style="margin-top:8px;">
        ${products.slice(0, 8).map(p => `
          <div class="search-result-item" data-id="${p.productID}">
            <div class="sri-name">${p.productName}</div>
            <div class="sri-sku">${p.sku} · Stock: ${p.currentStock} · ${App.fmt$(p.retailPrice)}</div>
          </div>`).join('')}
      </div>`;

      resultsEl.querySelectorAll('.search-result-item').forEach(el => {
        el.addEventListener('click', () => {
          const p = products.find(x => x.productID == el.dataset.id);
          if (p) addToCart(p);
          document.getElementById('prod-search').value = '';
          resultsEl.innerHTML = '';
        });
      });
    } catch { resultsEl.innerHTML = ''; }
  }

  function addToCart(product) {
    const existing = cart.find(c => c.productId === product.productID);
    if (existing) { existing.qty++; }
    else {
      cart.push({
        productId: product.productID,
        sku:   product.sku,
        title: product.productName,
        qty:   1,
        price: product.retailPrice,
      });
    }
    renderCart();
    updateTotal();
  }

  function renderCart() {
    const cartCard = document.getElementById('cart-card');
    const cartEl   = document.getElementById('cart-items');
    if (cart.length === 0) { cartCard.style.display = 'none'; return; }
    cartCard.style.display = '';
    cartEl.innerHTML = cart.map((item, i) => `
      <div class="cart-item">
        <div class="ci-info">
          <div class="ci-title">${item.title}</div>
          <div class="ci-price">${item.sku} · ${App.fmt$(item.price)} each</div>
        </div>
        <div class="ci-qty-control">
          <button class="qty-btn" data-action="dec" data-idx="${i}">−</button>
          <span class="qty-display">${item.qty}</span>
          <button class="qty-btn" data-action="inc" data-idx="${i}">+</button>
        </div>
        <button class="ci-remove" data-idx="${i}">
          <svg viewBox="0 0 24 24"><path d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z"/></svg>
        </button>
      </div>`).join('');

    cartEl.querySelectorAll('[data-action]').forEach(btn => {
      btn.addEventListener('click', () => {
        const idx = parseInt(btn.dataset.idx, 10);
        if (btn.dataset.action === 'inc') cart[idx].qty++;
        else if (cart[idx].qty > 1)       cart[idx].qty--;
        else                               cart.splice(idx, 1);
        renderCart();
        updateTotal();
      });
    });
    cartEl.querySelectorAll('.ci-remove').forEach(btn => {
      btn.addEventListener('click', () => {
        cart.splice(parseInt(btn.dataset.idx, 10), 1);
        renderCart();
        updateTotal();
      });
    });
  }

  function bindOrderInputs() {
    ['o-discount', 'o-shipping'].forEach(id => {
      document.getElementById(id)?.addEventListener('input', updateTotal);
    });
  }

  function updateTotal() {
    const discount  = parseFloat(document.getElementById('o-discount')?.value || 0) || 0;
    const shipping  = parseFloat(document.getElementById('o-shipping')?.value || 0) || 0;
    const subtotal  = cart.reduce((s, i) => s + i.qty * i.price, 0);
    const total     = Math.max(0, subtotal - discount + shipping);
    const el        = document.getElementById('order-total-display');
    if (!el) return;
    el.innerHTML = `
      <div class="total-row"><span>Subtotal</span><span>${App.fmt$(subtotal)}</span></div>
      ${discount > 0 ? `<div class="total-row text-success"><span>Discount</span><span>−${App.fmt$(discount)}</span></div>` : ''}
      ${shipping > 0 ? `<div class="total-row"><span>Shipping</span><span>${App.fmt$(shipping)}</span></div>` : ''}
      <div class="total-row grand"><span>Total</span><span>${App.fmt$(total)}</span></div>`;
  }

  async function submitOrder() {
    const email    = document.getElementById('o-email').value.trim();
    const name     = document.getElementById('o-name').value.trim();
    const discount = parseFloat(document.getElementById('o-discount').value || 0) || 0;
    const shipping = parseFloat(document.getElementById('o-shipping').value || 0) || 0;
    const notes    = document.getElementById('o-notes').value.trim();

    if (!email)          { App.toast('Customer email is required', 'error'); return; }
    if (cart.length === 0){ App.toast('Add at least one item', 'error'); return; }

    const btn = document.getElementById('submit-order');
    btn.disabled = true;
    btn.textContent = 'Creating…';

    try {
      const payload = {
        customerEmail:  email,
        customerName:   name,
        orderDate:      new Date().toISOString(),
        currency:       'CAD',
        orderType:      'Manual',
        notes:          notes || null,
        shippingCost:   shipping,
        discountAmount: discount,
        items: cart.map(i => ({
          productId: i.productId,
          sku:       i.sku,
          title:     i.title,
          quantity:  i.qty,
          unitPrice: i.price,
        })),
      };
      const res = await Api.post('/api/orders', payload);
      App.toast('Order created!', 'success');
      cart = [];
      App.navigate(`orders/${res.salesOrderId}`);
    } catch (err) {
      App.toast(err.message, 'error');
      btn.disabled    = false;
      btn.textContent = 'Create Order';
    }
  }

  return { render };
})();
