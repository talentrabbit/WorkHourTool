<template>
  <div class="planning-container">
    <h2>Production Planning</h2>
    <div class="search-section">
      <input v-model="serialNo" @keydown.enter="searchProduct" placeholder="Enter SerialNo" class="search-input" />
      <button @click="searchProduct" class="search-btn">Search</button>
    </div>
    <div v-if="product" class="product-info">
      <h3>Product Info</h3>
      <div class="product-grid">
        <div><strong>SerialNo:</strong> {{ product.serialNo }}</div>
        <div><strong>ProjectNo:</strong> {{ product.projectNo }}</div>
        <div><strong>SystemType:</strong> {{ product.systemType }}</div>
        <div class="current-state" style="grid-column: 1 / span 3; text-align: center; margin-top: 0.5vw;">
          <strong>Current State:</strong> {{ productState }}
        </div>
      </div>
      <div class="assign-section">
        <h4>Assign Task</h4>
        <div class="assign-grid">
          <div>
            <label>Worker Name</label>
            <select v-model="task.workerName">
              <option v-for="name in workerNames" :key="name" :value="name">{{ name }}</option>
            </select>
          </div>
          <div>
            <label>Process</label>
            <select v-model="task.process">
              <option v-for="proc in processNames" :key="proc" :value="proc">{{ proc }}</option>
            </select>
          </div>
          <!-- Co-worker occupies left column; spacer forces Start Date to next row -->
          <div>
            <label>Co-worker (optional)</label>
            <select v-model="task.coWorkerName">
              <option value="">-- None --</option>
              <option v-for="name in coWorkerOptions" :key="name" :value="name">{{ name }}</option>
            </select>
          </div>
          <div class="grid-spacer" aria-hidden="true"></div>
          <div>
            <label>Start Date</label>
            <input type="date" v-model="task.startDate" />
          </div>
          <div>
            <label>Start Time</label>
            <input type="time" v-model="task.startTime" />
          </div>
          <div>
            <label>End Date</label>
            <input type="date" v-model="task.endDate" />
          </div>
          <div>
            <label>End Time</label>
            <input type="time" v-model="task.endTime" />
          </div>
        </div>
        <button @click="assignTask" class="assign-btn" :disabled="isAssignDisabled">Assign</button>
        <div v-if="hasConflict" class="error" style="margin-top:8px;">Selected time range conflicts with an existing assignment for {{ conflictFor }}.</div>
        <div v-if="task.workerName" class="existing-assignments modern-table" style="margin-top: 1vw;">
          <h4>Assignments for {{ task.workerName }}</h4>
          <vxe-table :data="existingAssignments" border stripe round class="modern-vxe-table">
            <vxe-column field="serialNo" title="SerialNo" width="120" />
            <vxe-column field="systemType" title="SystemType" width="140" />
            <vxe-column field="processName" title="Process" width="160" />
            <vxe-column field="coWorkerName" title="Co-worker" width="160" />
            <vxe-column field="startTime" title="Start Time" width="180" />
            <vxe-column field="endTime" title="End Time" width="180" />
          </vxe-table>
        </div>
        <!-- New: show co-worker assignments when selected -->
        <div v-if="task.coWorkerName && task.coWorkerName !== task.workerName" class="existing-assignments modern-table" style="margin-top: 1vw;">
          <h4>Assignments for {{ task.coWorkerName }}</h4>
          <vxe-table :data="coWorkerAssignments" border stripe round class="modern-vxe-table">
            <vxe-column field="serialNo" title="SerialNo" width="120" />
            <vxe-column field="systemType" title="SystemType" width="140" />
            <vxe-column field="processName" title="Process" width="160" />
            <vxe-column field="coWorkerName" title="Co-worker" width="160" />
            <vxe-column field="startTime" title="Start Time" width="180" />
            <vxe-column field="endTime" title="End Time" width="180" />
          </vxe-table>
        </div>
      </div>
    </div>
    <div v-if="searchError" class="error">{{ searchError }}</div>

    <div class="all-products-section modern-table">
      <h3>All Systems Production State</h3>
      <div v-if="allLoading" class="loading">Loading...</div>
      <vxe-table v-else :data="allProducts" border stripe round class="modern-vxe-table">
        <vxe-column field="serialNo" title="SerialNo" width="120" />
        <vxe-column field="projectNo" title="ProjectNo" width="120" />
        <vxe-column field="systemType" title="SystemType" width="120" />
        <vxe-column field="productionState" title="Production State" width="150" />
        <vxe-column field="workHourOverall" title="WorkHour Overall" width="150" />
        <vxe-column field="ncmTimeOverall" title="NCM Time Overall" width="150" />
      </vxe-table>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, watch, computed } from 'vue'
import axios from 'axios'
import { VXETable } from 'vxe-table'

const serialNo = ref('')
const product = ref(null)
const productState = ref('')
const searchError = ref('')
const task = ref({
  workerName: '',
  process: '',
  coWorkerName: '',
  startDate: '',
  endDate: '',
  startTime: '',
  endTime: ''
})

const allProducts = ref([])
const allLoading = ref(false)
const workerNames = ref([])
const processNames = ref([])
const existingAssignments = ref([])
const coWorkerAssignments = ref([]) // new: assignments for selected co-worker
const hasConflict = ref(false)
const conflictFor = ref('') // new: who has the conflict

const coWorkerOptions = computed(() => workerNames.value.filter(n => n !== task.value.workerName))

function getTomorrowDateStr() {
  const d = new Date()
  d.setDate(d.getDate() + 1)
  return d.toISOString().split('T')[0]
}

onMounted(async () => {
  fetchAllProducts()
  // Fetch worker and process names
  const [workerRes, processRes] = await Promise.all([
    axios.get('/api/workhours/all-worker-names'),
    axios.get('/api/workhours/all-process-names')
  ])
  workerNames.value = workerRes.data
  processNames.value = processRes.data
  // Set defaults
  const tomorrow = getTomorrowDateStr()
  task.value.startDate = tomorrow
  task.value.endDate = tomorrow
  task.value.startTime = '08:30'
  task.value.endTime = '17:00'
})

watch(() => task.value.workerName, () => {
  if (task.value.workerName) updateWorkerAssignments()
  else existingAssignments.value = []
})
watch(() => task.value.coWorkerName, async () => {
  if (task.value.coWorkerName && task.value.coWorkerName !== task.value.workerName) {
    await updateCoWorkerAssignments()
  } else {
    coWorkerAssignments.value = []
  }
  recomputeConflict()
})
watch(() => task.value.process, () => {
  // process no longer affects fetched assignments; recompute conflict only
  recomputeConflict()
})
// Keep endDate in sync with startDate unless user chose a later endDate
watch(() => task.value.startDate, (newStart, oldStart) => {
  if (!newStart) return
  const end = task.value.endDate
  if (!end || end === oldStart || new Date(end) < new Date(newStart)) {
    task.value.endDate = newStart
  }
})

function currentRange() {
  if (!task.value.startDate || !task.value.startTime || !task.value.endDate || !task.value.endTime) return null
  const startStr = `${task.value.startDate}T${task.value.startTime}:00`
  const endStr = `${task.value.endDate}T${task.value.endTime}:00`
  const start = new Date(startStr)
  const end = new Date(endStr)
  if (isNaN(start.getTime()) || isNaN(end.getTime()) || end <= start) return null
  return { start, end }
}

function recomputeConflict() {
  const r = currentRange()
  if (!r) { hasConflict.value = false; conflictFor.value = ''; return }
  // Check primary worker
  const conflictMain = existingAssignments.value.some(a => {
    const s = new Date(a.startTime)
    const e = new Date(a.endTime)
    return Math.max(r.start.getTime(), s.getTime()) < Math.min(r.end.getTime(), e.getTime())
  })
  if (conflictMain) { hasConflict.value = true; conflictFor.value = task.value.workerName; return }
  // Check co-worker
  const hasCo = !!task.value.coWorkerName && task.value.coWorkerName !== task.value.workerName
  if (hasCo) {
    const conflictCo = coWorkerAssignments.value.some(a => {
      const s = new Date(a.startTime)
      const e = new Date(a.endTime)
      return Math.max(r.start.getTime(), s.getTime()) < Math.min(r.end.getTime(), e.getTime())
    })
    if (conflictCo) { hasConflict.value = true; conflictFor.value = task.value.coWorkerName; return }
  }
  hasConflict.value = false
  conflictFor.value = ''
}

watch(() => [task.value.startDate, task.value.startTime, task.value.endDate, task.value.endTime], recomputeConflict)
watch(existingAssignments, recomputeConflict)
watch(coWorkerAssignments, recomputeConflict)

async function fetchAllProducts() {
  allLoading.value = true
  try {
    const res = await axios.get('/api/workhours/all-product-states')
    if (Array.isArray(res.data)) {
      allProducts.value = res.data.map(p => ({
        serialNo: p.serialNo,
        projectNo: p.projectNo,
        systemType: p.systemType,
        productionState: p.workingProcess,
        workHourOverall: p.workHourOverall,
        ncmTimeOverall: p.ncmTimeOverall
      }))
    } else {
      console.error('Expected an array but got:', res.data)
      allProducts.value = []
    }
  } catch (error) {
    console.error('Error fetching products:', error)
    allProducts.value = []
  } finally {
    allLoading.value = false
  }
}

async function searchProduct() {
  searchError.value = ''
  product.value = null
  productState.value = ''
  try {
    const res = await axios.get(`/api/WorkHours/product-status/${serialNo.value}`)
    product.value = res.data
    productState.value = res.data.WorkHours?.length ? 'In Progress' : 'Not Started'
    if (product.value && Object.keys(product.value).length > 0) {
      console.log('Record fetched from backend.')
    } else {
      console.log('No record found for the entered SerialNo.')
    }
  } catch (err) {
    searchError.value = err.response?.data?.message || 'Product not found.'
    console.log('Error fetching product:', searchError.value)
  }
}

async function updateWorkerAssignments() {
  try {
    const res = await axios.get('/api/WorkHours/worker-assignments', {
      params: { workerName: task.value.workerName }
    })
    if (Array.isArray(res.data)) {
      // hide assignments earlier than 2 days before today
      const threshold = new Date()
      threshold.setDate(threshold.getDate() - 2)
      existingAssignments.value = res.data
        .filter(a => {
          const s = a && a.startTime ? new Date(a.startTime) : null
          return !s || s >= threshold
        })
        .map(a => ({
          serialNo: a.serialNo,
          systemType: a.systemType,
          processName: a.processName,
          coWorkerName: a.coWorkerName || '',
          startTime: a.startTime,
          endTime: a.endTime
        }))
    } else {
      existingAssignments.value = []
    }
  } catch (e) {
    console.error('Error fetching worker assignments', e)
    existingAssignments.value = []
  } finally {
    recomputeConflict()
  }
}

async function updateCoWorkerAssignments() {
  try {
    const res = await axios.get('/api/WorkHours/worker-assignments', {
      params: { workerName: task.value.coWorkerName }
    })
    if (Array.isArray(res.data)) {
      coWorkerAssignments.value = res.data.map(a => ({
        serialNo: a.serialNo,
        systemType: a.systemType,
        processName: a.processName,
        coWorkerName: a.coWorkerName || '',
        startTime: a.startTime,
        endTime: a.endTime
      }))
    } else {
      coWorkerAssignments.value = []
    }
  } catch (e) {
    console.error('Error fetching co-worker assignments', e)
    coWorkerAssignments.value = []
  }
}

function overlapMs(aStart, aEnd, bStart, bEnd) {
  const start = Math.max(aStart.getTime(), bStart.getTime())
  const end = Math.min(aEnd.getTime(), bEnd.getTime())
  return Math.max(0, end - start)
}
function computeHoursMinusLunch(start, end) {
  let totalMs = end.getTime() - start.getTime()
  // iterate each day spanned and subtract any overlap with lunch (12:00-12:30)
  const cur = new Date(start.getFullYear(), start.getMonth(), start.getDate())
  const last = new Date(end.getFullYear(), end.getMonth(), end.getDate())
  while (cur <= last) {
    const lunchStart = new Date(cur.getFullYear(), cur.getMonth(), cur.getDate(), 12, 0, 0)
    const lunchEnd = new Date(cur.getFullYear(), cur.getMonth(), cur.getDate(), 12, 30, 0)
    const dayStart = new Date(Math.max(start.getTime(), new Date(cur.getFullYear(), cur.getMonth(), cur.getDate(), 0, 0, 0).getTime()))
    const dayEnd = new Date(Math.min(end.getTime(), new Date(cur.getFullYear(), cur.getMonth(), cur.getDate(), 23, 59, 59, 999).getTime()))
    if (dayEnd > dayStart) {
      totalMs -= overlapMs(dayStart, dayEnd, lunchStart, lunchEnd)
    }
    cur.setDate(cur.getDate() + 1)
  }
  return Math.max(0, totalMs / 36e5)
}

async function assignTask() {
  if (!product.value) return
  const startStr = `${task.value.startDate}T${task.value.startTime}:00`
  const endStr = `${task.value.endDate}T${task.value.endTime}:00`
  const startDt = new Date(startStr)
  const endDt = new Date(endStr)
  if (isNaN(startDt.getTime()) || isNaN(endDt.getTime()) || endDt <= startDt) {
    alert('Please ensure start time is before end time.')
    return
  }
  if (hasConflict.value) {
    alert(`Selected time range conflicts with existing assignment for ${conflictFor.value}.`)
    return
  }
  const hours = computeHoursMinusLunch(startDt, endDt)
  // Primary worker
  let primaryPlanned = hours
  try {
    const resPrimary = await axios.post('/api/WorkHours/submit-work-hours', {
      SerialNo: product.value.serialNo,
      WorkerName: task.value.workerName,
      ProcessName: task.value.process,
      Hours: hours,
      StartTime: startStr,
      EndTime: endStr
    })
    // Prefer server-returned planned hours when present (flattened response expected)
    primaryPlanned = Number(resPrimary?.data?.plannedHours ?? resPrimary?.data?.PlannedHours ?? hours)
  } catch (e) {
    console.error('Failed to submit primary assignment', e)
  }

  // Optional co-worker
  let coPlanned = null
  if (task.value.coWorkerName && task.value.coWorkerName !== task.value.workerName) {
    try {
      const resCo = await axios.post('/api/WorkHours/submit-work-hours', {
        SerialNo: product.value.serialNo,
        WorkerName: task.value.coWorkerName,
        ProcessName: task.value.process,
        Hours: hours,
        StartTime: startStr,
        EndTime: endStr
      })
      coPlanned = Number(resCo?.data?.plannedHours ?? resCo?.data?.PlannedHours ?? hours)
    } catch (e) {
      console.error('Failed to submit co-worker assignment', e)
    }
  }

  // Build success message including planned hours for both workers and include start date
  let msg = `Task assigned on ${task.value.startDate}. Planned hours - ${task.value.workerName}: ${primaryPlanned.toFixed(2)}`
  if (coPlanned !== null) msg += `; ${task.value.coWorkerName}: ${coPlanned.toFixed(2)}`

  alert(msg)
  if (task.value.workerName) await updateWorkerAssignments()
  if (task.value.coWorkerName && task.value.coWorkerName !== task.value.workerName) {
    await updateCoWorkerAssignments()
  }
}

const isAssignDisabled = computed(() => {
  return !product.value ||
    !task.value.workerName ||
    !task.value.process ||
    !task.value.startDate ||
    !task.value.endDate ||
    !task.value.startTime ||
    !task.value.endTime ||
    hasConflict.value
})
</script>

<style scoped>
.planning-container {
  max-width: 900px;
  width: 70vw;
  margin: 4vw auto;
  padding: 2.5vw;
  background: linear-gradient(180deg, #FFF7EF 0%, #FFFFFF 100%);
  border-radius: 18px;
  box-shadow: 0 6px 28px rgba(236,102,2,0.12);
}
.search-section {
  display: flex;
  gap: 2vw;
  margin-bottom: 2vw;
}
.search-input {
  flex: 1;
  padding: 0.7vw;
  border-radius: 10px;
  border: 1px solid #f2c7a6;
  background: #fff;
  font-size: 1.1em;
  box-shadow: inset 0 1px 2px rgba(0,0,0,0.04);
}
.search-btn {
  background: linear-gradient(90deg, #EC6602 0%, #FF9D4D 100%);
  color: #fff;
  border: none;
  border-radius: 10px;
  padding: 0.7vw 2vw;
  cursor: pointer;
  font-size: 1.1em;
  transition: background 0.2s, transform 0.1s;
  box-shadow: 0 6px 16px rgba(236,102,2,0.25);
}
.search-btn:hover { background: linear-gradient(90deg, #D45500 0%, #EC6602 100%); transform: translateY(-1px); }
.product-info {
  margin-top: 2vw;
  background: #fff;
  padding: 1.5vw;
  border-radius: 14px;
  box-shadow: 0 3px 14px rgba(236,102,2,0.12);
}
.product-grid { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 0.7vw 2vw; align-items: center; margin-bottom: 1vw; }
.current-state { font-size: 1.1em; color: #EC6602; font-weight: 700; }
.assign-section { margin-top: 2vw; background: #FFF6EE; border: 1px solid #f2c7a6; border-radius: 12px; padding: 1.5vw 1vw 1vw 1vw; box-shadow: 0 2px 10px rgba(236,102,2,0.08); }
.assign-section h4 { margin-bottom: 1vw; font-size: 1.1em; color: #EC6602; }
.assign-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 1vw 2vw; align-items: end; margin-bottom: 1vw; }
.assign-grid label { display: block; margin-bottom: 0.3vw; font-weight: 600; color: #8a4b22; }
.assign-grid input, .assign-grid select { width: 100%; padding: 0.5vw; border-radius: 8px; border: 1px solid #f2c7a6; font-size: 1em; background: #fff; box-shadow: inset 0 1px 2px rgba(0,0,0,0.04); }
/* New spacer to keep grid alignment while moving Start Date to the next row */
.assign-grid .grid-spacer { visibility: hidden; }
/* Removed: .full-row and manual width hacks to ensure equal column widths */
.time-row { display: flex; gap: 1vw; }
.assign-btn { margin-top: 1vw; background: linear-gradient(90deg, #EC6602 0%, #FF9D4D 100%); color: #fff; border: none; border-radius: 10px; padding: 0.7vw 2vw; cursor: pointer; font-size: 1.1em; transition: background 0.2s, transform 0.1s; box-shadow: 0 6px 16px rgba(236,102,2,0.25); }
.assign-btn:disabled { opacity: 0.55; cursor: not-allowed; box-shadow: none; filter: saturate(70%); }
.assign-btn:hover { background: linear-gradient(90deg, #D45500 0%, #EC6602 100%); transform: translateY(-1px); }

.all-products-section { margin-top: 3vw; background: #fff; border-radius: 14px; box-shadow: 0 3px 14px rgba(236,102,2,0.12); padding: 1.5vw 1vw 2vw 1vw; border: 1px solid #f2c7a6; }
.modern-vxe-table { border-radius: 12px; overflow: hidden; font-size: 1.05em; background: #fff; }
.vxe-table--border .vxe-header--row th { background: #FFE6D3; color: #A64E00; font-weight: 700; }
.vxe-table--border .vxe-body--row { background: #fff; }
.error { color: #dc2626; margin-top: 1vw; }
.existing-assignments h4 { margin: 1vw 0; font-size: 1.05em; color: #A64E00; }
.assign-grid .full-row { grid-column: 1 / -1; }
.assign-grid .col-1-width { justify-self: start; width: calc((100% - 2vw) / 2); }
.loading { padding: 0.5rem 0.25rem; color: #82451F; font-weight: 600; }
</style>
