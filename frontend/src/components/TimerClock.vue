<script setup>
import { ref, inject, onMounted, onBeforeUnmount, defineProps, toRef, watch } from 'vue'
import axios from 'axios'
// Sync timer state with parent for disabling tabs
const isCountingTimerActive = inject('isCountingTimerActive', null)

const props = defineProps({
  serialNo: { type: String, default: 'SN-101009' },
  process: { type: String, default: 'Assembly' },
  workSeat: { type: String, default: 'WS-02' }
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


// Start overall timer on mount, clean up on unmount
onMounted(() => {
    if (isCountingTimerActive) isCountingTimerActive.value = true
    void fetchAggregates()
})
onBeforeUnmount(() => {
  if (isCountingTimerActive) isCountingTimerActive.value = false
})


async function setWorkingBackend() {
  try {
    const workerName = (username && username.value) ? username.value : 'Guest'
    // Resolve product id by serial number
    const pRes = await axios.get(`/api/WorkHours/product-status/${encodeURIComponent(serialNo.value)}`)
    const product = pRes?.data
    const productId = product?.id ?? product?.Id ?? null
    if (!productId) {
      console.warn('ProductId not found for serial', serialNo.value)
      return null
    }
    const payload = {
      ProductId: productId,
      ProcessName: process.value,
      WorkerName: workerName
    }
    const res = await axios.post('/api/WorkHours/set-working', payload)
    const id = res?.data?.id ?? null
    if (id) currentWorkHourId.value = id
    console.log('Set working response', res.data)
    return id
  } catch (err) {
    console.error('Failed to set working state', err)
    return null
  }
}

async function ensureWorkHourId() {
  if (currentWorkHourId.value) return currentWorkHourId.value
  const id = await setWorkingBackend()
  return id
}

function startClock(type) {
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
    else if (activeClock.value === 'ncm') ncmTime.value++
  }, 1000)

  // If starting the work clock, notify backend to set state to Working
  if (type === 'work') {
    // fire-and-forget
    void setWorkingBackend()
  }
}

// stopClock removed

function formatTime(sec) {
  const h = Math.floor(sec / 3600).toString().padStart(2, '0')
  const m = Math.floor((sec % 3600) / 60).toString().padStart(2, '0')
  const s = (sec % 60).toString().padStart(2, '0')
  return `${h}:${m}:${s}`
}

async function submitWorkHours() {
  // Mark current work hour as Completed, set EndTimeActual to now
  try {
    const id = await ensureWorkHourId()
    if (!id) {
      alert('No WorkHour record found to complete')
      return
    }
    const payload = { WorkHourId: id, WorkerName: (username && username.value) ? username.value : 'Guest' }
    const res = await axios.post('/api/WorkHours/complete', payload)
    console.log('Complete response', res.data)
    // stop timer locally
    if (timer) clearInterval(timer)
    timer = null
    activeClock.value = ''
    if (isCountingTimerActive) isCountingTimerActive.value = false
    alert('Work hour completed.')
    // Optionally clear currentWorkHourId so next start will create/find a new record
    currentWorkHourId.value = null
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
      // If we have a current WorkHour, reset it on backend
      let id = currentWorkHourId.value
      if (!id) {
        // attempt to resolve one (will set to Working briefly)
        id = await setWorkingBackend()
      }
      if (id) {
        await axios.post('/api/WorkHours/reset', { WorkHourId: id })
        console.log('Reset workhour', id)
      }
    } catch (err) {
      console.error('Failed to reset workhour', err)
    }
    workTime.value = 0
    ncmTime.value = 0
    activeClock.value = ''
    if (isCountingTimerActive) isCountingTimerActive.value = false
    if (timer) clearInterval(timer)
    currentWorkHourId.value = null
    alert('Clocks have been reset.')
  } else if (password !== null) {
    alert('Incorrect password. Reset cancelled.')
  }
}

// call on mount and when serial changes
watch(serialNo, (nv) => {
  void fetchAggregates()
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
      <span><strong>Work Seat:</strong> {{ workSeat }}</span>
      <span><strong>WorkHour Mean:</strong> {{ workHourMean }}</span>
    </div>
  </div>
  <div class="workhour-panel">
    <div class="workhour-panel-title">Today's working</div>
    <div class="workhour-clocks-row">
      <div class="clock-block">
        <div class="clock-label">Effective Working Time</div>
        <div class="clock-time" :class="{active: activeClock === 'work'}">{{ formatTime(workTime) }}</div>
        <button class="circle-wide-btn" :class="{active: activeClock === 'work'}" @click="startClock('work')" title="Work">Work</button>
      </div>
      <div class="clock-block">
        <div class="clock-label">NCM Time</div>
        <div class="clock-time" :class="{active: activeClock === 'ncm'}">{{ formatTime(ncmTime) }}</div>
        <button class="circle-wide-btn" :class="[{active: activeClock === 'ncm'}, {'ncm-active': activeClock === 'ncm'}]" @click="startClock('ncm')" title="NCM">NCM</button>
      </div>
    </div>
    <div class="reset-btn-row">
      <span class="reset-btn-spacer"></span>
      <button class="submit-btn" @click="submitWorkHours" title="Submit Work Hours">Submit Work Hours</button>
      <span class="reset-btn-spacer"></span>
      <button class="reset-btn" @click="resetClocks" title="Will reset all clocks!">⟳</button>
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
  flex: 1 1 35%;
  max-width: 35%;
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
  flex: 1 1 35%;
  max-width: 60%;
  min-width: 96px;
  aspect-ratio: 1 / 1;
  height: auto;
  border-radius: 50%;
  border: none;
  background: #eee;
  color: #333;
  font-size: 2em;
  font-weight: bold;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  margin: 0;
  transition: background 0.2s, color 0.2s, box-shadow 0.2s;
  box-shadow: 0 1px 4px #0001;
  width: 100%;
}
.circle-wide-btn.active {
  background: #42b883;
  color: #fff;
  box-shadow: 0 2px 8px #42b88333;
}
.circle-wide-btn.ncm-active {
  background: #e74c3c !important;
  color: #fff !important;
  box-shadow: 0 2px 8px #e74c3c33 !important;
}
.circle-wide-btn:hover {
  background: #c2f0d3;
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
   font-size: 1.1em;
   border: none;
   box-shadow: 0 2px 8px #42b88333;
   display: flex;
   align-items: center;
   justify-content: center;
   font-weight: bold;
   cursor: pointer;
   transition: background 0.2s;
 }
 .submit-btn:hover {
   background: #36976b;
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
</style>
