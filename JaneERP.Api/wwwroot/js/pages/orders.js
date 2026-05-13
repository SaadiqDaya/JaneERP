// ── Orders list ───────────────────────────────────────────────────────────────
const OrdersPage = (() => {
  let currentStatus = '';

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
          <div class="filter-row" id="status-filters">
            ${['', 'Draft', 'Live', 'WIP', 'Complete'].map(s => `
              <button class="filter-chip${s === currentStatus ? ' active' : ''}" data-status="${s}">
                ${s || 'All'}
              </button>`).join('')}
          </div>
          <div id="orders-list"></div>
        </div>
      </div>`;

    document.getElementById('new-order-btn').addEventListener('click', () => App.navigate('orders/new'));
    document.getElementById('status-filters').addEventListener('click', async e => {
      const chip = e.target.closest('.filter-chip');
      if (!chip) return;
      document.querySelectorAll('.filter-chip').forEach(c => c.classList.remove('active'));
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
      const url = `/api/orders${currentStatus ? '?status=' + currentStatus : ''}`;
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
  const STATUSES = ['Draft', 'Live', 'WIP', 'Complete'];

  async function render(container, orderId) {
    container.innerHTML = `
      <div class="page">
        <div class="page-header">
          <button class="back-btn" id="orders-back">
            <svg viewBox="0 0 24 24"><path d="M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z"/></svg>
          </button>
          <h1>Order Detail</h1>
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
          ${o.notes ? `<div class="text-small mt-8" style="color:var(--text-2)">${o.notes}</div>` : ''}
        </div>

        <!-- Line Items -->
        <div class="card">
          <div style="font-weight:700;font-size:14px;margin-bottom:8px;">Items (${o.items.length})</div>
          ${o.items.map(item => `
            <div class="order-item-row">
              <div class="oi-main">
                <div class="oi-title">${item.title || item.sKU}</div>
                <div class="oi-sku">${item.sKU}</div>
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

        <!-- Change Status -->
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
        </div>`;

      document.querySelectorAll('#status-btns button:not([disabled])').forEach(btn => {
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
    } catch (err) {
      contentEl.innerHTML = `<div class="empty-state"><p>${err.message}</p></div>`;
    }
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
            <div class="sri-sku">${p.sKU} · Stock: ${p.currentStock} · ${App.fmt$(p.retailPrice)}</div>
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
        sku:   product.sKU,
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
