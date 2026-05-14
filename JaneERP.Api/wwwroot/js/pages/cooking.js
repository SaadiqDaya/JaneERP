// ── Batch Cooking — session list + create ─────────────────────────────────────

const CookingPage = (() => {
  async function render(container) {
    container.innerHTML = `
      <div class="page">
        <div class="page-header">
          <h1>Batch Cooking</h1>
          <div class="header-actions">
            <button class="btn-icon" id="cooking-new-btn" title="New session">
              <svg viewBox="0 0 24 24"><path d="M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z"/></svg>
            </button>
          </div>
        </div>
        <div class="content" id="cooking-content">
          ${App.skeletonCards(3)}
        </div>
      </div>`;

    document.getElementById('cooking-new-btn').addEventListener('click', showCreatePanel);
    await loadSessions();
  }

  async function loadSessions() {
    const el = document.getElementById('cooking-content');
    if (!el) return;
    el.innerHTML = App.skeletonCards(3);
    try {
      const sessions = await Api.get('/api/cooking/sessions');
      if (sessions.length === 0) {
        el.innerHTML = `
          <div class="empty-state">
            <svg viewBox="0 0 24 24"><path d="M13.5.67s.74 2.65.74 4.8c0 2.06-1.35 3.73-3.41 3.73-2.07 0-3.63-1.67-3.63-3.73l.03-.36C5.21 7.51 4 10.62 4 14c0 4.42 3.58 8 8 8s8-3.58 8-8C20 8.61 17.41 3.8 13.5.67zM11.71 19c-1.78 0-3.22-1.4-3.22-3.14 0-1.62 1.05-2.76 2.81-3.12 1.77-.36 3.6-1.21 4.62-2.58.39 1.29.59 2.65.59 4.04 0 2.65-2.15 4.8-4.8 4.8z"/></svg>
            <p>No open cook sessions</p>
            <button class="btn btn-primary" id="cooking-start-empty">Start New Session</button>
          </div>`;
        document.getElementById('cooking-start-empty')?.addEventListener('click', showCreatePanel);
        return;
      }

      el.innerHTML = `<div class="list-card">
        ${sessions.map(s => {
          const pct  = s.totalSteps > 0 ? Math.round((s.doneSteps / s.totalSteps) * 100) : 0;
          const loss = s.batchLossPercent > 0 ? ` · ${s.batchLossPercent}% loss` : '';
          return `
          <div class="list-item" data-id="${s.cookSessionID}">
            <div class="li-main">
              <div class="li-title">${s.sessionName}</div>
              <div class="li-sub">${s.createdBy || '—'} · ${App.fmtDateShort(s.createdAt)}${loss}</div>
              <div style="margin-top:6px;">
                <div style="display:flex;justify-content:space-between;margin-bottom:3px;">
                  <span class="text-small text-muted">Progress</span>
                  <span class="text-small" style="font-weight:700;">${s.doneSteps}/${s.totalSteps} steps</span>
                </div>
                <div style="background:var(--bg);border-radius:4px;height:6px;overflow:hidden;">
                  <div style="height:100%;width:${pct}%;background:${pct >= 100 ? 'var(--success)' : 'var(--primary)'};border-radius:4px;"></div>
                </div>
              </div>
            </div>
            <div class="chevron">${App.chevronSvg()}</div>
          </div>`;
        }).join('')}
      </div>`;

      el.querySelectorAll('.list-item').forEach(item =>
        item.addEventListener('click', () => App.navigate(`cooking/${item.dataset.id}`)));
    } catch (err) {
      el.innerHTML = `<div class="empty-state"><p>${err.message}</p></div>`;
    }
  }

  async function showCreatePanel() {
    const el = document.getElementById('cooking-content');
    if (!el) return;
    el.innerHTML = `<div class="card"><p class="text-muted text-small" style="text-align:center;padding:16px 0;">Loading…</p></div>`;

    let wos = [], settings = { flaskConfigs: [], batchLossPresets: [] };
    try {
      [wos, settings] = await Promise.all([
        Api.get('/api/cooking/work-orders'),
        Api.get('/api/cooking/manufacturing-settings'),
      ]);
    } catch (err) {
      el.innerHTML = `<div class="empty-state"><p>${err.message}</p></div>`; return;
    }

    const defaultName = `Cook ${new Date().toLocaleDateString('en-CA')}`;
    const presets     = settings.batchLossPresets || [];

    // Build preset options — first preset is default selection
    const presetOpts = presets.map((p, i) =>
      `<option value="${p.percent}" ${i === 0 ? 'selected' : ''}>${p.label} (${p.percent}%)</option>`
    ).join('');

    el.innerHTML = `
      <div class="card">
        <div style="font-size:16px;font-weight:800;margin-bottom:16px;">New Cook Session</div>
        <div style="margin-bottom:14px;">
          <label class="form-label">Session Name</label>
          <input id="cook-name" class="form-control" value="${defaultName}">
        </div>
        <div style="margin-bottom:14px;">
          <label class="form-label">Batch Loss</label>
          <div style="display:flex;gap:8px;align-items:center;">
            <select id="cook-loss-preset" class="form-control" style="flex:1;">
              ${presetOpts}
              <option value="custom">Custom…</option>
            </select>
            <div id="cook-loss-custom-wrap" style="display:none;align-items:center;gap:4px;">
              <input id="cook-loss-custom" type="number" class="form-control" min="0" max="100" step="0.5"
                     style="width:80px;" value="0">
              <span class="text-muted" style="font-size:13px;">%</span>
            </div>
          </div>
        </div>
        <div style="margin-bottom:14px;">
          <label class="form-label">Work Orders${wos.length ? ' — select one or more' : ''}</label>
          ${wos.length === 0
            ? `<p class="text-muted text-small" style="padding:6px 0;">No in-progress work orders.</p>`
            : `<div style="border:1px solid var(--border);border-radius:10px;overflow:hidden;max-height:320px;overflow-y:auto;">
                ${wos.map(wo => `
                  <label style="display:flex;align-items:center;gap:12px;padding:12px 14px;border-bottom:1px solid var(--bg);cursor:pointer;">
                    <input type="checkbox" class="cook-wo-check" value="${wo.workOrderID}" style="width:18px;height:18px;flex-shrink:0;">
                    <div style="flex:1;min-width:0;">
                      <div style="font-weight:700;font-size:13px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;">${wo.productName}</div>
                      <div class="text-small text-muted">${wo.moNumber} · Qty ${wo.quantity}${wo.assignedTo ? ' · ' + wo.assignedTo : ''}</div>
                    </div>
                    ${App.statusBadge(wo.status)}
                  </label>`).join('')}
              </div>`}
        </div>
        <div id="cook-form-err" style="color:var(--danger);font-size:13px;margin-bottom:8px;display:none;"></div>
        <div style="display:flex;gap:10px;">
          <button class="btn btn-primary" id="cook-submit" style="flex:1;" ${wos.length === 0 ? 'disabled' : ''}>Start Session</button>
          <button class="btn btn-outline" id="cook-cancel" style="flex:1;">Cancel</button>
        </div>
      </div>`;

    // Show/hide custom loss input
    const presetSel  = document.getElementById('cook-loss-preset');
    const customWrap = document.getElementById('cook-loss-custom-wrap');
    presetSel.addEventListener('change', () => {
      const isCustom = presetSel.value === 'custom';
      customWrap.style.display = isCustom ? 'flex' : 'none';
    });

    function getSelectedLoss() {
      if (presetSel.value === 'custom') {
        return parseFloat(document.getElementById('cook-loss-custom').value) || 0;
      }
      return parseFloat(presetSel.value) || 0;
    }

    document.getElementById('cook-cancel').addEventListener('click', loadSessions);
    document.getElementById('cook-submit').addEventListener('click', async () => {
      const name   = document.getElementById('cook-name').value.trim() || defaultName;
      const ids    = [...document.querySelectorAll('.cook-wo-check:checked')].map(cb => parseInt(cb.value, 10));
      const loss   = getSelectedLoss();
      const errEl  = document.getElementById('cook-form-err');
      if (ids.length === 0) { errEl.style.display = ''; errEl.textContent = 'Select at least one work order.'; return; }
      errEl.style.display = 'none';
      const btn = document.getElementById('cook-submit');
      btn.disabled = true; btn.textContent = 'Starting…';
      try {
        const res = await Api.post('/api/cooking/sessions', {
          sessionName: name, workOrderIds: ids, batchLossPercent: loss
        });
        App.navigate(`cooking/${res.cookSessionId}`);
      } catch (err) {
        errEl.style.display = ''; errEl.textContent = err.message;
        btn.disabled = false; btn.textContent = 'Start Session';
      }
    });
  }

  return { render };
})();

// ── Cook Session Detail ────────────────────────────────────────────────────────

const CookSessionDetailPage = (() => {
  let expandedPartId = null;

  async function render(container, sessionId) {
    container.innerHTML = `
      <div class="page">
        <div class="page-header">
          <button class="back-btn" id="cook-back">
            <svg viewBox="0 0 24 24"><path d="M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z"/></svg>
          </button>
          <h1>Cook Session</h1>
        </div>
        <div class="content" id="cook-content">
          ${App.skeletonCards(4)}
        </div>
      </div>`;

    document.getElementById('cook-back').addEventListener('click', () => history.back());
    expandedPartId = null;
    await loadAndRender(sessionId);
  }

  async function loadAndRender(sessionId) {
    const contentEl = document.getElementById('cook-content');
    if (!contentEl) return;
    try {
      const s = await Api.get(`/api/cooking/sessions/${sessionId}`);
      renderFull(contentEl, s);
    } catch (err) {
      contentEl.innerHTML = `<div class="empty-state"><p>${err.message}</p></div>`;
    }
  }

  function renderFull(contentEl, s) {
    const totalSteps = s.ingredients.reduce((n, i) => n + i.stepsTotal, 0);
    const doneSteps  = s.ingredients.reduce((n, i) => n + i.stepsDone,  0);
    const pct        = totalSteps > 0 ? Math.round((doneSteps / totalSteps) * 100) : 0;
    const isComplete = s.status === 'Complete';
    const lossTag    = s.batchLossPercent > 0
      ? `<span class="badge" style="background:var(--primary-lt,rgba(99,102,241,.15));color:var(--primary);font-size:11px;">${s.batchLossPercent}% loss</span>`
      : '';

    contentEl.innerHTML = `
      <div class="card" style="margin-bottom:12px;">
        <div style="display:flex;justify-content:space-between;align-items:flex-start;margin-bottom:10px;">
          <div>
            <div style="font-size:17px;font-weight:800;">${s.sessionName}</div>
            <div class="text-muted text-small" style="display:flex;align-items:center;gap:6px;flex-wrap:wrap;margin-top:3px;">
              <span>${s.createdBy || '—'} · ${App.fmtDate(s.createdAt)}</span>
              ${lossTag}
            </div>
          </div>
          <span class="badge ${isComplete ? 'badge-complete' : 'badge-wip'}">${s.status}</span>
        </div>
        <div style="display:flex;justify-content:space-between;margin-bottom:4px;">
          <span class="text-small text-muted">Overall progress</span>
          <span class="text-small" style="font-weight:700;">${doneSteps}/${totalSteps} steps · ${pct}%</span>
        </div>
        <div style="background:var(--bg);border-radius:4px;height:8px;overflow:hidden;">
          <div style="height:100%;width:${pct}%;background:${pct >= 100 ? 'var(--success)' : 'var(--primary)'};border-radius:4px;transition:width 0.3s;"></div>
        </div>
      </div>

      <div id="cook-ingr-list">${renderIngredients(s.ingredients)}</div>

      ${isComplete
        ? `<div class="card" style="background:var(--success-lt,#e8f5e9);border-color:var(--success);margin-top:8px;">
             <div style="color:var(--success);font-weight:700;">✓  Session completed ${App.fmtDate(s.completedAt)}</div>
           </div>`
        : `<div style="padding:12px 0 24px;">
             <button class="btn btn-primary" id="cook-complete-btn" style="width:100%;height:48px;font-size:15px;">
               ${pct >= 100 ? '✓  Complete Session' : '⚠  Force Complete Session'}
             </button>
           </div>`}`;

    // Single delegated listener on the ingredient list — survives innerHTML replacements
    const listEl = document.getElementById('cook-ingr-list');
    listEl.addEventListener('click', async e => {
      const header  = e.target.closest('.cook-ingr-header');
      const stepBtn = e.target.closest('.cook-step-done');
      const allBtn  = e.target.closest('.cook-all-done');

      // Accordion toggle
      if (header) {
        const partId = parseInt(header.dataset.partId, 10);
        expandedPartId = expandedPartId === partId ? null : partId;
        listEl.innerHTML = renderIngredients(s.ingredients);
        return;
      }

      // Mark one step done
      if (stepBtn && !stepBtn.disabled) {
        const stepId = parseInt(stepBtn.dataset.stepId, 10);
        stepBtn.disabled = true; stepBtn.textContent = '…';
        try {
          await Api.post(`/api/cooking/steps/${stepId}/done`, {});
          const fresh = await Api.get(`/api/cooking/sessions/${s.cookSessionID}`);
          s.ingredients = fresh.ingredients;
          renderFull(contentEl, s);
        } catch (err) {
          App.toast(err.message, 'error');
          stepBtn.disabled = false; stepBtn.textContent = 'Mark Done';
        }
        return;
      }

      // Mark all steps for one ingredient done
      if (allBtn && !allBtn.disabled) {
        const partId = parseInt(allBtn.dataset.partId, 10);
        allBtn.disabled = true; allBtn.textContent = '…';
        try {
          await Api.post(`/api/cooking/sessions/${s.cookSessionID}/ingredients/${partId}/done`, {});
          const fresh = await Api.get(`/api/cooking/sessions/${s.cookSessionID}`);
          s.ingredients = fresh.ingredients;
          renderFull(contentEl, s);
        } catch (err) {
          App.toast(err.message, 'error');
          allBtn.disabled = false; allBtn.textContent = '✓✓  Mark All Done';
        }
        return;
      }
    });

    // Complete session button
    const completeBtn = document.getElementById('cook-complete-btn');
    if (completeBtn) {
      completeBtn.addEventListener('click', async () => {
        const total   = s.ingredients.reduce((n, i) => n + i.stepsTotal, 0);
        const done    = s.ingredients.reduce((n, i) => n + i.stepsDone,  0);
        const pending = total - done;
        const forceComplete = pending > 0;
        if (forceComplete && !confirm(`${pending} step(s) still pending.\nForce complete anyway?`)) return;
        completeBtn.disabled = true; completeBtn.textContent = 'Completing…';
        try {
          await Api.post(`/api/cooking/sessions/${s.cookSessionID}/complete`, { forceComplete });
          App.toast('Session completed!', 'success');
          const fresh = await Api.get(`/api/cooking/sessions/${s.cookSessionID}`);
          renderFull(contentEl, fresh);
        } catch (err) {
          App.toast(err.message, 'error');
          completeBtn.disabled = false;
          completeBtn.textContent = pending === 0 ? '✓  Complete Session' : '⚠  Force Complete Session';
        }
      });
    }
  }

  function renderIngredients(ingredients) {
    if (!ingredients.length)
      return `<div class="empty-state"><p>No ingredients found for this session.</p></div>`;

    return ingredients.map(ingr => {
      const allDone = ingr.stepsDone >= ingr.stepsTotal && ingr.stepsTotal > 0;
      const enough  = ingr.onHand >= ingr.totalRequired;
      const isOpen  = expandedPartId === ingr.partID;

      return `
      <div class="card" style="margin-bottom:8px;padding:0;overflow:hidden;">
        <div class="cook-ingr-header" data-part-id="${ingr.partID}"
             style="display:flex;align-items:center;gap:12px;padding:14px 16px;cursor:pointer;">
          <div style="flex:1;min-width:0;">
            <div style="font-weight:700;font-size:14px;${allDone ? 'text-decoration:line-through;opacity:.6;' : ''}white-space:nowrap;overflow:hidden;text-overflow:ellipsis;">
              ${ingr.partName}
            </div>
            <div class="text-small text-muted">
              ${ingr.totalRequired.toFixed(3)} ${ingr.unitOfMeasure || ''} needed ·
              ${ingr.onHand} on hand
              <span style="font-weight:700;color:${enough ? 'var(--success)' : 'var(--danger)'};">${enough ? ' ✓' : ' ✗ SHORT'}</span>
            </div>
          </div>
          <div style="text-align:right;flex-shrink:0;margin-right:6px;">
            <div style="font-size:15px;font-weight:800;color:${allDone ? 'var(--success)' : 'var(--text)'};">${ingr.stepsDone}/${ingr.stepsTotal}</div>
            <div style="font-size:11px;color:${allDone ? 'var(--success)' : 'var(--text-2)'};">${allDone ? 'Done ✓' : 'Pending'}</div>
          </div>
          <svg viewBox="0 0 24 24" style="width:20px;height:20px;fill:var(--text-2);flex-shrink:0;transition:transform 0.2s;${isOpen ? 'transform:rotate(90deg);' : ''}">
            <path d="M10 6L8.59 7.41 13.17 12l-4.58 4.59L10 18l6-6z"/>
          </svg>
        </div>

        ${isOpen ? `
        <div style="border-top:1px solid var(--border);padding:8px 16px 14px;">
          ${ingr.steps.map(step => `
            <div style="display:flex;align-items:center;gap:10px;padding:9px 0;border-bottom:1px solid var(--bg);">
              <div style="flex:1;min-width:0;">
                <div style="font-size:13px;font-weight:600;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;">
                  ${step.productName}
                  ${step.flaskType ? `<span class="text-muted text-small" style="font-weight:400;"> [${step.flaskType}]</span>` : ''}
                </div>
                <div class="text-small text-muted">${step.moNumber} · ${step.requiredQty.toFixed(3)} ${ingr.unitOfMeasure || ''}</div>
              </div>
              ${step.isDone
                ? `<span class="badge badge-complete" style="font-size:11px;flex-shrink:0;">Done ✓</span>`
                : `<button class="btn btn-primary cook-step-done" data-step-id="${step.stepID}"
                     style="font-size:12px;padding:6px 14px;height:auto;flex-shrink:0;">
                     Mark Done
                   </button>`}
            </div>`).join('')}
          ${!allDone ? `
          <div style="margin-top:10px;">
            <button class="btn btn-outline cook-all-done" data-part-id="${ingr.partID}" style="width:100%;font-size:13px;">
              ✓✓  Mark All Done
            </button>
          </div>` : ''}
        </div>` : ''}
      </div>`;
    }).join('');
  }

  return { render };
})();
