<template>
  <div class="maintenance-container">
    <h2>WorkHour Maintenance</h2>

    <section class="collapsible">
      <header @click="toggle('wh')" class="collapsible-header">
        <h3>WorkHours</h3>
        <span>{{ open.wh ? '▾' : '▸' }}</span>
      </header>
      <div v-show="open.wh" class="collapsible-body">
        <div class="actions">
          <button :disabled="!changed.wh.size" @click="saveWorkHours">Save</button>
          <button :disabled="!selected.wh.size" @click="deleteWorkHours">Delete</button>
          <button @click="loadAll">Refresh</button>
        </div>

        <!-- Filters for WorkHours -->
        <div class="filter-header" @click="toggleFilter('wh')">
          <strong>Filters</strong>
          <span class="filter-toggle">{{ filterOpen.wh ? '▾' : '▸' }}</span>
        </div>
        <div v-show="filterOpen.wh" class="filters">
          <input v-model="whFilter.serialNo" placeholder="SerialNo" class="filter-input" />
          <select v-model="whFilter.workerName" class="filter-input">
            <option value="">All Workers</option>
            <option v-for="w in workerNames" :key="w" :value="w">{{ w }}</option>
          </select>
          <select v-model="whFilter.processName" class="filter-input">
            <option value="">All Processes</option>
            <option v-for="p in processNames" :key="p" :value="p">{{ p }}</option>
          </select>
          <select v-model="whFilter.state" class="filter-input">
            <option value="">All States</option>
            <option v-for="s in workHourStates" :key="s" :value="s">{{ s }}</option>
          </select>
          <label class="filter-input">Start From: <input type="date" v-model="whFilter.startFrom" /></label>
          <label class="filter-input">Start To: <input type="date" v-model="whFilter.startTo" /></label>
          <button @click.stop="clearWhFilters">Clear</button>
        </div>

        <vxe-table :data="filteredWorkHours" border stripe round class="modern-vxe-table" @checkbox-change="onCheckChange('wh', $event)" @checkbox-all="onCheckChange('wh', $event)">
          <vxe-column type="checkbox" width="50" />
          <vxe-column field="id" title="ID" width="70" />
          <vxe-column field="serialNo" title="SerialNo" width="90" />
          <vxe-column field="systemType" title="SystemType" width="140" />
          <vxe-column field="state" title="State" width="90" />

          <vxe-column field="workerName" title="WorkerName" width="200">
            <template #default="{ row }">
              <select v-model="row.workerName" class="cell-input" @change="() => markChanged('wh', row.id)">
                <option v-for="w in workerNames" :key="w" :value="w">{{ w }}</option>
              </select>
            </template>
          </vxe-column>

          <vxe-column field="plannedHours" title="PlannedHours" width="120">
            <template #default="{ row }">
              <input type="number" step="0.1" min="0" v-model.number="row.plannedHours" class="cell-input eh-input" @input="() => markChanged('wh', row.id)" />
            </template>
          </vxe-column>

          <vxe-column field="processName" title="ProcessName" width="200">
            <template #default="{ row }">
              <select v-model="row.processName" class="cell-input" @change="() => markChanged('wh', row.id)">
                <option v-for="p in processNames" :key="p" :value="p">{{ p }}</option>
              </select>
            </template>
          </vxe-column>

          <vxe-column field="effectiveHours" title="EffectiveHours" width="120">
            <template #default="{ row }">
              <input type="number" step="0.1" min="0" v-model.number="row.effectiveHours" class="cell-input eh-input" @input="() => markChanged('wh', row.id)" />
            </template>
          </vxe-column>

          <vxe-column field="startTimeActual" title="StartTimeActual" width="220">
            <template #default="{ row }">
              <input type="datetime-local" :value="toLocalInput(row.startTimeActual || row.startTime)" @change="e => onStartChange(row, e.target.value, true)" class="cell-input" />
            </template>
          </vxe-column>

          <vxe-column field="endTimeActual" title="EndTimeActual" width="220">
            <template #default="{ row }">
              <input type="datetime-local" :value="toLocalInput(row.endTimeActual || row.endTime)" @change="e => onEndChange(row, e.target.value, true)" class="cell-input" />
            </template>
          </vxe-column>

          <vxe-column field="startTime" title="Start Time" width="220">
            <template #default="{ row }">
              <input type="datetime-local" :value="toLocalInput(row.startTime)" @change="e => onStartChange(row, e.target.value, false)" class="cell-input" />
            </template>
          </vxe-column>

          <vxe-column field="endTime" title="End Time" width="220">
            <template #default="{ row }">
              <input type="datetime-local" :value="toLocalInput(row.endTime)" @change="e => onEndChange(row, e.target.value, false)" class="cell-input" />
            </template>
          </vxe-column>

          

        </vxe-table>
      </div>
    </section>

  <h2>NCM Time Maintenance</h2>
    <section class="collapsible">
      <header @click="toggle('ncm')" class="collapsible-header">
        <h3>NcmTimes</h3>
        <span>{{ open.ncm ? '▾' : '▸' }}</span>
      </header>
      <div v-show="open.ncm" class="collapsible-body">
        <div class="actions">
          <button :disabled="!changed.ncm.size" @click="saveNcmTimes">Save</button>
          <button :disabled="!selected.ncm.size" @click="deleteNcmTimes">Delete</button>
        </div>

        <!-- Filters for NcmTimes -->
        <div class="filter-header" @click="toggleFilter('ncm')">
          <strong>Filters</strong>
          <span class="filter-toggle">{{ filterOpen.ncm ? '▾' : '▸' }}</span>
        </div>
        <div v-show="filterOpen.ncm" class="filters">
          <input v-model="ncmFilter.serialNo" placeholder="SerialNo" class="filter-input" />
          <select v-model="ncmFilter.processEngineer" class="filter-input">
            <option value="">All Engineers</option>
            <option v-for="e in processEngineerNames" :key="e" :value="e">{{ e }}</option>
          </select>
          <select v-model="ncmFilter.processName" class="filter-input">
            <option value="">All Processes</option>
            <option v-for="p in processNames" :key="p" :value="p">{{ p }}</option>
          </select>
          <label class="filter-input">Start From: <input type="date" v-model="ncmFilter.startFrom" /></label>
          <label class="filter-input">Start To: <input type="date" v-model="ncmFilter.startTo" /></label>
          <button @click.stop="clearNcmFilters">Clear</button>
        </div>

        <vxe-table :data="filteredNcmTimes" border stripe round class="modern-vxe-table" @checkbox-change="onCheckChange('ncm', $event)" @checkbox-all="onCheckChange('ncm', $event)">
          <vxe-column type="checkbox" width="50" />
          <vxe-column field="id" title="ID" width="70" />
          <vxe-column field="serialNo" title="SerialNo" width="90" />
          <vxe-column field="systemType" title="SystemType" width="140" />

          <vxe-column field="processEngineer" title="ProcessEngineer" width="200">
            <template #default="{ row }">
              <select v-model="row.processEngineer" class="cell-input" @change="() => markChanged('ncm', row.id)">
                <option v-for="e in processEngineerNames" :key="e" :value="e">{{ e }}</option>
              </select>
            </template>
          </vxe-column>

          <vxe-column field="processName" title="ProcessName" width="200">
            <template #default="{ row }">
              <select v-model="row.processName" class="cell-input" @change="() => markChanged('ncm', row.id)">
                <option v-for="p in processNames" :key="p" :value="p">{{ p }}</option>
              </select>
            </template>
          </vxe-column>

          <vxe-column field="startTime" title="Start Time" width="190">
            <template #default="{ row }">
              <div>{{ formatDateTime(row.startTime) }}</div>
            </template>
          </vxe-column>

          <vxe-column field="endTime" title="End Time" width="190">
            <template #default="{ row }">
              <div>{{ formatDateTime(row.endTime) }}</div>
            </template>
          </vxe-column>

          <vxe-column field="ncmHour" title="NcmHour" width="140" />
          <vxe-column field="ncmAction" title="NcmAction" width="200" />

        </vxe-table>
      </div>
    </section>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted, computed } from 'vue'
import axios from 'axios'

const open = ref({ wh: true, ncm: false })
const workHours = ref([])
const ncmTimes = ref([])

const workerNames = ref([])
const processNames = ref([])
const processEngineerNames = ref([])

// track selected ids and changed rows
const selected = reactive({ wh: new Set(), ncm: new Set() })
const changed = reactive({ wh: new Set(), ncm: new Set() })

// filters
const whFilter = reactive({ serialNo: '', workerName: '', processName: '', state: '', startFrom: '', startTo: '' })
const ncmFilter = reactive({ serialNo: '', processEngineer: '', processName: '', startFrom: '', startTo: '' })
// filter panel open flags
const filterOpen = ref({ wh: true, ncm: true })

function toggleFilter(kind){
  filterOpen.value[kind] = !filterOpen.value[kind]
}

function toggle(kind){ open.value[kind] = !open.value[kind] }
function onCheckChange(kind, { records }){
  const set = selected[kind]
  set.clear()
  for (const r of records) set.add(r.id)
}
function markChanged(kind, id){ changed[kind].add(id) }

function toLocalInput(dt){
  if (!dt) return ''
  const d = new Date(dt)
  const pad = n => String(n).padStart(2, '0')
  const yyyy = d.getFullYear()
  const MM = pad(d.getMonth()+1)
  const dd = pad(d.getDate())
  const hh = pad(d.getHours())
  const mm = pad(d.getMinutes())
  return `${yyyy}-${MM}-${dd}T${hh}:${mm}`
}

function formatDateTime(dt){
  if (!dt) return ''
  try { return new Date(dt).toLocaleString() } catch { return String(dt) }
}

function onStartChange(row, value, actual=false){
  const iso = new Date(value)
  if (!isNaN(iso)){
    // store in startTime or startTimeActual depending on actual flag
    if (actual) row.startTimeActual = iso.toISOString()
    else row.startTime = iso.toISOString()
    markChanged('wh', row.id)
  }
}

function onEndChange(row, value, actual=false){
  const iso = new Date(value)
  if (!isNaN(iso)){
    if (actual) row.endTimeActual = iso.toISOString()
    else row.endTime = iso.toISOString()
    markChanged('wh', row.id)
  }
}

function clearWhFilters(){
  whFilter.serialNo = ''
  whFilter.workerName = ''
  whFilter.processName = ''
  whFilter.state = ''
  whFilter.startFrom = ''
  whFilter.startTo = ''
}
function clearNcmFilters(){
  ncmFilter.serialNo = ''
  ncmFilter.processEngineer = ''
  ncmFilter.processName = ''
  ncmFilter.startFrom = ''
  ncmFilter.startTo = ''
}

const workHourStates = computed(() => {
  const s = new Set()
  for (const w of workHours.value) if (w && w.state) s.add(w.state)
  return Array.from(s)
})

const filteredWorkHours = computed(() => {
  const from = whFilter.startFrom ? new Date(whFilter.startFrom) : null
  const to = whFilter.startTo ? new Date(whFilter.startTo) : null
  return workHours.value.filter(r => {
    if (whFilter.serialNo && !(r.serialNo || '').toLowerCase().includes(whFilter.serialNo.toLowerCase())) return false
    if (whFilter.workerName && r.workerName !== whFilter.workerName) return false
    if (whFilter.processName && r.processName !== whFilter.processName) return false
    if (whFilter.state && r.state !== whFilter.state) return false
    if (from){ const st = r.startTime ? new Date(r.startTime) : null; if (!st || st < from) return false }
    if (to){ const st = r.startTime ? new Date(r.startTime) : null; if (!st || st > new Date(to.getFullYear(), to.getMonth(), to.getDate(),23,59,59,999)) return false }
    return true
  })
})

const filteredNcmTimes = computed(() => {
  const from = ncmFilter.startFrom ? new Date(ncmFilter.startFrom) : null
  const to = ncmFilter.startTo ? new Date(ncmFilter.startTo) : null
  return ncmTimes.value.filter(r => {
    if (ncmFilter.serialNo && !(r.serialNo || '').toLowerCase().includes(ncmFilter.serialNo.toLowerCase())) return false
    if (ncmFilter.processEngineer && r.processEngineer !== ncmFilter.processEngineer) return false
    if (ncmFilter.processName && r.processName !== ncmFilter.processName) return false
    if (from){ const st = r.startTime ? new Date(r.startTime) : null; if (!st || st < from) return false }
    if (to){ const st = r.startTime ? new Date(r.startTime) : null; if (!st || st > new Date(to.getFullYear(), to.getMonth(), to.getDate(),23,59,59,999)) return false }
    return true
  })
})

async function loadAll(){
  try{
    const [whRes, ncmRes, workersRes, processesRes, peRes] = await Promise.all([
      axios.get('/api/WorkHours/all-workhours'),
      axios.get('/api/WorkHours/all-ncmtimes'),
      axios.get('/api/WorkHours/all-worker-names'),
      axios.get('/api/WorkHours/all-process-names'),
      axios.get('/api/WorkHours/all-process-engineer-names')
    ])
    workHours.value = whRes.data || []
    ncmTimes.value = ncmRes.data || []
    workerNames.value = workersRes.data || []
    processNames.value = processesRes.data || []
    processEngineerNames.value = peRes.data || []

    // clear selections/changed
    selected.wh.clear(); selected.ncm.clear(); changed.wh.clear(); changed.ncm.clear()
  }catch(e){
    console.error('loadAll failed', e)
  }
}

async function saveWorkHours(){
  if (!changed.wh.size) return
  const toSave = workHours.value.filter(r => changed.wh.has(r.id))
  try{
    await Promise.all(toSave.map(r => axios.put(`/api/WorkHours/${r.id}`, r)))
    changed.wh.clear()
    await loadAll()
  }catch(e){
    console.error('saveWorkHours failed', e)
  }
}

async function deleteWorkHours(){
  if (!selected.wh.size) return
  const ids = Array.from(selected.wh)
  try{
    await Promise.all(ids.map(id => axios.delete(`/api/WorkHours/${id}`)))
    selected.wh.clear()
    await loadAll()
  }catch(e){
    console.error('deleteWorkHours failed', e)
  }
}

async function saveNcmTimes(){
  if (!changed.ncm.size) return
  const toSave = ncmTimes.value.filter(r => changed.ncm.has(r.id))
  try{
    await Promise.all(toSave.map(r => axios.put(`/api/NcmTimes/${r.id}`, r)))
    changed.ncm.clear()
    await loadAll()
  }catch(e){
    console.error('saveNcmTimes failed', e)
  }
}

async function deleteNcmTimes(){
  if (!selected.ncm.size) return
  const ids = Array.from(selected.ncm)
  try{
    await Promise.all(ids.map(id => axios.delete(`/api/NcmTimes/${id}`)))
    selected.ncm.clear()
    await loadAll()
  }catch(e){
    console.error('deleteNcmTimes failed', e)
  }
}

onMounted(loadAll)
</script>

<style scoped>
.maintenance-container{ padding: 12px }
.collapsible{ margin-bottom: 16px; border: 1px solid #ddd; border-radius: 6px }
.collapsible-header{ display:flex; justify-content:space-between; padding:8px; background:#f7f7f7; cursor:pointer }
.collapsible-body{ padding:12px }
.actions{ margin-bottom:8px }
.cell-input{ width:100% }
.eh-input{ width:80px }
.modern-vxe-table{ font-size:13px }
.filters{ display:flex; gap:8px; align-items:center; margin-bottom:8px; flex-wrap:wrap }
.filter-input{ padding:6px 8px; border-radius:6px; border:1px solid #ddd }
.filter-header{ display:flex; align-items:center; gap:8px; cursor:pointer; margin-bottom:6px }
.filter-toggle{ color:#666; font-size:13px }
</style>
