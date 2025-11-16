<template>
  <div class="ncm-container">
    <h2>NCM Time Maintenance</h2>
    <p>{{ showAll ? 'Administrator login, so all NCM listed' : 'Showing NCM records assigned to you (' + username + ').' }}</p>
    <div v-if="loading" class="loading">Loading...</div>
    <div v-else>
      <!-- render each systemType group as its own titled block with its own table so headers align -->
      <template v-for="group in records" :key="group.systemType">
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
                  <th class="col-content">Content</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="row in group.rows" :key="row.id" class="clickable" @click="openEditor(row)">
                  <td class="col-serial" :title="row.serialNo">{{ row.serialNo }}</td>
                  <td v-if="showAll" class="col-engineer" :title="row.processEngineer">{{ row.processEngineer }}</td>
                  <td class="col-process" :title="row.processName">{{ row.processName }}</td>
                  <td class="col-hours" :title="formatNcmHours(row.ncmHours, row.startTime, row.endTime)">{{ formatNcmHours(row.ncmHours, row.startTime, row.endTime) }}</td>
                  <td class="col-start" :title="row._startLocal">{{ row._startLocal }}</td>
                  <td class="col-end" :title="row._endLocal">{{ row._endLocal }}</td>
                  <td class="col-state" :title="row.state">{{ row.state }}</td>
                  <td class="col-content" :title="row.callingContent">{{ row.callingContent }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </template>
      <div v-if="!records.length" class="empty">No NCM records for your account.</div>
    </div>
    
    <!-- Modal Editor -->
    <div v-if="modalOpen" class="modal-overlay" @click.self="closeEditor">
      <div class="modal">
        <div class="modal-header">
          <div>
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

        <div class="modal-footer">
          <button class="secondary" @click="closeEditor">Cancel</button>
          <button @click="saveEditor">Save</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, inject, computed } from 'vue'
import axios from 'axios'

const username = inject('username')
const userRole = inject('userRole')

const role = computed(() => (userRole && userRole.value) ? userRole.value.toLowerCase() : '')
const showAll = computed(() => role.value === 'administrator' || role.value === 'productionmanager')

const records = ref([])
const loading = ref(false)
// list of process engineer names for dropdown
const processEngineers = ref([])

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

async function saveEditor() {
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
.ncm-container { max-width: 96%; width: 75vw;  margin: 1.5rem auto; padding: 1rem; background: #fff; border-radius: 12px; box-shadow: 0 4px 14px rgba(0,0,0,0.06); }
/* Scrollable table wrapper: prevents the table from exceeding the container and enables scrolling */
.ncm-table-wrap { max-width: 100%; max-height: 60vh; overflow: auto; }
.ncm-table { width: 100%; max-width: 100%; border-collapse: collapse; table-layout: fixed; }
.ncm-table th, .ncm-table td { padding: 8px 10px; border-bottom: 1px solid #eee; text-align: left; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; font-weight: 500; color: #333; }
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

/* column sizing helpers */
.ncm-table th.col-serial, .ncm-table td.col-serial { min-width: 90px; width: 8%; }
.ncm-table th.col-engineer, .ncm-table td.col-engineer { min-width: 140px; width: 14%; }
.ncm-table th.col-process, .ncm-table td.col-process { min-width: 80px; width: 10%; }
.ncm-table th.col-hours, .ncm-table td.col-hours { min-width: 80px; width: 8%; }
.ncm-table th.col-start, .ncm-table td.col-start { min-width: 160px; width: 12%; }
.ncm-table th.col-end, .ncm-table td.col-end { min-width: 160px; width: 12%; }
.ncm-table th.col-state, .ncm-table td.col-state { min-width: 100px; width: 10%; }
.ncm-table th.col-content, .ncm-table td.col-content { min-width: 160px; width: 12%; }
.ncm-table th.col-actions, .ncm-table td.col-actions { min-width: 160px; width: 28%; }
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
.modal-header { display: flex; align-items: center; justify-content: space-between; background: #FFF4E6; padding: 12px 16px; border-bottom: 1px solid #f1dfd1; }
.modal-header .serial { font-size: 1rem; color: #6a3a12; }
.modal-header .brief { font-size: 0.9rem; color: #8a4b1a; margin-top: 2px; }
.close-btn { background: transparent; color: #8a4b1a; font-size: 22px; line-height: 1; padding: 4px 8px; border-radius: 6px; }
.close-btn:hover { background: rgba(0,0,0,0.06); }
.modal-body { padding: 14px 16px; overflow: auto; }
.form-row { display: flex; flex-direction: column; gap: 6px; margin-bottom: 10px; }
.form-row > label { font-weight: 600; color: #5b3312; }
.form-row > input, .form-row > select, .form-row > textarea { border: 1px solid #ddd; border-radius: 6px; padding: 8px 10px; }
.form-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 10px; }
.modal-footer { display: flex; justify-content: flex-end; gap: 10px; padding: 12px 16px; border-top: 1px solid #eee; }
.modal-footer .secondary { background: #e6e6e6; color: #333; }
</style>
