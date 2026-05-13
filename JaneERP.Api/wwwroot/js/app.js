// ── App router & shell ────────────────────────────────────────────────────────

const App = (() => {
  const appEl  = document.getElementById('app');
  const navEl  = document.getElementById('bottom-nav');

  const PUBLIC_PAGES = ['login'];

  // All page scripts load before app.js (see index.html script order)
  const PAGES = {
    'login':               LoginPage,
    'dashboard':           DashboardPage,
    'inventory':           InventoryPage,
    'inventory/history':   InventoryHistoryPage,
    'orders':              OrdersPage,
    'orders/new':          NewOrderPage,
    'orders/detail':       OrderDetailPage,
    'po':                  PurchaseOrdersPage,
    'po/detail':           PoDetailPage,
    'cycle-count':         CycleCountPage,
    'customers':           CustomersPage,
    'customers/detail':    CustomerDetailPage,
    'work-orders':         WorkOrdersPage,
    'work-orders/detail':  WorkOrderDetailPage,
  };

  function currentHash() {
    return (location.hash || '#/').replace('#/', '');
  }

  function navigate(hash) {
    location.hash = '/' + hash;
  }

  function render() {
    const hash   = currentHash();
    const isAuth = Api.isLoggedIn();

    if (!isAuth && !PUBLIC_PAGES.includes(hash)) { navigate('login'); return; }
    if (isAuth && hash === 'login')               { navigate('dashboard'); return; }

    let pageKey = hash;
    let param   = null;

    // Explicit detail routes — each pattern is unambiguous
    if (hash.startsWith('po/') && hash !== 'po') {
      pageKey = 'po/detail';
      param   = parseInt(hash.split('/')[1], 10);
    } else if (hash.startsWith('customers/') && hash !== 'customers') {
      pageKey = 'customers/detail';
      param   = parseInt(hash.split('/')[1], 10);
    } else if (hash.startsWith('work-orders/') && hash !== 'work-orders') {
      pageKey = 'work-orders/detail';
      param   = parseInt(hash.split('/')[1], 10);
    } else if (hash.startsWith('inventory/history/')) {
      pageKey = 'inventory/history';
      param   = parseInt(hash.split('/')[2], 10);
    } else {
      // Generic: orders/123 → orders/detail
      const m = hash.match(/^(.+?)\/(\d+)$/);
      if (m && !PAGES[hash]) {
        pageKey = m[1] + '/detail';
        param   = parseInt(m[2], 10);
      }
    }

    const pageFn = PAGES[pageKey];
    if (!pageFn) { navigate(isAuth ? 'dashboard' : 'login'); return; }

    if (PUBLIC_PAGES.includes(hash)) {
      navEl.classList.add('hidden');
    } else {
      navEl.classList.remove('hidden');
      updateNavActive(hash);
    }

    appEl.innerHTML = '';
    pageFn.render(appEl, param);
  }

  function updateNavActive(hash) {
    navEl.querySelectorAll('.nav-item').forEach(el => {
      const page = el.dataset.page;
      el.classList.toggle('active', hash === page || hash.startsWith(page + '/'));
    });
  }

  function toast(msg, type = '') {
    const container = document.getElementById('toast-container');
    const el = document.createElement('div');
    el.className = 'toast' + (type ? ' ' + type : '');
    el.textContent = msg;
    container.appendChild(el);
    setTimeout(() => el.remove(), 3000);
  }

  function fmt$  (n)  { return '$' + (n ?? 0).toFixed(2); }
  function fmtDate(d) { if (!d) return '—'; const dt = new Date(d); return dt.toLocaleDateString('en-CA', { month:'short', day:'numeric', year:'numeric' }); }
  function fmtDateShort(d) { if (!d) return '—'; const dt = new Date(d); return dt.toLocaleDateString('en-CA', { month:'short', day:'numeric' }); }

  function statusBadge(status) {
    const map = {
      'Draft': 'badge-draft', 'Live': 'badge-live', 'WIP': 'badge-wip',
      'Complete': 'badge-complete', 'Sent': 'badge-sent',
      'PartiallyReceived': 'badge-partial', 'Received': 'badge-received',
      'Cancelled': 'badge-draft',
      'Pending': 'badge-draft', 'InProgress': 'badge-wip', 'Completed': 'badge-complete',
    };
    return `<span class="badge ${map[status] || 'badge-draft'}">${status}</span>`;
  }

  function chevronSvg() {
    return `<svg viewBox="0 0 24 24"><path d="M10 6L8.59 7.41 13.17 12l-4.58 4.59L10 18l6-6z"/></svg>`;
  }

  function skeletonCards(n = 4) {
    return Array(n).fill('<div class="skeleton skeleton-card"></div>').join('');
  }

  window.addEventListener('hashchange', render);
  window.addEventListener('load', () => {
    if (navigator.serviceWorker) navigator.serviceWorker.register('/sw.js').catch(() => {});
    render();
  });

  return { navigate, toast, fmt$, fmtDate, fmtDateShort, statusBadge, chevronSvg, skeletonCards };
})();
