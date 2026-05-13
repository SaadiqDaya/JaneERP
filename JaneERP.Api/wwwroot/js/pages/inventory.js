// ── Inventory / Stock Lookup ──────────────────────────────────────────────────

// Cache product info when navigating from list → history
let _invHistoryCtx = null;

const InventoryPage = (() => {
  let searchTimer = null;
  let currentPage = 1;
  let currentMode = 'all'; // 'all' | 'low'

  async function render(container) {
    container.innerHTML = `
      <div class="page">
        <div class="page-header">
          <h1>Stock Lookup</h1>
          <div class="header-actions">
            <button class="btn-icon" id="scan-btn" title="Scan barcode">
              <svg viewBox="0 0 24 24"><path d="M1 5h2V3H1v2zm4-2H3v2h2V3zm10 0h-2v2h2V3zm2 0v2h2V3h-2zM1 9h2V7H1v2zm4-2H3v2h2V7zM1 13h2v-2H1v2zm18-4h2V7h-2v2zm0 4h2v-2h-2v2zM3 21v-6H1v8h8v-2H3zm6-16H7v2h2V5zm2 0h-2v2h2V5zm2 18h-2v2h2v-2zm-4 0H7v2h2v-2zM5 21h2v2H5v-2zm8-8H7v6h6v-6zm-2 4H9v-2h2v2zm9-4h-6v6h6v-6zm-2 4h-2v-2h2v2zM15 5h-2v2h2V5z"/></svg>
            </button>
          </div>
        </div>
        <div class="content">
          <div class="search-bar">
            <div class="search-icon">
              <svg viewBox="0 0 24 24"><path d="M15.5 14h-.79l-.28-.27A6.47 6.47 0 0 0 16 9.5 6.5 6.5 0 1 0 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z"/></svg>
            </div>
            <input id="inv-search" type="search" placeholder="Search by name or SKU…" autocomplete="off">
          </div>
          <div class="filter-row">
            <button class="filter-chip active" data-mode="all">All Stock</button>
            <button class="filter-chip" data-mode="low">Low Stock</button>
          </div>
          <div id="inv-list"></div>
        </div>
      </div>`;

    document.getElementById('scan-btn').addEventListener('click', openBarcodeScanner);

    document.querySelectorAll('.filter-chip[data-mode]').forEach(chip => {
      chip.addEventListener('click', () => {
        document.querySelectorAll('.filter-chip[data-mode]').forEach(c => c.classList.remove('active'));
        chip.classList.add('active');
        currentMode = chip.dataset.mode;
        currentPage = 1;
        if (currentMode === 'low') {
          loadLowStock(1);
        } else {
          doSearch(document.getElementById('inv-search').value.trim(), 1);
        }
      });
    });

    document.getElementById('inv-search').addEventListener('input', e => {
      if (currentMode === 'low') return;
      clearTimeout(searchTimer);
      searchTimer = setTimeout(() => doSearch(e.target.value.trim(), 1), 350);
    });

    await doSearch('', 1);
  }

  async function loadLowStock(page) {
    const listEl = document.getElementById('inv-list');
    if (page === 1) listEl.innerHTML = App.skeletonCards(5);
    try {
      const data = await Api.get(`/api/inventory/low-stock?page=${page}`);
      renderProductList(data, listEl, page, () => loadLowStock(page + 1));
    } catch (err) {
      if (page === 1) listEl.innerHTML = `<div class="empty-state"><p>${err.message}</p></div>`;
    }
  }

  async function doSearch(q, page) {
    const listEl = document.getElementById('inv-list');
    if (page === 1) listEl.innerHTML = App.skeletonCards(5);
    try {
      const data = await Api.get(`/api/inventory?q=${encodeURIComponent(q)}&page=${page}`);
      renderProductList(data, listEl, page, () => doSearch(q, page + 1));
    } catch (err) {
      if (page === 1) listEl.innerHTML = `<div class="empty-state"><p>${err.message}</p></div>`;
    }
  }

  function renderProductList(data, listEl, page, loadMore) {
    if (page === 1) listEl.innerHTML = '';
    const { items, total } = data;

    if (items.length === 0 && page === 1) {
      listEl.innerHTML = `<div class="empty-state">
        <svg viewBox="0 0 24 24"><path d="M19 3H5c-1.1 0-2 .9-2 2v14l4-4h12c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2z"/></svg>
        <p>No products found</p></div>`;
      return;
    }

    const html = items.map(p => `
      <div class="list-item inv-item" data-id="${p.productID}"
           style="border-radius:12px;background:white;border:1px solid var(--border);margin-bottom:8px;">
        <div class="li-main">
          <div class="li-title">${p.productName}</div>
          <div class="li-sub">${p.sKU}${p.retailPrice > 0 ? ' · ' + App.fmt$(p.retailPrice) : ''}</div>
        </div>
        <div class="li-right">
          <div class="li-val" style="font-size:22px;font-weight:800;color:${p.isLowStock ? 'var(--danger)' : 'var(--text)'}">
            ${p.currentStock}
          </div>
          <div class="li-badge">${p.isLowStock ? '<span class="badge badge-low">Low</span>' : ''}</div>
        </div>
        <div class="chevron">${App.chevronSvg()}</div>
      </div>`).join('');

    listEl.insertAdjacentHTML('beforeend', html);

    const moreEl = document.getElementById('inv-more');
    if (moreEl) moreEl.remove();
    const loaded = page * 40;
    if (loaded < total) {
      listEl.insertAdjacentHTML('beforeend',
        `<button class="btn btn-outline btn-full mt-8" id="inv-more">
          Load more (${total - loaded} remaining)
        </button>`);
      document.getElementById('inv-more').addEventListener('click', loadMore);
    }

    listEl.querySelectorAll('.inv-item').forEach(el => {
      el.addEventListener('click', () => showStockDetail(el.dataset.id, items));
    });
  }

  async function showStockDetail(productId, items) {
    const product = items.find(p => p.productID == productId);
    if (!product) return;

    const overlay = document.createElement('div');
    overlay.className = 'sheet-overlay';
    overlay.innerHTML = `
      <div class="sheet">
        <div class="sheet-handle"></div>
        <h2 style="font-size:16px;font-weight:700;margin-bottom:2px;">${product.productName}</h2>
        <p class="text-muted text-small" style="margin-bottom:14px;">SKU: ${product.sKU}</p>
        <div id="stock-detail-content">
          <div class="skeleton skeleton-line"></div>
          <div class="skeleton skeleton-line short"></div>
        </div>
        <div style="display:flex;gap:8px;margin-top:16px;">
          <button class="btn btn-outline" style="flex:1;font-size:13px;padding:10px 8px;" id="adjust-btn">
            <svg viewBox="0 0 24 24" style="width:16px;height:16px;fill:currentColor;"><path d="M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z"/></svg>
            Adjust Stock
          </button>
          <button class="btn btn-outline" style="flex:1;font-size:13px;padding:10px 8px;" id="history-btn">
            <svg viewBox="0 0 24 24" style="width:16px;height:16px;fill:currentColor;"><path d="M13 3c-4.97 0-9 4.03-9 9H1l3.89 3.89.07.14L9 12H6c0-3.87 3.13-7 7-7s7 3.13 7 7-3.13 7-7 7c-1.93 0-3.68-.79-4.94-2.06l-1.42 1.42C8.27 19.99 10.51 21 13 21c4.97 0 9-4.03 9-9s-4.03-9-9-9zm-1 5v5l4.28 2.54.72-1.21-3.5-2.08V8H12z"/></svg>
            History
          </button>
        </div>
        <button class="btn btn-outline btn-full mt-8" id="close-sheet">Close</button>
      </div>`;

    document.body.appendChild(overlay);
    overlay.addEventListener('click', e => { if (e.target === overlay) overlay.remove(); });
    document.getElementById('close-sheet').addEventListener('click', () => overlay.remove());

    document.getElementById('history-btn').addEventListener('click', () => {
      _invHistoryCtx = { productId: product.productID, productName: product.productName, sku: product.sKU };
      overlay.remove();
      App.navigate(`inventory/history/${product.productID}`);
    });

    document.getElementById('adjust-btn').addEventListener('click', () => {
      overlay.remove();
      openAdjustModal(product);
    });

    try {
      const stock = await Api.get(`/api/inventory/${productId}/stock`);
      const contentEl = document.getElementById('stock-detail-content');
      if (!contentEl) return;

      if (stock.length === 0) {
        contentEl.innerHTML = `<div class="empty-state" style="padding:16px 0;"><p>No stock recorded</p></div>`;
      } else {
        const totalStock = stock.reduce((s, r) => s + r.stock, 0);
        contentEl.innerHTML = `
          <div style="display:flex;justify-content:space-between;margin-bottom:10px;">
            <span class="text-muted text-small">Total Stock</span>
            <span style="font-size:22px;font-weight:800;">${totalStock}</span>
          </div>
          ${stock.map(r => `
            <div class="stock-row">
              <span class="loc-name">${r.locationName}</span>
              <span class="loc-qty">${r.stock}</span>
            </div>`).join('')}`;
      }
    } catch (err) {
      const contentEl = document.getElementById('stock-detail-content');
      if (contentEl) contentEl.innerHTML = `<p class="text-danger">${err.message}</p>`;
    }
  }

  function openAdjustModal(product) {
    const overlay = document.createElement('div');
    overlay.className = 'sheet-overlay';
    overlay.innerHTML = `
      <div class="sheet">
        <div class="sheet-handle"></div>
        <h2 style="font-size:16px;font-weight:700;margin-bottom:2px;">Adjust Stock</h2>
        <p class="text-muted text-small" style="margin-bottom:16px;">${product.productName}</p>

        <div class="form-group">
          <label>Adjustment Quantity</label>
          <div style="display:flex;align-items:center;gap:12px;margin-top:4px;">
            <button class="qty-btn" id="adj-dec">−</button>
            <input id="adj-qty" type="number" class="form-control"
                   style="text-align:center;font-size:22px;font-weight:800;width:100px;"
                   value="0">
            <button class="qty-btn" id="adj-inc">+</button>
          </div>
          <p class="text-muted text-small" style="margin-top:6px;">
            Positive = add stock &nbsp;·&nbsp; Negative = remove stock
          </p>
        </div>

        <div class="form-group">
          <label>Reason *</label>
          <select id="adj-reason" class="form-control">
            <option value="">Select reason…</option>
            <option>Recount correction</option>
            <option>Damaged goods</option>
            <option>Returned item</option>
            <option>Found stock</option>
            <option>Other</option>
          </select>
        </div>

        <div class="form-group" id="other-reason-group" style="display:none;">
          <label>Specify reason</label>
          <input id="adj-reason-other" type="text" class="form-control" placeholder="Describe the reason…">
        </div>

        <button class="btn btn-primary btn-full mt-8" id="adj-save">Save Adjustment</button>
        <button class="btn btn-outline btn-full mt-8" id="adj-cancel">Cancel</button>
      </div>`;

    document.body.appendChild(overlay);

    document.getElementById('adj-cancel').addEventListener('click', () => overlay.remove());
    overlay.addEventListener('click', e => { if (e.target === overlay) overlay.remove(); });

    const qtyInput = document.getElementById('adj-qty');
    document.getElementById('adj-inc').addEventListener('click', () => {
      qtyInput.value = (parseInt(qtyInput.value) || 0) + 1;
    });
    document.getElementById('adj-dec').addEventListener('click', () => {
      qtyInput.value = (parseInt(qtyInput.value) || 0) - 1;
    });

    document.getElementById('adj-reason').addEventListener('change', e => {
      document.getElementById('other-reason-group').style.display =
        e.target.value === 'Other' ? '' : 'none';
    });

    document.getElementById('adj-save').addEventListener('click', async () => {
      const qty = parseInt(qtyInput.value) || 0;
      if (qty === 0) { App.toast('Quantity cannot be zero', 'error'); return; }

      let reason = document.getElementById('adj-reason').value;
      if (!reason) { App.toast('Please select a reason', 'error'); return; }
      if (reason === 'Other') {
        reason = document.getElementById('adj-reason-other').value.trim();
        if (!reason) { App.toast('Please specify the reason', 'error'); return; }
      }

      const btn = document.getElementById('adj-save');
      btn.disabled = true; btn.textContent = 'Saving…';
      try {
        await Api.post(`/api/inventory/${product.productID}/adjust`, { qty, reason });
        App.toast(`Stock adjusted by ${qty > 0 ? '+' : ''}${qty}`, 'success');
        overlay.remove();
      } catch (err) {
        App.toast(err.message, 'error');
        btn.disabled = false; btn.textContent = 'Save Adjustment';
      }
    });
  }

  function openBarcodeScanner() {
    if (!('BarcodeDetector' in window)) {
      App.toast('Barcode scanning requires Chrome on Android. Type the SKU to search.', 'error');
      document.getElementById('inv-search')?.focus();
      return;
    }

    const overlay = document.createElement('div');
    overlay.className = 'scanner-overlay';
    overlay.innerHTML = `
      <div class="scanner-modal">
        <div class="scanner-header">
          <span>Scan Barcode / QR</span>
          <button id="close-scanner">✕</button>
        </div>
        <div class="scanner-viewport">
          <video id="scanner-video" autoplay playsinline muted></video>
          <div class="scanner-crosshair"></div>
        </div>
        <p class="scanner-hint">Point camera at a product barcode</p>
      </div>`;

    document.body.appendChild(overlay);
    let stream = null, rafId = null;

    const cleanup = () => {
      if (rafId)  cancelAnimationFrame(rafId);
      if (stream) stream.getTracks().forEach(t => t.stop());
      overlay.remove();
    };

    document.getElementById('close-scanner').addEventListener('click', cleanup);
    overlay.addEventListener('click', e => { if (e.target === overlay) cleanup(); });

    (async () => {
      try {
        stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' } });
        const video = document.getElementById('scanner-video');
        if (!video) { cleanup(); return; }
        video.srcObject = stream;

        const detector = new BarcodeDetector({
          formats: ['code_128', 'ean_13', 'ean_8', 'code_39', 'qr_code', 'upc_a', 'upc_e']
        });

        const scan = async () => {
          if (!document.getElementById('scanner-video')) return;
          try {
            const codes = await detector.detect(video);
            if (codes.length > 0) {
              const code = codes[0].rawValue;
              cleanup();
              const inp = document.getElementById('inv-search');
              if (inp) {
                inp.value = code;
                currentMode = 'all';
                document.querySelectorAll('.filter-chip[data-mode]').forEach(c => {
                  c.classList.toggle('active', c.dataset.mode === 'all');
                });
                doSearch(code, 1);
              }
              return;
            }
          } catch { /* detector throws on frames without barcodes, ignore */ }
          rafId = requestAnimationFrame(scan);
        };
        video.addEventListener('loadedmetadata', () => { rafId = requestAnimationFrame(scan); });
      } catch (err) {
        cleanup();
        App.toast('Camera error: ' + err.message, 'error');
      }
    })();
  }

  return { render };
})();

// ── Stock Transaction History ─────────────────────────────────────────────────

const InventoryHistoryPage = (() => {
  async function render(container, productId) {
    const ctx = _invHistoryCtx;
    const title = ctx?.productName || 'Stock History';

    container.innerHTML = `
      <div class="page">
        <div class="page-header">
          <button class="back-btn" id="hist-back">
            <svg viewBox="0 0 24 24"><path d="M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z"/></svg>
          </button>
          <div style="flex:1;min-width:0;">
            <h1 style="font-size:16px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">${title}</h1>
            ${ctx?.sku ? `<div class="text-muted text-small">${ctx.sku}</div>` : ''}
          </div>
        </div>
        <div class="content" id="hist-content">
          ${App.skeletonCards(6)}
        </div>
      </div>`;

    document.getElementById('hist-back').addEventListener('click', () => history.back());
    await loadPage(productId, 1);
  }

  async function loadPage(productId, page) {
    const content = document.getElementById('hist-content');
    try {
      const data = await Api.get(`/api/inventory/${productId}/history?page=${page}`);

      if (page === 1) {
        if (data.items.length === 0) {
          content.innerHTML = `<div class="empty-state"><p>No transactions recorded</p></div>`;
          return;
        }
        content.innerHTML = '';
      }

      const typeColor = t => {
        if (['Sale','Adjustment'].includes(t) && false) return ''; // handled below
        if (t === 'Sale' || t === 'ManufacturingOut') return 'var(--danger)';
        if (t === 'PurchaseReceipt' || t === 'ManufacturingIn' || t === 'WorkOrderComplete') return 'var(--success)';
        if (t === 'Adjustment') return 'var(--primary)';
        return 'var(--text-2)';
      };

      const html = data.items.map(tx => `
        <div class="list-item" style="background:white;border-radius:12px;border:1px solid var(--border);margin-bottom:8px;cursor:default;">
          <div class="li-main">
            <div class="li-title" style="font-size:13px;">${tx.transactionType}</div>
            <div class="li-sub">
              ${App.fmtDate(tx.transactionDate)}
              ${tx.locationName ? ' · ' + tx.locationName : ''}
            </div>
            ${tx.notes ? `<div class="li-sub" style="margin-top:2px;white-space:normal;">${tx.notes}</div>` : ''}
          </div>
          <div style="font-size:22px;font-weight:800;color:${typeColor(tx.transactionType)};flex-shrink:0;text-align:right;">
            ${tx.quantityChange > 0 ? '+' : ''}${tx.quantityChange}
          </div>
        </div>`).join('');

      content.insertAdjacentHTML('beforeend', html);

      const moreEl = document.getElementById('hist-more');
      if (moreEl) moreEl.remove();
      const loaded = page * 30;
      if (loaded < data.total) {
        content.insertAdjacentHTML('beforeend',
          `<button class="btn btn-outline btn-full mt-8" id="hist-more">
            Load more (${data.total - loaded} remaining)
          </button>`);
        document.getElementById('hist-more').addEventListener('click', () => loadPage(productId, page + 1));
      }
    } catch (err) {
      if (page === 1) content.innerHTML = `<div class="empty-state"><p>${err.message}</p></div>`;
    }
  }

  return { render };
})();
