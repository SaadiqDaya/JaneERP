// ── Offline queue (IndexedDB) ─────────────────────────────────────────────────
const CycleQueue = (() => {
  const DB = 'janeerp-cc-queue';
  const STORE = 'verifications';

  function open() {
    return new Promise((res, rej) => {
      const r = indexedDB.open(DB, 1);
      r.onupgradeneeded = e => e.target.result.createObjectStore(STORE, { autoIncrement: true });
      r.onsuccess = e => res(e.target.result);
      r.onerror   = e => rej(e.target.error);
    });
  }

  async function push(payload) {
    const db = await open();
    return new Promise((res, rej) => {
      const tx = db.transaction(STORE, 'readwrite');
      tx.objectStore(STORE).add(payload);
      tx.oncomplete = res;
      tx.onerror    = e => rej(e.target.error);
    });
  }

  async function getAllWithKeys() {
    const db = await open();
    return new Promise((res, rej) => {
      const results = [];
      const tx = db.transaction(STORE, 'readonly');
      tx.objectStore(STORE).openCursor().onsuccess = e => {
        const c = e.target.result;
        if (c) { results.push({ key: c.key, value: c.value }); c.continue(); }
        else res(results);
      };
      tx.onerror = e => rej(e.target.error);
    });
  }

  async function deleteKey(key) {
    const db = await open();
    return new Promise((res, rej) => {
      const tx = db.transaction(STORE, 'readwrite');
      tx.objectStore(STORE).delete(key);
      tx.oncomplete = res;
      tx.onerror    = e => rej(e.target.error);
    });
  }

  async function flush() {
    if (!navigator.onLine) return 0;
    const items = await getAllWithKeys();
    let flushed = 0;
    for (const { key, value } of items) {
      try {
        await Api.post('/api/cycle-count/verify', value);
        await deleteKey(key);
        flushed++;
      } catch { break; } // network error — stop and retry next time
    }
    return flushed;
  }

  async function count() {
    const items = await getAllWithKeys();
    return items.length;
  }

  return { push, flush, count };
})();

// ── Cycle Count page ──────────────────────────────────────────────────────────
const CycleCountPage = (() => {
  let locations          = [];
  let entries            = [];
  let currentLocIdx      = 0;  // index into [allLocations] array; 0 = "All Locations"
  let allLocations       = []; // [{ locationID: null, locationName: 'All Locations' }, ...real locs]
  let showOverdueOnly    = false;

  async function render(container) {
    container.innerHTML = `
      <div class="page">
        <div class="page-header">
          <h1>Cycle Count</h1>
          <div class="header-actions">
            <button class="btn-icon" id="cc-scan-btn" title="Scan barcode">
              <svg viewBox="0 0 24 24"><path d="M1 5h2v14H1V5zm4 0h1v14H5V5zm2 0h3v14H7V5zm4 0h1v14h-1V5zm3 0h2v14h-2V5zm3 0h1v14h-1V5zm2 0h2v14h-2V5z"/></svg>
            </button>
            <button class="btn-icon" id="cc-overdue-btn" title="Toggle overdue filter">
              <svg viewBox="0 0 24 24"><path d="M10 18h4v-2h-4v2zM3 6v2h18V6H3zm3 7h12v-2H6v2z"/></svg>
            </button>
          </div>
        </div>
        <div id="cc-queue-banner" style="display:none;background:var(--warning-lt);color:var(--warning);
             border-radius:8px;padding:8px 12px;font-size:13px;font-weight:600;
             margin:8px 16px 0;text-align:center;"></div>
        <div class="content">

          <!-- Location navigator: arrows + dropdown in one row -->
          <div class="cc-loc-nav">
            <button class="cc-loc-btn" id="cc-prev-loc" title="Previous location">
              <svg viewBox="0 0 24 24"><path d="M15.41 7.41L14 6l-6 6 6 6 1.41-1.41L10.83 12z"/></svg>
            </button>
            <select id="cc-location" class="form-control" style="flex:1;margin:0;font-weight:700;font-size:14px;">
              <option value="">All Locations</option>
            </select>
            <button class="cc-loc-btn" id="cc-next-loc" title="Next location">
              <svg viewBox="0 0 24 24"><path d="M10 6L8.59 7.41 13.17 12l-4.58 4.59L10 18l6-6z"/></svg>
            </button>
          </div>

          <!-- Overdue filter indicator -->
          <div id="cc-overdue-bar" style="display:none;background:var(--warning-lt);
               color:var(--warning);border-radius:8px;padding:8px 12px;
               font-size:13px;font-weight:600;margin-bottom:10px;text-align:center;">
            Showing overdue only (30+ days since last count)
          </div>

          <div id="cc-list"></div>
        </div>
      </div>`;

    // Flush offline queue + update banner on load
    flushQueue();
    window.addEventListener('online', flushQueue);

    // Scan button
    document.getElementById('cc-scan-btn').addEventListener('click', openBarcodeScanner);

    // Overdue toggle
    document.getElementById('cc-overdue-btn').addEventListener('click', () => {
      showOverdueOnly = !showOverdueOnly;
      document.getElementById('cc-overdue-bar').style.display = showOverdueOnly ? '' : 'none';
      const btn = document.getElementById('cc-overdue-btn');
      btn.style.background = showOverdueOnly ? 'var(--warning-lt)' : '';
      btn.style.color       = showOverdueOnly ? 'var(--warning)' : '';
      renderEntries();
    });

    // Location navigation
    document.getElementById('cc-prev-loc').addEventListener('click', () => changeLocation(-1));
    document.getElementById('cc-next-loc').addEventListener('click', () => changeLocation(+1));

    // Build allLocations list (first slot = All)
    try {
      locations = await Api.get('/api/locations');
    } catch {}

    allLocations = [
      { locationID: null, locationName: 'All Locations' },
      ...locations
    ];

    // Populate dropdown
    const sel = document.getElementById('cc-location');
    locations.forEach(l => {
      sel.insertAdjacentHTML('beforeend',
        `<option value="${l.locationID}">${l.locationName}</option>`);
    });

    // Dropdown change → sync arrows
    sel.addEventListener('change', async () => {
      const val = sel.value ? parseInt(sel.value, 10) : null;
      currentLocIdx = allLocations.findIndex(l => l.locationID === val);
      if (currentLocIdx < 0) currentLocIdx = 0;
      updateArrows();
      await loadEntries();
    });

    currentLocIdx = 0;
    updateArrows();
    await loadEntries();
  }

  function updateArrows() {
    const loc = allLocations[currentLocIdx];

    // Sync dropdown to match arrow position
    const sel = document.getElementById('cc-location');
    if (sel) sel.value = loc?.locationID ?? '';

    // Disable arrows at boundaries
    const prevBtn = document.getElementById('cc-prev-loc');
    const nextBtn = document.getElementById('cc-next-loc');
    if (prevBtn) prevBtn.disabled = currentLocIdx === 0;
    if (nextBtn) nextBtn.disabled = currentLocIdx === allLocations.length - 1;
  }

  async function changeLocation(delta) {
    const next = currentLocIdx + delta;
    if (next < 0 || next >= allLocations.length) return;
    currentLocIdx = next;
    updateArrows();
    await loadEntries();
  }

  async function loadEntries() {
    const listEl = document.getElementById('cc-list');
    listEl.innerHTML = App.skeletonCards(5);
    try {
      const loc = allLocations[currentLocIdx];
      const url = '/api/cycle-count/entries' +
        (loc?.locationID ? `?locationId=${loc.locationID}` : '');
      entries = await Api.get(url);
      renderEntries();
    } catch (err) {
      listEl.innerHTML = `<div class="empty-state"><p>${err.message}</p></div>`;
    }
  }

  function isOverdue(entry) {
    if (!entry.lastVerifiedAt) return true;
    return (Date.now() - new Date(entry.lastVerifiedAt).getTime()) > 30 * 24 * 60 * 60 * 1000;
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
      const locLabel = e.locationName
        ? `<span class="badge badge-draft">${e.locationName}</span>` : '';
      const lastVerified = e.lastVerifiedAt
        ? `<span class="text-muted text-small">Last: ${App.fmtDate(e.lastVerifiedAt)}</span>`
        : `<span class="badge badge-overdue">Never counted</span>`;

      return `
        <div class="cc-item${overdue ? ' overdue' : ''}" id="cc-item-${idx}" data-idx="${idx}">
          <div class="cc-item-header">
            <div style="flex:1;min-width:0;">
              <div class="cc-name">${e.productName}</div>
              <div class="cc-sku">${e.sku} ${locLabel}</div>
            </div>
            <div style="text-align:right;flex-shrink:0;">${lastVerified}</div>
          </div>

          <div class="cc-qty-row">
            <!-- System qty display -->
            <div class="cc-system" title="System quantity">
              <div style="font-size:10px;color:var(--text-2);margin-bottom:1px;">System</div>
              <div style="font-size:18px;font-weight:800;">${e.systemQty}</div>
            </div>

            <!-- Diff -->
            <div class="cc-diff" id="cc-diff-${idx}">—</div>

            <!-- Actual qty with +/- -->
            <div class="cc-actual-group">
              <div style="font-size:10px;color:var(--text-2);margin-bottom:4px;text-align:center;">Actual</div>
              <div style="display:flex;align-items:center;gap:6px;">
                <button class="qty-btn cc-dec" data-idx="${idx}" style="width:34px;height:34px;">−</button>
                <input class="cc-actual" type="number" min="0"
                       value="${e.systemQty}"
                       data-system="${e.systemQty}" data-idx="${idx}"
                       inputmode="numeric"
                       style="width:60px;text-align:center;font-size:18px;font-weight:800;
                              padding:6px 4px;border:1.5px solid var(--border);border-radius:8px;
                              outline:none;background:var(--surface);">
                <button class="qty-btn cc-inc" data-idx="${idx}" style="width:34px;height:34px;">+</button>
              </div>
            </div>
          </div>

          <button class="verify-btn" data-idx="${idx}">Verify</button>
        </div>`;
    }).join('');

    // Bind +/- buttons and input → update diff
    listEl.querySelectorAll('.cc-actual').forEach(inp => {
      updateDiff(inp);
      inp.addEventListener('input', () => updateDiff(inp));
    });

    listEl.querySelectorAll('.cc-inc').forEach(btn => {
      btn.addEventListener('click', () => {
        const inp = listEl.querySelector(`.cc-actual[data-idx="${btn.dataset.idx}"]`);
        if (inp) { inp.value = (parseInt(inp.value) || 0) + 1; updateDiff(inp); }
      });
    });

    listEl.querySelectorAll('.cc-dec').forEach(btn => {
      btn.addEventListener('click', () => {
        const inp = listEl.querySelector(`.cc-actual[data-idx="${btn.dataset.idx}"]`);
        if (inp) {
          const cur = parseInt(inp.value) || 0;
          if (cur > 0) { inp.value = cur - 1; updateDiff(inp); }
        }
      });
    });

    // Bind verify buttons
    listEl.querySelectorAll('.verify-btn').forEach(btn => {
      btn.addEventListener('click', () => verifyItem(parseInt(btn.dataset.idx, 10), visible));
    });
  }

  function updateDiff(inp) {
    const idx    = parseInt(inp.dataset.idx, 10);
    const system = parseInt(inp.dataset.system, 10);
    const actual = inp.value !== '' ? parseInt(inp.value, 10) : null;
    const diffEl = document.getElementById(`cc-diff-${idx}`);
    if (!diffEl) return;
    if (actual === null || isNaN(actual)) {
      diffEl.textContent = '—';
      diffEl.className   = 'cc-diff';
    } else {
      const d = actual - system;
      diffEl.textContent = d > 0 ? '+' + d : d === 0 ? '✓' : String(d);
      diffEl.className   = 'cc-diff' + (d > 0 ? ' pos' : d < 0 ? ' neg' : '');
    }
  }

  async function verifyItem(idx, visible) {
    const entry      = visible[idx];
    const inp        = document.querySelector(`.cc-actual[data-idx="${idx}"]`);
    const actualQty  = inp && inp.value !== '' ? parseInt(inp.value, 10) : entry.systemQty;
    const locationId = entry.locationID ?? (allLocations[currentLocIdx]?.locationID ?? 0);

    if (isNaN(actualQty) || actualQty < 0) {
      App.toast('Enter a valid quantity', 'error');
      return;
    }

    const btn = document.querySelector(`.verify-btn[data-idx="${idx}"]`);
    btn.disabled    = true;
    btn.textContent = 'Saving…';

    const payload = {
      productId:  entry.productID,
      locationId: locationId,
      systemQty:  entry.systemQty,
      actualQty:  actualQty,
    };

    try {
      if (!navigator.onLine) throw new Error('offline');
      await Api.post('/api/cycle-count/verify', payload);
      markVerified(btn, entry, actualQty, idx);
      App.toast(`${entry.productName} verified`, 'success');
    } catch (err) {
      if (!navigator.onLine || err.message === 'offline') {
        await CycleQueue.push(payload);
        markVerified(btn, entry, actualQty, idx);
        App.toast(`${entry.productName} queued (offline)`, '');
        updateQueueBanner();
      } else {
        App.toast(err.message, 'error');
        btn.disabled    = false;
        btn.textContent = 'Verify';
      }
    }
  }

  function markVerified(btn, entry, actualQty, idx) {
    const card = document.getElementById(`cc-item-${idx}`);
    card?.classList.add('verified');
    btn.textContent = '✓ Verified';
    btn.className   = 'verify-btn verified-state';
    entry.lastVerifiedAt = new Date().toISOString();
    entry.systemQty      = actualQty;
  }

  async function flushQueue() {
    const flushed = await CycleQueue.flush();
    if (flushed > 0)
      App.toast(`Synced ${flushed} offline verification${flushed !== 1 ? 's' : ''}`, 'success');
    updateQueueBanner();
  }

  async function updateQueueBanner() {
    const banner = document.getElementById('cc-queue-banner');
    if (!banner) return;
    const n = await CycleQueue.count();
    if (n > 0) {
      banner.style.display = '';
      banner.textContent   = `${n} verification${n !== 1 ? 's' : ''} queued offline — will sync when connected`;
    } else {
      banner.style.display = 'none';
    }
  }

  // ── Barcode scanner ───────────────────────────────────────────────────────

  async function openBarcodeScanner() {
    if (!('BarcodeDetector' in window)) {
      // Fallback: text input
      const sku = prompt('Enter SKU or product name to find:');
      if (sku) findAndHighlight(sku.trim());
      return;
    }

    const overlay = document.createElement('div');
    overlay.className = 'sheet-overlay';
    overlay.innerHTML = `
      <div class="sheet" style="max-height:85vh;">
        <div class="sheet-handle"></div>
        <h2 style="font-size:16px;font-weight:700;margin-bottom:12px;">Scan Item</h2>
        <video id="scan-video" style="width:100%;border-radius:8px;background:#000;max-height:50vh;object-fit:cover;" autoplay muted playsinline></video>
        <div id="scan-status" class="text-muted text-small mt-8" style="text-align:center;">Point camera at barcode…</div>
        <button class="btn btn-outline btn-full mt-8" id="close-scan">Cancel</button>
      </div>`;
    document.body.appendChild(overlay);

    const video    = document.getElementById('scan-video');
    const statusEl = document.getElementById('scan-status');
    let stream;

    function stopAndClose() {
      stream?.getTracks().forEach(t => t.stop());
      overlay.remove();
    }

    document.getElementById('close-scan').addEventListener('click', stopAndClose);
    overlay.addEventListener('click', e => { if (e.target === overlay) stopAndClose(); });

    try {
      stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' } });
      video.srcObject = stream;

      const detector = new BarcodeDetector({ formats: ['qr_code', 'code_128', 'ean_13', 'code_39', 'upc_a', 'upc_e'] });
      let scanning = true;

      const scan = async () => {
        if (!scanning || !document.contains(video)) return;
        if (video.readyState >= 2) {
          try {
            const found = await detector.detect(video);
            if (found.length > 0) {
              scanning = false;
              stopAndClose();
              findAndHighlight(found[0].rawValue);
              return;
            }
          } catch { /* frame not ready */ }
        }
        requestAnimationFrame(scan);
      };
      requestAnimationFrame(scan);

    } catch (err) {
      statusEl.textContent = 'Camera unavailable: ' + err.message;
    }
  }

  function findAndHighlight(barcode) {
    const q = barcode.trim().toLowerCase();
    // Search in full entries array (not just visible), then scroll the rendered card
    const idx = entries.findIndex(e =>
      e.sku?.toLowerCase() === q ||
      e.productName?.toLowerCase().includes(q)
    );
    if (idx < 0) {
      App.toast(`No item found for "${barcode}"`, 'error');
      return;
    }
    // Re-render entries to make sure the item is visible (overdue filter may hide it)
    showOverdueOnly = false;
    document.getElementById('cc-overdue-bar').style.display = 'none';
    renderEntries();

    // Find the rendered card index (visible array may differ from entries index)
    const visible = entries; // overdue filter is now off
    const visIdx = visible.indexOf(entries[idx]);
    const card = document.getElementById(`cc-item-${visIdx}`);
    if (card) {
      card.scrollIntoView({ behavior: 'smooth', block: 'center' });
      card.style.outline = '3px solid var(--primary)';
      setTimeout(() => { card.style.outline = ''; }, 2500);
    }
    App.toast(`Found: ${entries[idx].productName}`, 'success');
  }

  return { render };
})();
