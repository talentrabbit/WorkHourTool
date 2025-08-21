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
        </div>
        <vxe-table :data="workHours" border stripe round class="modern-vxe-table" @checkbox-change="onCheckChange('wh', $event)" @checkbox-all="onCheckChange('wh', $event)">
          <vxe-column type="checkbox" width="50" />
          <vxe-column field="id" title="ID" width="70" />
          <vxe-column field="serialNo" title="SerialNo" width="140" />
          <vxe-column field="systemType" title="SystemType" width="140" />
          <vxe-column field="workerName" title="WorkerName" width="200">
            <template #default="{ row }">
              <select v-model="row.workerName" class="cell-input" @change="markChanged('wh', row.id)">
                <option v-for="w in workerNames" :key="w" :value="w">{{ w }}</option>
              </select>
            </template>
          </vxe-column>
          <vxe-column field="processName" title="ProcessName" width="200">
            <template #default="{ row }">
              <select v-model="row.processName" class="cell-input" @change="markChanged('wh', row.id)">
                <option v-for="p in processNames" :key="p" :value="p">{{ p }}</option>
              </select>
            </template>
          </vxe-column>
          <vxe-column field="effectiveHours" title="EffectiveHours" width="160">
            <template #default="{ row }">
              <input type="number" step="0.1" min="0" v-model.number="row.effectiveHours" class="cell-input" @input="markChanged('wh', row.id)" />
            </template>
          </vxe-column>
          <vxe-column field="startTime" title="Start Time" width="190" />
          <vxe-column field="endTime" title="End Time" width="190" />
        </vxe-table>
      </div>
    </section>

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
        <vxe-table :data="ncmTimes" border stripe round class="modern-vxe-table" @checkbox-change="onCheckChange('ncm', $event)" @checkbox-all="onCheckChange('ncm', $event)">
          <vxe-column type="checkbox" width="50" />
          <vxe-column field="id" title="ID" width="70" />
          <vxe-column field="serialNo" title="SerialNo" width="140" />
          <vxe-column field="systemType" title="SystemType" width="140" />
          <vxe-column field="processEngineer" title="ProcessEngineer" width="200">
            <template #default="{ row }">
              <select v-model="row.processEngineer" class="cell-input" @change="markChanged('ncm', row.id)">
                <option v-for="e in processEngineerNames" :key="e" :value="e">{{ e }}</option>
              </select>
            </template>
          </vxe-column>
          <vxe-column field="processName" title="ProcessName" width="200">
            <template #default="{ row }">
              <select v-model="row.processName" class="cell-input" @change="markChanged('ncm', row.id)">
                <option v-for="p in processNames" :key="p" :value="p">{{ p }}</option>
              </select>
            </template>
          </vxe-column>
          <vxe-column field="startTime" title="Start Time" width="190" />
          <vxe-column field="endTime" title="End Time" width="190" />
          <vxe-column field="ncmHour" title="NcmHour" width="140" />
        </vxe-table>
      </div>
    </section>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import axios from 'axios'

const open = ref({ wh: true, ncm: false })
const workHours = ref([])
const ncmTimes = ref([])

const workerNames = ref([])
const processNames = ref([])
const processEngineerNames = ref([])

// track selected ids and changed rows (reactive so size updates trigger UI)
const selected = reactive({ wh: new Set(), ncm: new Set() })
const changed = reactive({ wh: new Set(), ncm: new Set() })

function toggle(kind){ open.value[kind] = !open.value[kind] }
function onCheckChange(kind, { records }){
  const set = selected[kind]
  set.clear()
  for (const r of records) set.add(r.id)
}
function markChanged(kind, id){ changed[kind].add(id) }

async function loadAll(){
  const [whRes, ncmRes, workersRes, processesRes, engineersRes] = await Promise.all([
    axios.get('/api/workhours/all-workhours'),
    axios.get('/api/workhours/all-ncmtimes'),
    axios.get('/api/workhours/all-worker-names'),
    axios.get('/api/workhours/all-process-names'),
    axios.get('/api/workhours/all-process-engineer-names')
  ])
  workHours.value = whRes.data
  ncmTimes.value = ncmRes.data
  workerNames.value = workersRes.data
  processNames.value = processesRes.data
  processEngineerNames.value = engineersRes.data
}

async function saveWorkHours(){
  const ids = Array.from(changed.wh)
  for (const id of ids){
    const row = workHours.value.find(r => r.id === id)
    if (!row) continue
    await axios.put(`/api/workhours/workhours/${id}`, {
      workerName: row.workerName,
      processName: row.processName,
      effectiveHours: row.effectiveHours
    })
  }
  changed.wh.clear()
  await loadAll()
}

async function saveNcmTimes(){
  const ids = Array.from(changed.ncm)
  for (const id of ids){
    const row = ncmTimes.value.find(r => r.id === id)
    if (!row) continue
    await axios.put(`/api/workhours/ncmtimes/${id}`, {
      processEngineer: row.processEngineer,
      processName: row.processName
    })
  }
  changed.ncm.clear()
  await loadAll()
}

async function deleteWorkHours(){
  const ids = Array.from(selected.wh)
  if (!ids.length) return
  await axios.post('/api/workhours/workhours/delete-batch', { ids })
  selected.wh.clear()
  await loadAll()
}
async function deleteNcmTimes(){
  const ids = Array.from(selected.ncm)
  if (!ids.length) return
  await axios.post('/api/workhours/ncmtimes/delete-batch', { ids })
  selected.ncm.clear()
  await loadAll()
}

onMounted(loadAll)
</script>

<style scoped>
.maintenance-container{ max-width: 1100px; width: 75vw; margin: 3vw auto; background: #fff; border-radius: 14px; box-shadow: 0 3px 14px rgba(236,102,2,0.12); padding: 1.5vw; border: 1px solid #f2c7a6; }
.collapsible{ margin-bottom: 1.2vw; }
.collapsible-header{ display:flex; justify-content: space-between; align-items:center; cursor:pointer; padding: 0.8rem 1rem; background: #FFF6EE; border: 1px solid #f2c7a6; border-radius: 10px; }
.collapsible-body{ margin-top: 0.8rem; }
.actions{ display:flex; gap: 0.6rem; margin-bottom: 0.8rem; }
.actions button{ background: linear-gradient(90deg, #EC6602 0%, #FF9D4D 100%); color: #fff; border: none; border-radius: 10px; padding: 0.45rem 1rem; cursor: pointer; font-size: 0.95em; box-shadow: 0 6px 16px rgba(236,102,2,0.25); }
.actions button:disabled{ opacity: .6; cursor: not-allowed; box-shadow: none; }
.cell-input{ width: 100%; padding: .35rem .5rem; border: 1px solid #f2c7a6; border-radius: 6px; }
</style>
