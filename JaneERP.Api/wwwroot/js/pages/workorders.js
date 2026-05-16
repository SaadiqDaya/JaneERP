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
  let _woId = null;
  let _locations = [];

  async function render(container, woId) {
    _woId = woId;
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

    // Pre-load locations for the complete form
    try { _locations = await Api.get('/api/inventory/locations'); } catch { _locations = []; }

    await loadDetail(woId);
  }

  async function loadDetail(woId) {
    const content = document.getElementById('wo-detail-content');
    try {
      const wo = await Api.get(`/api/work-orders/${woId}`);
      const progress = wo.quantity > 0
        ? Math.round((wo.completedQty / wo.quantity) * 100) : 0;

      const actionBtn = buildActionButton(wo);

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

        ${actionBtn}

        <!-- Lot picker panel (Go Live) -->
        <div id="wo-lot-panel" style="display:none;margin-top:12px;"></div>

        <!-- Verify & Complete panel -->
        <div id="wo-complete-panel" style="display:none;margin-top:12px;"></div>`;

      bindActionButtons(wo);

    } catch (err) {
      content.innerHTML = `<div class="empty-state"><p>${err.message}</p></div>`;
    }
  }

  function buildActionButton(wo) {
    if (wo.status === 'Pending') {
      return `<button class="btn btn-primary btn-full" id="wo-golive-btn">Go Live — Lock Inventory</button>`;
    }
    if (wo.status === 'Live') {
      return `
        <div class="card" style="background:var(--bg-2);padding:12px 16px;">
          <div style="font-weight:700;margin-bottom:4px;">Inventory Locked</div>
          <div class="text-small text-muted">Lots are reserved. Start a cook session to begin manufacturing.</div>
        </div>
        <button class="btn btn-primary btn-full" id="wo-cook-link-btn" style="margin-top:8px;">Go to Cooking</button>`;
    }
    if (wo.status === 'InProgress') {
      return `<button class="btn btn-primary btn-full" id="wo-verify-btn">Verify &amp; Complete</button>`;
    }
    return '';
  }

  function bindActionButtons(wo) {
    document.getElementById('wo-golive-btn')?.addEventListener('click', () => showLotPicker(wo.workOrderID));
    document.getElementById('wo-cook-link-btn')?.addEventListener('click', () => App.navigate('cooking'));
    document.getElementById('wo-verify-btn')?.addEventListener('click', () => showCompletePanel(wo));
  }

  // ── Go Live: lot picker ─────────────────────────────────────────────────────

  async function showLotPicker(woId) {
    const btn   = document.getElementById('wo-golive-btn');
    const panel = document.getElementById('wo-lot-panel');
    if (btn) { btn.disabled = true; btn.textContent = 'Loading lots…'; }
    panel.style.display = 'none';

    try {
      const [parts, bom] = await Promise.all([
        Api.get(`/api/work-orders/${woId}/lot-availability`),
        Api.get(`/api/work-orders/${woId}/bom-preview`),
      ]);

      const bomMap = Object.fromEntries(bom.map(r => [r.partID, r]));

      panel.innerHTML = `
        <div class="card">
          <div style="font-size:15px;font-weight:800;margin-bottom:12px;">Lock Inventory Lots</div>
          <div class="text-small text-muted" style="margin-bottom:16px;">
            Select which lots to reserve for this work order. Lots are sorted earliest-expiry first (FEFO).
          </div>
          <div id="lot-parts-list"></div>
          <div class="divider" style="margin:16px 0;"></div>
          <button class="btn btn-primary btn-full" id="lot-confirm-btn">Confirm &amp; Go Live</button>
          <button class="btn btn-full" id="lot-cancel-btn" style="margin-top:8px;background:var(--bg-2);">Cancel</button>
        </div>`;

      panel.style.display = 'block';
      if (btn) btn.style.display = 'none';

      const partsEl = document.getElementById('lot-parts-list');
      partsEl.innerHTML = parts.map(part => {
        const hasSufficientStock = part.lots.some(l => l.available > 0);
        const totalAvail = part.lots.reduce((s, l) => s + l.available, 0);
        return `
          <div class="lot-part" data-part-id="${part.partID}" style="margin-bottom:16px;">
            <div style="font-weight:700;margin-bottom:4px;">
              ${part.partNumber} — ${part.partName}
            </div>
            <div class="text-small text-muted" style="margin-bottom:8px;">
              Required: <strong>${part.required}</strong> &nbsp;|&nbsp;
              Available across lots: <strong style="color:${totalAvail < part.required ? 'var(--danger)' : 'var(--success)'}">${totalAvail}</strong>
            </div>
            ${part.lots.length === 0 ? `<div class="text-small" style="color:var(--danger);">No stock available</div>` :
              part.lots.map(lot => {
                const suggestedQty = Math.min(lot.available, part.required);
                const isExpired = lot.expirationDate && new Date(lot.expirationDate) < new Date();
                return `
                <div class="lot-row" data-lot-id="${lot.lotID}" style="padding:10px;background:var(--bg-2);border-radius:8px;margin-bottom:6px;${isExpired ? 'border:1px solid var(--danger);' : ''}">
                  <div style="display:flex;justify-content:space-between;align-items:center;gap:8px;">
                    <div>
                      <div class="text-small" style="font-weight:600;">
                        ${lot.lotID === 0 ? 'Unlotted stock' : (lot.lotNumber || 'No lot #')}
                        ${isExpired ? ' <span style="color:var(--danger);">(EXPIRED)</span>' : ''}
                      </div>
                      <div class="text-small text-muted">
                        ${lot.locationName}
                        ${lot.expirationDate ? ' · Exp: ' + App.fmtDate(lot.expirationDate) : ''}
                        ${lot.alreadyReserved > 0 ? ' · Reserved by others: ' + lot.alreadyReserved : ''}
                      </div>
                      <div class="text-small">Available: <strong>${lot.available}</strong></div>
                    </div>
                    <div style="display:flex;flex-direction:column;align-items:flex-end;gap:4px;min-width:80px;">
                      <div class="text-small text-muted">Lock qty:</div>
                      <input type="number" class="lot-qty-input"
                             data-part-id="${part.partID}"
                             data-lot-id="${lot.lotID}"
                             min="0" max="${lot.available}"
                             value="${suggestedQty}"
                             style="width:72px;padding:4px 8px;border:1px solid var(--border);border-radius:6px;text-align:center;font-size:15px;font-weight:700;">
                    </div>
                  </div>
                </div>`;
              }).join('')
            }
          </div>`;
      }).join('');

      document.getElementById('lot-confirm-btn').addEventListener('click', () => confirmGoLive(woId, parts));
      document.getElementById('lot-cancel-btn').addEventListener('click', () => {
        panel.style.display = 'none';
        if (btn) { btn.disabled = false; btn.textContent = 'Go Live — Lock Inventory'; btn.style.display = ''; }
      });

    } catch (err) {
      App.toast(err.message, 'error');
      if (btn) { btn.disabled = false; btn.textContent = 'Go Live — Lock Inventory'; }
    }
  }

  async function confirmGoLive(woId, parts) {
    const confirmBtn = document.getElementById('lot-confirm-btn');
    confirmBtn.disabled = true;
    confirmBtn.textContent = 'Going Live…';

    // Collect reservations from inputs
    const reservations = [];
    document.querySelectorAll('.lot-qty-input').forEach(inp => {
      const qty = parseInt(inp.value, 10);
      if (qty > 0) {
        reservations.push({
          lotID:    parseInt(inp.dataset.lotId, 10),
          partID:   parseInt(inp.dataset.partId, 10),
          quantity: qty,
        });
      }
    });

    // Validate: every part must have at least 1 unit reserved
    const reservedByPart = {};
    reservations.forEach(r => { reservedByPart[r.partID] = (reservedByPart[r.partID] || 0) + r.quantity; });
    const short = parts.filter(p => (reservedByPart[p.partID] || 0) < p.required);
    if (short.length > 0) {
      const names = short.map(p => `${p.partName} (need ${p.required}, locked ${reservedByPart[p.partID] || 0})`).join(', ');
      App.toast(`Insufficient locked qty: ${names}`, 'error');
      confirmBtn.disabled = false;
      confirmBtn.textContent = 'Confirm & Go Live';
      return;
    }

    try {
      await Api.post(`/api/work-orders/${woId}/go-live`, { reservations });
      App.toast('Work order is Live — inventory locked', 'success');
      await loadDetail(woId);
    } catch (err) {
      App.toast(err.message, 'error');
      confirmBtn.disabled = false;
      confirmBtn.textContent = 'Confirm & Go Live';
    }
  }

  // ── Verify & Complete panel ─────────────────────────────────────────────────

  async function showCompletePanel(wo) {
    const btn   = document.getElementById('wo-verify-btn');
    const panel = document.getElementById('wo-complete-panel');
    if (btn) btn.style.display = 'none';

    const locOptions = _locations.map(l =>
      `<option value="${l.locationID}">${l.locationName}</option>`).join('');

    panel.innerHTML = `
      <div class="card">
        <div style="font-size:15px;font-weight:800;margin-bottom:12px;">Verify &amp; Complete</div>
        <div style="display:grid;gap:14px;">
          <div>
            <label class="text-small text-muted">Units Completed (target: ${wo.quantity})</label>
            <input type="number" id="complete-qty" class="input" min="0" max="${wo.quantity}"
                   value="${wo.quantity}" style="margin-top:4px;">
          </div>
          <div>
            <label class="text-small text-muted">Scrapped Units</label>
            <input type="number" id="complete-scrap" class="input" min="0" value="0" style="margin-top:4px;">
          </div>
          ${locOptions.length > 0 ? `
          <div>
            <label class="text-small text-muted">Output Location (finished goods)</label>
            <select id="complete-location" class="input" style="margin-top:4px;">
              <option value="">Product default</option>
              ${locOptions}
            </select>
          </div>` : ''}
          <div>
            <label class="text-small text-muted">Notes (optional)</label>
            <textarea id="complete-notes" class="input" rows="2" style="margin-top:4px;resize:none;"></textarea>
          </div>
        </div>
        <div class="divider" style="margin:16px 0;"></div>
        <button class="btn btn-primary btn-full" id="complete-confirm-btn">Complete Work Order</button>
        <button class="btn btn-full" id="complete-cancel-btn" style="margin-top:8px;background:var(--bg-2);">Cancel</button>
      </div>`;

    panel.style.display = 'block';

    document.getElementById('complete-confirm-btn').addEventListener('click', () => confirmComplete(wo.workOrderID));
    document.getElementById('complete-cancel-btn').addEventListener('click', () => {
      panel.style.display = 'none';
      if (btn) btn.style.display = '';
    });
  }

  async function confirmComplete(woId) {
    const confirmBtn = document.getElementById('complete-confirm-btn');
    confirmBtn.disabled = true;
    confirmBtn.textContent = 'Completing…';

    const completedQty = parseInt(document.getElementById('complete-qty').value, 10) || 0;
    const scrapQty     = parseInt(document.getElementById('complete-scrap').value, 10) || 0;
    const locationEl   = document.getElementById('complete-location');
    const locationID   = locationEl && locationEl.value ? parseInt(locationEl.value, 10) : null;
    const notes        = document.getElementById('complete-notes').value.trim() || null;

    if (completedQty + scrapQty === 0) {
      App.toast('Enter at least 1 completed or scrapped unit', 'error');
      confirmBtn.disabled = false;
      confirmBtn.textContent = 'Complete Work Order';
      return;
    }

    try {
      await Api.post(`/api/work-orders/${woId}/complete`, {
        completedQty, scrapQty, locationID, notes,
      });
      App.toast('Work order completed', 'success');
      await loadDetail(woId);
    } catch (err) {
      App.toast(err.message, 'error');
      confirmBtn.disabled = false;
      confirmBtn.textContent = 'Complete Work Order';
    }
  }

  return { render };
})();

// Work order status badge helper (module-scoped so both pages can use it)
function woStatusBadge(status) {
  const map = {
    'Pending':    'badge-draft',
    'Live':       'badge-wip',
    'InProgress': 'badge-wip',
    'Complete':   'badge-complete',
    'Completed':  'badge-complete',
    'Cancelled':  'badge-draft',
  };
  return `<span class="badge ${map[status] || 'badge-draft'}">${status}</span>`;
}
