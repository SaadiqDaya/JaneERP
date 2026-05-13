const InventoryPage = (() => {
  let searchTimer = null;
  let currentPage = 1;

  async function render(container) {
    container.innerHTML = `
      <div class="page">
        <div class="page-header">
          <h1>Stock Lookup</h1>
        </div>
        <div class="content">
          <div class="search-bar">
            <div class="search-icon">
              <svg viewBox="0 0 24 24"><path d="M15.5 14h-.79l-.28-.27A6.47 6.47 0 0 0 16 9.5 6.5 6.5 0 1 0 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z"/></svg>
            </div>
            <input id="inv-search" type="search" placeholder="Search by name or SKU…" autocomplete="off">
          </div>
          <div id="inv-list"></div>
        </div>
      </div>`;

    // Initial load
    await doSearch('', 1);

    document.getElementById('inv-search').addEventListener('input', e => {
      clearTimeout(searchTimer);
      searchTimer = setTimeout(() => doSearch(e.target.value.trim(), 1), 350);
    });
  }

  async function doSearch(q, page) {
    const listEl = document.getElementById('inv-list');
    if (page === 1) listEl.innerHTML = App.skeletonCards(5);

    try {
      const data  = await Api.get(`/api/inventory?q=${encodeURIComponent(q)}&page=${page}`);
      const total = data.total;
      const items = data.items;

      if (page === 1) listEl.innerHTML = '';

      if (items.length === 0 && page === 1) {
        listEl.innerHTML = `<div class="empty-state">
          <svg viewBox="0 0 24 24"><path d="M19 3H5c-1.1 0-2 .9-2 2v14l4-4h12c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2z"/></svg>
          <p>No products found</p></div>`;
        return;
      }

      const html = items.map(p => `
        <div class="list-item" data-id="${p.productID}" style="border-radius:12px;background:white;border:1px solid var(--border);margin-bottom:8px;">
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

      // Load more button
      const loaded = page * 40;
      const moreEl = document.getElementById('inv-more');
      if (moreEl) moreEl.remove();

      if (loaded < total) {
        listEl.insertAdjacentHTML('beforeend',
          `<button class="btn btn-outline btn-full mt-8" id="inv-more">
            Load more (${total - loaded} remaining)
          </button>`);
        document.getElementById('inv-more').addEventListener('click', () => {
          currentPage = page + 1;
          doSearch(document.getElementById('inv-search').value.trim(), currentPage);
        });
      }

      // Bind tap → stock by location
      listEl.querySelectorAll('.list-item').forEach(el => {
        el.addEventListener('click', () => showStockDetail(el.dataset.id, items));
      });
    } catch (err) {
      if (page === 1) listEl.innerHTML =
        `<div class="empty-state"><p>${err.message}</p></div>`;
    }
  }

  async function showStockDetail(productId, items) {
    const product = items.find(p => p.productID == productId);
    if (!product) return;

    const overlay = document.createElement('div');
    overlay.style.cssText = `position:fixed;inset:0;background:rgba(0,0,0,.5);z-index:500;display:flex;align-items:flex-end;`;
    overlay.innerHTML = `
      <div style="background:white;border-radius:20px 20px 0 0;width:100%;max-height:80vh;overflow-y:auto;padding:20px;">
        <div style="width:40px;height:4px;background:var(--border);border-radius:2px;margin:0 auto 16px;"></div>
        <h2 style="font-size:17px;font-weight:700;margin-bottom:4px;">${product.productName}</h2>
        <p class="text-muted text-small" style="margin-bottom:16px;">SKU: ${product.sKU}</p>
        <div id="stock-detail-content">
          <div class="skeleton skeleton-line"></div>
          <div class="skeleton skeleton-line short"></div>
        </div>
        <button class="btn btn-outline btn-full mt-16" id="close-stock-detail">Close</button>
      </div>`;

    document.body.appendChild(overlay);
    overlay.addEventListener('click', e => { if (e.target === overlay) overlay.remove(); });
    document.getElementById('close-stock-detail').addEventListener('click', () => overlay.remove());

    try {
      const stock = await Api.get(`/api/inventory/${productId}/stock`);
      const contentEl = document.getElementById('stock-detail-content');
      if (!contentEl) return;

      if (stock.length === 0) {
        contentEl.innerHTML = `<div class="empty-state" style="padding:24px 0;"><p>No stock recorded</p></div>`;
      } else {
        const totalStock = stock.reduce((s, r) => s + r.stock, 0);
        contentEl.innerHTML = `
          <div class="card" style="margin-bottom:0">
            <div style="display:flex;justify-content:space-between;margin-bottom:12px;">
              <span class="text-muted text-small">Total Stock</span>
              <span style="font-size:22px;font-weight:800;">${totalStock}</span>
            </div>
            ${stock.map(r => `
              <div class="stock-row">
                <span class="loc-name">${r.locationName}</span>
                <span class="loc-qty">${r.stock}</span>
              </div>`).join('')}
          </div>`;
      }
    } catch (err) {
      const contentEl = document.getElementById('stock-detail-content');
      if (contentEl) contentEl.innerHTML = `<p class="text-danger">${err.message}</p>`;
    }
  }

  return { render };
})();
