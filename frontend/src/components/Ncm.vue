<template>
  <div class="ncm-container">
    <h2>NCM Time Maintenance</h2>
    <p>{{ showAll ? 'All NCM listed' : 'Showing NCM records assigned to you (Process Engineer).' }}</p>
    <div v-if="loading" class="loading">Loading...</div>
    <div v-else>
      <table class="ncm-table">
        <thead>
          <tr>
            <th>SerialNo</th>
            <th>SystemType</th>
            <th v-if="showAll">ProcessEngineer</th>
            <th>ProcessName</th>
            <th>NCM Hours</th>
            <th>Start Time</th>
            <th>End Time</th>
            <th>State</th>
            <th>NCM Action</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <!-- grouped by systemType -->
          <template v-for="group in records" :key="group.systemType">
            <tr class="group-header"><td :colspan="showAll ? 10 : 9">{{ group.systemType || 'Unknown' }}</td></tr>
            <tr v-for="row in group.rows" :key="row.id">
              <td>{{ row.serialNo }}</td>
              <td>{{ row.systemType }}</td>
              <td v-if="showAll">{{ row.processEngineer }}</td>
              <td>{{ row.processName }}</td>
              <td>
                <input type="number" v-model.number="row.ncmHours" step="0.001" placeholder="" />
              </td>
              <td>
                <input type="datetime-local" v-model="row._startLocal" />
              </td>
              <td>
                <input type="datetime-local" v-model="row._endLocal" />
              </td>
              <td>
                <input v-model="row.state" />
              </td>
              <td>
                <input v-model="row.ncmAction" />
              </td>
              <td>
                <button @click="updateRow(row)">Save</button>
              </td>
            </tr>
          </template>
        </tbody>
      </table>
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
        ncmHours: (r.NcmHours !== undefined ? r.NcmHours : (r.ncmHours !== undefined ? r.ncmHours : null)),
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
.ncm-container { max-width: 1100px; margin: 1.5rem auto; padding: 1rem; background: #fff; border-radius: 12px; box-shadow: 0 4px 14px rgba(0,0,0,0.06); }
.ncm-table { width: 100%; border-collapse: collapse; }
.ncm-table th, .ncm-table td { padding: 8px 10px; border-bottom: 1px solid #eee; text-align: left; }
.ncm-table input { width: 100%; padding: 6px; border: 1px solid #ddd; border-radius: 6px; }
.empty { margin-top: 1rem; color: #666 }
.loading { color: #82451F }
button { background: #EC6602; color: #fff; border: none; padding: 6px 10px; border-radius: 6px; cursor: pointer }
button:hover { opacity: 0.95 }
.group-header { background: #FFF4E6; font-weight: 700; color: #8a4b1a; }
</style>
