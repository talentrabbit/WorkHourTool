<template>
  <div class="ncm-container">
    <h2>NCM Time Maintenance</h2>
    <p>{{ showAll ? 'All NCM listed' : 'Showing NCM records assigned to you (' + username + ').' }}</p>
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
                  <th class="col-action">Action</th>
                  <th class="col-save" aria-hidden="true"></th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="row in group.rows" :key="row.id">
                  <td class="col-serial" :title="row.serialNo">{{ row.serialNo }}</td>
                  <td v-if="showAll" class="col-engineer" :title="row.processEngineer">{{ row.processEngineer }}</td>
                  <td class="col-process" :title="row.processName">{{ row.processName }}</td>
                  <td class="col-hours">
                    <input type="number" v-model.number="row.ncmHours" step="0.001" placeholder="" :title="row.ncmHours" />
                  </td>
                  <td class="col-start">
                    <input type="datetime-local" v-model="row._startLocal" :title="row._startLocal" />
                  </td>
                  <td class="col-end">
                    <input type="datetime-local" v-model="row._endLocal" :title="row._endLocal" />
                  </td>
                  <td class="col-state">
                    <input v-model="row.state" :title="row.state" />
                  </td>
                  <td class="col-action">
                    <input v-model="row.ncmAction" :title="row.ncmAction" />
                  </td>
                  <td class="col-save">
                    <button @click="updateRow(row)">Save</button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </template>
      <div v-if="!records.length" class="empty">No NCM records for your account.</div>
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
        ncmAction: r.ncmAction || '',
        _startLocal: toLocalInput(r.startTime),
        _endLocal: toLocalInput(r.endTime)
      })
    })

    records.value = Object.keys(groupsMap).map(k => ({ systemType: k, rows: groupsMap[k] }))
  } catch (e) {
    console.error('Failed to load NCM records', e)
    records.value = []
  } finally {
    loading.value = false
  }
})

async function updateRow(row) {
  const payload = {
    // allow editing timestamps and state/action
    StartTime: fromLocalInput(row._startLocal),
    EndTime: fromLocalInput(row._endLocal),
    State: row.state,
    NcmAction: row.ncmAction,
    NcmHours: (row.ncmHours !== undefined && row.ncmHours !== null) ? Number(row.ncmHours) : (null)
  }
  try {
    await axios.put(`/api/NcmTimes/${row.id}`, payload)
    alert('Saved')
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
.ncm-table th.col-process, .ncm-table td.col-process { min-width: 220px; width: 28%; }
.ncm-table th.col-hours, .ncm-table td.col-hours { min-width: 80px; width: 8%; }
.ncm-table th.col-start, .ncm-table td.col-start { min-width: 160px; width: 12%; }
.ncm-table th.col-end, .ncm-table td.col-end { min-width: 160px; width: 12%; }
.ncm-table th.col-state, .ncm-table td.col-state { min-width: 120px; width: 10%; }
.ncm-table th.col-action, .ncm-table td.col-action { min-width: 160px; width: 12%; }
.ncm-table th.col-save, .ncm-table td.col-save { min-width: 80px; width: 6%; }
.ncm-table input { width: 100%; padding: 6px; border: 1px solid #ddd; border-radius: 6px; }
.empty { margin-top: 1rem; color: #666 }
.loading { color: #82451F }
button { background: #EC6602; color: #fff; border: none; padding: 6px 10px; border-radius: 6px; cursor: pointer }
button:hover { opacity: 0.95 }
.group-header { background: #FFF4E6; font-weight: 700; color: #8a4b1a; }
</style>
