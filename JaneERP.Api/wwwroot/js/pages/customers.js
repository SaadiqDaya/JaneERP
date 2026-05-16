// ── Customers ─────────────────────────────────────────────────────────────────

const CustomersPage = (() => {
  let searchTimer = null;

  async function render(container) {
    container.innerHTML = `
      <div class="page">
        <div class="page-header">
          <h1>Customers</h1>
        </div>
        <div class="content">
          <div class="search-bar">
            <div class="search-icon">
              <svg viewBox="0 0 24 24"><path d="M15.5 14h-.79l-.28-.27A6.47 6.47 0 0 0 16 9.5 6.5 6.5 0 1 0 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z"/></svg>
            </div>
            <input id="cust-search" type="search" placeholder="Search by name or email…" autocomplete="off">
          </div>
          <div id="cust-list"></div>
        </div>
      </div>`;

    document.getElementById('cust-search').addEventListener('input', e => {
      clearTimeout(searchTimer);
      searchTimer = setTimeout(() => loadCustomers(e.target.value.trim(), 1), 350);
    });

    await loadCustomers('', 1);
  }

  async function loadCustomers(q, page) {
    const listEl = document.getElementById('cust-list');
    if (page === 1) listEl.innerHTML = App.skeletonCards(5);
    try {
      const data = await Api.get(`/api/customers?q=${encodeURIComponent(q)}&page=${page}`);

      if (page === 1) listEl.innerHTML = '';

      if (data.items.length === 0 && page === 1) {
        listEl.innerHTML = `<div class="empty-state">
          <svg viewBox="0 0 24 24"><path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"/></svg>
          <p>No customers found</p></div>`;
        return;
      }

      const html = `<div class="list-card">
        ${data.items.map(c => `
          <div class="list-item" data-id="${c.customerID}">
            <div class="li-main">
              <div class="li-title">${c.fullName || '(No name)'}</div>
              <div class="li-sub">${c.email}</div>
            </div>
            <div class="li-right">
              <div class="li-val">${App.fmt$(c.totalSpent)}</div>
              <div class="li-badge" style="color:var(--text-2);font-size:11px;">${c.orderCount} order${c.orderCount !== 1 ? 's' : ''}</div>
            </div>
            <div class="chevron">${App.chevronSvg()}</div>
          </div>`).join('')}
      </div>`;

      listEl.insertAdjacentHTML('beforeend', html);

      const moreEl = document.getElementById('cust-more');
      if (moreEl) moreEl.remove();
      const loaded = page * 40;
      if (loaded < data.total) {
        listEl.insertAdjacentHTML('beforeend',
          `<button class="btn btn-outline btn-full mt-8" id="cust-more">
            Load more (${data.total - loaded} remaining)
          </button>`);
        document.getElementById('cust-more').addEventListener('click', () => loadCustomers(q, page + 1));
      }

      listEl.querySelectorAll('.list-item[data-id]').forEach(el => {
        el.addEventListener('click', () => App.navigate(`customers/${el.dataset.id}`));
      });
    } catch (err) {
      if (page === 1) listEl.innerHTML = `<div class="empty-state"><p>${err.message}</p></div>`;
    }
  }

  return { render };
})();

// ── Customer detail ───────────────────────────────────────────────────────────

const CustomerDetailPage = (() => {
  async function render(container, customerId) {
    container.innerHTML = `
      <div class="page">
        <div class="page-header">
          <button class="back-btn" id="cust-back">
            <svg viewBox="0 0 24 24"><path d="M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z"/></svg>
          </button>
          <h1>Customer</h1>
        </div>
        <div class="content" id="cust-detail-content">
          ${App.skeletonCards(3)}
        </div>
      </div>`;

    document.getElementById('cust-back').addEventListener('click', () => history.back());
    await loadDetail(customerId);
  }

  async function loadDetail(customerId) {
    const content = document.getElementById('cust-detail-content');
    try {
      const [c, tasks] = await Promise.all([
        Api.get(`/api/customers/${customerId}`),
        Api.get(`/api/tasks?linkedModule=Customer&linkedId=${customerId}`).catch(() => [])
      ]);

      content.innerHTML = `
        <!-- Customer info -->
        <div class="card">
          <div style="display:flex;align-items:center;gap:14px;margin-bottom:12px;">
            <div style="width:48px;height:48px;border-radius:50%;background:var(--primary-lt);
                        display:flex;align-items:center;justify-content:center;flex-shrink:0;">
              <span style="font-size:20px;font-weight:700;color:var(--primary);">
                ${(c.fullName || c.email)[0].toUpperCase()}
              </span>
            </div>
            <div>
              <div style="font-size:17px;font-weight:800;">${c.fullName || '(No name)'}</div>
              <div class="text-muted text-small">${c.email}</div>
            </div>
          </div>
          <div class="divider"></div>
          <div style="display:grid;grid-template-columns:1fr 1fr;gap:12px;margin-top:12px;">
            <div>
              <div class="text-muted text-small">Total Orders</div>
              <div style="font-size:22px;font-weight:800;color:var(--primary);">${c.orderCount}</div>
            </div>
            <div>
              <div class="text-muted text-small">Total Spent</div>
              <div style="font-size:22px;font-weight:800;">${App.fmt$(c.totalSpent)}</div>
            </div>
          </div>
        </div>

        <!-- Quick actions -->
        <div style="display:flex;gap:8px;margin-bottom:12px;">
          <button class="btn btn-outline" style="flex:1;font-size:13px;" id="new-order-for-cust">
            + New Order
          </button>
        </div>

        <!-- Order history -->
        <div class="section-header">
          <h2>Order History (${c.recentOrders.length})</h2>
        </div>

        ${c.recentOrders.length === 0
          ? `<div class="empty-state" style="padding:24px;"><p>No orders yet</p></div>`
          : `<div class="list-card">
              ${c.recentOrders.map(o => `
                <div class="list-item" data-id="${o.salesOrderID}">
                  <div class="li-main">
                    <div class="li-title">#${o.orderNumber} — ${o.orderType}</div>
                    <div class="li-sub">${App.fmtDate(o.orderDate)}</div>
                  </div>
                  <div class="li-right">
                    <div class="li-val">${App.fmt$(o.totalPrice)}</div>
                    <div class="li-badge">${App.statusBadge(o.status)}</div>
                  </div>
                  <div class="chevron">${App.chevronSvg()}</div>
                </div>`).join('')}
            </div>`}

        <!-- Linked Tasks -->
        <div class="section-header" style="margin-top:16px;">
          <h2>Linked Tasks (${tasks.length})</h2>
        </div>
        ${tasks.length === 0
          ? `<div class="empty-state" style="padding:16px;"><p>No linked tasks</p></div>`
          : `<div class="list-card">
              ${tasks.map(t => {
                const today     = new Date(); today.setHours(0,0,0,0);
                const due       = new Date(t.dueDate);
                const isOverdue = due < today && t.status !== 'Done';
                const stage     = t.workflowCurrentStatus || t.status;
                return `
                <div class="list-item" data-taskid="${t.taskID}">
                  <div class="li-main">
                    <div class="li-title">${t.title}</div>
                    <div class="li-sub">
                      ${t.assignedTo} · Due ${App.fmtDateShort(t.dueDate)}
                      ${isOverdue ? '<span style="color:var(--danger);font-weight:700;"> OVERDUE</span>' : ''}
                    </div>
                  </div>
                  <div class="li-right">
                    ${App.statusBadge(stage === 'In Progress' ? 'WIP' : stage === 'Done' ? 'Complete' : stage)}
                  </div>
                  <div class="chevron">${App.chevronSvg()}</div>
                </div>`;
              }).join('')}
            </div>`}`;

      document.getElementById('new-order-for-cust')?.addEventListener('click', () => {
        window._newOrderCustomer = { customerID: c.customerID, email: c.email, fullName: c.fullName };
        App.navigate('orders/new');
      });

      content.querySelectorAll('.list-item[data-id]').forEach(el => {
        el.addEventListener('click', () => App.navigate(`orders/${el.dataset.id}`));
      });

      content.querySelectorAll('.list-item[data-taskid]').forEach(el => {
        el.addEventListener('click', () => App.navigate(`tasks/${el.dataset.taskid}`));
      });
    } catch (err) {
      content.innerHTML = `<div class="empty-state"><p>${err.message}</p></div>`;
    }
  }

  return { render };
})();
