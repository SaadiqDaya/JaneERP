// ── Tasks List ────────────────────────────────────────────────────────────────
const TasksPage = (() => {
  let currentFilter = '';   // '' = Open (outstanding), 'Done', 'all'
  let myOnly        = true;
  let currentPage   = 1;
  let totalPages    = 1;
  let searchTimer   = null;
  let lastSearch    = '';

  const PAGE_SIZE       = 25;
  const PRIORITY_ORDER  = { Urgent: 0, High: 1, Normal: 2, Low: 3 };

  function priorityBadge(p) {
    const map = { Urgent: 'badge-overdue', High: 'badge-wip', Normal: 'badge-live', Low: 'badge-draft' };
    return `<span class="badge ${map[p] || 'badge-draft'}">${p}</span>`;
  }

  async function render(container) {
    container.innerHTML = `
      <div class="page">
        <div class="page-header">
          <h1>Tasks</h1>
          <button class="btn btn-primary" id="task-new-btn" style="padding:8px 14px;font-size:14px;">+ New</button>
        </div>
        <div class="content">
          <div id="task-create-panel" class="hidden"></div>

          <!-- Search bar -->
          <div class="search-bar" style="margin-bottom:10px;">
            <div class="search-icon">
              <svg viewBox="0 0 24 24"><path d="M15.5 14h-.79l-.28-.27A6.47 6.47 0 0 0 16 9.5 6.5 6.5 0 1 0 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z"/></svg>
            </div>
            <input id="task-search" type="search" placeholder="Search tasks…" autocomplete="off">
          </div>

          <!-- Filter chips + Mine toggle -->
          <div style="display:flex;align-items:center;gap:10px;margin-bottom:10px;">
            <div class="filter-row" id="task-filters" style="flex:1;">
              ${[['','Open'],['Done','Done'],['all','All']].map(([v,l]) =>
                `<button class="filter-chip${v === currentFilter ? ' active' : ''}" data-filter="${v}">${l}</button>`
              ).join('')}
            </div>
            <label style="display:flex;align-items:center;gap:5px;font-size:13px;white-space:nowrap;cursor:pointer;">
              <input type="checkbox" id="my-tasks-toggle" ${myOnly ? 'checked' : ''}> Mine
            </label>
          </div>

          <div id="task-list"></div>

          <!-- Pagination bar -->
          <div id="task-pager" style="display:flex;align-items:center;justify-content:center;gap:12px;padding:12px 0;"></div>
        </div>
      </div>`;

    document.getElementById('task-new-btn').addEventListener('click', () => showCreatePanel(container));

    document.getElementById('task-search').addEventListener('input', e => {
      clearTimeout(searchTimer);
      searchTimer = setTimeout(() => {
        lastSearch  = e.target.value.trim();
        currentPage = 1;
        loadTasks();
      }, 350);
    });

    document.getElementById('my-tasks-toggle').addEventListener('change', e => {
      myOnly      = e.target.checked;
      currentPage = 1;
      loadTasks();
    });

    document.getElementById('task-filters').addEventListener('click', e => {
      const chip = e.target.closest('.filter-chip');
      if (!chip) return;
      document.querySelectorAll('#task-filters .filter-chip').forEach(c => c.classList.remove('active'));
      chip.classList.add('active');
      currentFilter = chip.dataset.filter;
      currentPage   = 1;
      loadTasks();
    });

    await loadTasks();
  }

  async function loadTasks() {
    const listEl  = document.getElementById('task-list');
    const pagerEl = document.getElementById('task-pager');
    if (!listEl) return;
    listEl.innerHTML = App.skeletonCards(4);

    try {
      const params = new URLSearchParams();
      params.set('page',     currentPage);
      params.set('pageSize', PAGE_SIZE);

      if (currentFilter === 'Done') {
        params.set('status', 'Done');
      } else if (currentFilter === '') {
        params.set('status', 'Open');
      }
      // 'all' = no status filter

      if (myOnly) params.set('assignedTo', Api.getSession()?.username ?? '');
      if (lastSearch) params.set('search', lastSearch);

      const data = await Api.get(`/api/tasks/paged?${params}`);
      // data: { items, total, page, pageSize }

      const items = data.items || [];
      totalPages  = Math.max(1, Math.ceil((data.total || 0) / PAGE_SIZE));

      // Render pagination
      if (pagerEl) {
        if (data.total > PAGE_SIZE) {
          pagerEl.innerHTML = `
            <button class="btn btn-outline" id="pager-prev" ${currentPage <= 1 ? 'disabled' : ''}>← Prev</button>
            <span style="font-size:13px;color:var(--text-2);">Page ${currentPage} of ${totalPages} &nbsp;(${data.total} records)</span>
            <button class="btn btn-outline" id="pager-next" ${currentPage >= totalPages ? 'disabled' : ''}>Next →</button>`;
          document.getElementById('pager-prev')?.addEventListener('click', () => { currentPage--; loadTasks(); });
          document.getElementById('pager-next')?.addEventListener('click', () => { currentPage++; loadTasks(); });
        } else {
          pagerEl.innerHTML = data.total > 0
            ? `<span style="font-size:13px;color:var(--text-2);">${data.total} record${data.total !== 1 ? 's' : ''}</span>`
            : '';
        }
      }

      if (items.length === 0) {
        listEl.innerHTML = `<div class="empty-state">
          <svg viewBox="0 0 24 24"><path d="M19 3H5c-1.11 0-2 .9-2 2v14c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.11-.9-2-2-2zm-9 14l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z"/></svg>
          <p>No tasks found</p></div>`;
        return;
      }

      // Sort by priority then due date (client-side secondary sort)
      items.sort((a, b) => {
        const pa = PRIORITY_ORDER[a.priority] ?? 2;
        const pb = PRIORITY_ORDER[b.priority] ?? 2;
        if (pa !== pb) return pa - pb;
        return new Date(a.dueDate) - new Date(b.dueDate);
      });

      const today = new Date(); today.setHours(0,0,0,0);

      listEl.innerHTML = `<div class="list-card">
        ${items.map(t => {
          const due       = new Date(t.dueDate);
          const isOverdue = due < today && t.status !== 'Done';
          const stage     = t.workflowCurrentStatus || t.status;
          return `
          <div class="list-item" data-id="${t.taskID}">
            <div class="li-main">
              <div class="li-title">${t.title}</div>
              <div class="li-sub">
                ${t.assignedTo}
                · Due ${App.fmtDateShort(t.dueDate)}
                ${isOverdue ? '<span style="color:var(--danger);font-weight:700;"> OVERDUE</span>' : ''}
                ${t.tags ? `<span style="color:var(--text-2);font-size:11px;"> · ${t.tags}</span>` : ''}
              </div>
            </div>
            <div class="li-right">
              ${priorityBadge(t.priority)}
              ${App.statusBadge(stage === 'In Progress' ? 'WIP' : stage === 'Done' ? 'Complete' : stage)}
            </div>
            <div class="chevron">${App.chevronSvg()}</div>
          </div>`;
        }).join('')}
      </div>`;

      listEl.querySelectorAll('.list-item').forEach(el =>
        el.addEventListener('click', () => App.navigate(`tasks/${el.dataset.id}`))
      );
    } catch (err) {
      listEl.innerHTML = `<div class="empty-state"><p>${err.message}</p></div>`;
    }
  }

  async function showCreatePanel(container) {
    const panelEl = document.getElementById('task-create-panel');
    if (!panelEl.classList.contains('hidden')) { panelEl.classList.add('hidden'); return; }

    let usernames = [];
    let workflows = [];
    try { usernames = await Api.get('/api/tasks/users'); } catch {}
    try { workflows = await Api.get('/api/tasks/workflows'); } catch {}

    const today = new Date().toISOString().split('T')[0];

    panelEl.innerHTML = `
      <div class="card" style="margin-bottom:12px;">
        <div style="font-weight:700;margin-bottom:12px;">New Task</div>
        <input id="tc-title" type="text" placeholder="Title *" style="width:100%;padding:10px;border:1.5px solid var(--border);border-radius:8px;font-size:15px;margin-bottom:8px;box-sizing:border-box;">
        <textarea id="tc-desc" placeholder="Description (optional)" rows="2"
          style="width:100%;padding:10px;border:1.5px solid var(--border);border-radius:8px;font-size:14px;resize:none;margin-bottom:8px;box-sizing:border-box;"></textarea>
        <div style="display:grid;grid-template-columns:1fr 1fr;gap:8px;margin-bottom:8px;">
          <div>
            <div style="font-size:12px;color:var(--text-2);margin-bottom:4px;">Assign To *</div>
            <select id="tc-assignee" style="width:100%;padding:9px;border:1.5px solid var(--border);border-radius:8px;font-size:14px;">
              ${usernames.length
                ? usernames.map(u => `<option value="${u}"${u === (Api.getSession()?.username) ? ' selected' : ''}>${u}</option>`).join('')
                : `<option value="${Api.getSession()?.username ?? ''}">${Api.getSession()?.username ?? 'me'}</option>`}
            </select>
          </div>
          <div>
            <div style="font-size:12px;color:var(--text-2);margin-bottom:4px;">Priority</div>
            <select id="tc-priority" style="width:100%;padding:9px;border:1.5px solid var(--border);border-radius:8px;font-size:14px;">
              <option value="Low">Low</option>
              <option value="Normal" selected>Normal</option>
              <option value="High">High</option>
              <option value="Urgent">Urgent</option>
            </select>
          </div>
        </div>
        <div style="margin-bottom:8px;">
          <div style="font-size:12px;color:var(--text-2);margin-bottom:4px;">Due Date *</div>
          <input id="tc-due" type="date" value="${today}" style="width:100%;padding:9px;border:1.5px solid var(--border);border-radius:8px;font-size:14px;box-sizing:border-box;">
        </div>
        ${workflows.length > 0 ? `
        <div style="margin-bottom:12px;">
          <div style="font-size:12px;color:var(--text-2);margin-bottom:4px;">Workflow (optional)</div>
          <select id="tc-workflow" style="width:100%;padding:9px;border:1.5px solid var(--border);border-radius:8px;font-size:14px;">
            <option value="">(None)</option>
            ${workflows.map(w => `<option value="${w.workflowId}">${w.name}</option>`).join('')}
          </select>
        </div>` : ''}
        <div style="display:flex;gap:8px;">
          <button class="btn btn-primary" id="tc-save-btn" style="flex:1;">Create Task</button>
          <button class="btn btn-outline" id="tc-cancel-btn">Cancel</button>
        </div>
      </div>`;
    panelEl.classList.remove('hidden');

    document.getElementById('tc-cancel-btn').addEventListener('click', () => {
      panelEl.classList.add('hidden');
      panelEl.innerHTML = '';
    });

    document.getElementById('tc-save-btn').addEventListener('click', async () => {
      const title    = document.getElementById('tc-title').value.trim();
      const desc     = document.getElementById('tc-desc').value.trim();
      const assignee = document.getElementById('tc-assignee').value;
      const priority = document.getElementById('tc-priority').value;
      const dueDate  = document.getElementById('tc-due').value;

      if (!title)   { App.toast('Title is required', 'error'); return; }
      if (!dueDate) { App.toast('Due date is required', 'error'); return; }

      const btn = document.getElementById('tc-save-btn');
      btn.disabled = true; btn.textContent = 'Saving…';

      try {
        await Api.post('/api/tasks', {
          title,
          description: desc || null,
          assignedTo:  assignee,
          dueDate:     new Date(dueDate).toISOString(),
          priority
        });
        App.toast('Task created!', 'success');
        panelEl.classList.add('hidden');
        panelEl.innerHTML = '';
        currentPage = 1;
        await loadTasks();
      } catch (err) {
        App.toast(err.message, 'error');
        btn.disabled = false; btn.textContent = 'Create Task';
      }
    });
  }

  return { render };
})();

// ── Task Detail ───────────────────────────────────────────────────────────────
const TaskDetailPage = (() => {
  function priorityBadge(p) {
    const map = { Urgent: 'badge-overdue', High: 'badge-wip', Normal: 'badge-live', Low: 'badge-draft' };
    return `<span class="badge ${map[p] || 'badge-draft'}">${p}</span>`;
  }

  async function render(container, taskId) {
    container.innerHTML = `
      <div class="page">
        <div class="page-header">
          <button class="back-btn" id="task-back">
            <svg viewBox="0 0 24 24"><path d="M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z"/></svg>
          </button>
          <h1>Task Detail</h1>
        </div>
        <div class="content" id="task-detail-content">${App.skeletonCards(4)}</div>
      </div>`;

    document.getElementById('task-back').addEventListener('click', () => history.back());
    await loadDetail(taskId);
  }

  async function loadDetail(taskId) {
    const contentEl = document.getElementById('task-detail-content');
    try {
      const t = await Api.get(`/api/tasks/${taskId}`);
      const today     = new Date(); today.setHours(0,0,0,0);
      const due       = new Date(t.dueDate);
      const isOverdue = due < today && t.status !== 'Done';
      const stage     = t.workflowCurrentStatus || t.status;

      // ── Header card ──────────────────────────────────────────────────────────
      let html = `
        <div class="card">
          <div style="font-size:17px;font-weight:800;margin-bottom:6px;">${t.title}</div>
          <div style="display:flex;gap:6px;flex-wrap:wrap;margin-bottom:10px;">
            ${priorityBadge(t.priority)}
            ${App.statusBadge(stage === 'In Progress' ? 'WIP' : stage === 'Done' ? 'Complete' : stage)}
            ${t.workflowName ? `<span class="badge badge-draft">${t.workflowName}</span>` : ''}
          </div>
          ${t.tags ? `<div style="font-size:12px;color:var(--text-2);margin-bottom:8px;">🏷 ${t.tags}</div>` : ''}
          <div class="divider"></div>
          <div style="display:grid;grid-template-columns:1fr 1fr;gap:8px;margin-top:10px;">
            <div>
              <div class="text-muted text-small">Assigned To</div>
              <div style="font-weight:600;">${t.assignedTo}</div>
            </div>
            <div>
              <div class="text-muted text-small">Due Date</div>
              <div style="font-weight:600;${isOverdue ? 'color:var(--danger)' : ''}">${App.fmtDate(t.dueDate)}${isOverdue ? ' ⚠' : ''}</div>
            </div>
            <div>
              <div class="text-muted text-small">Created By</div>
              <div style="font-weight:600;">${t.createdBy}</div>
            </div>
            <div>
              <div class="text-muted text-small">Created</div>
              <div style="font-weight:600;">${App.fmtDate(t.createdAt)}</div>
            </div>
          </div>
          ${t.description ? `<div class="divider" style="margin-top:10px;"></div><div style="margin-top:10px;font-size:14px;color:var(--text-2);white-space:pre-wrap;">${t.description}</div>` : ''}
        </div>`;

      // ── Workflow section ──────────────────────────────────────────────────────
      if (t.workflowStages && t.workflowStages.length > 0) {
        const currentIdx = t.workflowStages.indexOf(t.workflowCurrentStatus);
        const canAdvance = currentIdx < t.workflowStages.length - 1;
        html += `
          <div class="card">
            <div style="font-weight:700;font-size:14px;margin-bottom:10px;">Workflow: ${t.workflowName || ''}</div>
            <div style="display:flex;gap:6px;flex-wrap:wrap;margin-bottom:12px;">
              ${t.workflowStages.map((s, i) => `
                <span style="padding:4px 10px;border-radius:12px;font-size:12px;font-weight:600;
                  background:${i === currentIdx ? 'var(--primary)' : 'var(--surface-2)'};
                  color:${i === currentIdx ? '#fff' : 'var(--text-2)'};">${s}</span>
              `).join('')}
            </div>
            <div style="display:flex;gap:8px;flex-wrap:wrap;">
              ${canAdvance ? `<button class="btn btn-primary" id="btn-advance-stage">→ Next Stage</button>` : ''}
              <select id="sel-jump-stage" style="padding:8px 12px;border:1.5px solid var(--border);border-radius:8px;font-size:14px;">
                <option value="">Jump to stage…</option>
                ${t.workflowStages.map(s => `<option value="${s}" ${s === t.workflowCurrentStatus ? 'selected' : ''}>${s}</option>`).join('')}
              </select>
            </div>
          </div>`;
      } else {
        // No workflow — show simple status advance
        const NEXT_STATUS = { 'Open': 'In Progress', 'In Progress': 'Done' };
        const nextStatus  = NEXT_STATUS[t.status];
        if (nextStatus) {
          html += `<button class="btn btn-primary btn-full" id="task-advance-btn" data-next="${nextStatus}">
            Mark as ${nextStatus}
          </button>`;
        }
      }

      // ── Subtasks section ─────────────────────────────────────────────────────
      const subtasks = t.subtasks || [];
      const doneCount = subtasks.filter(s => s.isComplete).length;
      html += `
        <div class="card">
          <div style="font-weight:700;font-size:14px;margin-bottom:10px;">
            Subtasks
            ${subtasks.length > 0 ? `<span style="font-weight:400;color:var(--text-2);font-size:12px;"> (${doneCount}/${subtasks.length})</span>` : ''}
          </div>
          ${subtasks.length === 0 ? '<div class="text-muted text-small">No subtasks yet.</div>' : ''}
          <div id="subtask-list">
            ${subtasks.map(s => `
              <div style="display:flex;align-items:center;gap:10px;padding:6px 0;border-bottom:1px solid var(--border);">
                <input type="checkbox" ${s.isComplete ? 'checked disabled' : ''} data-subid="${s.subtaskId}"
                  style="width:18px;height:18px;cursor:${s.isComplete ? 'default' : 'pointer'};">
                <div style="flex:1;">
                  <div style="${s.isComplete ? 'text-decoration:line-through;color:var(--text-2);' : ''}">${s.title}</div>
                  ${s.isComplete && s.completedBy ? `<div class="text-muted text-small">✓ ${s.completedBy} · ${App.fmtDate(s.completedAt)}</div>` : ''}
                </div>
              </div>`).join('')}
          </div>
          <div style="display:flex;gap:8px;margin-top:10px;">
            <input id="new-subtask-input" type="text" placeholder="New subtask…"
              style="flex:1;padding:8px 10px;border:1.5px solid var(--border);border-radius:8px;font-size:14px;">
            <button class="btn btn-primary" id="add-subtask-btn">Add</button>
          </div>
        </div>`;

      // ── Linked Records section ────────────────────────────────────────────────
      const links = t.linkedRecords || [];
      html += `
        <div class="card">
          <div style="font-weight:700;font-size:14px;margin-bottom:10px;">Linked Records</div>
          <div id="links-list">
            ${links.length === 0 ? '<div class="text-muted text-small">No linked records.</div>' : ''}
            ${links.map(lr => `
              <div style="display:flex;align-items:center;gap:8px;padding:6px 0;border-bottom:1px solid var(--border);"
                data-linkid="${lr.linkId}">
                <span class="badge badge-draft">${lr.linkedModule}</span>
                <span style="flex:1;font-size:14px;">${lr.linkedDisplay || lr.linkedId}</span>
                <button class="btn btn-danger" data-linkid="${lr.linkId}" style="font-size:12px;padding:4px 8px;">Remove</button>
              </div>`).join('')}
          </div>
          <div style="margin-top:10px;" id="add-link-area">
            <div style="display:flex;gap:8px;flex-wrap:wrap;">
              <select id="new-link-module" style="padding:8px;border:1.5px solid var(--border);border-radius:8px;font-size:14px;">
                <option value="">Module…</option>
                <option value="Sales Order">Sales Order</option>
                <option value="Purchase Order">Purchase Order</option>
                <option value="Customer">Customer</option>
                <option value="Product">Product</option>
              </select>
              <input id="new-link-id" type="text" placeholder="ID or reference"
                style="flex:1;min-width:100px;padding:8px 10px;border:1.5px solid var(--border);border-radius:8px;font-size:14px;">
              <button class="btn btn-primary" id="add-link-btn">Add Link</button>
            </div>
          </div>
        </div>`;

      // ── History / Activity ────────────────────────────────────────────────────
      const history = t.history || [];
      if (history.length > 0) {
        html += `
          <div class="card">
            <div style="font-weight:700;font-size:14px;margin-bottom:10px;">Activity (${history.length})</div>
            <div style="font-size:12px;font-family:monospace;color:var(--text-2);">
              ${history.slice(-10).reverse().map(h => `
                <div style="padding:3px 0;border-bottom:1px solid var(--border);">
                  <span style="color:var(--text-1);">${h.changedBy}</span>
                  changed <b>${h.fieldName}</b>:
                  ${h.oldValue || '—'} → ${h.newValue || '—'}
                  <span style="float:right;">${App.fmtDate(h.changedAt)}</span>
                </div>`).join('')}
            </div>
          </div>`;
      }

      // ── Comments ──────────────────────────────────────────────────────────────
      html += `
        <div class="card">
          <div style="font-weight:700;font-size:14px;margin-bottom:10px;">Comments (${t.comments.length})</div>
          ${t.comments.length === 0 ? '<div class="text-muted text-small">No comments yet.</div>' : ''}
          <div id="comment-list">
            ${t.comments.map(c => `
              <div style="margin-bottom:10px;padding-bottom:10px;border-bottom:1px solid var(--border);">
                <div style="display:flex;justify-content:space-between;margin-bottom:3px;">
                  <span style="font-weight:700;font-size:13px;">${c.username}</span>
                  <span class="text-muted text-small">${App.fmtDate(c.createdAt)}</span>
                </div>
                <div style="font-size:14px;">${c.body}</div>
              </div>`).join('')}
          </div>
          <div style="display:flex;gap:8px;margin-top:8px;">
            <textarea id="comment-body" placeholder="Add a comment…" rows="2"
              style="flex:1;padding:9px;border:1.5px solid var(--border);border-radius:8px;font-size:14px;resize:none;"></textarea>
            <button class="btn btn-primary" id="comment-send-btn" style="align-self:flex-end;padding:10px 16px;">Send</button>
          </div>
        </div>`;

      contentEl.innerHTML = html;

      // ── Wire up events ────────────────────────────────────────────────────────

      // Workflow: advance stage
      document.getElementById('btn-advance-stage')?.addEventListener('click', async e => {
        const btn = e.currentTarget;
        btn.disabled = true; btn.textContent = '…';
        try {
          await Api.post(`/api/tasks/${taskId}/advance`, {});
          App.toast('Advanced to next stage', 'success');
          await loadDetail(taskId);
        } catch (err) {
          App.toast(err.message, 'error');
          btn.disabled = false; btn.textContent = '→ Next Stage';
        }
      });

      // Workflow: jump to stage
      document.getElementById('sel-jump-stage')?.addEventListener('change', async e => {
        const stage = e.target.value;
        if (!stage) return;
        try {
          await Api.post(`/api/tasks/${taskId}/stage`, { stage });
          App.toast(`Moved to ${stage}`, 'success');
          await loadDetail(taskId);
        } catch (err) {
          App.toast(err.message, 'error');
        }
      });

      // Status advance (no workflow)
      document.getElementById('task-advance-btn')?.addEventListener('click', async e => {
        const btn  = e.currentTarget;
        const next = btn.dataset.next;
        btn.disabled = true; btn.textContent = 'Saving…';
        try {
          await Api.patch(`/api/tasks/${taskId}/status`, { status: next });
          App.toast(`Marked as ${next}`, 'success');
          await loadDetail(taskId);
        } catch (err) {
          App.toast(err.message, 'error');
          btn.disabled = false; btn.textContent = `Mark as ${next}`;
        }
      });

      // Subtasks: check (complete)
      document.querySelectorAll('#subtask-list input[type=checkbox]:not([disabled])').forEach(cb => {
        cb.addEventListener('change', async e => {
          if (!e.target.checked) return; // only complete from mobile
          const subId = e.target.dataset.subid;
          e.target.disabled = true;
          try {
            await Api.post(`/api/tasks/${taskId}/subtasks/${subId}/complete`, {});
            App.toast('Subtask completed', 'success');
            await loadDetail(taskId);
          } catch (err) {
            App.toast(err.message, 'error');
            e.target.disabled = false;
            e.target.checked  = false;
          }
        });
      });

      // Subtasks: add new
      document.getElementById('add-subtask-btn')?.addEventListener('click', async () => {
        const inp   = document.getElementById('new-subtask-input');
        const title = inp.value.trim();
        if (!title) { App.toast('Enter a subtask title', 'error'); return; }
        const btn = document.getElementById('add-subtask-btn');
        btn.disabled = true; btn.textContent = '…';
        try {
          await Api.post(`/api/tasks/${taskId}/subtasks`, { title });
          App.toast('Subtask added', 'success');
          await loadDetail(taskId);
        } catch (err) {
          App.toast(err.message, 'error');
          btn.disabled = false; btn.textContent = 'Add';
        }
      });

      document.getElementById('new-subtask-input')?.addEventListener('keypress', e => {
        if (e.key === 'Enter') document.getElementById('add-subtask-btn')?.click();
      });

      // Linked records: remove
      document.querySelectorAll('#links-list button[data-linkid]').forEach(btn => {
        btn.addEventListener('click', async e => {
          const linkId = e.currentTarget.dataset.linkid;
          btn.disabled = true;
          try {
            await Api.delete(`/api/tasks/${taskId}/links/${linkId}`);
            App.toast('Link removed', 'success');
            await loadDetail(taskId);
          } catch (err) {
            App.toast(err.message, 'error');
            btn.disabled = false;
          }
        });
      });

      // Linked records: add
      document.getElementById('add-link-btn')?.addEventListener('click', async () => {
        const module  = document.getElementById('new-link-module').value;
        const linkedId = document.getElementById('new-link-id').value.trim();
        if (!module)    { App.toast('Select a module', 'error'); return; }
        if (!linkedId)  { App.toast('Enter an ID', 'error'); return; }
        const btn = document.getElementById('add-link-btn');
        btn.disabled = true; btn.textContent = '…';
        try {
          await Api.post(`/api/tasks/${taskId}/links`, {
            module,
            linkedId,
            linkedDisplay: `${module} #${linkedId}`
          });
          App.toast('Link added', 'success');
          await loadDetail(taskId);
        } catch (err) {
          App.toast(err.message, 'error');
          btn.disabled = false; btn.textContent = 'Add Link';
        }
      });

      // Comments: send
      document.getElementById('comment-send-btn')?.addEventListener('click', async () => {
        const body = document.getElementById('comment-body').value.trim();
        if (!body) { App.toast('Enter a comment', 'error'); return; }
        const btn = document.getElementById('comment-send-btn');
        btn.disabled = true; btn.textContent = '…';
        try {
          await Api.post(`/api/tasks/${taskId}/comments`, { body });
          document.getElementById('comment-body').value = '';
          await loadDetail(taskId);
        } catch (err) {
          App.toast(err.message, 'error');
          btn.disabled = false; btn.textContent = 'Send';
        }
      });

    } catch (err) {
      contentEl.innerHTML = `<div class="empty-state"><p>${err.message}</p></div>`;
    }
  }

  return { render };
})();
