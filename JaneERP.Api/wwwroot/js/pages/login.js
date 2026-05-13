const LoginPage = (() => {
  async function render(container) {
    container.innerHTML = `
      <div class="login-page">
        <div class="login-logo">
          <svg width="64" height="64" viewBox="0 0 48 48" fill="none">
            <rect width="48" height="48" rx="12" fill="#2563eb"/>
            <path d="M12 34V14h8l4 8 4-8h8v20h-6V22l-6 10-6-10v12z" fill="white"/>
          </svg>
          <h1>JaneERP</h1>
          <p>Companion App</p>
        </div>
        <div class="login-card">
          <div id="login-error" class="login-error hidden"></div>
          <div class="form-group">
            <label>Company</label>
            <select id="login-company" class="form-control">
              <option value="">Loading...</option>
            </select>
          </div>
          <div class="form-group">
            <label>Username</label>
            <input id="login-user" type="text" class="form-control"
                   placeholder="Enter username" autocomplete="username">
          </div>
          <div class="form-group">
            <label>Password</label>
            <input id="login-pass" type="password" class="form-control"
                   placeholder="Enter password" autocomplete="current-password">
          </div>
          <button id="login-btn" class="btn btn-primary btn-full">Sign In</button>
        </div>
      </div>`;

    // Load companies
    try {
      const companies = await Api.get('/api/companies');
      const sel = document.getElementById('login-company');
      sel.innerHTML = companies.map(c => `<option value="${c}">${c}</option>`).join('');

      // Remember last company
      const saved = localStorage.getItem('janeerp_last_company');
      if (saved && companies.includes(saved)) sel.value = saved;
    } catch {
      document.getElementById('login-company').innerHTML = '<option value="">Could not load companies</option>';
    }

    // Enter key submits
    document.getElementById('login-pass').addEventListener('keydown', e => {
      if (e.key === 'Enter') document.getElementById('login-btn').click();
    });

    document.getElementById('login-btn').addEventListener('click', async () => {
      const btn     = document.getElementById('login-btn');
      const errEl   = document.getElementById('login-error');
      const company = document.getElementById('login-company').value;
      const username= document.getElementById('login-user').value.trim();
      const password= document.getElementById('login-pass').value;

      errEl.classList.add('hidden');
      if (!company || !username || !password) {
        errEl.textContent = 'Please fill in all fields.';
        errEl.classList.remove('hidden');
        return;
      }

      btn.textContent = 'Signing in…';
      btn.disabled    = true;

      try {
        const res = await Api.post('/api/auth/login', { company, username, password });
        Api.setSession(res.token, res.username, res.role, res.company);
        localStorage.setItem('janeerp_last_company', company);
        App.navigate('dashboard');
      } catch (err) {
        errEl.textContent = err.message || 'Sign in failed.';
        errEl.classList.remove('hidden');
        btn.textContent = 'Sign In';
        btn.disabled    = false;
      }
    });
  }

  return { render };
})();
