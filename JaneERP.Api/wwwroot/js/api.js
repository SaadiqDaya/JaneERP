// ── API client ────────────────────────────────────────────────────────────────
// Thin wrapper around fetch. Reads the JWT from localStorage, adds the
// Authorization header, and redirects to login on 401.

const Api = (() => {
  const TOKEN_KEY   = 'janeerp_token';
  const SESSION_KEY = 'janeerp_session';

  function getToken()   { return localStorage.getItem(TOKEN_KEY); }
  function getSession() { const s = localStorage.getItem(SESSION_KEY); return s ? JSON.parse(s) : null; }

  function setSession(token, username, role, company) {
    localStorage.setItem(TOKEN_KEY, token);
    localStorage.setItem(SESSION_KEY, JSON.stringify({ username, role, company }));
  }

  function clearSession() {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(SESSION_KEY);
  }

  function isLoggedIn() { return !!getToken(); }

  async function request(method, path, body) {
    const headers = { 'Content-Type': 'application/json' };
    const token = getToken();
    if (token) headers['Authorization'] = `Bearer ${token}`;

    const opts = { method, headers };
    if (body !== undefined) opts.body = JSON.stringify(body);

    const res = await fetch(path, opts);

    if (res.status === 401) {
      clearSession();
      App.navigate('login');
      throw new Error('Session expired. Please log in again.');
    }

    if (!res.ok) {
      let msg = `Error ${res.status}`;
      try { const j = await res.json(); msg = j.error || j.title || msg; } catch {}
      throw new Error(msg);
    }

    if (res.status === 204) return null;
    try {
      return await res.json();
    } catch {
      const text = await res.text().catch(() => '');
      const preview = text.slice(0, 80);
      throw new Error(`Server returned non-JSON (${res.status}): ${preview}`);
    }
  }

  return {
    getToken, getSession, setSession, clearSession, isLoggedIn,
    get:    (path)        => request('GET',    path),
    post:   (path, body)  => request('POST',   path, body),
    patch:  (path, body)  => request('PATCH',  path, body),
    put:    (path, body)  => request('PUT',    path, body),
    delete: (path)        => request('DELETE', path),
  };
})();
