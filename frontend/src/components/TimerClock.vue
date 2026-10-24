<script setup>
import { ref, inject, onMounted, onBeforeUnmount, defineProps, toRef, watch, computed, defineEmits, nextTick } from 'vue'
import axios from 'axios'
// Sync timer state with parent for disabling tabs
const isCountingTimerActive = inject('isCountingTimerActive', null)
const isWorkSubmitted = inject('isWorkSubmitted', null)
// inject username from parent/app so backend calls can include worker name
const username = inject('username', ref('Guest'))

const props = defineProps({
  serialNo: { type: String, default: '101000' },
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

const now = ref(new Date())
const dailyWorkHours = ref([])
const totalsUnavailable = ref(false)
const localStart = ref(null)
const localEnd = ref(null)
let wallClockTimer = null
const currentDateTime = computed(() => formatLocalIso(now.value)?.replace('T', ' '))
const currentRecord = computed(() => dailyWorkHours.value.find(w => String(w.id) === String(currentWorkHourId.value)))
const workLocation = computed(() => currentRecord.value?.location || 'Not specified')
const todayTotal = computed(() => {
  const startOfDay = new Date(now.value); startOfDay.setHours(0, 0, 0, 0)
  const endOfDay = new Date(startOfDay); endOfDay.setDate(endOfDay.getDate() + 1)
  let total = 0
  const rows = [...dailyWorkHours.value]
  if (localStart.value && !currentRecord.value) {
    rows.push({ id: currentWorkHourId.value, workerName: username.value, startTimeActual: localStart.value })
  }
  for (const row of rows) {
    if ((row.workerName || '').trim().toLowerCase() !== (username.value || '').trim().toLowerCase()) continue
    const isCurrent = String(row.id) === String(currentWorkHourId.value)
    const start = new Date(row.startTimeActual || (isCurrent && localStart.value) || NaN)
    const end = new Date(row.endTimeActual || (isCurrent && localEnd.value) || now.value)
    if (!Number.isFinite(start.getTime()) || !Number.isFinite(end.getTime())) continue
    total += Math.max(0, Math.min(end.getTime(), endOfDay.getTime(), now.value.getTime()) - Math.max(start.getTime(), startOfDay.getTime())) / 1000
  }
  return formatTime(total)
})

async function fetchDailyWorkHours() {
  try {
    const res = await axios.get('/api/WorkHours/all-workhours')
    dailyWorkHours.value = (Array.isArray(res.data) ? res.data : []).map(w => ({
      id: w.id ?? w.Id,
      workerName: w.workerName ?? w.WorkerName,
      location: w.location ?? w.Location,
      startTimeActual: w.startTimeActual ?? w.StartTimeActual,
      endTimeActual: w.endTimeActual ?? w.EndTimeActual
    }))
    totalsUnavailable.value = false
  } catch {
    totalsUnavailable.value = true
  }
}

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
const activeClock = ref('paused') // changed from '' to 'paused' to have two explicit states: 'active' | 'paused'
let timer = null
const currentWorkHourId = ref(null)

// NCM metadata captured when NCM timer is started
const processEngineer = ref('')
// Renamed: ncmAction -> callingContent (backend NcmTime.CallingContent)
const callingContent = ref('')
const showNcmInputs = ref(false)
const submitted = ref(false)

// Confirmation modal state for submitting work hours
const showSubmitConfirm = ref(false)
const confirmCancelBtn = ref(null)

// Transient message (fade-out) state
const messageText = ref('')
const messageVisible = ref(false)
let messageTimeout = null

function showTransientMessage(text, ms = 3000) {
  if (messageTimeout) { clearTimeout(messageTimeout); messageTimeout = null }
  messageText.value = text
  messageVisible.value = true
  messageTimeout = setTimeout(() => { messageVisible.value = false; messageTimeout = null }, ms)
}

async function requestSubmitWorkHours() {
  if (submitted.value) return
  showSubmitConfirm.value = true
  await nextTick()
  try { confirmCancelBtn.value?.focus() } catch (e) { /* ignore */ }
}

function cancelSubmit() {
  showSubmitConfirm.value = false
}

function confirmSubmit() {
  showSubmitConfirm.value = false
  // call the (possibly wrapped) submitWorkHours function
  void submitWorkHours()
}

// New: support multiple NCM entries and dropdown options
const processEngineerOptions = ref([])
const ncmRows = ref([{ processEngineer: '', callingContent: '', sent: false }])
const sendingNcms = ref(false)

const availableCount = computed(() => {
  return ncmRows.value.filter(r => !r.sent && ((r.processEngineer && r.processEngineer.trim()) || (r.callingContent && r.callingContent.trim()))).length
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
  ncmRows.value.push({ processEngineer: '', callingContent: '', sent: false })
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
  const start = new Date()
  const end = new Date()
  const payload = ncmRows.value
    .filter(r => !r.sent && ((r.processEngineer && r.processEngineer.trim()) || (r.callingContent && r.callingContent.trim())))
    .map(r => ({
      SerialNo: serialNo.value,
      ProcessEngineer: r.processEngineer && r.processEngineer.trim() ? r.processEngineer.trim() : null,
      ProcessName: process.value || null,
      // send local time string (no Z) instead of Date object which serializes as UTC
      StartTime: formatLocalIso(start),
      EndTime: formatLocalIso(end),
      // Transitional: send both new and legacy names
      CallingContent: r.callingContent && r.callingContent.trim() ? r.callingContent.trim() : null,
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
      if (!r.sent && ((r.processEngineer && r.processEngineer.trim()) || (r.callingContent && r.callingContent.trim()))) {
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

// helper: try to restore existing session for current WorkHourId
async function tryRestoreSessionForWorkHour() {
  if (!currentWorkHourId.value) return
  try {
    console.log('TimerClock: attempting to restore session for WorkHourId', currentWorkHourId.value)
    const res = await axios.get(`/api/WorkHours/session-by-workhour/${currentWorkHourId.value}`)
    const session = res.data
    // API returns camelCase JSON (sessionId, elapsedSeconds, state)
    if (session && session.sessionId) {
      // populate session id and sync elapsed seconds
      sessionId.value = session.sessionId
      const serverElapsed = Number(session.elapsedSeconds ?? 0)
      // normalize activeClock from server into client values 'active' or 'paused'
      if (session.activeClock && (session.activeClock === 'Active' || session.activeClock === 'active')) {
        activeClock.value = 'active'
      } else {
        activeClock.value = 'paused'
      }

      console.log('TimerClock: restored session', sessionId.value, 'with elapsed seconds', serverElapsed)
      if ((Number(workTime.value) || 0) < serverElapsed) workTime.value = serverElapsed

      // if server says session is Working and elapsed > 0 then start the local timer automatically
      const state = (session.state || session.State || '').toString()
      if ((state === 'Working' || state === 'working') && serverElapsed > 0 && activeClock.value === 'active') {
        if (isCountingTimerActive) isCountingTimerActive.value = true
        if (timer) clearInterval(timer)
        timer = setInterval(() => { if (activeClock.value === 'active') workTime.value++ }, 1000)
      }

      // start heartbeat loop (always run heartbeat to keep server session in sync even when paused)
      if (heartbeatTimer) clearInterval(heartbeatTimer)
      heartbeatTimer = setInterval(() => sendHeartbeat(), HEARTBEAT_INTERVAL_MS)
    }
  } catch (err) {
    // 404 is expected when no session exists; ignore
    if (err && err.response && err.response.status === 404) return
    console.error('Failed to restore session for WorkHourId', currentWorkHourId.value, err)
  }
}

// Start overall timer on mount, clean up on unmount
onMounted(() => {
  wallClockTimer = setInterval(() => { now.value = new Date() }, 1000)
  void fetchDailyWorkHours()
    // Only fetch aggregates on mount; do not toggle global isCountingTimerActive here
    void fetchAggregates()
    void fetchProcessEngineers()
    // try to restore any existing session for the provided WorkHourId
    void tryRestoreSessionForWorkHour()
})
onBeforeUnmount(() => {
  if (wallClockTimer) clearInterval(wallClockTimer)
  if (messageTimeout) clearTimeout(messageTimeout)
  // ensure we clear local timer and global flag if active
  if (timer) clearInterval(timer)
  if (isCountingTimerActive && activeClock.value === 'paused') isCountingTimerActive.value = false
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
    console.log('Response of set-working-by-id', res.data)
    return workHourId
  } catch (err) {
    console.error('Failed to set working state by id', err)
    alert('Failed to set working state. See console for details.')
    return null
  }
}


async function startClock() {
  // Prevent starting new clocks if the current session has already been submitted
  if (submitted.value) {
    alert('Work hours already submitted for this session. Reset clocks to start again.')
    return
  }
  if (activeClock.value === 'active' || starting.value) return
  starting.value = true
  try {
    if (!await setWorkingBackend(currentWorkHourId.value)) return
    if (!localStart.value) localStart.value = new Date()
    activeClock.value = 'active'
    if (isCountingTimerActive) isCountingTimerActive.value = true
    if (timer) clearInterval(timer)
    timer = setInterval(() => { if (activeClock.value === 'active') workTime.value++ }, 1000)
    void fetchDailyWorkHours()
  } finally {
    starting.value = false
  }
}

const starting = ref(false)
function pauseClock() {
  if (submitted.value || activeClock.value !== 'active') return
  if (timer) clearInterval(timer)
  timer = null
  activeClock.value = 'paused'
  void sendHeartbeat()
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
      return false
    }

    // compute hours from timers (rounded to 2 decimals)
    const effectiveHours = Math.round((workTime.value / 3600) * 100) / 100

    // build payload
    const payload = { WorkHourId: id, WorkerName: (username && username.value) ? username.value : 'Guest', EffectiveHours: effectiveHours }

    payload.ProcessEngineer = processEngineer.value || null
    // Send both new and legacy property names until backend drops legacy
    payload.CallingContent = callingContent.value || null

    const res = await axios.post('/api/WorkHours/complete', payload)
    console.log('Complete response', res.data)
    localEnd.value = new Date(res.data?.endTimeActual || Date.now())
    void fetchDailyWorkHours()
    // stop timer locally
    if (timer) clearInterval(timer)
    timer = null
    activeClock.value = 'paused'
    if (isCountingTimerActive) isCountingTimerActive.value = false

    // keep timers and NCM metadata visible but mark as submitted and dim UI
    submitted.value = true
    showNcmInputs.value = false
    // notify parent that work was submitted so tabs can be disabled
    if (isWorkSubmitted) isWorkSubmitted.value = true
    // emit an event as well so parent can react if provide/inject didn't reach it
    try { emit('work-submitted') } catch (e) { /* ignore in older runtimes */ }

    // show fade-out message instead of blocking alert
    showTransientMessage('Work hour completed.')
    return true
  } catch (err) {
    console.error('Failed to complete work hour', err)
    alert('Failed to complete work hour')
    return false
  }
}

async function resetClocks() {
  const id = currentWorkHourId.value
  const password = prompt('Enter password to reset all clocks:')
  // Hardcoded password for now; move to backend later
  const correctPassword = 'reset';
  if (password === correctPassword) {
    try {
      // snapshot current id for logging/preserve
      console.debug('TimerClock.resetClocks: starting reset for WorkHourId=', id)

      // attempt to resolve one (will set to Working briefly)
      if (!id) {
        alert('No assigned WorkHour found. Please select a task before resetting.')
      }

      if (id) {
        await axios.post('/api/WorkHours/reset', { WorkHourId: id })
        console.debug('TimerClock.resetClocks: reset request completed for WorkHourId=', id)
      }

      // Ensure any heartbeat loop is stopped and any server-side session is removed
      try {
        // Prefer explicit delete-session endpoint to remove manager + DB record
        if (sessionId.value) {
          await axios.post('/api/WorkHours/delete-session', { SessionId: sessionId.value })
        } else if (id) {
          await axios.post('/api/WorkHours/delete-session', { WorkHourId: id })
        }
      } catch (delErr) {
        // non-fatal; log for diagnostics
        console.warn('TimerClock.resetClocks: delete-session failed', delErr)
      }

      // stop client-side heartbeat and clear session reference unconditionally
      if (heartbeatTimer) { clearInterval(heartbeatTimer); heartbeatTimer = null }
      sessionId.value = null

    } catch (err) {
      console.error('Failed to reset workhour', err)
    }
    workTime.value = 0
    localStart.value = null
    localEnd.value = null
    void fetchDailyWorkHours()
    ncmTime.value = 0
    activeClock.value = 'paused'
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
// also watch selected serial to clear timers when serial changes
watch(serialNo, (nv, ov) => {
  if (nv && nv !== ov) {
    // stop any running timers when switching serials
    if (timer) clearInterval(timer)
    timer = null
    activeClock.value = 'paused'
    if (isCountingTimerActive) isCountingTimerActive.value = false
    currentWorkHourId.value = null
    // clear ncm inputs when serial changes
    processEngineer.value = ''
    callingContent.value = ''
    showNcmInputs.value = false
    // clear submitted state when switching systems
    submitted.value = false
  }
})

// watch for changes to the parent-provided initialWorkHourId so we can switch
// to a different existing WorkHour and attempt to resume its session
watch(() => props.initialWorkHourId, (nv, ov) => {
  if (nv && nv !== ov) {
    currentWorkHourId.value = nv
    void tryRestoreSessionForWorkHour()
  } else if (nv == null) {
    // parent cleared the id — clear local state
    currentWorkHourId.value = null
  }
})


const sessionId = ref(null)
let heartbeatTimer = null
const HEARTBEAT_INTERVAL_MS = 15000 // 15s
// Milliseconds to wait for heartbeat response before marking disconnected
const HEARTBEAT_TIMEOUT_MS = 5000 // 5s
const disconnected = ref(false)

// Optional: count consecutive failures if you want backoff or auto-retry behavior
let consecutiveHeartbeatFailures = 0

async function startSessionIfNeeded() {
  if (sessionId.value) return
  try {
    const res = await axios.post('/api/WorkHours/start-session', {
      WorkHourId: currentWorkHourId.value,
      WorkerName: (username && username.value) ? username.value : 'Guest',
      SerialNo: serialNo.value,
      ProcessName: process.value,
      ElapsedSeconds: workTime.value,
      ActiveClock: activeClock.value === 'active' ? 'active' : 'paused', // send 'paused' to DB when not active
      MetadataJson: JSON.stringify({ note: 'started from client' }),
      State: 'Working'
    })
    sessionId.value = res.data?.sessionId || null

    // If server returns a persisted elapsedSeconds (resuming an existing session), use it.
    const serverElapsed = res.data?.elapsedSeconds
    if (typeof serverElapsed === 'number') {
      // Avoid clobbering a larger client-side counter (e.g. if client already advanced)
      if ((Number(workTime.value) || 0) < serverElapsed) {
        workTime.value = serverElapsed
      }
    }

    // start periodic heartbeat
    if (heartbeatTimer) clearInterval(heartbeatTimer)
    heartbeatTimer = setInterval(() => sendHeartbeat(), HEARTBEAT_INTERVAL_MS)
  } catch (err) {
    console.error('Failed to start session', err)
  }
}

// Replace sendHeartbeat to read server's response and sync elapsedSeconds/sessionId
async function sendHeartbeat() {
  if (!sessionId.value) return
  try {
    const res = await axios.post('/api/WorkHours/session-heartbeat', {
      SessionId: sessionId.value,
      ElapsedSeconds: workTime.value,
      ActiveClock: activeClock.value === 'active' ? 'active' : 'paused', // send 'paused' to DB when not active
      MetadataJson: JSON.stringify({ ncmCount: ncmRows.value.length }),
      State: submitted.value ? 'Completed' : 'Working'
    }, { timeout: HEARTBEAT_TIMEOUT_MS })

    // on success, clear disconnected state and reset failure counter
    disconnected.value = false
    consecutiveHeartbeatFailures = 0

    // If server responded with updated elapsedSeconds, use it (but only if it's larger)
    const serverElapsed = res?.data?.elapsedSeconds
    if (typeof serverElapsed === 'number') {
      if ((Number(workTime.value) || 0) < serverElapsed) {
        workTime.value = serverElapsed
      }
    }

    // Server may also return a canonical sessionId (e.g., if manager created or rotated it)
    const returnedSessionId = res?.data?.sessionId
    if (returnedSessionId && returnedSessionId !== sessionId.value) {
      sessionId.value = returnedSessionId
    }
  } catch (err) {
    console.error('Heartbeat failed', err)
    // mark disconnected when request times out or network fails
    consecutiveHeartbeatFailures++
    if (consecutiveHeartbeatFailures >= 1) {
      disconnected.value = true
    }
  }
}

async function stopSession() {
  if (!sessionId.value) return
  try {
    await axios.post('/api/WorkHours/complete-session', { SessionId: sessionId.value, ElapsedSeconds: workTime.value })
  } catch (err) {
    console.error('Failed to complete session', err)
  } finally {
    sessionId.value = null
    if (heartbeatTimer) { clearInterval(heartbeatTimer); heartbeatTimer = null }
  }
}

// Hook into startClock and submit/reset to manage session lifecycle
const originalStartClock = startClock
startClock = async function() {
  await originalStartClock()
  // when starting work clock, ensure server session exists
  if (activeClock.value === 'active') {
    void startSessionIfNeeded()
  }
}

// after submitWorkHours completes we mark session completed
const originalSubmit = submitWorkHours
submitWorkHours = async function() {
  // Call original submit and only proceed with final heartbeat/stop if a submission actually occurred
  try {
    const didSubmit = await originalSubmit()
    if (!didSubmit) return
  } catch (e) {
    // if original throws, avoid proceeding
    console.error('submit wrapper: original submit failed', e)
    return
  }

  // send final heartbeat and stop session tracking
  try {
    await sendHeartbeat()
  } catch {}
  await stopSession()
}

// reset also stops session
const originalReset = resetClocks
resetClocks = async function() {
  // originalReset already handles delete-session and clears heartbeatTimer/sessionId
  await originalReset()
}

// ensure heartbeats stop on unmount
onBeforeUnmount(() => {
  if (heartbeatTimer) clearInterval(heartbeatTimer)
  // logoff doesn't submit current work.
  //if (sessionId.value) { void stopSession() }
})
</script>

<template>
  <section class="timer-dashboard" aria-label="Work hour counting tool">
    <header class="dashboard-topbar">
      <div class="dashboard-identity">
        <strong>{{ username }}</strong>
        <span>Serial No. <strong>{{ serialNo }}</strong></span>
        <span>Work Location <strong>{{ workLocation }}</strong></span>
      </div>
      <time :datetime="formatLocalIso(now)">{{ currentDateTime }}</time>
    </header>
    <!-- Disconnection banner: shown when heartbeats fail -->
    <div v-if="disconnected" class="disconnected-banner" role="alert" aria-live="assertive">
      ⚠️ Disconnected from Server. Please check your network!
    </div>
    <div class="dashboard-body">
      <div class="timer-display" :class="{ running: activeClock === 'active', 'submitted-dim': submitted }">
        <div class="timer-orbit" aria-hidden="true"></div>
        <div class="timer-digits" role="timer">{{ formatTime(workTime) }}</div>
        <div class="timer-caption">Effective Working Time</div>
        <span class="timer-state">{{ submitted ? 'Submitted' : activeClock === 'active' ? 'Running' : 'Paused' }}</span>
      </div>
      <div class="task-summary">
        <div><span>Current Process</span><strong>{{ process }}</strong></div>
      </div>
      <div class="dashboard-actions">
        <aside class="daily-total" title="Today's actual End − Start durations, including pauses; running work uses the current time.">
          <span>Today's Total</span>
          <strong>{{ totalsUnavailable ? 'Unavailable' : todayTotal }}</strong>
        </aside>
        <div class="timer-controls">
          <button class="timer-action start-action" @click="startClock" :disabled="submitted || starting || activeClock === 'active'">{{ starting ? 'Starting…' : 'Start' }}</button>
          <button class="timer-action pause-action" @click="pauseClock" :disabled="submitted || starting || activeClock !== 'active'">Pause</button>
          <button class="timer-action end-action" @click="requestSubmitWorkHours" :disabled="submitted || starting" title="Submit Work Hours">End</button>
        </div>
        <button class="dashboard-reset" @click="resetClocks" :disabled="submitted || starting" title="Reset Work Hour Clock" aria-label="Reset Work Hour Clock">⟳</button>
      </div>
      <div class="dashboard-aggregates">
        <span>System Work Hours <strong>{{ workHourOverall }}</strong></span>
        <span>NCM Hours <strong>{{ ncmHours }}</strong></span>
      </div>
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

            <label class="ncm-input-label" style="flex:0 0 120px;">Calling Content:</label>
            <input v-model="row.callingContent" class="ncm-input" placeholder="Enter content" :disabled="row.sent" />

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
    <!-- Confirmation modal for submitting work hours -->
    <div v-if="showSubmitConfirm" class="confirm-overlay" role="dialog" aria-modal="true">
      <div class="confirm-dialog">
        <div class="confirm-title">Confirm Submission</div>
        <div class="confirm-body">This will submit your work hour, and the operation cannot be reverted. Continue?</div>
        <div class="confirm-buttons">
          <button ref="confirmCancelBtn" class="confirm-btn cancel" @click="cancelSubmit">Cancel</button>
          <button class="confirm-btn confirm" @click="confirmSubmit">Confirm</button>
        </div>
      </div>
    </div>

    <!-- Transient fade-out message -->
    <div v-if="messageVisible" class="fade-message" role="status" aria-live="polite">{{ messageText }}</div>
  </section>
</template>

<style scoped>
.timer-dashboard {
  width: 100%;
  padding: clamp(14px, 2vw, 28px);
  border: 1px solid #f2c7a6;
  border-radius: 20px;
  color: #82451f;
  background: linear-gradient(135deg, #ffffff, #fff7ef 65%, #fff0e4);
  box-shadow: inset 0 1px 0 #fff, 0 8px 24px #ec660210;
}
.dashboard-topbar { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 8px 24px; padding-bottom: 16px; font-size: 0.9rem; color: #79553c; text-align: left; }
.dashboard-identity { display: flex; flex-wrap: wrap; align-items: baseline; gap: 8px 24px; min-width: 0; }
.dashboard-identity strong { color: #82451f; overflow-wrap: anywhere; }
.dashboard-identity span strong { margin-left: 6px; }
.dashboard-topbar time { margin-left: auto; font-variant-numeric: tabular-nums; }
.dashboard-body { width: min(100%, 960px); margin: 8px auto 0; }
.timer-display { position: relative; isolation: isolate; display: flex; flex-direction: column; justify-content: center; align-items: center; min-height: clamp(220px, 28vh, 320px); overflow: hidden; border: 1px solid #f2c7a6; border-radius: 14px; background: linear-gradient(135deg, #fff, #fff3e8); box-shadow: inset 0 0 24px #fff, 0 6px 18px #ec66020c; }
.timer-orbit { position: absolute; z-index: -1; width: 70%; max-width: 500px; height: 85%; border-radius: 50%; border: 1px solid #f3b788; box-shadow: 0 0 22px #ec660214, inset 0 0 18px #ec660208; }
.running .timer-orbit { border-color: #ec6602; box-shadow: 0 0 28px #ec660225; }
.timer-digits { color: #ec6602; font-size: clamp(2.6rem, 6vw, 5.5rem); line-height: 1.15; font-weight: 650; font-variant-numeric: tabular-nums; letter-spacing: 0.02em; text-shadow: 0 2px 4px #ec660212; }
.timer-caption { margin-top: 8px; font-size: 1rem; }
.timer-state { margin-top: 8px; font-size: 0.8rem; color: #79553c; }
.task-summary { margin-top: 20px; padding: 18px; border: 1px solid #f2c7a6; border-radius: 12px; background: #fff; text-align: center; }
.task-summary > div:first-child { display: flex; flex-wrap: wrap; justify-content: center; gap: 8px 14px; }
.dashboard-actions { display: grid; grid-template-columns: minmax(110px, 1fr) auto minmax(44px, 1fr); align-items: center; gap: 20px; margin-top: 26px; }
.daily-total { justify-self: start; display: flex; flex-direction: column; gap: 3px; padding: 10px 14px; background: #fff; border: 1px solid #f2c7a6; border-radius: 10px; }
.daily-total span { color: #79553c; font-size: 0.75rem; }
.daily-total strong { font-size: 1.2rem; font-variant-numeric: tabular-nums; }
.timer-controls { display: flex; flex-wrap: wrap; justify-content: center; gap: clamp(12px, 2vw, 28px); }
.timer-action { min-width: 120px; border-radius: 999px; padding: 10px 28px; color: #fff; border: 1px solid #ffffff70; box-shadow: inset 0 0 10px #ffffff35, 0 0 12px #0004; }
.start-action { background: linear-gradient(#20b7ab, #087d78); box-shadow: inset 0 0 10px #b4fff955, 0 0 14px #15d7b955; }
.pause-action { background: linear-gradient(#68778b, #364254); }
.end-action { background: linear-gradient(#b83b4c, #711727); box-shadow: inset 0 0 10px #ffb6bf44, 0 0 14px #f6335555; }
.timer-action:disabled, .dashboard-reset:disabled { opacity: 0.4; cursor: not-allowed; }
.timer-action:not(:disabled):hover { filter: brightness(1.2); }
.dashboard-reset { justify-self: end; background: transparent; color: #82451f; padding: 8px 12px; font-size: 1.5rem; }
.dashboard-aggregates { display: flex; flex-wrap: wrap; justify-content: center; gap: 10px 28px; margin: 18px 0; color: #79553c; font-size: 0.85rem; }
.timer-dashboard .ncm-panel { background: #fff; color: #82451f; }
@media (max-width: 720px) {
  .dashboard-actions { grid-template-columns: 1fr auto; gap: 16px; }
  .timer-controls { grid-column: 1 / -1; grid-row: 1; }
  .timer-action { min-width: 90px; padding: 10px 18px; }
}
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

.circle-wide-btn:disabled {
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
 /* Dim the reset button when the session has been submitted (match submit button behavior) */
 .reset-btn.submitted-dim {
   opacity: 0.6;
   cursor: not-allowed;
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
.disconnected-banner {
  background: #fff4f4;
  color: #7a1f1f;
  border: 1px solid #f5c6cb;
  padding: 0.6rem 1rem;
  margin: 0.6rem 0;
  border-radius: 6px;
  font-weight: 700;
  display: flex;
  align-items: center;
  gap: 0.6rem;
}
.confirm-overlay {
  position: fixed;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(0,0,0,0.35);
  z-index: 1000;
}
.confirm-dialog {
  background: #fff;
  border-radius: 8px;
  padding: 1.2rem;
  width: 420px;
  box-shadow: 0 6px 20px rgba(0,0,0,0.2);
  display: flex;
  flex-direction: column;
  gap: 0.8rem;
}
.confirm-title { font-weight: 700; color: #e74c3c; }
.confirm-body { color: #333; }
.confirm-buttons { display:flex; justify-content:flex-end; gap:0.5rem; }
.confirm-btn { padding: 0.5rem 0.9rem; border-radius: 6px; border: none; cursor: pointer; }
.confirm-btn.cancel { background: #eee; color: #333; }
.confirm-btn.confirm { background: #e74c3c; color: #fff; }

.fade-message {
  position: fixed;
  top: 1rem;
  right: 1rem;
  background: #42b883;
  color: #fff;
  padding: 0.6rem 1rem;
  border-radius: 6px;
  box-shadow: 0 4px 12px rgba(0,0,0,0.15);
  opacity: 1;
  transition: opacity 0.45s ease-in-out, transform 0.45s ease-in-out;
}
.fade-message[style*="display: none"] { opacity: 0 }
.fade-message[aria-hidden="true"] { opacity: 0 }
</style>