<template>
  <div class="planning-container">
    <h2>Production Planning</h2>
    <div class="search-section">
      <input v-model="serialNo" @keydown.enter="searchProduct" placeholder="Enter SerialNo" class="search-input" />
      <button @click="searchProduct" class="search-btn">Search</button>
    </div>
    <div v-if="product&&!showNonProduct" class="product-info">
      <h3>Product Info</h3>
      <div class="product-grid">
        <div><strong>SerialNo:</strong> {{ product.serialNo }}</div>
        <div><strong>ProjectNo:</strong> {{ product.projectNo }}</div>
        <div><strong>SystemType:</strong> {{ product.systemType }}</div>
        <div class="current-state" style="grid-column: 1 / span 3; text-align: center; margin-top: 0.5vw;">
          <strong>Current State:</strong> {{ productState }}
        </div>
      </div>
      <div class="assign-section" v-if="!showNonProduct">
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

    <div class="all-products-section modern-table" v-if="!showNonProduct">
      <div class="all-products-header">
        <h3>All Systems Production State</h3>
        <button class="add-nonproduct-btn switch-btn" @click="openNonProductPanel" aria-label="Open Non-Product Task panel">
          <span class="switch-label">Add Non-Product Task</span>
          <span class="forward-arrow" aria-hidden="true">→</span>
        </button>
      </div>
      <div v-if="allLoading" class="loading">Loading...</div>
      <div v-else>
        <div v-for="group in groupedProducts" :key="group.systemType" class="system-group">
          <div class="group-title">{{ group.systemType || 'Unknown' }}</div>
          <vxe-table :data="group.rows" border stripe round class="modern-vxe-table">
            <vxe-column field="serialNo" title="SerialNo" width="120">
              <template #default="{ row }">
                <span class="serial-link" @click="openProductFromSerial(row.serialNo)">{{ row.serialNo }}</span>
              </template>
            </vxe-column>
            <vxe-column field="projectNo" title="ProjectNo" width="120" />
            <vxe-column field="systemType" title="SystemType" width="120" />
            <vxe-column field="productionState" title="Production State" width="150" />
            <vxe-column field="workHourOverall" title="WorkHour Overall" width="150" />
            <vxe-column field="ncmTimeOverall" title="NCM Time Overall" width="150" />
          </vxe-table>
        </div>
      </div>
    </div>

    <!-- Non-Product Task Panel -->
    <div class="non-product-panel" v-if="showNonProduct">
      <div class="non-product-header">
        <button class="back-btn switch-btn" @click="closeNonProductPanel" aria-label="Back to Products">
          <span class="back-arrow" aria-hidden="true">←</span>
          <span class="switch-label">Back to Products</span>
        </button>
        <h3>Non-Product Task</h3>
      </div>
      <div class="assign-grid non-product-grid">
        <div>
          <label>Worker Name</label>
          <select v-model="nonProductTask.workerName">
            <option value="">-- Select --</option>
            <option v-for="name in workerNames" :key="name" :value="name">{{ name }}</option>
          </select>
        </div>
        <div>
          <label>Work Description</label>
          <input type="text" v-model="nonProductTask.process" placeholder="Describe task or process" />
        </div>
        <div>
          <label>Co-worker (optional)</label>
          <select v-model="nonProductTask.coWorkerName">
            <option value="">-- None --</option>
            <option v-for="name in coWorkerOptionsNon" :key="name" :value="name">{{ name }}</option>
          </select>
        </div>

        <div class="grid-spacer" aria-hidden="true"></div>
        <div>
          <label>Start Date</label>
          <input type="date" v-model="nonProductTask.startDate" />
        </div>
        <div>
          <label>Start Time</label>
          <input type="time" v-model="nonProductTask.startTime" />
        </div>
        <div>
          <label>End Date</label>
          <input type="date" v-model="nonProductTask.endDate" />
        </div>
        <div>
          <label>End Time</label>
          <input type="time" v-model="nonProductTask.endTime" />
        </div>
      </div>
      <div style="margin-top:12px;">
        <button @click="assignNonProductTask" class="assign-btn" :disabled="isNonProductAssignDisabled">Assign</button>
        <button @click="closeNonProductPanel" class="cancel-btn" style="margin-left:8px;">Cancel</button>
      </div>

      <div v-if="nonHasConflict" class="error" style="margin-top:8px;">Selected time range conflicts with an existing assignment for {{ nonConflictFor }}.</div>

      <div v-if="nonProductTask.workerName" class="existing-assignments modern-table" style="margin-top: 1vw;">
        <h4>Assignments for {{ nonProductTask.workerName }}</h4>
        <vxe-table :data="nonProductExistingAssignments" border stripe round class="modern-vxe-table">
          <vxe-column field="serialNo" title="SerialNo" width="120" />
          <vxe-column field="systemType" title="SystemType" width="140" />
          <vxe-column field="processName" title="Process" width="160" />
          <vxe-column field="coWorkerName" title="Co-worker" width="160" />
          <vxe-column field="startTime" title="Start Time" width="180" />
          <vxe-column field="endTime" title="End Time" width="180" />
        </vxe-table>
      </div>

      <div v-if="nonProductTask.coWorkerName && nonProductTask.coWorkerName !== nonProductTask.workerName" class="existing-assignments modern-table" style="margin-top: 1vw;">
        <h4>Assignments for {{ nonProductTask.coWorkerName }}</h4>
        <vxe-table :data="nonCoWorkerAssignments" border stripe round class="modern-vxe-table">
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
</template>

<script setup>
import { ref, onMounted, watch, computed, nextTick } from 'vue'
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
const nonProductExistingAssignments = ref([]) // assignments for non-product task worker
const nonCoWorkerAssignments = ref([]) // assignments for non-product task co-worker
const hasConflict = ref(false)
const nonHasConflict = ref(false)
const conflictFor = ref('') // new: who has the conflict
const nonConflictFor = ref("") // who has conflict for non-product task

const showNonProduct = ref(false)
const nonProductTask = ref({
  workerName: '',
  process: '',
  coWorkerName: '',
  startDate: '',
  endDate: '',
  startTime: '',
  endTime: ''
})

const coWorkerOptions = computed(() => workerNames.value.filter(n => n !== task.value.workerName))
const coWorkerOptionsNon = computed(() => workerNames.value.filter(n => n !== nonProductTask.value.workerName))

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
  nonProductTask.value.startDate = tomorrow
  nonProductTask.value.endDate = tomorrow
  nonProductTask.value.startTime = '08:30'
  nonProductTask.value.endTime = '17:00'
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

function currentRangeNon() {
  if (!nonProductTask.value.startDate || !nonProductTask.value.startTime || !nonProductTask.value.endDate || !nonProductTask.value.endTime) return null
  const startStr = `${nonProductTask.value.startDate}T${nonProductTask.value.startTime}:00`
  const endStr = `${nonProductTask.value.endDate}T${nonProductTask.value.endTime}:00`
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

function recomputeNonProductConflict() {
  const r = currentRangeNon()
  if (!r) { nonHasConflict.value = false; nonConflictFor.value = ''; return }

  // Check primary worker
  if (nonProductExistingAssignments.value && nonProductExistingAssignments.value.length) {
    const conflictMain = nonProductExistingAssignments.value.some(a => {
      const s = new Date(a.startTime)
      const e = new Date(a.endTime)
      return Math.max(r.start.getTime(), s.getTime()) < Math.min(r.end.getTime(), e.getTime())
    })
    if (conflictMain) { nonHasConflict.value = true; nonConflictFor.value = nonProductTask.value.workerName; return }
  }

  // Check co-worker
  if (nonCoWorkerAssignments.value && nonCoWorkerAssignments.value.length && nonProductTask.value.coWorkerName && nonProductTask.value.coWorkerName !== nonProductTask.value.workerName) {
    const conflictCo = nonCoWorkerAssignments.value.some(a => {
      const s = new Date(a.startTime)
      const e = new Date(a.endTime)
      return Math.max(r.start.getTime(), s.getTime()) < Math.min(r.end.getTime(), e.getTime())
    })
    if (conflictCo) { nonHasConflict.value = true; nonConflictFor.value = nonProductTask.value.coWorkerName; return }
  }

  nonHasConflict.value = false
  nonConflictFor.value = ''
}

watch(() => [task.value.startDate, task.value.startTime, task.value.endDate, task.value.endTime], recomputeConflict)
watch(existingAssignments, recomputeConflict)
watch(coWorkerAssignments, recomputeConflict)
watch(() => nonProductTask.value.workerName, () => {
  if (nonProductTask.value.workerName) updateNonProductWorkerAssignments()
  else nonProductExistingAssignments.value = []
  recomputeNonProductConflict()
})
watch(() => nonProductTask.value.coWorkerName, async () => {
  if (nonProductTask.value.coWorkerName && nonProductTask.value.coWorkerName !== nonProductTask.value.workerName) {
    await updateNonProductCoWorkerAssignments()
  } else {
    nonCoWorkerAssignments.value = []
  }
  recomputeNonProductConflict()
})
watch(() => [nonProductTask.value.startDate, nonProductTask.value.startTime, nonProductTask.value.endDate, nonProductTask.value.endTime], recomputeNonProductConflict)

async function fetchAllProducts() {
  allLoading.value = true
  try {
    const res = await axios.get('/api/workhours/all-product-states')
    if (Array.isArray(res.data)) {
      // exclude the placeholder non-product record (SerialNo '999999') so it doesn't appear in All Products
      const items = res.data.filter(p => {
        const sn = (p.serialNo ?? p.SerialNo ?? '').toString()
        return sn !== '999999'
      })
      allProducts.value = items.map(p => ({
        serialNo: p.serialNo ?? p.SerialNo,
        projectNo: p.projectNo ?? p.ProjectNo,
        systemType: p.systemType ?? p.SystemType,
        productionState: p.workingProcess ?? p.WorkingProcess,
        workHourOverall: p.workHourOverall ?? p.WorkHourOverall ?? 0,
        ncmTimeOverall: p.ncmTimeOverall ?? p.NcmTimeOverall ?? 0
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

// New helper: open product info when clicking serial in All Products table
async function openProductFromSerial(serial) {
  if (!serial) return
  serialNo.value = serial
  await searchProduct()
  // wait for DOM to update and then scroll Assign Task into view
  await nextTick()
  const el = document.querySelector('.product-info')
  if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' })
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

async function updateNonProductWorkerAssignments() {
  try {
    const res = await axios.get('/api/WorkHours/worker-assignments', { params: { workerName: nonProductTask.value.workerName } })
    if (Array.isArray(res.data)) {
      const threshold = new Date()
      threshold.setDate(threshold.getDate() - 2)
      nonProductExistingAssignments.value = res.data
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
        console.info(`Fetched ${nonProductExistingAssignments.value.length} assignments for non-product worker.`)
    } else {
      console.info('No assignments found for non-product worker.')
      nonProductExistingAssignments.value = []
    }
  } catch (e) {
    console.error('Error fetching non-product worker assignments', e)
    nonProductExistingAssignments.value = []
  }
}

async function updateNonProductCoWorkerAssignments() {
  try {
    const res = await axios.get('/api/WorkHours/worker-assignments', { params: { workerName: nonProductTask.value.coWorkerName } })
    if (Array.isArray(res.data)) {
      nonCoWorkerAssignments.value = res.data.map(a => ({
        serialNo: a.serialNo,
        systemType: a.systemType,
        processName: a.processName,
        coWorkerName: a.coWorkerName || '',
        startTime: a.startTime,
        endTime: a.endTime
      }))
    } else {
      nonCoWorkerAssignments.value = []
    }
  } catch (e) {
    console.error('Error fetching non-product co-worker assignments', e)
    nonCoWorkerAssignments.value = []
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
    primaryPlanned = Number(resPrimary.data?.plannedHours ??  hours)
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
      coPlanned = Number(resCo.data?.plannedHours ?? hours)
    } catch (e) {
      console.error('Failed to submit co-worker assignment', e)
    }
  }

  // Build success message including planned hours for both workers and include start date
  let msg = `Task assigned on ${task.value.startDate}. Planned hours - ${task.value.workerName}: ${primaryPlanned.toFixed(2)}.`
  if (coPlanned !== null)   msg += `; ${task.value.coWorkerName}: ${coPlanned.toFixed(2)}`

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
const isNonProductAssignDisabled = computed(() => {
  return !nonProductTask.value.workerName || !nonProductTask.value.process || !currentRangeNon() || nonHasConflict.value
})
// grouped view by systemType for All Products area
const groupedProducts = computed(() => {
  try {
    const groups = {}
    const items = Array.isArray(allProducts.value) ? allProducts.value : []
    for (const p of items) {
      const key = (p && p.systemType) ? p.systemType : 'Unknown'
      if (!groups[key]) groups[key] = []
      groups[key].push(p)
    }
    const keys = Object.keys(groups).sort()
    const out = []
    for (const k of keys) {
      out.push({ systemType: k, rows: groups[k] })
    }
    return out
  } catch (err) {
    console.error('groupedProducts error', err)
    return []
  }
})

function openNonProductPanel() {
  showNonProduct.value = true
  // hide assign-section is handled by template v-if
  // initialize defaults similar to assign task
  const tomorrow = getTomorrowDateStr()
  nonProductTask.value.startDate = tomorrow
  nonProductTask.value.endDate = tomorrow
  nonProductTask.value.startTime = '08:30'
  nonProductTask.value.endTime = '17:00'
}
function closeNonProductPanel() {
  showNonProduct.value = false
}

async function assignNonProductTask() {
  const r = currentRangeNon()
  if (!r) {
    alert('Please ensure start time is before end time.')
    return
  }
  // compute hours with lunch deduction
  const hours = computeHoursMinusLunch(r.start, r.end)

  try {
    const startStr = `${nonProductTask.value.startDate}T${nonProductTask.value.startTime}:00`
    const endStr = `${nonProductTask.value.endDate}T${nonProductTask.value.endTime}:00`

    // Primary (assigned) worker
    let plannedPrimary = hours
    try {
      const res = await axios.post('/api/WorkHours/submit-work-hours', {
        SerialNo: '999999', // Give 999999 SerialNo as Non-Product Task (DB should contain this placeholder product)
        WorkerName: nonProductTask.value.workerName,
        ProcessName: nonProductTask.value.process,
        Hours: hours,
        StartTime: startStr,
        EndTime: endStr
      })
      plannedPrimary = Number(res.data?.plannedHours ?? hours)
    } catch (errPrimary) {
      console.error('Failed to submit primary non-product assignment', errPrimary)
    }

    // Optional co-worker: submit a separate WorkHour record for the co-worker
    let plannedCo = null
    if (nonProductTask.value.coWorkerName && nonProductTask.value.coWorkerName !== nonProductTask.value.workerName) {
      try {
        const resCo = await axios.post('/api/WorkHours/submit-work-hours', {
          SerialNo: '999999',
          WorkerName: nonProductTask.value.coWorkerName,
          ProcessName: nonProductTask.value.process,
          Hours: hours,
          StartTime: startStr,
          EndTime: endStr
        })
        plannedCo = Number(resCo.data?.plannedHours ?? hours)
      } catch (errCo) {
        console.error('Failed to submit co-worker non-product assignment', errCo)
      }
    }

    // Build message including both planned hours
    let msg = `Non-Product Task assigned on ${nonProductTask.value.startDate}. Planned hours - ${nonProductTask.value.workerName}: ${plannedPrimary.toFixed(2)}.`
    if (plannedCo !== null) msg += `; ${nonProductTask.value.coWorkerName}: ${plannedCo.toFixed(2)}`

    alert(msg)

    // refresh lists and reset form
    //showNonProduct.value = false
    if (nonProductTask.value.workerName) await updateWorkerAssignments()
    if (plannedCo !== null && nonProductTask.value.coWorkerName) {
      // refresh co-worker assignments as well
      const prevCo = nonProductTask.value.coWorkerName
      nonProductTask.value.coWorkerName = ''
      nonProductTask.value.coWorkerName = prevCo
      // optional: call updateCoWorkerAssignments to refresh immediately
      await updateCoWorkerAssignments()
    }

    nonProductTask.value.workerName = ''
    nonProductTask.value.process = ''
    nonProductTask.value.coWorkerName = ''
  } catch (e) {
    console.error('Failed to submit non-product task', e)
    alert('Failed to assign non-product task: ' + (e.response?.data?.message || e.message))
  }
}
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
.all-products-header { position: relative; display:flex; align-items:center; }
.all-products-header h3 { position: absolute; left: 50%; transform: translateX(-50%); margin: 0; }
.all-products-header .add-nonproduct-btn { margin-left: auto }
.add-nonproduct-btn { background:#f3f4f6; border:1px solid #ddd; padding:6px 10px; border-radius:6px; cursor:pointer }
.add-nonproduct-btn .arrow{ margin-left:8px }
/* Prominent switch button shared style */
.switch-btn { display: inline-flex; align-items: center; gap: 0.6rem; background: linear-gradient(90deg,#FFF4EA 0%,#FFF8F2 100%); border: 1px solid #F5D3B0; padding: 0.5rem 0.8rem; border-radius: 10px; cursor: pointer; box-shadow: 0 4px 10px rgba(236,102,2,0.08); }
.switch-btn:hover { transform: translateY(-2px); }
.switch-label { font-weight: 700; color: #6b3b1f; }
.forward-arrow, .back-arrow { display:inline-flex; align-items:center; justify-content:center; background: #FFF3E8; color: #EC6602; font-weight: 800; border-radius: 999px; padding: 0.25rem 0.5rem; font-size: 1.1rem; box-shadow: 0 2px 6px rgba(236,102,2,0.12); }
.back-btn { background:transparent; border:none; font-size:18px; cursor:pointer; padding: 0; }
.back-btn.switch-btn { padding: 0.2rem 0.5rem; }
.non-product-panel { background: #fff; border: 1px solid #eee; padding: 16px; border-radius: 8px; margin-top: 12px }
.non-product-header { position: relative; display:flex; align-items:center; gap:8px; }
.non-product-header h3 { position: absolute; left: 50%; transform: translateX(-50%); margin: 0; }
.modern-vxe-table { border-radius: 12px; overflow: hidden; font-size: 1.05em; background: #fff; }
.vxe-table--border .vxe-header--row th { background: #FFE6D3; color: #A64E00; font-weight: 700; }
.vxe-table--border .vxe-body--row { background: #fff; }
.error { color: #dc2626; margin-top: 1vw; }
.existing-assignments h4 { margin: 1vw 0; font-size: 1.05em; color: #A64E00; }
.assign-grid .full-row { grid-column: 1 / -1; }
.assign-grid .col-1-width { justify-self: start; width: calc((100% - 2vw) / 2); }
.loading { padding: 0.5rem 0.25rem; color: #82451F; font-weight: 600; }
/* Make serial number in All Products table look clickable */
.serial-link { cursor: pointer; color: #EC6602; font-weight: 700; text-decoration: underline; }
.system-group { margin-bottom: 1.25rem }
.group-title { padding: 0.6rem 0.8rem; background: #FFF4E6; color: #8a4b1a; font-weight: 700; border-radius: 8px; margin-bottom: 0.5rem }
</style>
