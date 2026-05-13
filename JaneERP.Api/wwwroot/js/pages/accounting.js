// ── Accounting / Finance ──────────────────────────────────────────────────────
// Roles: admin, finance

const AccountingPage = (() => {
  let categories  = [];
  let currentFrom = null;
  let currentTo   = null;

  function defaultRange() {
    const now  = new Date();
    const from = new Date(now.getFullYear(), now.getMonth(), 1);
    const to   = new Date(now.getFullYear(), now.getMonth() + 1, 0);
    return {
      from: from.toISOString().split('T')[0],
      to:   to.toISOString().split('T')[0],
    };
  }

  function monthChips(activeFrom) {
    const now = new Date();
    return Array.from({ length: 6 }, (_, i) => {
      const d    = new Date(now.getFullYear(), now.getMonth() - i, 1);
      const from = d.toISOString().split('T')[0];
      const to   = new Date(d.getFullYear(), d.getMonth() + 1, 0).toISOString().split('T')[0];
      const label = d.toLocaleDateString('en-CA', { month: 'short', year: i === 0 ? undefined : '2-digit' });
      return `<button class="filter-chip${from === activeFrom ? ' active' : ''}" data-range="${from}|${to}">${label}</button>`;
    }).join('');
  }

  async function render(container) {
    const range = defaultRange();
    currentFrom = range.from;
    currentTo   = range.to;

    container.innerHTML = `
      <div class="page">
        <div class="page-header">
          <h1>Finance</h1>
          <div class="header-actions">
            <button class="btn-icon" id="log-expense-btn" title="Log expense">
              <svg viewBox="0 0 24 24"><path d="M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z"/></svg>
            </button>
          </div>
        </div>
        <div class="content">
          <div class="filter-row" id="acct-months">
            ${monthChips(currentFrom)}
          </div>

          <div id="acct-summary">${App.skeletonCards(5)}</div>

          <div class="section-header mt-16">
            <h2>Expenses</h2>
            <button class="btn btn-outline" style="font-size:12px;padding:4px 10px;" id="log-expense-btn2">+ Log</button>
          </div>
          <div id="acct-expenses">${App.skeletonCards(4)}</div>
        </div>
      </div>`;

    document.getElementById('log-expense-btn').addEventListener('click', openExpenseSheet);
    document.getElementById('log-expense-btn2').addEventListener('click', openExpenseSheet);

    document.getElementById('acct-months').addEventListener('click', async e => {
      const chip = e.target.closest('.filter-chip');
      if (!chip) return;
      document.querySelectorAll('#acct-months .filter-chip').forEach(c => c.classList.remove('active'));
      chip.classList.add('active');
      const [from, to] = chip.dataset.range.split('|');
      currentFrom = from;
      currentTo   = to;
      await loadData();
    });

    // Pre-load categories for the expense sheet
    try { categories = await Api.get('/api/accounting/expense-categories'); } catch {}

    await loadData();
  }

  async function loadData() {
    await Promise.all([loadSummary(), loadExpenses()]);
  }

  async function loadSummary() {
    const el = document.getElementById('acct-summary');
    el.innerHTML = App.skeletonCards(5);
    try {
      const s = await Api.get(`/api/accounting/summary?from=${currentFrom}&to=${currentTo}`);
      el.innerHTML = `<div class="kpi-grid">
        ${[
          { label: 'Revenue',      val: s.revenue,     cls: s.revenue > 0 ? 'success' : '' },
          { label: 'COGS',         val: s.cogs,        cls: s.cogs > 0 ? 'warn' : '' },
          { label: 'Gross Profit', val: s.grossProfit, cls: s.grossProfit > 0 ? 'success' : s.grossProfit < 0 ? 'danger' : '' },
          { label: 'Expenses',     val: s.expenses,    cls: s.expenses > 0 ? 'warn' : '' },
          { label: 'Net Profit',   val: s.netProfit,   cls: s.netProfit > 0 ? 'success' : s.netProfit < 0 ? 'danger' : '' },
        ].map(c => `
          <div class="kpi-card ${c.cls}">
            <div class="kpi-val">${App.fmt$(c.val)}</div>
            <div class="kpi-label">${c.label}</div>
          </div>`).join('')}
      </div>`;
    } catch (err) {
      el.innerHTML = `<div class="empty-state"><p>${err.message}</p></div>`;
    }
  }

  async function loadExpenses() {
    const el = document.getElementById('acct-expenses');
    el.innerHTML = App.skeletonCards(4);
    try {
      const rows = await Api.get(`/api/accounting/expenses?from=${currentFrom}&to=${currentTo}`);
      if (rows.length === 0) {
        el.innerHTML = `<div class="card card-sm text-muted text-small">No expenses this period</div>`;
        return;
      }
      const total = rows.reduce((s, r) => s + r.amount, 0);
      el.innerHTML = `
        <div class="list-card">
          ${rows.map(r => `
            <div class="list-item" style="cursor:default;">
              <div class="li-main">
                <div class="li-title">${r.description || r.category}</div>
                <div class="li-sub">${r.category} · ${App.fmtDate(r.expenseDate)}</div>
              </div>
              <div class="li-right">
                <div class="li-val" style="color:var(--danger);">−${App.fmt$(r.amount)}</div>
              </div>
            </div>`).join('')}
        </div>
        <div class="card" style="margin-top:8px;">
          <div class="total-row grand">
            <span>Total Expenses</span>
            <span style="color:var(--danger);">−${App.fmt$(total)}</span>
          </div>
        </div>`;
    } catch (err) {
      el.innerHTML = `<div class="empty-state"><p>${err.message}</p></div>`;
    }
  }

  function openExpenseSheet() {
    const today = new Date().toISOString().split('T')[0];
    const overlay = document.createElement('div');
    overlay.className = 'sheet-overlay';
    overlay.innerHTML = `
      <div class="sheet">
        <div class="sheet-handle"></div>
        <h2 style="font-size:16px;font-weight:700;margin-bottom:16px;">Log Expense</h2>
        <div class="form-group">
          <label>Category *</label>
          <select id="exp-cat" class="form-control">
            <option value="">— Select category —</option>
            ${categories.map(c => `<option value="${c.categoryID}">${c.name}</option>`).join('')}
          </select>
        </div>
        <div class="form-group">
          <label>Amount *</label>
          <input id="exp-amount" type="number" class="form-control" placeholder="0.00"
                 min="0.01" step="0.01" inputmode="decimal">
        </div>
        <div class="form-group">
          <label>Date</label>
          <input id="exp-date" type="date" class="form-control" value="${today}">
        </div>
        <div class="form-group">
          <label>Description</label>
          <input id="exp-desc" type="text" class="form-control" placeholder="Optional…">
        </div>
        <button class="btn btn-primary btn-full" id="exp-save">Save Expense</button>
        <button class="btn btn-outline btn-full mt-8" id="exp-cancel">Cancel</button>
      </div>`;

    document.body.appendChild(overlay);
    overlay.addEventListener('click', e => { if (e.target === overlay) overlay.remove(); });
    document.getElementById('exp-cancel').addEventListener('click', () => overlay.remove());

    document.getElementById('exp-save').addEventListener('click', async () => {
      const catId  = parseInt(document.getElementById('exp-cat').value, 10);
      const amount = parseFloat(document.getElementById('exp-amount').value);
      const date   = document.getElementById('exp-date').value;
      const desc   = document.getElementById('exp-desc').value.trim() || null;

      if (!catId)              { App.toast('Select a category', 'error'); return; }
      if (!amount || amount <= 0) { App.toast('Enter a valid amount', 'error'); return; }

      const btn = document.getElementById('exp-save');
      btn.disabled = true; btn.textContent = 'Saving…';
      try {
        await Api.post('/api/accounting/expenses', {
          categoryId:  catId,
          amount,
          description: desc,
          date:        date ? new Date(date + 'T12:00:00').toISOString() : null,
        });
        overlay.remove();
        App.toast('Expense saved', 'success');
        await loadData();
      } catch (err) {
        App.toast(err.message, 'error');
        btn.disabled = false; btn.textContent = 'Save Expense';
      }
    });
  }

  return { render };
})();
