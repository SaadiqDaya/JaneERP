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
    'cooking':             CookingPage,
    'cooking/detail':      CookSessionDetailPage,
    'accounting':          AccountingPage,
    'tasks':               TasksPage,
    'tasks/detail':        TaskDetailPage,
  };

  // ── Nav items ───────────────────────────────────────────────────────────────
  // roles[] documents which role(s) should see this item.
  // '*' = all authenticated users.
  // TODO: when role-based access is implemented, filter this array by
  //       Api.getSession().role before rendering. The API endpoints themselves
  //       are the real security gate — this is display-only filtering.
  const NAV_ITEMS = [
    {
      hash: 'dashboard',   label: 'Dash',     roles: ['*'],
      icon: `<svg viewBox="0 0 24 24"><path d="M3 13h8V3H3v10zm0 8h8v-6H3v6zm10 0h8V11h-8v10zm0-18v6h8V3h-8z"/></svg>`,
    },
    {
      hash: 'orders',      label: 'Orders',   roles: ['admin', 'sales'],
      icon: `<svg viewBox="0 0 24 24"><path d="M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-7 14l-5-5 1.41-1.41L12 14.17l7.59-7.59L21 8l-9 9z"/></svg>`,
    },
    {
      hash: 'inventory',   label: 'Stock',    roles: ['admin', 'warehouse', 'sales'],
      icon: `<svg viewBox="0 0 24 24"><path d="M20 6h-2.18c.07-.44.18-.87.18-1.33C18 2.54 15.96.5 13.5.5c-1.39 0-2.58.72-3.5 1.77C9.08 1.22 7.89.5 6.5.5 4.04.5 2 2.54 2 4.67c0 .46.11.89.18 1.33H0v14h24V6h-4zM13.5 2.5c1.1 0 2 .9 2 2S14.6 6.5 13.5 6.5 11.5 5.6 11.5 4.5s.9-2 2-2zm-7 0c1.1 0 2 .9 2 2s-.9 2-2 2-2-.9-2-2 .9-2 2-2zM22 18H2V8h7.08c.62.8 1.53 1.34 2.58 1.46L12 10l.34-.54C13.38 9.34 14.3 8.8 14.92 8H22v10z"/></svg>`,
    },
    {
      hash: 'po',          label: 'POs',      roles: ['admin', 'warehouse'],
      icon: `<svg viewBox="0 0 24 24"><path d="M20 8h-3V4H3c-1.1 0-2 .9-2 2v11h2c0 1.66 1.34 3 3 3s3-1.34 3-3h6c0 1.66 1.34 3 3 3s3-1.34 3-3h2v-5l-3-4zM6 18.5c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5zm13.5-9l1.96 2.5H17V9.5h2.5zm-1.5 9c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5z"/></svg>`,
    },
    {
      hash: 'customers',   label: 'Customers',roles: ['admin', 'sales'],
      icon: `<svg viewBox="0 0 24 24"><path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"/></svg>`,
    },
    {
      hash: 'work-orders', label: 'Work',     roles: ['admin', 'warehouse'],
      icon: `<svg viewBox="0 0 24 24"><path d="M22 9V7h-2V5c0-1.1-.9-2-2-2H4c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2v-2h2v-2h-2v-2h2v-2h-2V9h2zm-4 10H4V5h14v14zM6 13h5v4H6zm6-6h4v3h-4zM6 7h5v5H6zm6 4h4v6h-4z"/></svg>`,
    },
    {
      hash: 'cycle-count', label: 'Count',    roles: ['admin', 'warehouse'],
      icon: `<svg viewBox="0 0 24 24"><path d="M9 11H7v2h2v-2zm4 0h-2v2h2v-2zm4 0h-2v2h2v-2zm2-7h-1V2h-2v2H8V2H6v2H5c-1.11 0-1.99.9-1.99 2L3 20c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 16H5V9h14v11z"/></svg>`,
    },
    {
      hash: 'cooking',     label: 'Cook',     roles: ['admin', 'warehouse'],
      icon: `<svg viewBox="0 0 24 24"><path d="M13.5.67s.74 2.65.74 4.8c0 2.06-1.35 3.73-3.41 3.73-2.07 0-3.63-1.67-3.63-3.73l.03-.36C5.21 7.51 4 10.62 4 14c0 4.42 3.58 8 8 8s8-3.58 8-8C20 8.61 17.41 3.8 13.5.67zM11.71 19c-1.78 0-3.22-1.4-3.22-3.14 0-1.62 1.05-2.76 2.81-3.12 1.77-.36 3.6-1.21 4.62-2.58.39 1.29.59 2.65.59 4.04 0 2.65-2.15 4.8-4.8 4.8z"/></svg>`,
    },
    {
      hash: 'accounting',  label: 'Finance',  roles: ['admin', 'finance'],
      icon: `<svg viewBox="0 0 24 24"><path d="M11.8 10.9c-2.27-.59-3-1.2-3-2.15 0-1.09 1.01-1.85 2.7-1.85 1.78 0 2.44.85 2.5 2.1h2.21c-.07-1.72-1.12-3.3-3.21-3.81V3h-3v2.16c-1.94.42-3.5 1.68-3.5 3.61 0 2.31 1.91 3.46 4.7 4.13 2.5.6 3 1.48 3 2.41 0 .69-.49 1.79-2.7 1.79-2.06 0-2.87-.92-2.98-2.1h-2.2c.12 2.19 1.76 3.42 3.68 3.83V21h3v-2.15c1.95-.37 3.5-1.5 3.5-3.55 0-2.84-2.43-3.81-4.7-4.4z"/></svg>`,
    },
    {
      hash: 'tasks',       label: 'Tasks',    roles: ['*'],
      icon: `<svg viewBox="0 0 24 24"><path d="M19 3H5c-1.11 0-2 .9-2 2v14c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.11-.9-2-2-2zm-9 14l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z"/></svg>`,
    },
  ];

  let navBuilt = false;

  function buildNav() {
    // Future: filter NAV_ITEMS by Api.getSession()?.role
    navEl.innerHTML = NAV_ITEMS.map(item => `
      <a href="#/${item.hash}" class="nav-item" data-page="${item.hash}">
        ${item.icon}
        <span>${item.label}</span>
      </a>`).join('');
    navBuilt = true;
  }

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
    } else if (hash.startsWith('cooking/') && hash !== 'cooking') {
      pageKey = 'cooking/detail';
      param   = parseInt(hash.split('/')[1], 10);
    } else if (hash.startsWith('tasks/') && hash !== 'tasks') {
      pageKey = 'tasks/detail';
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
      if (!navBuilt) buildNav();
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

  function fmt$  (n)  { return '$' + (n ?? 0).toLocaleString('en-CA', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }
  function fmtDate(d) { if (!d) return '—'; const dt = new Date(d); return dt.toLocaleDateString('en-CA', { month:'short', day:'numeric', year:'numeric' }); }
  function fmtDateShort(d) { if (!d) return '—'; const dt = new Date(d); return dt.toLocaleDateString('en-CA', { month:'short', day:'numeric' }); }

  function statusBadge(status) {
    const map = {
      'Draft':              'badge-draft',
      'Live':               'badge-live',
      'WIP':                'badge-wip',
      'Packed':             'badge-wip',
      'Shipped':            'badge-sent',
      'Complete':           'badge-complete',
      'Sent':               'badge-sent',
      'PartiallyReceived':  'badge-partial',
      'Received':           'badge-received',
      'Cancelled':          'badge-draft',
      'Pending':            'badge-draft',
      'InProgress':         'badge-wip',
      'Completed':          'badge-complete',
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
