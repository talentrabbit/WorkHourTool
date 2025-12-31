<template>
  <div class="ncm-container">
    <h2>NCM Time Maintenance</h2>
    <p>{{ showAll ? 'Administrator login, so all NCM listed' : 'Showing NCM records assigned to you (' + username + ').' }}</p>
    <div v-if="loading" class="loading">Loading...</div>
    <div v-else>
      <!-- render each systemType group as its own titled block with its own table so headers align -->
      <template v-for="group in pagedGroups" :key="group.systemType">
        <div class="group-block">
          <div class="group-subtitle">{{ group.systemType || 'Unknown' }}</div>
          <div class="ncm-table-wrap">
            <table class="ncm-table">
              <thead>
                <tr>
                  <th class="col-serial">SerialNo</th>
                  <th v-if="showAll" class="col-engineer">Engineer</th>
                  <th class="col-process">Process</th>
                  <th class="col-hours">Hours</th>
                  <th class="col-start">Start</th>
                  <th class="col-end">End</th>
                  <th class="col-state">State</th>
                    <th class="col-calltype">Call Type</th>
                    <th class="col-content">Content</th>
                    <th class="col-actions">Actions</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="row in group.rows" :key="row.id" class="clickable" @click="openEditor(row)" @contextmenu.prevent="onRowContextMenu($event, row)">
                  <td class="col-serial" :title="row.serialNo">{{ row.serialNo }}</td>
                  <td v-if="showAll" class="col-engineer" :title="row.processEngineer">{{ row.processEngineer }}</td>
                  <td class="col-process" :title="row.processName">{{ row.processName }}</td>
                  <td class="col-hours" :title="formatNcmHours(row.ncmHours, row.startTime, row.endTime)">{{ formatNcmHours(row.ncmHours, row.startTime, row.endTime) }}</td>
                  <td class="col-start" :title="row._startLocal">{{ row._startLocal }}</td>
                  <td class="col-end" :title="row._endLocal">{{ row._endLocal }}</td>
                  <td class="col-state" :title="row.state">{{ row.state }}</td>
                  <td class="col-calltype" :title="row.callType">{{ row.callType }}</td>
                  <td class="col-content" :title="row.callingContent">{{ row.callingContent }}</td>
                  <td class="col-actions" :title="row.actions">{{ row.actions }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </template>
      <div class="pager" style="display:flex;align-items:center;gap:8px;margin-top:12px;justify-content:center">
        <button :disabled="ncmPage <= 1" @click="ncmPage = 1">First</button>
        <button :disabled="ncmPage <= 1" @click="ncmPage = Math.max(1, ncmPage-1)">Prev</button>
        <span>Page {{ ncmPage }} / {{ ncmTotalPages }}</span>
        <button :disabled="ncmPage >= ncmTotalPages" @click="ncmPage = Math.min(ncmTotalPages, ncmPage+1)">Next</button>
        <button :disabled="ncmPage >= ncmTotalPages" @click="ncmPage = ncmTotalPages">Last</button>
        <span style="margin-left:8px;color:#666">Total: {{ totalNcmRows }}</span>
      </div>
      <div v-if="!records.length" class="empty">No NCM records for your account.</div>
    </div>
    
    <!-- Modal Editor -->
    <div v-if="modalOpen" class="modal-overlay" @click.self="closeEditor">
      <div class="modal">
        <div class="modal-header">
              <div class="modal-title-block">
                <div class="serial">SerialNo: <strong>{{ productBrief.serialNo || editing.serialNo }}</strong></div>
                <div class="brief">ProductLine: <strong>{{ productBrief.productLine || '-' }}</strong> · SystemType: <strong>{{ productBrief.systemType || editing.systemType || '-' }}</strong></div>
              </div>
              <button class="close-btn" @click="closeEditor">×</button>
            </div>

        <div class="modal-body">
          <div class="form-row" v-if="showAll">
            <label>Process Engineer</label>
            <select v-model="editing.processEngineer">
              <option value="">--</option>
              <option v-for="name in processEngineers" :key="name" :value="name">{{ name }}</option>
            </select>
          </div>
          <div class="form-row">
            <label>Process Name</label>
            <input v-model="editing.processName" />
          </div>
          <div class="form-row">
            <label>Call Type</label>
            <select v-model="editing.callType">
              <option value="">--</option>
              <option value="NCM">NCM</option>
              <option value="OTHER">Other Anomaly</option>
            </select>
          </div>
          <div class="form-row">
            <label>Actions</label>
            <textarea v-model="editing.actions" rows="3"></textarea>
          </div>
          <div class="form-grid">
            <div class="form-row">
              <label>Start</label>
              <input type="datetime-local" v-model="editing._startLocal" />
            </div>
            <div class="form-row">
              <label>End</label>
              <input type="datetime-local" v-model="editing._endLocal" />
            </div>
            <div class="form-row">
              <label>NCM Hours</label>
              <input type="number" step="0.001" v-model.number="editing.ncmHours" />
            </div>
          </div>
          <div class="form-row">
            <label>State</label>
            <input v-model="editing.state" />
          </div>
          <div class="form-row">
            <label>Content</label>
            <textarea v-model="editing.callingContent" rows="3"></textarea>
          </div>
        </div>

        <!-- Compact badge-style hint for missing/invalid fields -->
        <div v-if="missingFields.length" class="modal-warning-badge" :title="missingFields.join(', ')" role="status" aria-live="polite">
          <span class="badge-icon" aria-hidden="true">⚠</span>
          <span class="badge-text">Missing: {{ missingFields.join(', ') }}</span>
        </div>

        <div class="modal-footer">
          <div class="left-actions" v-if="isAdmin && editing.id">
            <button class="danger" @click="openDeleteConfirm(editing.id)">Delete</button>
          </div>
            <div class="right-actions" style="display: flex; gap: 10px;">
              <button class="secondary" @click="closeEditor">Cancel</button>
              <button :disabled="!isEditorValid" @click="saveEditor" :title="isEditorValid ? 'Save' : 'Complete required fields to enable Save'" aria-disabled="{{ !isEditorValid }}">Save</button>
          </div>
        </div>
      </div>
    </div>
    <!-- Context Menu -->
    <div v-if="contextMenu.visible" class="context-menu" :style="{ top: contextMenu.y + 'px', left: contextMenu.x + 'px' }">
      <ul>
        <li @click="onContextEdit">Edit</li>
        <li v-if="isAdmin" class="danger-item" @click="onContextDelete">Delete</li>
      </ul>
    </div>
    <!-- Delete Confirmation (reuses TimerClock confirm-dialog styles) -->
    <div v-if="showDeleteConfirm" class="confirm-overlay" role="dialog" aria-modal="true">
      <div class="confirm-dialog">
        <div class="confirm-title">Confirm Deletion</div>
        <div class="confirm-body">This will permanently delete the selected NCM record. Continue?</div>
        <div class="confirm-buttons">
          <button class="confirm-btn cancel" @click="cancelDelete">No</button>
          <button class="confirm-btn confirm" @click="confirmDelete">Yes, Delete</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted, inject, computed, watch } from 'vue'
import axios from 'axios'

const username = inject('username')
const userRole = inject('userRole')

const role = computed(() => (userRole && userRole.value) ? userRole.value.toLowerCase() : '')
const showAll = computed(() => role.value === 'administrator' || role.value === 'productionmanager')
// treat production manager as an admin for deletion and full NCM visibility
const isAdmin = computed(() => role.value === 'administrator' || role.value === 'productionmanager')

const records = ref([])
const loading = ref(false)
// list of process engineer names for dropdown
const processEngineers = ref([])

// --- Pagination ---
const pageSize = 10
const ncmPage = ref(1)

const flatRecords = computed(() => {
  // flatten groups to a simple array of rows
  return (records.value || []).flatMap(g => (g.rows || []).map(r => ({ ...r, systemType: g.systemType })))
})

const totalNcmRows = computed(() => flatRecords.value.length)
const ncmTotalPages = computed(() => Math.max(1, Math.ceil(totalNcmRows.value / pageSize)))

// clamp page when total changes
watch(ncmTotalPages, (t) => { if (ncmPage.value > t) ncmPage.value = t })

// reset page to 1 when records change
watch(flatRecords, () => { ncmPage.value = 1 })

const pagedGroups = computed(() => {
  const start = (Math.max(1, ncmPage.value) - 1) * pageSize
  const pageRows = flatRecords.value.slice(start, start + pageSize)
  // regroup by systemType
  const map = {}
  for (const r of pageRows) {
    const st = r.systemType || 'Unknown'
    if (!map[st]) map[st] = []
    map[st].push(r)
  }
  return Object.keys(map).map(k => ({ systemType: k, rows: map[k] }))
})

function toLocalInput(dtStr) {
  if (!dtStr) return ''
  const d = new Date(dtStr)
  if (isNaN(d.getTime())) return ''
  // format to YYYY-MM-DDTHH:MM for datetime-local (omit seconds)
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  const hh = String(d.getHours()).padStart(2, '0')
  const mm = String(d.getMinutes()).padStart(2, '0')
  return `${y}-${m}-${day}T${hh}:${mm}`
}
function fromLocalInput(localStr) {
  if (!localStr) return null
  // localStr like "YYYY-MM-DDTHH:MM" — append :00 seconds
  return localStr + ':00'
}

onMounted(async () => {
  loading.value = true
  try {
    const res = await axios.get('/api/WorkHours/all-ncmtimes')
    const list = Array.isArray(res.data) ? res.data : []
    const me = (username && username.value) ? username.value : ''

    // if admin or production manager, do not filter; otherwise only show records assigned to current process engineer
    const chosen = showAll.value ? list : list.filter(r => (r.processEngineer || '').toLowerCase() === me.toLowerCase())

    // group by systemType
    const groupsMap = {}
    chosen.forEach(r => {
      const st = r.systemType || 'Unknown'
      if (!groupsMap[st]) groupsMap[st] = []
      groupsMap[st].push({
        id: r.id,
        serialNo: r.serialNo,
        processEngineer: r.processEngineer || '',
        systemType: r.systemType,
        processName: r.processName,
        // Prefer backend's `NcmHour` (singular) which is returned by the API; fall back to other casings if present
        ncmHours: (r.NcmHours !== undefined && r.NcmHours !== null) ? Number(r.NcmHours)
                  : (r.ncmHours !== undefined && r.ncmHours !== null) ? Number(r.ncmHours)
                  : null,
        startTime: r.startTime,
        endTime: r.endTime,
        state: r.state || '',
        // normalize callType into internal codes: 'NCM' or 'OTHER' when possible
        callType: (function(){
          const raw = (r.callType !== undefined && r.callType !== null) ? r.callType : (r.CallType !== undefined && r.CallType !== null ? r.CallType : '');
          const s = String(raw || '').trim();
          if (!s) return '';
          if (/other/i.test(s)) return 'OTHER';
          if (s.toUpperCase() === 'NCM') return 'NCM';
          return s;
        })(),
        actions: (r.actions !== undefined && r.actions !== null) ? r.actions : (r.Actions !== undefined && r.Actions !== null ? r.Actions : ''),
        callingContent: r.callingContent || '',
        _startLocal: toLocalInput(r.startTime),
        _endLocal: toLocalInput(r.endTime)
      })
    })

    records.value = Object.keys(groupsMap).map(k => ({ systemType: k, rows: groupsMap[k] }))
    // fetch process engineer names for the modal dropdown (best-effort)
    try {
      const pe = await axios.get('/api/WorkHours/all-process-engineer-names')
      if (Array.isArray(pe.data)) processEngineers.value = pe.data
    } catch (e) {
      // non-fatal: keep list empty
      console.warn('Failed to load process engineer names', e)
    }
  } catch (e) {
    console.error('Failed to load NCM records', e)
    records.value = []
  } finally {
    loading.value = false
  }
})

// Modal state and helpers
const modalOpen = ref(false)
const editing = ref({})
const productBrief = ref({ serialNo: '', productLine: '', systemType: '' })

// context menu state
const contextMenu = ref({ visible: false, x: 0, y: 0, row: null })
const showDeleteConfirm = ref(false)
const deleteTargetId = ref(null)

function onRowContextMenu(e, row) {
  contextMenu.value.visible = true
  contextMenu.value.x = e.clientX
  contextMenu.value.y = e.clientY
  contextMenu.value.row = row
}
function closeContextMenu() { contextMenu.value.visible = false }
function onContextEdit() { if (contextMenu.value.row) openEditor(contextMenu.value.row); closeContextMenu() }
function onContextDelete() {
  if (contextMenu.value.row) { openDeleteConfirm(contextMenu.value.row.id) }
  closeContextMenu()
}

function openEditor(row) {
  // deep-ish copy for editing
  editing.value = JSON.parse(JSON.stringify(row))
  productBrief.value = { serialNo: row.serialNo, productLine: '', systemType: row.systemType || '' }
  modalOpen.value = true
  // fetch product brief (productLine/systemType) by serial
  fetchProductBrief(row.serialNo)
  // ensure current engineer appears in the dropdown list so it can be selected
  if (editing.value.processEngineer && !processEngineers.value.includes(editing.value.processEngineer)) {
    processEngineers.value = [editing.value.processEngineer].concat(processEngineers.value)
  }
}

function closeEditor() {
  modalOpen.value = false
}

async function fetchProductBrief(serial) {
  try {
    const res = await axios.get(`/api/WorkHours/product-status/${encodeURIComponent(serial)}`)
    const p = res.data || {}
    productBrief.value = {
      serialNo: p.serialNo || serial,
      productLine: p.productLine || '',
      systemType: p.systemType || editing.value.systemType || ''
    }
  } catch (e) {
    // non-fatal
    console.warn('Failed to fetch product brief', e)
  }
}

// Provide a human-friendly list of missing/invalid fields for the modal editor
const missingFields = computed(() => {
  const e = editing.value || {}
  const required = [
    { key: 'processName', label: 'Process Name' },
    { key: 'callType', label: 'Call Type' },
    { key: 'actions', label: 'Actions' },
    { key: '_startLocal', label: 'Start' },
    { key: '_endLocal', label: 'End' },
    { key: 'ncmHours', label: 'NCM Hours' },
    { key: 'state', label: 'State' },
    { key: 'callingContent', label: 'Content' }
  ]

  const out = []
  for (const f of required) {
    const v = e[f.key]
    if (v === undefined || v === null) { out.push(f.label); continue }
    if (typeof v === 'string' && v.trim() === '') { out.push(f.label); continue }
  }

  // ncmHours must be numeric
  if (!(e.ncmHours !== undefined && e.ncmHours !== null && !isNaN(Number(e.ncmHours)))) {
    if (!out.includes('NCM Hours')) out.push('NCM Hours')
  }

  // start/end validity: require parsable times and end > start
  if (e._startLocal && e._endLocal) {
    const s = new Date(fromLocalInput(e._startLocal))
    const en = new Date(fromLocalInput(e._endLocal))
    if (isNaN(s.getTime()) || isNaN(en.getTime()) || en <= s) {
      out.push('Start/End (End must be after Start)')
    }
  } else {
    // if either is missing, the generic presence checks above will have already added them
  }

  return out
})

// editor is valid when there are no missing/invalid fields
const isEditorValid = computed(() => (missingFields.value.length === 0))

async function saveEditor() {
  if (!isEditorValid.value) {
    alert('Please complete all required fields in the editor. Ensure Start is before End and numeric Hours is provided.')
    return
  }

  const row = editing.value
  const payload = {
    StartTime: fromLocalInput(row._startLocal),
    EndTime: fromLocalInput(row._endLocal),
    State: row.state,
    CallingContent: row.callingContent,
    CallType: row.callType,
    Actions: row.actions,
    NcmHours: (row.ncmHours !== undefined && row.ncmHours !== null) ? Number(row.ncmHours) : null,
    ProcessName: row.processName,
    ProcessEngineer: row.processEngineer
  }
  try {
    await axios.put(`/api/NcmTimes/${row.id}`, payload)
    // reflect changes back into list
    const grp = records.value.find(g => g.rows.some(r => r.id === row.id))
    if (grp) {
      const idx = grp.rows.findIndex(r => r.id === row.id)
      if (idx >= 0) {
        const updated = { ...grp.rows[idx], ...row }
        grp.rows.splice(idx, 1, updated)
      }
    }
    closeEditor()
  } catch (e) {
    console.error('Save failed', e)
    alert('Save failed: ' + (e?.response?.data?.message || e.message))
  }
}

async function performDelete(id) {
  try {
    await axios.delete(`/api/NcmTimes/${id}`)
    // remove row from grouped records
    for (const g of records.value) {
      const idx = g.rows.findIndex(r => r.id === id)
      if (idx >= 0) {
        g.rows.splice(idx, 1)
        if (g.rows.length === 0) {
          records.value = records.value.filter(gr => gr !== g)
        }
        break
      }
    }
  } catch (e) {
    console.error('Delete failed', e)
    alert('Delete failed: ' + (e?.response?.data?.message || e.message))
  }
}

function openDeleteConfirm(id) {
  if (!isAdmin.value) return
  deleteTargetId.value = id
  showDeleteConfirm.value = true
}
function cancelDelete() {
  showDeleteConfirm.value = false
  deleteTargetId.value = null
}
async function confirmDelete() {
  if (!isAdmin.value || !deleteTargetId.value) { cancelDelete(); return }
  await performDelete(deleteTargetId.value)
  cancelDelete()
  if (modalOpen.value) closeEditor()
}

onMounted(() => {
  window.addEventListener('click', closeContextMenu)
})
onUnmounted(() => {
  window.removeEventListener('click', closeContextMenu)
})

function formatNcmHours(ncm, start, end) {
  // ncm might be null/undefined; if present show with 2 decimals
  if (ncm !== undefined && ncm !== null) {
    const num = Number(ncm)
    if (!isNaN(num)) return num.toFixed(2)
  }
  // fallback to computing from start/end timestamps if available
  if (start && end) {
    const s = new Date(start)
    const e = new Date(end)
    if (!isNaN(s.getTime()) && !isNaN(e.getTime()) && e > s) {
      const hours = (e - s) / 3600000
      return (Math.round(hours * 100) / 100).toFixed(2)
    }
  }
  return ''
}
</script>

<style scoped>
.ncm-container { max-width: 96%; width: 75vw;  margin: 1.5rem 0 1.5rem 2rem; padding: 1rem; background: #fff; border-radius: 12px; box-shadow: 0 4px 14px rgba(0,0,0,0.06); }
/* Scrollable table wrapper: prevents the table from exceeding the container and enables scrolling */
.ncm-table-wrap { max-width: 100%; max-height: 60vh; overflow: auto; }
.ncm-table { width: 100%; max-width: 100%; border-collapse: collapse; table-layout: fixed; }
.ncm-table th, .ncm-table td { padding: 8px 10px; border-bottom: 1px solid #eee; text-align: left; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; color: #333; }
.ncm-table th { font-weight: 600; }
.ncm-table td { font-weight: 400; font-size: 0.92rem; }
.ncm-table-wrap thead th {
  position: sticky;
  top: 0;
  background: #FFF4E6; /* match group header palette */
  color: #8a4b1a;
  z-index: 4;
  box-shadow: 0 2px 6px rgba(0,0,0,0.04);
}
.group-block { margin-bottom: 1.25rem; }
.group-subtitle { background: linear-gradient(90deg, #FFF7ED 0%, #FFF4E6 60%); color: #8a4b1a; font-weight: 800; font-size: 1.02rem; letter-spacing: 0.4px; padding: 10px 14px; border-left: 4px solid #EC6602; border-radius: 8px; margin-bottom: 10px; box-shadow: 0 2px 8px rgba(236,102,2,0.06); text-transform: none; }
/* Use Times New Roman for top-level titles and group subtitles to match portal typography */
.ncm-container h2 { font-family: 'Times New Roman', Times, serif; font-size: 1.25rem; font-weight: 600; margin: 0 0 8px 0; }
.group-subtitle { font-family: 'Times New Roman', Times, serif; font-weight: 700; }

/* column sizing helpers */
.ncm-table th.col-serial, .ncm-table td.col-serial { min-width: 90px; width: 9%; }
.ncm-table th.col-engineer, .ncm-table td.col-engineer { min-width: 140px; width: 12%; }
.ncm-table th.col-process, .ncm-table td.col-process { min-width: 80px; width: 6%; }
.ncm-table th.col-hours, .ncm-table td.col-hours { min-width: 80px; width: 7%; }
.ncm-table th.col-start, .ncm-table td.col-start { min-width: 160px; width: 15%; }
.ncm-table th.col-end, .ncm-table td.col-end { min-width: 160px; width: 15%; }
.ncm-table th.col-state, .ncm-table td.col-state { min-width: 100px; width: 6%; }
.ncm-table th.col-calltype, .ncm-table td.col-calltype { min-width: 110px; width: 8%; }
.ncm-table th.col-content, .ncm-table td.col-content { min-width: 280px; width: 24%; }
.ncm-table th.col-actions, .ncm-table td.col-actions { min-width: 240px; width: 16%; }
/* Unified styling for form controls inside table cells so edges are visible and consistent */
.ncm-table td input,
.ncm-table td select,
.ncm-table td textarea {
  width: 100%;
  padding: 6px;
  border: 1px solid #ddd;
  border-radius: 6px;
  background: #fff;
  box-sizing: border-box;
  -webkit-appearance: none;
  appearance: none;
}
.ncm-table td textarea { min-height: 56px; resize: vertical; }
/* add a visible down-arrow cue for selects to remind users to click */
.ncm-table td select {
  cursor: pointer;
  /* small chevron SVG (grey) as background image, percent-encoded */
  background-image: url("data:image/svg+xml;utf8,%3Csvg%20xmlns='http://www.w3.org/2000/svg'%20viewBox='0%200%2010%206'%3E%3Cpath%20fill='none'%20stroke='%23888'%20stroke-width='1.5'%20stroke-linecap='round'%20stroke-linejoin='round'%20d='M1%201l4%204%204-4'/%3E%3C/svg%3E");
  background-repeat: no-repeat;
  background-position: right 10px center;
  background-size: 12px 12px;
  padding-right: 36px; /* leave room for arrow */
}
/* allow long content to wrap instead of forcing single-line truncation */
.ncm-table td.col-content {
  white-space: normal;
  word-break: break-word;
}
.ncm-table td.col-actions {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.empty { margin-top: 1rem; color: #666 }
.loading { color: #82451F }
button { background: #EC6602; color: #fff; border: none; padding: 6px 10px; border-radius: 6px; cursor: pointer }
button:hover { opacity: 0.95 }
.group-header { background: #FFF4E6; font-weight: 700; color: #8a4b1a; }

/* clickable rows */
.clickable { cursor: pointer; }
.clickable:hover { background: #fff8f1; }

/* Modal styles */
.modal-overlay { position: fixed; inset: 0; background: rgba(0,0,0,0.35); display: flex; align-items: center; justify-content: center; z-index: 50; }
.modal { width: min(900px, 92vw); max-height: 86vh; background: #fff; border-radius: 10px; box-shadow: 0 10px 30px rgba(0,0,0,0.2); display: flex; flex-direction: column; overflow: hidden; }
.modal { position: relative; }
.modal-header { position: relative; background: #FFF4E6; padding: 14px 18px; border-bottom: 1px solid #f1dfd1; min-height: 45px; }
.modal-header .modal-title-block { position: absolute; left: 50%; top: 50%; transform: translate(-50%, -50%); display: flex; flex-direction: column; justify-content: center; align-items: center; text-align: center; max-width: calc(100% - 140px); padding: 0 12px; line-height: 1.1; }
.modal-header .serial { font-size: 1rem; color: #6a3a12; }
.modal-header .brief { font-size: 0.9rem; color: #8a4b1a; margin-top: 2px; }
.modal-header .close-btn { position: absolute; right: 10px; top: 50%; transform: translateY(-50%); background: transparent; color: #8a4b1a; font-size: 22px; line-height: 1; padding: 4px 8px; border-radius: 6px; }
.modal-header .close-btn:hover { background: rgba(0,0,0,0.06); }
.modal-body { padding: 14px 16px; overflow: auto; font-weight: 400; }
.form-row { display: flex; flex-direction: column; gap: 6px; margin-bottom: 10px; }
.form-row > label { font-weight: 400; color: #5b3312; }
.form-row > input, .form-row > select, .form-row > textarea { border: 1px solid #ddd; border-radius: 6px; padding: 8px 10px; }
.form-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 10px; }
.modal-footer { display: flex; justify-content: flex-end; align-items: center; gap: 10px; padding: 12px 16px; border-top: 1px solid #eee; }
.modal-footer .right-actions { display: flex; gap: 10px; }
.modal-footer .left-actions { margin-right: auto; }
.modal-footer .secondary { background: #e6e6e6; color: #333; }
.modal-footer .danger { background: #c0372b; color: #fff; }
/* Side delete button container */
.modal-side-delete { position: absolute; left: 10px; top: 50%; transform: translateY(-50%); z-index: 5; }
/* disabled save button appearance */
.modal-footer button[disabled] {
  opacity: 0.5;
  cursor: not-allowed;
  pointer-events: none;
}

/* Warning block shown when required fields are missing or invalid */
.modal-warning-badge { display: inline-flex; align-items: center; gap: 8px; background: #fff6f0; color: #6a3a12; border: 1px solid #f2c5a8; padding: 4px 8px; border-radius: 999px; font-size: 0.88rem; max-width: 62%; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; margin: 6px 12px; }
.modal-warning-badge .badge-icon { font-size: 1rem; line-height: 1; }
.modal-warning-badge .badge-text { display: inline-block; overflow: hidden; text-overflow: ellipsis; }

/* Pager buttons: match WorkHourMaintenance look (light, bordered) */
.pager button { background: transparent; border: 1px solid #E6C9B0; color: #82451F; padding: 6px 10px; border-radius: 6px; cursor: pointer }
.pager button:disabled { opacity: 0.45; cursor: not-allowed }
/* Context menu */
.context-menu { position: fixed; z-index: 60; background: #fff; border: 1px solid #ccc; border-radius: 6px; box-shadow: 0 4px 12px rgba(0,0,0,0.15); padding: 4px 0; min-width: 140px; }
.context-menu ul { list-style: none; margin: 0; padding: 0; }
.context-menu li { padding: 6px 14px; cursor: pointer; font-size: 0.9rem; }
.context-menu li:hover { background: #f6f6f6; }
.context-menu li.danger-item { color: #c0372b; font-weight: 600; }
/* Confirm dialog (reuse look from TimerClock) */
.confirm-overlay { position: fixed; inset: 0; display: flex; align-items: center; justify-content: center; background: rgba(0,0,0,0.35); z-index: 1000; }
.confirm-dialog { background: #fff; border-radius: 8px; padding: 1.2rem; width: 420px; box-shadow: 0 6px 20px rgba(0,0,0,0.2); display: flex; flex-direction: column; gap: 0.8rem; }
.confirm-title { font-weight: 700; color: #e74c3c; }
.confirm-body { color: #333; }
.confirm-buttons { display:flex; justify-content:flex-end; gap:0.5rem; }
.confirm-btn { padding: 0.5rem 0.9rem; border-radius: 6px; border: none; cursor: pointer; }
.confirm-btn.cancel { background: #eee; color: #333; }
.confirm-btn.confirm { background: #e74c3c; color: #fff; }
</style>
