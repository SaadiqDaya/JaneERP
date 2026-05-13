const CycleCountPage = (() => {
  let locations = [];
  let entries   = [];
  let currentLocationId = null;
  let showOverdueOnly   = false;

  async function render(container) {
    container.innerHTML = `
      <div class="page">
        <div class="page-header">
          <h1>Cycle Count</h1>
          <div class="header-actions">
            <button class="btn-icon" id="cc-filter-btn" title="Filter overdue">
              <svg viewBox="0 0 24 24"><path d="M10 18h4v-2h-4v2zM3 6v2h18V6H3zm3 7h12v-2H6v2z"/></svg>
            </button>
          </div>
        </div>
        <div class="content">
          <div class="form-group" style="margin-bottom:12px;">
            <select id="cc-location" class="form-control">
              <option value="">All Locations</option>
            </select>
          </div>
          <div id="cc-filter-bar" style="display:flex;align-items:center;gap:10px;margin-bottom:12px;">
            <label style="display:flex;align-items:center;gap:6px;font-size:13px;font-weight:600;cursor:pointer;">
              <input type="checkbox" id="cc-overdue-toggle" style="width:16px;height:16px;">
              Show overdue only (30+ days)
            </label>
          </div>
          <div id="cc-list"></div>
        </div>
      </div>`;

    // Load locations
    try {
      locations = await Api.get('/api/locations');
      const sel = document.getElementById('cc-location');
      locations.forEach(l => {
        sel.insertAdjacentHTML('beforeend', `<option value="${l.locationID}">${l.locationName}</option>`);
      });
    } catch {}

    document.getElementById('cc-location').addEventListener('change', async e => {
      currentLocationId = e.target.value ? parseInt(e.target.value) : null;
      await loadEntries();
    });

    document.getElementById('cc-overdue-toggle').addEventListener('change', e => {
      showOverdueOnly = e.target.checked;
      renderEntries();
    });

    await loadEntries();
  }

  async function loadEntries() {
    const listEl = document.getElementById('cc-list');
    listEl.innerHTML = App.skeletonCards(5);
    try {
      const url = '/api/cycle-count/entries' +
        (currentLocationId ? `?locationId=${currentLocationId}` : '');
      entries = await Api.get(url);
      renderEntries();
    } catch (err) {
      listEl.innerHTML = `<div class="empty-state"><p>${err.message}</p></div>`;
    }
  }

  function isOverdue(entry) {
    if (!entry.lastVerifiedAt) return true;
    const d = new Date(entry.lastVerifiedAt);
    return (Date.now() - d.getTime()) > 30 * 24 * 60 * 60 * 1000;
  }

  function renderEntries() {
    const listEl = document.getElementById('cc-list');
    const visible = showOverdueOnly ? entries.filter(isOverdue) : entries;

    if (visible.length === 0) {
      listEl.innerHTML = `<div class="empty-state">
        <svg viewBox="0 0 24 24"><path d="M9 11H7v2h2v-2zm4 0h-2v2h2v-2zm4 0h-2v2h2v-2zm2-7h-1V2h-2v2H8V2H6v2H5c-1.11 0-1.99.9-1.99 2L3 20c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2z"/></svg>
        <p>${showOverdueOnly ? 'No overdue items' : 'No items to count'}</p></div>`;
      return;
    }

    listEl.innerHTML = visible.map((e, idx) => {
      const overdue  = isOverdue(e);
      const locLabel = e.locationName ? `<span class="badge badge-draft">${e.locationName}</span>` : '';
      const lastVerified = e.lastVerifiedAt
        ? `<span class="text-muted text-small">Last: ${App.fmtDate(e.lastVerifiedAt)} by ${e.lastVerifiedBy}</span>`
        : `<span class="badge badge-overdue">Never counted</span>`;

      return `
        <div class="cc-item" id="cc-item-${idx}" data-idx="${idx}">
          <div class="cc-item-header">
            <div>
              <div class="cc-name">${e.productName}</div>
              <div class="cc-sku">${e.sKU} ${locLabel}</div>
            </div>
            <div style="text-align:right;">${lastVerified}</div>
          </div>
          <div class="cc-qty-row">
            <div class="cc-system" title="System quantity">${e.systemQty} sys</div>
            <input class="cc-actual" type="number" min="0"
                   placeholder="${e.systemQty}" value=""
                   data-system="${e.systemQty}" data-idx="${idx}"
                   inputmode="numeric">
            <div class="cc-diff" id="cc-diff-${idx}">—</div>
          </div>
          <button class="verify-btn" data-idx="${idx}">Verify</button>
        </div>`;
    }).join('');

    // Bind actual qty input → show difference
    listEl.querySelectorAll('.cc-actual').forEach(inp => {
      inp.addEventListener('input', () => {
        const idx     = parseInt(inp.dataset.idx, 10);
        const system  = parseInt(inp.dataset.system, 10);
        const actual  = inp.value !== '' ? parseInt(inp.value, 10) : null;
        const diffEl  = document.getElementById(`cc-diff-${idx}`);
        if (actual === null || isNaN(actual)) {
          diffEl.textContent = '—'; diffEl.className = 'cc-diff';
        } else {
          const d = actual - system;
          diffEl.textContent = d > 0 ? '+' + d : d === 0 ? '✓' : d;
          diffEl.className = 'cc-diff' + (d > 0 ? ' pos' : d < 0 ? ' neg' : '');
        }
      });
    });

    // Bind verify buttons
    listEl.querySelectorAll('.verify-btn').forEach(btn => {
      btn.addEventListener('click', () => verifyItem(parseInt(btn.dataset.idx, 10), visible));
    });
  }

  async function verifyItem(idx, visible) {
    const entry   = visible[idx];
    const inp     = document.querySelector(`.cc-actual[data-idx="${idx}"]`);
    const actualQty = inp && inp.value !== '' ? parseInt(inp.value, 10) : entry.systemQty;
    const locationId = entry.locationID ?? (currentLocationId ?? 0);

    if (isNaN(actualQty) || actualQty < 0) {
      App.toast('Enter a valid quantity', 'error');
      return;
    }

    const btn = document.querySelector(`.verify-btn[data-idx="${idx}"]`);
    btn.disabled = true;
    btn.textContent = 'Saving…';

    try {
      await Api.post('/api/cycle-count/verify', {
        productId:  entry.productID,
        locationId: locationId,
        systemQty:  entry.systemQty,
        actualQty:  actualQty,
      });

      const card = document.getElementById(`cc-item-${idx}`);
      card.classList.add('verified');
      btn.textContent = '✓ Verified';
      btn.className   = 'verify-btn verified-state';

      // Update the entry locally so it shows the new verified time
      entry.lastVerifiedAt = new Date().toISOString();
      entry.systemQty      = actualQty;

      App.toast(`${entry.productName} verified`, 'success');
    } catch (err) {
      App.toast(err.message, 'error');
      btn.disabled    = false;
      btn.textContent = 'Verify';
    }
  }

  return { render };
})();
