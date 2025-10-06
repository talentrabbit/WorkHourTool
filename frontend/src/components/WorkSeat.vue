<script setup>
import { ref, onMounted, inject, watch } from 'vue'
import axios from 'axios'
const emit = defineEmits(['start-work-and-switch'])

// Dropdown data arrays (to be fetched from backend in future)
const orderNumbers = ref([])
const productNames = ref([])
const seriesNos = ref([])
const processes = ref([])
const workSeats = ref([])
// Co-workers options (to be fetched from backend Process-Worker Table)
const coWorkers = ref([])
// show/hide co-worker listbox
const showCoWorker = ref(false)

// inject username from parent/app
const username = inject('username', ref('Guest'))

// Selected product details
const product = ref(null)

// Form state
const form = ref({
  orderNumber: '',
  productName: '',
  seriesNo: '',
  process: '',
  workSeat: '',
  coWorkers: [], // array of selected co-worker IDs or names
  startDate: '',
  startTime: '',
  endDate: '',
  endTime: ''
})

// coworker conflict state - do not overwrite existing onMounted
const coworkerConflict = ref(false)
const coworkerConflictMessages = ref([])

async function checkCoworkerConflicts(){
  coworkerConflict.value = false
  coworkerConflictMessages.value = []
  const selected = Array.isArray(form.value.coWorkers) ? form.value.coWorkers.filter(Boolean) : []
  if (!selected.length) return
  const todayStr = new Date().toISOString().slice(0,10)
  try{
    for (const cw of selected){
      const res = await axios.get('/api/WorkHours/worker-assignments', { params: { workerName: cw } })
      const assignments = Array.isArray(res.data) ? res.data : (res.data?.assignments || [])
      for (const a of assignments){
        const st = a?.StartTime ?? a?.startTime ?? null
        const en = a?.EndTime ?? a?.endTime ?? null
        const stDate = st ? new Date(st).toISOString().slice(0,10) : null
        const enDate = en ? new Date(en).toISOString().slice(0,10) : null
        if (stDate === todayStr || enDate === todayStr){
          const proc = a?.ProcessName ?? a?.processName ?? ''
          const serial = a?.SerialNo ?? a?.serialNo ?? ''
          const sys = a?.SystemType ?? a?.systemType ?? ''
          let location = ''
          if (serial && sys) location = ` on system ${serial} (${sys})`
          // else if (serial) location = ` on system ${serial}`
          // else if (sys) location = ` (${sys})`

          coworkerConflictMessages.value.push(`${cw} has an assignment today${location}${proc ? ` — ${proc}` : ''}`)
          coworkerConflict.value = true
          break
        }
      }
    }
  }catch(e){
    console.warn('Failed to check coworker assignments', e)
    // do not block start if backend fails
  }
}

watch(() => form.value.coWorkers, () => { checkCoworkerConflicts() }, { deep: true })
onMounted(() => { checkCoworkerConflicts() })

onMounted(async () => {
  // load seriesNos, processes and worker names from backend
  try {
    const [allProductsRes, procRes, workerRes] = await Promise.all([
      axios.get('/api/workhours/all-product-states'),
      axios.get('/api/workhours/all-process-names'),
      axios.get('/api/workhours/all-worker-names')
    ])
    if (Array.isArray(allProductsRes.data)) {
      seriesNos.value = allProductsRes.data.map(p => p.serialNo).filter(Boolean)
    }
    if (Array.isArray(procRes.data)) {
      processes.value = procRes.data
    }
    if (Array.isArray(workerRes.data)) {
      coWorkers.value = workerRes.data
    }
  } catch (e) {
    console.error('Error loading dropdowns', e)
  }

  const today = new Date().toISOString().slice(0,10)
  form.value.startDate = today
  form.value.endDate = today
  // default working hours for a worker
  form.value.startTime = '08:30'
  form.value.endTime = '17:00'
})

async function onSerialChange() {
  if (!form.value.seriesNo) {
    product.value = null
    return
  }
  try {
    const res = await axios.get(`/api/WorkHours/product-status/${encodeURIComponent(form.value.seriesNo)}`)
    product.value = res.data
    // populate some form fields to match selected product
    form.value.productName = product.value?.productName || form.value.productName
    form.value.orderNumber = product.value?.projectNo || form.value.orderNumber
  } catch (err) {
    console.error('Product fetch error', err)
    product.value = null
  }
}

function overlapMs(aStart, aEnd, bStart, bEnd) {
  const start = Math.max(aStart.getTime(), bStart.getTime())
  const end = Math.min(aEnd.getTime(), bEnd.getTime())
  return Math.max(0, end - start)
}
function computeHoursMinusLunch(start, end) {
  let totalMs = end.getTime() - start.getTime()
  // subtract overlap with lunch (12:00-12:30) for each day spanned
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

let isSubmitting = ref(false)

async function startWork(){
  if (isSubmitting.value) return
  if (!form.value.seriesNo) { alert('Please select a Series No.'); return }
  if (!form.value.process) { alert('Please select a Process'); return }
  if (coworkerConflict.value){ alert('One or more selected co-workers have assignments today. Remove them or pick another date.'); return }

  // compute start/end datetimes
  const startStr = `${form.value.startDate}T${form.value.startTime}:00`
  const endStr = `${form.value.endDate}T${form.value.endTime}:00`
  const startDt = new Date(startStr)
  const endDt = new Date(endStr)
  if (isNaN(startDt.getTime()) || isNaN(endDt.getTime()) || endDt <= startDt) {
    alert('Please ensure start time is before end time.'); return
  }

  isSubmitting.value = true
  try {
    // determine if current user is a worker (simple heuristic: username provided and not Guest)
    const submittingUser = (username && username.value) ? username.value : 'Guest'
    const isWorkerLogin = submittingUser && submittingUser !== 'Guest'
    // Note: caller may provide a more robust role check via injected userRole if available; for now use presence

    // split into per-day records
    function pad(n){ return String(n).padStart(2,'0') }
    const firstDay = new Date(startDt.getFullYear(), startDt.getMonth(), startDt.getDate())
    const lastDay = new Date(endDt.getFullYear(), endDt.getMonth(), endDt.getDate())

    const createdIds = []
    const createdRecords = []

    for (let d = new Date(firstDay); d <= lastDay; d.setDate(d.getDate() + 1)) {
      const isFirst = d.getFullYear() === startDt.getFullYear() && d.getMonth() === startDt.getMonth() && d.getDate() === startDt.getDate()
      const isLast = d.getFullYear() === endDt.getFullYear() && d.getMonth() === endDt.getMonth() && d.getDate() === endDt.getDate()

      const dayStart = isFirst ? startDt : new Date(d.getFullYear(), d.getMonth(), d.getDate(), parseInt(form.value.startTime.split(':')[0]||0), parseInt(form.value.startTime.split(':')[1]||0), 0)
      const dayEnd = isLast ? endDt : new Date(d.getFullYear(), d.getMonth(), d.getDate(), parseInt(form.value.endTime.split(':')[0]||0), parseInt(form.value.endTime.split(':')[1]||0), 0)

      if (isNaN(dayStart.getTime()) || isNaN(dayEnd.getTime()) || dayEnd <= dayStart) { continue }

      const dayDateStr = `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())}`
      const sStr = `${dayDateStr}T${pad(dayStart.getHours())}:${pad(dayStart.getMinutes())}:00`
      const eStr = `${dayDateStr}T${pad(dayEnd.getHours())}:${pad(dayEnd.getMinutes())}:00`
      const hrs = computeHoursMinusLunch(dayStart, dayEnd)

      const payload = {
        SerialNo: form.value.seriesNo,
        WorkerName: submittingUser,
        ProcessName: form.value.process,
        Hours: hrs,
        StartTime: sStr,
        EndTime: eStr,
        IfToInformProductionManager: !!isWorkerLogin
      }

      try {
        const res = await axios.post('/api/WorkHours/submit-work-hours', payload)
        // backend may return flat { id } or wrapped { data: { id } } or { data: { Id } }
        const id = res?.data?.id ?? res?.data?.data?.id ?? res?.data?.data?.Id ?? res?.data?.data?.ID ?? null
        if (id) createdIds.push({ id, date: dayDateStr })
        createdRecords.push({ payload, response: res?.data })
      } catch (e) {
        console.error('Failed to create workhour for day', dayDateStr, e)
        // continue creating remaining days
      }
    }

    // also create records for selected co-workers (one record per day per co-worker)
    if (Array.isArray(form.value.coWorkers) && form.value.coWorkers.length) {
      const coWorkersToCreate = form.value.coWorkers.filter(cw => cw && cw !== submittingUser)
      for (const cw of coWorkersToCreate) {
        for (const rec of createdRecords) {
          try {
            const coPayload = {
              SerialNo: form.value.seriesNo,
              WorkerName: cw,
              ProcessName: form.value.process,
              Hours: rec.payload.Hours,
              StartTime: rec.payload.StartTime,
              EndTime: rec.payload.EndTime,
              IfToInformProductionManager: !!isWorkerLogin
            }
            const cres = await axios.post('/api/WorkHours/submit-work-hours', coPayload)
            console.log('Created co-worker workhour for', cw, cres.data)
          } catch (ce) {
            console.error('Failed to create co-worker workhour for', cw, ce)
          }
        }
      }
    }

    // Choose today's created id if present, otherwise pick the first created id
    const todayStr = new Date().toISOString().slice(0,10)
    let chosen = null
    if (createdIds.length) {
      const todayRec = createdIds.find(x => x.date === todayStr)
      chosen = todayRec ? todayRec.id : createdIds[0].id
    }

    // emit to parent with created primary workHour id so it can switch to counting and pass id to TimerClock
    emit('start-work-and-switch', { workHourId: chosen, serialNo: form.value.seriesNo, process: form.value.process })

  } catch (err) {
    console.error('Failed to create work hour', err)
    alert('Failed to start work. See console for details.')
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <div class="workseat-container">
    <h2>Self-service Production Scheduling</h2>

    <div class="form-row">
      <label for="seriesNo">Select Series No.</label>
      <select id="seriesNo" v-model="form.seriesNo" @change="onSerialChange">
        <option value="" disabled>Select Series No.</option>
        <option v-for="series in seriesNos" :key="series" :value="series">{{ series }}</option>
      </select>
    </div>

    <div v-if="product" class="product-info" style="margin-top:1em;">
      <h3>Product Info</h3>
      <div class="product-grid">
        <div><strong>SerialNo:</strong> {{ product.serialNo }}</div>
        <div><strong>ProjectNo:</strong> {{ product.projectNo }}</div>
        <div><strong>IvkNo:</strong> {{ product.ivkNo || '-' }}</div>
        <div><strong>ModalityType:</strong> {{ product.modalityType || '-' }}</div>
        <div><strong>SystemType:</strong> {{ product.systemType }}</div>
        <div class="current-state" style="grid-column: 1 / span 3; text-align: center; margin-top: 0.5vw;">
          <strong>Current State:</strong> {{ product.workingProcess || '-' }}
        </div>
      </div>
    </div>

    <form class="product-form" style="margin-top:1.5em;">
      <div class="form-row-columns">
        <div class="form-row">
          <label for="process">Process</label>
          <select id="process" v-model="form.process">
            <option value="" disabled>Select Process</option>
            <option v-for="proc in processes" :key="proc" :value="proc">{{ proc }}</option>
          </select>
        </div>
        <div class="form-row">
          <label for="workSeat">Work Seat</label>
          <!-- visually dimmed to indicate low priority -->
          <select id="workSeat" v-model="form.workSeat" class="dimmed">
            <option value="" disabled>Select Work Seat</option>
            <option v-for="seat in workSeats" :key="seat" :value="seat">{{ seat }}</option>
          </select>
        </div>
        <div class="form-row">
          <label for="coWorkers">Co-workers</label>
          <div class="co-worker-toggle">
            <label class="switch">
              <input type="checkbox" v-model="showCoWorker">
              <span class="slider"></span>
            </label>
            <span class="toggle-label">Show Co-workers</span>
          </div>
          <select id="coWorkers" v-model="form.coWorkers" multiple size="3" v-if="showCoWorker">
            <option v-for="worker in coWorkers" :key="worker" :value="worker">{{ worker }}</option>
          </select>
          <small v-if="showCoWorker">Select one or more co-workers if this process requires multiple people.</small>
          <div v-if="coworkerConflict && coworkerConflictMessages.length" class="cw-conflict">
            <strong>Warning:</strong>
            <ul>
              <li v-for="(m,i) in coworkerConflictMessages" :key="i">{{ m }}</li>
            </ul>
          </div>
        </div>
      </div>

      <!-- Start / End grouped as 2-line inputs -->
      <div class="datetime-group">
        <div class="datetime-block">
          <label>Start</label>
          <input type="date" v-model="form.startDate" />
          <input type="time" v-model="form.startTime" />
        </div>
        <div class="datetime-block">
          <label>End</label>
          <input type="date" v-model="form.endDate" />
          <input type="time" v-model="form.endTime" />
        </div>
      </div>

      <div class="form-actions">
        <button type="button" class="start-work-btn" @click="startWork" :disabled="coworkerConflict">Start Work</button>
      </div>
    </form>
  </div>
</template>

<style scoped>
  .product-form {
    max-width: 600px;
    margin: 2em auto 0 auto;
    padding: 2em;
    background: #f9f9f9;
    border-radius: 8px;
    box-shadow: 0 2px 8px #0001;
  }
  .form-row-columns {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 1.5em;
    margin-bottom: 1em;
  }
  .form-row {
    display: flex;
    flex-direction: column;
    margin-bottom: 1em;
  }
  .product-grid { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 0.7vw 2vw; align-items: center; margin-bottom: 1vw; }
  .current-state { font-size: 1.1em; color: #EC6602; font-weight: 700; }
  .form-row label { margin-bottom: 0.5em; font-weight: 500; }
  .form-row input,
  .form-row select {
    padding: 0.5em;
    border: 1px solid #ccc;
    border-radius: 4px;
    font-size: 1em;
  }

  /* Make the Co-workers select control higher */
  #coWorkers {
    min-height: 6em;
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
    border-radius: 4px;
    font-size: 1em;
    cursor: pointer;
    font-weight: bold;
    transition: background 0.2s;
  }
  .start-work-btn:hover {
    background: #36976b;
  }
  /* visually deemphasize Work Seat selection (still selectable) */
  .dimmed {
    opacity: 0.6;
    background: #fafafa;
    border-color: #e6e6e6;
  }
  /* Start/End should be two-line blocks: date on first line, time on second */
  .datetime-group { display: flex; gap: 1em; margin-bottom: 1em; }
  .datetime-block { display: flex; flex-direction: column; }
  .datetime-block input[type="date"] { margin-top: 0.5em; margin-bottom: 0.4em; }
  .datetime-block input[type="time"] { margin-top: 0; }

  /* Toggle switch for co-workers visibility */
  .co-worker-toggle {
    display: flex;
    align-items: center;
    margin-top: 0.5em;
    margin-bottom: 1em;
  }
  .switch {
    position: relative;
    display: inline-block;
    width: 34px;
    height: 20px;
    margin-right: 0.5em;
  }
  .switch input {
    opacity: 0;
    width: 0;
    height: 0;
  }
  .slider {
    position: absolute;
    cursor: pointer;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background-color: #ccc;
    transition: .4s;
    border-radius: 34px;
  }
  .slider:before {
    position: absolute;
    content: "";
    height: 12px;
    width: 12px;
    left: 4px;
    bottom: 4px;
    background-color: white;
    border-radius: 50%;
    transition: .4s;
  }
  input:checked + .slider {
    background-color: #42b883;
  }
  input:checked + .slider:before {
    transform: translateX(14px);
  }
  .toggle-label {
    font-size: 0.9em;
    color: #333;
  }
  .cw-conflict{ margin-top:8px; background:#fff3cd; border:1px solid #ffeeba; padding:8px; border-radius:6px }
  .cw-conflict strong{ color:#856404 }
</style>
