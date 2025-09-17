<script setup>
import { ref, provide, watch, computed, onMounted, onUnmounted, inject } from 'vue'
import axios from 'axios'
import WorkSeat from './WorkSeat.vue'
import TimerClock from './TimerClock.vue'

const activeTab = ref('task')
// Shared state for timer running
const isCountingTimerActive = ref(false)
provide('isCountingTimerActive', isCountingTimerActive)
// Shared state to indicate the worker has submitted their work hour – used to keep some tabs disabled
const isWorkSubmitted = ref(false)
provide('isWorkSubmitted', isWorkSubmitted)

// selected serial from assignments
const selectedSerial = ref('')
const selectedProcess = ref('')
const selectedWorkHourId = ref(null)

const heroImages = [
  '/HeroSection/factory1.jpg',
  '/HeroSection/factory2.jpg',
  '/HeroSection/factory3.jpg'
]
const currentHero = ref(0)
function nextHero() {
  currentHero.value = (currentHero.value + 1) % heroImages.length
}

// Switch hero image every 5 seconds
let heroInterval = null
onMounted(() => {
  heroInterval = setInterval(nextHero, 5000)
})
onUnmounted(() => {
  if (heroInterval) clearInterval(heroInterval)
})

// Worker-mode detection and data
const username = inject('username', ref('Guest'))
const workerNames = ref([])
const isWorker = computed(() => {
  const norm = s => (s || '').toLowerCase().replace(/[^a-z0-9]/g, '')
  const u = norm(username.value)
  return workerNames.value.some(w => u.includes(norm(w)))
})
const matchedWorkerName = computed(() => {
  const norm = s => (s || '').toLowerCase().replace(/[^a-z0-9]/g, '')
  const u = norm(username.value)
  const found = workerNames.value.find(w => u.includes(norm(w)))
  return found || ''
})

const todayAssignments = ref([])
const planningMode = ref('view') // 'view' | 'edit' | 'wait'
const checkedToday = ref(false)

async function fetchOptions(){
  try {
    const workers = await axios.get('/api/workhours/all-worker-names')
    workerNames.value = Array.isArray(workers.data) ? workers.data : []
    console.log("Fetched worker names:", workerNames.value)
  } catch {}
}

function overlaps(aStart, aEnd, bStart, bEnd){
  return Math.max(aStart.getTime(), bStart.getTime()) < Math.min(aEnd.getTime(), bEnd.getTime())
}

async function checkTodayAssignments(){
  checkedToday.value = true
  todayAssignments.value = []
  const worker = matchedWorkerName.value || username.value
  console.log("matchedWorkerName is", matchedWorkerName.value, "username is", username.value)
  if (!worker) { planningMode.value = 'wait'; return }  //If no work assigned, show "Wait for arrangement from Production Manager"
  try {
    const res = await axios.get('/api/WorkHours/worker-assignments', { params: { workerName: worker } })
    const items = Array.isArray(res.data) ? res.data : []
    const startOfDay = new Date(); startOfDay.setHours(0,0,0,0)
    const endOfDay = new Date(); endOfDay.setHours(23,59,59,999)
    const todays = items.filter(a => {
      const s = new Date(a.startTime)
      const e = new Date(a.endTime)
      return overlaps(s, e, startOfDay, endOfDay)
    })
    todayAssignments.value = todays
    if (!todays.length) {
      if (window.confirm('No work arranged! Add new?')) {
        planningMode.value = 'edit'
      } else {
        planningMode.value = 'wait'
      }
    } else {
      planningMode.value = 'view'
    }
  } catch {
    planningMode.value = 'wait'
  }
}

// Notice, this function return 1st record of matching serial/process. If multiple assignments exist for same serial/process, it may not be the intended one.
async function resolveWorkHourId(serial, processName) {
  selectedWorkHourId.value = null
  if (!serial) return
  try {
    const worker = matchedWorkerName.value || username.value || 'Guest'

    // Helper: format a Date (or date-string) as local YYYY-MM-DD HH:mm:ss to match DB format
    const formatLocalDateTime = (dInput) => {
      const d = (dInput instanceof Date) ? dInput : new Date(dInput)
      if (isNaN(d.getTime())) return ''
      const pad = n => String(n).padStart(2, '0')
      return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
    }

    // Default to now (local) in DB-friendly format
    let startDateStr = formatLocalDateTime(new Date())
    const assign = todayAssignments.value.find(a => a.serialNo === serial && (!processName || ((a.processName || a.process || '') || '').toLowerCase().includes((processName || '').toLowerCase())))
    if (assign && assign.startTime) {
      console.info("assign start time is", assign.startTime)
      // Use the assignment's full local datetime (DB format)
      startDateStr = formatLocalDateTime(assign.startTime)
      console.info("startDateStr is", startDateStr)
    }

    const res = await axios.get('/api/WorkHours/find-workhour-id', { params: { workerName: worker, serialNo: serial, startDateTime: startDateStr, processName: processName } })
    if (res && res.data && (res.data.id !== undefined && res.data.id !== null)) {
      selectedWorkHourId.value = res.data.id
    } else {
      selectedWorkHourId.value = null
    }
  } catch (err) {
    // Not found or other error — clear id
    selectedWorkHourId.value = null
  }
}

function onAssignmentClick(params) {
  const row = params?.row || params
  // Ignore clicks on completed assignments (case-insensitive)
  const isCompleted = (row?.state || '').toString().toLowerCase() === 'completed'
  if (isCompleted) return
  if (row?.serialNo) selectedSerial.value = row.serialNo
  selectedProcess.value = row?.processName || row?.process || ''
  // try resolve work hour id for this selection
  void resolveWorkHourId(selectedSerial.value, selectedProcess.value)
}

async function onAssignmentCellClick(params) {
  // vxe-table cell-click provides { row, column, cell, rowIndex, columnIndex }
  const row = params?.row
  const col = params?.column
  // If the clicked column is the SerialNo column, treat it as selection
  const prop = col?.property || col?.field || ''
  const isCompletedCell = (row?.state || '').toString().toLowerCase() === 'completed'
  if (prop === 'serialNo') {
    // ignore clicks when already completed
    if (isCompletedCell) return
    if (row?.serialNo) selectedSerial.value = row.serialNo
    selectedProcess.value = row?.processName || row?.process || ''
    // resolve id for selection and wait for it so TimerClock receives the id on creation
    await resolveWorkHourId(selectedSerial.value, selectedProcess.value)
    if (selectedWorkHourId.value) {
      // switch to counting tab only if we resolved a WorkHourId
      activeTab.value = 'counting'
    } else {
      // no workhour found; keep selection and notify user
      console.warn('No WorkHourId found for selected assignment', selectedSerial.value, selectedProcess.value)
      // optional: alert('No WorkHour record found for this assignment.')
    }
  } else {
    // fallback to row click behavior
    onAssignmentClick(params)
  }
}

// Helper used by the State column button to start work for a row
async function startWorkFromRow(row) {
  if (!row) return
  // Prevent starting work for completed rows (case-insensitive)
  if ((row.state || '').toString().toLowerCase() === 'completed') return
  try {
    selectedSerial.value = row.serialNo
    selectedProcess.value = row.processName || row.process || ''
    console.info("startWorkFromRow selectedSerial is", selectedSerial.value, "selectedProcess is", selectedProcess.value,"selectedWorkHourId is", selectedWorkHourId.value)
    await resolveWorkHourId(selectedSerial.value, selectedProcess.value)
  } catch (e) {
    // ignore
  }
  if (selectedWorkHourId.value) activeTab.value = 'counting'
}

// Provide a row class function to dim completed rows
function rowClassName({ row }) {
  return row && row.state === 'Completed' ? 'row-completed' : ''
}

watch(activeTab, (nv) => {
  // When switching back to Task Arrangement, refresh today's assignments
  if (nv === 'task') {
    // re-check assignments so the table reflects recent submissions/assignments
    void checkTodayAssignments()
  }
})

watch([isWorker, activeTab], async ([w, tab]) => {
  if (w && tab === 'task' && !checkedToday.value) {
    await checkTodayAssignments()
  }
})

onMounted(() => {
  fetchOptions()
})

function handleStartWork(payload) {
  // payload expected: { workHourId, serialNo, process }
  try {
    if (payload && typeof payload === 'object') {
      if (payload.serialNo) selectedSerial.value = payload.serialNo
      if (payload.process) selectedProcess.value = payload.process
      selectedWorkHourId.value = payload.workHourId ?? null
    }
  } catch (e) {
    selectedWorkHourId.value = null
  }
  // switch to counting tab so TimerClock is shown and receives the id
  activeTab.value = 'counting'
}
</script>

<template>
  <div class="nav-layout">
    <div class="nav-header">
      <!-- Removed duplicate logo -->
      <div class="nav-header-spacer"></div>
      <!-- <img src="/title-graph.png" alt="Title Graph" class="title-graph" /> -->
    </div>
    <div class="hero-section">
      <div class="hero-carousel">
        <img :src="heroImages[currentHero]" alt="Hero" class="hero-img" />
      </div>
      <h1>Manufacturing Work Hour Tool</h1>
      <p>Track, analyze, and improve your department's productivity.</p>
    </div>
    <div class="nav-tabs-horizontal">
      <button
        :class="{ active: activeTab === 'task', disabled: (isCountingTimerActive && activeTab === 'counting') || isWorkSubmitted }"
        :disabled="(isCountingTimerActive && activeTab === 'counting') || isWorkSubmitted"
        @click="!((isCountingTimerActive && activeTab === 'counting') || isWorkSubmitted) && (activeTab = 'task')">
        Task Arrangement
      </button>

      <!-- Disabled: users must select a row to enter counting mode -->
      <button :class="{ active: activeTab === 'counting', disabled: activeTab !== 'counting' }" disabled title="Open a work item from Task Arrangement to switch to the Work Hour Counting Tool">Work Hour Counting Tool</button>

      <button
        :class="{ active: activeTab === 'intro', disabled: (isCountingTimerActive && activeTab === 'counting') || isWorkSubmitted }"
        :disabled="(isCountingTimerActive && activeTab === 'counting') || isWorkSubmitted"
        @click="!((isCountingTimerActive && activeTab === 'counting') || isWorkSubmitted) && (activeTab = 'intro')">
        Department Introduction
      </button>
    </div>
    <div class="tab-content">
      <div v-if="activeTab === 'task'">
        <template v-if="isWorker">
          <div v-if="planningMode === 'view'" class="today-assignment">
            <h3>Today's Arrangements for {{ matchedWorkerName || username }}</h3>
            <vxe-table :data="todayAssignments" border stripe round class="modern-vxe-table" @row-click="onAssignmentClick" @cell-click="onAssignmentCellClick" :row-class-name="rowClassName">
              <vxe-column field="serialNo" title="SerialNo" width="120" />
              <vxe-column field="systemType" title="SystemType" width="140" />
              <vxe-column field="processName" title="Process" width="160" />
              <vxe-column field="coWorkerName" title="Co-worker" width="160" />
              <vxe-column field="startTime" title="Start Time" width="180" />
              <vxe-column field="endTime" title="End Time" width="180" />
              <vxe-column title="State" width="120">
                <template #default="{ row }">
                  <div>
                    <span v-if="row.state === 'Completed'">Completed</span>
                    <button v-else class="start-work-btn" @click.stop="startWorkFromRow(row)">Work</button>
                  </div>
                </template>
              </vxe-column>
            </vxe-table>
          </div>
          <div v-else-if="planningMode === 'edit'">
            <WorkSeat @start-work-and-switch="handleStartWork" />
          </div>
          <div v-else class="wait-dim">
            <div>
              <p>Wait for arrangement from Production Manager</p>
              <div style="margin-top:1em; text-align:center;">
                <button class="create-task-btn" @click="planningMode = 'edit'">Create task for me</button>
              </div>
            </div>
          </div>
        </template>
      </div>

      <div v-if="activeTab === 'counting'">
        <TimerClock v-show="activeTab === 'counting'" :serialNo="selectedSerial" :process="selectedProcess" :username="username" :initialWorkHourId="selectedWorkHourId" @work-submitted="isWorkSubmitted = true" />
      </div>
      <div v-if="activeTab === 'intro'">
        <h2>Department Self-Introduction</h2>
        <p>Information about the department will be shown here.</p>
      </div>
    </div>
  </div>
</template>

<style scoped>
.hero-carousel {
    position: relative;
    overflow: hidden;
  }
  .hero-carousel img {
    width: 100%;
    height: auto;
    transition: opacity 0.5s ease-in-out;
  }
.nav-layout { display: flex; flex-direction: column; min-height: 100vh; background: linear-gradient(180deg, #FFF7EF 0%, #FFFFFF 100%); }
.nav-header { display: flex; align-items: center; padding: 1em 2em 0.5em 1em; }
.company-logo { display:none; }
.hero-section h1 { margin: 0; font-size: 2.5em; color: #EC6602; }
.hero-section p { margin: 0.5em 0 0 0; color: #82451F; font-size: 1.1em; }
.hero-carousel { width: 100%; height: 340px; margin-bottom: 0.7em; overflow: hidden; border-radius: 16px; box-shadow: 0 4px 16px rgba(236,102,2,0.15); background: #fff4ea; display: flex; align-items: center; justify-content: center; }
.hero-img { width: 75%; height: 15%; object-fit: cover; transition: opacity 0.5s; }
.nav-tabs-horizontal { display: flex; gap: 1em; background: #FFF0E4; padding: 0.5em 1em; border-bottom: 1px solid #F2C7A6; box-shadow: inset 0 -1px 0 #F2C7A6; }
.nav-tabs-horizontal button { padding: 0.75em 1.5em; border: none; background: #FFE6D3; cursor: pointer; border-radius: 8px 8px 0 0; font-size: 1em; color: #82451F; box-shadow: 0 2px 6px rgba(236,102,2,0.12); transition: transform 0.1s; }
.nav-tabs-horizontal button:hover { transform: translateY(-1px); }
.nav-tabs-horizontal button.active { background: #FFFFFF; border: 1px solid #F2C7A6; border-bottom: 2px solid #EC6602; font-weight: 700; color: #A64E00; }
/* Add disabled-tab styling when counting is active */
.nav-tabs-horizontal button.disabled { opacity: 0.45; cursor: not-allowed; transform: none; pointer-events: none; }
.tab-content { flex: 1; padding: 2em; background: #fff; border-radius: 0 0 8px 8px; box-shadow: 0 6px 18px rgba(236,102,2,0.12); min-height: 400px; overflow-y: auto; }
.wait-dim { color: #999; background: #f8f8f8; border: 1px dashed #ddd; padding: 1rem; border-radius: 8px; text-align: center; }
/* Add styles for product form */
.product-form {
  max-width: 480px;
  margin: 2em auto 0 auto;
  padding: 2em;
  background: #f9f9f9;
  border-radius: 8px;
  box-shadow: 0 2px 8px #0001;
}
.form-row {
  display: flex;
  flex-direction: column;
  margin-bottom: 1em;
}
.form-row label {
  margin-bottom: 0.5em;
  font-weight: 500;
}
.form-row input {
  padding: 0.5em;
  border: 1px solid #ccc;
  border-radius: 4px;
  font-size: 1em;
}
.form-actions {
  display: flex;
  justify-content: flex-end;
}
.start-work-btn {
  padding: 0.75em 2em;
  background: #42b883;
  color: #fff;
  border: none;
  border-radius: 8px 8px 0 0;
  font-size: 1em;
  color: #82451F;
  box-shadow: 0 2px 6px rgba(236,102,2,0.12);
}
.start-work-btn:hover { transform: translateY(-1px); }
.create-task-btn {
  padding: 0.6em 1.2em;
  background: #EC6602;
  color: #fff;
  border: none;
  border-radius: 8px;
  cursor: pointer;
  font-weight: 700;
}
.create-task-btn:hover { background: #D45500 }
/* Dim completed assignment rows */
.row-completed { opacity: 0.35; }
</style>
