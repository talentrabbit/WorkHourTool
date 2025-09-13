<script setup>
import { ref, inject, onMounted, onBeforeUnmount, defineProps, toRef, watch, computed, defineEmits } from 'vue'
import axios from 'axios'
// Sync timer state with parent for disabling tabs
const isCountingTimerActive = inject('isCountingTimerActive', null)
const isWorkSubmitted = inject('isWorkSubmitted', null)
// inject username from parent/app so backend calls can include worker name
const username = inject('username', ref('Guest'))

const props = defineProps({
  serialNo: { type: String, default: 'SN-101009' },
  process: { type: String, default: 'Assembly' },
  workSeat: { type: String, default: 'WS-02' },
  initialWorkHourId: { type: [String, Number], default: null }
})
// Use toRef so prop updates from parent remain reactive
const serialNo = toRef(props, 'serialNo')
const process = toRef(props, 'process')
const workSeat = toRef(props, 'workSeat')
const workHourOverall = ref('00:00')
const ncmHours = ref('0:00')
const workHourMean = ref('0:00')

// Helper to format fractional hours (e.g. 13.5) into H:MM
function hoursToHHMM(hours) {
  if (hours == null) return '0:00'
  const totalMinutes = Math.round(Number(hours) * 60)
  const h = Math.floor(totalMinutes / 60)
  const m = totalMinutes % 60
  return `${h}:${m.toString().padStart(2, '0')}`
}

// format seconds into HH:MM:SS for display in clocks
function formatTime(sec) {
  const s = Number(sec) || 0
  const h = Math.floor(s / 3600).toString().padStart(2, '0')
  const m = Math.floor((s % 3600) / 60).toString().padStart(2, '0')
  const ss = Math.floor(s % 60).toString().padStart(2, '0')
  return `${h}:${m}:${ss}`
}

// Format a Date into a local ISO-like string (YYYY-MM-DDTHH:mm:ss) so backend receives local time instead of UTC (no trailing Z)
function formatLocalIso(dt) {
  if (!dt) return null
  const pad = n => String(n).padStart(2, '0')
  return `${dt.getFullYear()}-${pad(dt.getMonth() + 1)}-${pad(dt.getDate())}T${pad(dt.getHours())}:${pad(dt.getMinutes())}:${pad(dt.getSeconds())}`
}

// fetch aggregates for current serial
async function fetchAggregates() {
  if (!serialNo.value) return
  try {
    const [whRes, ntRes] = await Promise.all([
      axios.get(`/api/WorkHours/workhours-by-system/${encodeURIComponent(serialNo.value)}`),
      axios.get(`/api/WorkHours/ncmtimes-by-system/${encodeURIComponent(serialNo.value)}`)
    ])
    const whList = Array.isArray(whRes.data) ? whRes.data : []
    const ntList = Array.isArray(ntRes.data) ? ntRes.data : []
    const totalEffective = whList.reduce((sum, w) => sum + (Number(w.EffectiveHours ?? w.effectiveHours ?? 0) || 0), 0)
    workHourOverall.value = hoursToHHMM(totalEffective)
    const totalNcm = ntList.reduce((sum, n) => sum + (Number(n.NcmHour ?? n.ncmHour ?? 0) || 0), 0)
    if (totalNcm != null) ncmHours.value = hoursToHHMM(totalNcm)
  } catch (err) {
    console.error('Failed to fetch aggregates for serial', serialNo.value, err)
  }
}

// Example static info (replace with props or API data as needed)

const workTime = ref(0) // seconds
const ncmTime = ref(0) // seconds
const activeClock = ref('') // 'work' or 'ncm' or ''
let timer = null
const currentWorkHourId = ref(null)

// NCM metadata captured when NCM timer is started
const processEngineer = ref('')
const ncmAction = ref('')
const showNcmInputs = ref(false)
const submitted = ref(false)

// New: support multiple NCM entries and dropdown options
const processEngineerOptions = ref([])
const ncmRows = ref([{ processEngineer: '', ncmAction: '', sent: false }])
const sendingNcms = ref(false)

const availableCount = computed(() => {
  return ncmRows.value.filter(r => !r.sent && ((r.processEngineer && r.processEngineer.trim()) || (r.ncmAction && r.ncmAction.trim()))).length
})

// Fetch process engineer names for the dropdown
async function fetchProcessEngineers() {
  try {
    const res = await axios.get('/api/WorkHours/all-process-engineer-names')
    processEngineerOptions.value = Array.isArray(res.data) ? res.data : []
  } catch (err) {
    console.error('Failed to fetch process engineer names', err)
    processEngineerOptions.value = []
  }
}

// Add a new empty NCM row
function addNcmRow() {
  ncmRows.value.push({ processEngineer: '', ncmAction: '', sent: false })
}
// Remove a row by index
function removeNcmRow(index) {
  if (index >= 0 && index < ncmRows.value.length) {
    ncmRows.value.splice(index, 1)
  }
}

// Send NCM rows to backend and attempt notifications
async function sendNcms() {
  if (!serialNo.value) {
    alert('No SerialNo selected for NCM')
    return
  }
  if (sendingNcms.value) return
  // Remind user that the operation may take a while and can not be cancelled
  if (!window.confirm('Sending NCMs may take a while and cannot be cancelled. Continue?')) {
    return
  }
  // build payload from rows; include approximate Start/End from the current ncmTime
  const start = Date.now()
  const end = Date.now()
  const payload = ncmRows.value
    .filter(r => !r.sent && ((r.processEngineer && r.processEngineer.trim()) || (r.ncmAction && r.ncmAction.trim())))
    .map(r => ({
      SerialNo: serialNo.value,
      ProcessEngineer: r.processEngineer && r.processEngineer.trim() ? r.processEngineer.trim() : null,
      ProcessName: process.value || null,
      // send local time string (no Z) instead of Date object which serializes as UTC
      StartTime: formatLocalIso(start),
      EndTime: formatLocalIso(end),
      NcmAction: r.ncmAction && r.ncmAction.trim() ? r.ncmAction.trim() : null
    }))
  if (!payload.length) {
    alert('Please add at least one NCM entry to send')
    return
  }
  try {
    sendingNcms.value = true
    const res = await axios.post('/api/WorkHours/save-ncm-and-notify', payload)
    console.log('save-ncm-and-notify response', res.data)
    // mark sent rows as sent (match by array order since backend returns results in same order)
    let sentIndex = 0
    for (let i = 0; i < ncmRows.value.length; i++) {
      const r = ncmRows.value[i]
      if (!r.sent && ((r.processEngineer && r.processEngineer.trim()) || (r.ncmAction && r.ncmAction.trim()))) {
        // mark as sent
        r.sent = true
        sentIndex++
      }
    }
    // optionally refresh aggregates
    void fetchAggregates()
  } catch (err) {
    console.error('Failed to send NCMs', err)
    alert('Failed to send NCMs')
  } finally {
    sendingNcms.value = false
  }
}

// initialize from parent-provided id if available
if (props.initialWorkHourId) {
  currentWorkHourId.value = props.initialWorkHourId
}

// watch for parent updates
watch(() => props.initialWorkHourId, (nv) => {
  console.debug('TimerClock: initialWorkHourId prop changed ->', nv, 'old currentWorkHourId=', currentWorkHourId.value)
  currentWorkHourId.value = nv || null
})

// Start overall timer on mount, clean up on unmount
onMounted(() => {
    // Only fetch aggregates on mount; do not toggle global isCountingTimerActive here
    void fetchAggregates()
    void fetchProcessEngineers()
})
onBeforeUnmount(() => {
  // ensure we clear local timer and global flag if active
  if (timer) clearInterval(timer)
  if (isCountingTimerActive && activeClock.value === '') isCountingTimerActive.value = false
})


async function setWorkingBackend(workHourId) {
  try {
    // Require an existing WorkHour id — do not attempt to resolve product id here
    if (!workHourId) {
      alert('No assigned WorkHour found. Please select a task before starting.')
      return null
    }
    const payload = {
      WorkHourId: workHourId,
      WorkerName: (username && username.value) ? username.value : 'Guest'
    }
    // Call the by-id endpoint which directly updates the WorkHour state
    const res = await axios.post('/api/WorkHours/set-working-by-id', payload)
    // backend acknowledges the update; keep id in state
    console.log('Set working-by-id response', res.data)
    return workHourId
  } catch (err) {
    console.error('Failed to set working state by id', err)
    alert('Failed to set working state. See console for details.')
    return null
  }
}


function startClock(type) {
  // Prevent starting new clocks if the current session has already been submitted
  if (submitted.value) {
    alert('Work hours already submitted for this session. Reset clocks to start again.')
    return
  }

  // If clicking the active clock, pause it
  if (activeClock.value === type) {
    if (timer) clearInterval(timer)
    timer = null
    activeClock.value = ''
    if (isCountingTimerActive) isCountingTimerActive.value = false
    return
  }
  // Otherwise, start the selected clock
  if (timer) clearInterval(timer)
  activeClock.value = type
  if (isCountingTimerActive) isCountingTimerActive.value = true
  timer = setInterval(() => {
    if (activeClock.value === 'work') workTime.value++
  }, 1000)

  // If starting the work clock, ensure we have a WorkHour id but avoid extra lookup if parent provided it
  if (type === 'work') {
      // Update WorkHour to Working state
      (async () => {
        await setWorkingBackend(currentWorkHourId.value)
      })()
  }
}

function switchNcmInputs() {
  showNcmInputs.value = !showNcmInputs.value
}

async function submitWorkHours() {
  // Mark current work hour as Completed, set EndTimeActual to now
  try {
    // Prefer using known id to avoid extra server queries
    const id = currentWorkHourId.value 
    if (!id) {
      alert('No WorkHour record found to complete')
      return
    }

    if (!window.confirm('This will submit your work hour, and the operation can not be revert! Continue?')) {
      return
    }

    // compute hours from timers (rounded to 2 decimals)
    const effectiveHours = Math.round((workTime.value / 3600) * 100) / 100
    const reportedNcmHours = Math.round((ncmTime.value / 3600) * 100) / 100

    // build payload
    const payload = { WorkHourId: id, WorkerName: (username && username.value) ? username.value : 'Guest', EffectiveHours: effectiveHours }
   
    payload.ProcessEngineer = processEngineer.value || null
    payload.NcmAction = ncmAction.value || null
    

    const res = await axios.post('/api/WorkHours/complete', payload)
    console.log('Complete response', res.data)
    // stop timer locally
    if (timer) clearInterval(timer)
    timer = null
    activeClock.value = ''
    if (isCountingTimerActive) isCountingTimerActive.value = false

    // keep timers and NCM metadata visible but mark as submitted and dim UI
    submitted.value = true
    showNcmInputs.value = false
    // notify parent that work was submitted so tabs can be disabled
    if (isWorkSubmitted) isWorkSubmitted.value = true
    // emit an event as well so parent can react if provide/inject didn't reach it
    try { emit('work-submitted') } catch (e) { /* ignore in older runtimes */ }

    alert('Work hour completed.')
  } catch (err) {
    console.error('Failed to complete work hour', err)
    alert('Failed to complete work hour')
  }
}

async function resetClocks() {
  const password = prompt('Enter password to reset all clocks:')
  // Hardcoded password for now; move to backend later
  const correctPassword = 'reset';
  if (password === correctPassword) {
    try {
      // snapshot current id for logging/preserve
      const id = currentWorkHourId.value
      console.debug('TimerClock.resetClocks: starting reset for WorkHourId=', id)

      // attempt to resolve one (will set to Working briefly)
      if (!id) {
        alert('No assigned WorkHour found. Please select a task before resetting.')
      }

      if (id) {
        await axios.post('/api/WorkHours/reset', { WorkHourId: id })
        console.debug('TimerClock.resetClocks: reset request completed for WorkHourId=', id)
      }
    } catch (err) {
      console.error('Failed to reset workhour', err)
    }
    workTime.value = 0
    ncmTime.value = 0
    activeClock.value = ''
    if (isCountingTimerActive) isCountingTimerActive.value = false
    if (timer) clearInterval(timer)
    // clear submitted state so user can start again
    submitted.value = false
    // restore the local id snapshot in case parent briefly cleared the prop
    if (id) currentWorkHourId.value = id
    alert('Clocks have been reset.')
  } else if (password !== null) {
    alert('Incorrect password. Reset cancelled.')
  }
}

// call on mount and when serial changes
watch(serialNo, (nv) => {
  void fetchAggregates()
})
// also watch selected serial to clear timers when serial changes externally
watch(serialNo, (nv, ov) => {
  if (nv && nv !== ov) {
    // stop any running timers when switching serials
    if (timer) clearInterval(timer)
    timer = null
    activeClock.value = ''
    if (isCountingTimerActive) isCountingTimerActive.value = false
    currentWorkHourId.value = null
    // clear ncm inputs when serial changes
    processEngineer.value = ''
    ncmAction.value = ''
    showNcmInputs.value = false
    // clear submitted state when switching systems
    submitted.value = false
  }
})
</script>

<template>
  <div class="workhour-header-row">
    <div class="workhour-system-info">
      <span><strong>Serial No.:</strong> {{ serialNo }}</span>      
      <span><strong>WorkHour Accumulated:</strong> {{ workHourOverall }}</span>
      <span><strong>NCM Hours:</strong> {{ ncmHours }}</span>
    </div>
    <div class="workhour-seat-info">
      <span><strong>Process:</strong> {{ process }}</span>
      <span><strong>Worker:</strong> {{ username }}</span>
      <span><strong>WorkHour Mean:</strong> {{ workHourMean }}</span>
    </div>
  </div>
  <div class="workhour-panel">
    <div class="workhour-panel-title">Today's working</div>
    <div class="workhour-clocks-row">
      <div class="clock-block">
        <div class="clock-label">Effective Working Time</div>
        <div class="clock-time" :class="{active: activeClock === 'work', 'submitted-dim': submitted}">{{ formatTime(workTime) }}</div>
        <button class="circle-wide-btn" :class="{active: activeClock === 'work', dimmed: isCountingTimerActive && activeClock !== 'work'}" :disabled="submitted" @click="startClock('work')" title="Work">Work</button>
      </div>
      
    </div>
    <div class="reset-btn-row">
      <span class="reset-btn-spacer"></span>
      <button class="submit-btn" @click="submitWorkHours" :disabled="submitted" :class="{'submitted-dim': submitted}" title="Submit Work Hours">Submit Work Hours</button>
      <span class="reset-btn-spacer"></span>
      <button class="reset-btn" @click="resetClocks" title="Will reset all clocks!">⟳</button>
    </div>
    <!-- NCM panel: header always visible; inputs and Send button collapse/expand -->
    <div class="ncm-panel">
      <div class="ncm-panel-header" @click="switchNcmInputs" style="cursor:pointer; display:flex; align-items:center; justify-content:space-between;">
        <span>NCM Details</span>
        <span class="chevron" :class="{ open: showNcmInputs }">▾</span>
      </div>
      <div class="ncm-collapse" :class="{ open: showNcmInputs }" :aria-expanded="showNcmInputs">
        <div class="ncm-inputs">
          <div v-for="(row, idx) in ncmRows" :key="idx" class="ncm-input-row">
            <label class="ncm-input-label">Process Engineer:</label>
            <select v-model="row.processEngineer" class="ncm-input" :disabled="row.sent">
              <option value="">-- Select --</option>
              <option v-for="(opt, i) in processEngineerOptions" :key="i" :value="opt">{{ opt }}</option>
            </select>

            <label class="ncm-input-label" style="flex:0 0 120px;">NCM Action:</label>
            <input v-model="row.ncmAction" class="ncm-input" placeholder="Enter action" :disabled="row.sent" />

            <button class="ncm-row-btn" @click="removeNcmRow(idx)" title="Remove" v-if="ncmRows.length > 1 && !row.sent">-</button>
            <button class="ncm-row-btn" @click="addNcmRow" title="Add" v-if="idx === ncmRows.length - 1">+</button>
            <span v-if="row.sent" class="ncm-sent-badge">Sent</span>
          </div>
        </div>
        <div style="margin-top:1rem; text-align:center;">
          <button class="submit-btn" @click="sendNcms" :disabled="sendingNcms || availableCount === 0">Send NCM</button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.workhour-panel {
  background: #f8f8f8;
  border-radius: 12px;
  box-shadow: 0 2px 8px #0001;
  padding: 2em 2em 1.5em 2em;
  margin-bottom: 2em;
}
.workhour-panel-title {
  font-size: 1.3em;
  font-weight: bold;
  color: #42b883;
  margin-bottom: 1.2em;
}
.workhour-system-info {
  display: flex;
  flex-wrap: wrap;
  gap: 2em;
  margin-bottom: 1.5em;
  font-size: 1.1em;
}
/* Clocks row: two columns, each 50% width */
.workhour-clocks-row {
  display: flex;
  flex-direction: row;
  gap: 2em;
  margin-bottom: 1.5em;
  align-items: center;
  justify-content: center;
}
.clock-block {
  display: flex;
  flex-direction: column;
  align-items: center;
  background: #f5f5f5;
  border-radius: 8px;
  padding: 1.2em 2.5em;
  box-shadow: 0 2px 8px #0001;
  flex: 1 1 55%;
  max-width: 55%;
  width: 100%;
}
.clock-label {
  font-size: 1em;
  color: #888;
  margin-bottom: 0.5em;
}
.clock-time {
  font-size: 2.2em;
  font-family: monospace;
  color: #333;
  margin-bottom: 0.2em;
  transition: color 0.2s;
}
.clock-time.active {
  color: #42b883;
}
.clock-time.submitted-dim {
  opacity: 0.45;
}
/* Button row: two columns, each 50% width */
.workhour-btn-row {
  display: flex;
  flex-direction: row;
  margin-top: 1em;
  width: 100%;
  align-items: center;
  justify-content: center;
  gap: 2em;
}
.circle-wide-btn {
  flex: 1 1 50%;
  max-width: 260px; /* increased max width */
  min-width: 140px; /* increased min width */
  aspect-ratio: 1 / 1;
  height: 140px; /* explicit larger height to enlarge the circle */
  border-radius: 50%;
  border: none;
  background: #eee;
  color: #333;
  font-size: 2em; /* larger label */
  font-weight: bold;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  margin: 0;
  transition: background 0.15s, color 0.15s, box-shadow 0.15s, transform 0.08s;
  box-shadow: 0 2px 8px #0002; /* slightly stronger shadow */
}

/* Toggle (pressed) state for the circular button */
.circle-wide-btn.active {
  background: #42b883;
  color: #fff;
  box-shadow: 0 3px 10px #42b88333;
  transform: translateY(1px) scale(0.98);
}

/* Hover only when not active */
.circle-wide-btn:not(.active):hover {
  background: #c2f0d3;
}

.rectangle-wide-btn {
  background: #eee;
  color: #333;
  border: none;
  border-radius: 8px;
  padding: 0; /* remove horizontal padding so square sizing is consistent */
  font-size: 1.1em;
  font-weight: bold;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  box-shadow: 0 1px 4px #0001;
  transition: background 0.15s, color 0.15s, box-shadow 0.15s, transform 0.08s;
  width: 150px; /* square size */
  aspect-ratio: 1 / 1;
  height: 48px;
}

.rectangle-wide-btn.active {
  background: #e74c3c;
  color: #fff;
  box-shadow: 0 2px 8px #e74c3c33;
  transform: translateY(1px) scale(0.98);
}

.circle-wide-btn:disabled, .circle-wide-btn.dimmed {
  opacity: 0.45;
  cursor: not-allowed;
}

.workhour-seat-info {
  display: flex;
  flex-direction: row;
  gap: 2em;
  font-size: 1.1em;
  background: #e8f5e9;
  border-radius: 8px;
  padding: 1em 2em;
  box-shadow: 0 1px 4px #0001;
 }
 .reset-btn-row {
   display: flex;
   align-items: center;
   margin-top: 2em;
   gap: 1.5em;
   position: relative;
 }
.reset-btn-spacer {
   flex: 1 1 0;
 }
.submit-btn {
   margin: 0;
   position: absolute;
   left: 50%;
   transform: translateX(-50%);
   z-index: 1;
 }
 .submit-btn {
   background: #42b883;
   color: #fff;
   border-radius: 8px;
   min-width: 160px;
   min-height: 44px;
   font-size: 1em;
   border: none;
   box-shadow: 0 2px 8px #42b88333;
   display: flex;
   align-items: center;
   justify-content: center;
   font-weight: none;
   cursor: pointer;
   transition: background 0.2s;
 }
 .submit-btn:hover {
   background: #36976b;
 }
 .submit-btn.submitted-dim {
   opacity: 0.6;
   cursor: not-allowed;
 }
 .reset-btn {
   background: #ffe066;
   color: #333;
   border-radius: 8px;
   min-width: 48px;
   min-height: 44px;
   width: 48px;
   height: 44px;
   font-size: 1.3em;
   border: none;
   box-shadow: 0 2px 8px #ffe06655;
   display: flex;
   align-items: center;
   justify-content: center;
   margin-left: auto;
   cursor: pointer;
   transition: background 0.2s;
 }
 .reset-btn:hover {
   background: #ffd43b;
 }
 /* NCM metadata input styles */
 .ncm-inputs {
   display: flex;
   flex-direction: column;
   gap: 1em;
   margin-top: 1.5em;
 }
 .ncm-input-row {
   display: flex;
   flex-direction: row;
   gap: 1em;
   width: 100%;
   font-size: 0.92rem; /* slightly smaller overall text */
   padding: 0.28rem 0; /* small vertical padding */
   align-items: center;
 }
 .ncm-input-label {
   flex: 0 0 120px; /* slightly narrower label column */
   font-weight: 700; /* keep emphasis but slightly smaller */
   font-size: 0.92rem;
 }
 .ncm-input {
   flex: 1;
   padding: 0.45rem 0.5rem; /* more compact input padding */
   font-size: 0.95rem; /* slightly smaller input text */
   border: 1px solid #ccc;
   border-radius: 4px;
   box-shadow: 0 1px 3px #0001;
 }
/* Add panel styles */
.ncm-panel {
  background: #fff;
  border: 1px solid #f2c7a6;
  padding: 1rem;
  border-radius: 8px;
  box-shadow: 0 2px 6px rgba(0,0,0,0.04);
  margin-top: 1rem;
}
.ncm-panel-header {
  font-weight: 700;
  color: #e74c3c;
  margin-bottom: 0.75rem;
}
/* Ensure NCM panel's submit button uses normal flow (not the top-level absolute .submit-btn) */
.ncm-panel .submit-btn {
  position: static;
  left: auto;
  transform: none;
  min-width: 140px;
  display: inline-block;
  margin: 0 auto;
}
.ncm-row-btn {
  background: #d0f0ff; /* stronger tint so buttons are visible */
  color: #01579b;
  padding: 0.32rem 0.55rem; /* compact button padding */
  font-size: 0.95rem;
  border-radius: 4px;
  margin-left: 0.35rem;
}
.ncm-row-btn:hover {
  background: #9fe1ff;
}
.ncm-sent-badge {
  font-size: 0.8rem;
  padding: 0.18rem 0.45rem;
  background: #e8f5e9;
  color: #2e7d32;
  border-radius: 4px;
  margin-left: 0.5em;
  align-self: center;
}

/* Add collapsible styles for NCM panel */
.ncm-collapse {
  overflow: hidden;
  transition: max-height 0.28s ease, opacity 0.2s ease, transform 0.18s ease;
  max-height: 0; /* collapsed */
  opacity: 0;
}
.ncm-collapse.open {
  max-height: 1000px; /* large enough to contain content */
  opacity: 1;
}
/* simple chevron rotation */
.chevron {
  display: inline-block;
  transition: transform 0.18s ease;
  transform: rotate(0deg);
  font-size: 1.1em;
  margin-left: 0.5rem;
}
.chevron.open {
  transform: rotate(180deg);
}
</style>