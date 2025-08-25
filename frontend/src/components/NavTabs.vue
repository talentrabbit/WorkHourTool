<script setup>
import { ref, provide, watch, computed, onMounted, onUnmounted } from 'vue'
import axios from 'axios'
import WorkSeat from './WorkSeat.vue'
import WorkHour from './WorkHour.vue'

const activeTab = ref('product')
// Shared state for timer running
const isCountingTimerActive = ref(false)
provide('isCountingTimerActive', isCountingTimerActive)

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
const username = ref(localStorage.getItem('username') || 'Guest')
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

async function fetchIdentityAndOptions(){
  try {
    const res = await axios.get('/api/auth/current-user', { withCredentials: true })
    if (res?.data?.user) username.value = res.data.user
  } catch {}
  try {
    const workers = await axios.get('/api/workhours/all-worker-names')
    workerNames.value = Array.isArray(workers.data) ? workers.data : []
  } catch {}
}

function overlaps(aStart, aEnd, bStart, bEnd){
  return Math.max(aStart.getTime(), bStart.getTime()) < Math.min(aEnd.getTime(), bEnd.getTime())
}

async function checkTodayAssignments(){
  checkedToday.value = true
  todayAssignments.value = []
  const worker = matchedWorkerName.value || username.value
  if (!worker) { planningMode.value = 'wait'; return }
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

watch([isWorker, activeTab], async ([w, tab]) => {
  if (w && tab === 'product' && !checkedToday.value) {
    await checkTodayAssignments()
  }
})

onMounted(fetchIdentityAndOptions)
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
      <button :class="{active: activeTab === 'product'}" @click="activeTab = 'product'">Work Hours Planning</button>
      <button :class="{active: activeTab === 'counting'}" @click="activeTab = 'counting'">Work Hour Counting Tool</button>
      <button :class="{active: activeTab === 'intro'}" @click="activeTab = 'intro'">Department Introduction</button>
    </div>
    <div class="tab-content">
      <div v-if="activeTab === 'product'">
        <template v-if="isWorker">
          <div v-if="planningMode === 'view'" class="today-assignment">
            <h3>Today's Arrangements for {{ matchedWorkerName || username }}</h3>
            <vxe-table :data="todayAssignments" border stripe round class="modern-vxe-table">
              <vxe-column field="serialNo" title="SerialNo" width="120" />
              <vxe-column field="systemType" title="SystemType" width="140" />
              <vxe-column field="processName" title="Process" width="160" />
              <vxe-column field="coWorkerName" title="Co-worker" width="160" />
              <vxe-column field="startTime" title="Start Time" width="180" />
              <vxe-column field="endTime" title="End Time" width="180" />
            </vxe-table>
          </div>
          <div v-else-if="planningMode === 'edit'">
            <WorkSeat />
          </div>
          <div v-else class="wait-dim">
            Wait for arrangement from Production Manager
          </div>
        </template>
        <template v-else>
          <WorkSeat @start-work-and-switch="handleStartWorkAndSwitch" />
        </template>
      </div>

      <div v-if="activeTab === 'counting'">
        <WorkHour />
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
.hero-img { width: 100%; height: 100%; object-fit: cover; transition: opacity 0.5s; }
.nav-tabs-horizontal { display: flex; gap: 1em; background: #FFF0E4; padding: 0.5em 1em; border-bottom: 1px solid #F2C7A6; box-shadow: inset 0 -1px 0 #F2C7A6; }
.nav-tabs-horizontal button { padding: 0.75em 1.5em; border: none; background: #FFE6D3; cursor: pointer; border-radius: 8px 8px 0 0; font-size: 1em; color: #82451F; box-shadow: 0 2px 6px rgba(236,102,2,0.12); transition: transform 0.1s; }
.nav-tabs-horizontal button:hover { transform: translateY(-1px); }
.nav-tabs-horizontal button.active { background: #FFFFFF; border: 1px solid #F2C7A6; border-bottom: 2px solid #EC6602; font-weight: 700; color: #A64E00; }
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
  border-radius: 4px;
  font-size: 1em;
  cursor: pointer;
  font-weight: bold;
  transition: background 0.2s;
}
.start-work-btn:hover {
  background: #36976b;
}
</style>
