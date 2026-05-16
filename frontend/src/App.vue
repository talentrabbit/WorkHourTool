<template>
  <div class="portal">
    <header class="portal-header">
      <div class="header-left">
        <img :src="logoUrl" alt="Company Logo" class="header-logo" />
        <router-link to="/" :class="['home-btn', {dimmed: isCountingTimerActive}]" title="Home" aria-label="Home">
          <img :src="homeUrl" alt="Home" class="home-btn-img" />
        </router-link>
        <div class="header-title">SSME MI Digital Factory</div>
      </div>
      <div class="header-right">
        <div class="version-info" title="Frontend / Backend version">FE: {{ frontendVersion || 'N/A' }}
          <span v-if="backendVersion"> | BE: {{ backendVersion }}</span>
        </div>
  <div class="user-info">{{ username }}</div>
  <!-- <img :src="profileIcon" alt="Profile" :class="['profile-icon', { dimmed: username === 'Guest' }]" /> -->
  <button v-if="username === 'Guest'" class="signin-btn" @click="openLogon" title="Sign in">Sign in</button>
    <button v-else class="signout-btn" @click="doSignOut" title="Sign out">Sign out</button>
      </div>
    </header>
    <div class="portal-body">
      <nav class="portal-nav">
        <router-link v-if="isAdmin || isManager" to="/planning" :class="['nav-link',{dimmed: isCountingTimerActive}]" active-class="active">Planning</router-link>
        <router-link v-if="isAdmin" to="/product-register" :class="['nav-link',{dimmed: isCountingTimerActive}]" active-class="active">Products</router-link>
        <router-link v-if="isWorker || isAdmin" to="/worker" :class="['nav-link',{dimmed: isCountingTimerActive}]" active-class="active">Work Hour Tool</router-link>
  <router-link v-if="isProcess || isAdmin || isManager" to="/ncm" :class="['nav-link',{dimmed: isCountingTimerActive}]" active-class="active">NCM Time</router-link>
        <router-link v-if="isAdmin || isManager" to="/maintenance" :class="['nav-link',{dimmed: isCountingTimerActive}]" active-class="active">Maintenance</router-link>
        <!-- Kanban entry hidden while feature is under construction -->
        <router-link v-if="false" to="/kanban" :class="['nav-link',{dimmed: isCountingTimerActive}]" active-class="active">Kanban</router-link>
        <router-link v-if="isAdmin || isManager" to="/orders" :class="['nav-link',{dimmed: isCountingTimerActive}]" active-class="active">Order Info</router-link>
      </nav>
      <main class="portal-content">
        <router-view />
      </main>

      <!-- Logon overlay: dims the portal-body until the user signs in -->
      <div v-if="overlayVisible" class="logon-overlay" role="dialog" aria-modal="true">
        <!-- show modal when actively logging on, otherwise keep the translucent shim to dim the body -->
        <div v-if="showLogon" class="logon-modal">
          <h2>Sign in</h2>
          <p class="logon-desc">Enter your full name (Zhang San) to continue.</p>
          <input v-model="logonName" class="logon-input" placeholder="DOMAIN\\username or username" @keyup.enter="doLogon" />
          <div class="logon-actions">
            <button class="btn" @click="doLogon">Log in</button>
            <button class="btn" style="background:#fff;color:#82451F;border:1px solid #E6C9B0;margin-left:12px" @click="doCancel">Cancel</button>
          </div>
        </div>
        <div v-else class="logon-shim" aria-hidden="true"></div>
      </div>

    </div>
    <footer class="portal-footer">
      <span>© 2025 SSME MI • Factory Portal</span>
    </footer>
  </div>
</template>

<script setup>
import { ref, onMounted, computed, provide, watch } from 'vue'
import axios from 'axios'
import logoUrl from './assets/company-logo.png?url'
import homeUrl from './assets/Home.jpeg?url'
import profileIcon from './assets/profile-icon-vector.jpg?url'
import { useRouter } from 'vue-router'

const username = ref(localStorage.getItem('username') || 'Guest')
const userRole = ref(localStorage.getItem('userRole') || '')
const userRoles = ref([])

try {
  const raw = localStorage.getItem('userRoles')
  const parsed = raw ? JSON.parse(raw) : []
  if (Array.isArray(parsed)) {
    userRoles.value = parsed.filter(r => typeof r === 'string' && r.trim()).map(r => r.trim())
  }
} catch {}

if (!userRoles.value.length && userRole.value) {
  userRoles.value = userRole.value.split(',').map(r => r.trim()).filter(Boolean)
}

provide('username', username)
provide('userRole', userRole)
provide('userRoles', userRoles)

// global flag to indicate a counting timer is active (provided to children)
import { ref as vueRef } from 'vue'
const isCountingTimerActive = vueRef(false)
provide('isCountingTimerActive', isCountingTimerActive)

const workerNames = ref([])
const router = useRouter()

// Frontend version provided at build time via Vite env (VITE_APP_VERSION)
const frontendVersion = ref('')
const backendVersion = ref('')

// Role-derived flags
const normalizedRoles = computed(() => userRoles.value.map(r => String(r).toLowerCase().trim()).filter(Boolean))
const hasRole = (roleName) => normalizedRoles.value.includes(String(roleName || '').toLowerCase())
const isWorker = computed(() => hasRole('worker'))
const isManager = computed(() => hasRole('productionmanager'))
const isAdmin = computed(() => hasRole('administrator'))
const isProcess = computed(() => hasRole('process') || hasRole('processengineer'))

function normalizeRolesFromResponse(data) {
  const list = []
  if (Array.isArray(data?.roles)) list.push(...data.roles)
  if (typeof data?.role === 'string') list.push(...data.role.split(','))
  return Array.from(new Set(list.map(r => String(r || '').trim()).filter(Boolean)))
}

// Logon UI state
const logonName = ref('')
const allowGuest = ref(false)
const showLogon = ref(username.value === 'Guest' && !allowGuest.value)
const overlayVisible = computed(() => showLogon.value || allowGuest.value)

async function applyUserFromResponse(data) {
    if (!data) return
    // remember previous values to avoid unnecessary navigation
    const prevUser = username.value
    const prevRole = userRole.value

    username.value = data.fullName || data.user || data.gid || 'Guest'
    const nextRoles = normalizeRolesFromResponse(data)
    userRoles.value = nextRoles
    userRole.value = nextRoles[0] || ''
    try { localStorage.setItem('username', username.value) } catch {}
    try { localStorage.setItem('userRole', userRole.value) } catch {}
    try { localStorage.setItem('userRoles', JSON.stringify(userRoles.value)) } catch {}

    // Navigate based on role but DO NOT force a full page reload.
    // Full reload caused an infinite refresh loop in some dev setups (blocked localStorage or proxy behavior).
    const r = normalizedRoles.value
    try {
      if (r.includes('productionmanager')) {
        await router.push('/planning')
      } else if (r.includes('worker')) {
        await router.push('/worker')
      } else if (r.includes('administrator')) {
        await router.push('/')
      }
    } catch (e) {
      // ignore routing errors
    }

    // Do not call window.location.reload() here — let components handle their own mounted/data refresh.
  }

async function doLogon() {
  const v = (logonName.value || '').trim()
  if (!v) return
  try {
    const res = await axios.get('/api/auth/find-user', { params: { q: v } })
    if (res?.data) {
      applyUserFromResponse(res.data)
      showLogon.value = false
      allowGuest.value = false
      return
    }
  } catch (err) {
    if (err?.response?.status === 404) {
      alert('No matching user found')
      return
    }
    console.error(err)
    alert(`Failed to look up user: ${err.message}`)
    return
  }
}

function doSignOut() {
  try { localStorage.removeItem('username') } catch {}
  try { localStorage.removeItem('userRole') } catch {}
  try { localStorage.removeItem('userRoles') } catch {}
  username.value = 'Guest'
  userRole.value = ''
  userRoles.value = []
  allowGuest.value = false
  showLogon.value = true
}

function doCancel() {
  allowGuest.value = true
  showLogon.value = false
}

function openLogon() {
  allowGuest.value = false
  logonName.value = ''
  showLogon.value = true
}

watch(username, (nv) => {
  showLogon.value = (!nv || nv === 'Guest') && !allowGuest.value
})

onMounted(async () => {
  loadFrontendVersion()
  // trigger Negotiate handshake first (non-blocking)
  // try {
  //   await axios.get('/api/auth/challenge')
  // } catch {}

  // Attempt to read current-user (may be anonymous)
  try {
    const res = await axios.get('/api/auth/current-user')
    if (res?.data?.user) {
      applyUserFromResponse(res.data)
    }
  } catch (e) {
    // ignore
  }

  // load worker names for legacy matching if needed
  try {
    const workers = await axios.get('/api/workhours/all-worker-names')
    workerNames.value = Array.isArray(workers.data) ? workers.data : []
  } catch {}

  // Read backend version via API so FE and BE can be deployed independently
  try {
    const vres = await axios.get('/api/WorkHours/version')
    if (vres && vres.data) {
      backendVersion.value = vres.data.backend || vres.data.version || ''
    }
  } catch (e) {
    // ignore — backend version optional
  }

  // Frontend version is resolved at build-time; no runtime fetch needed.
})

function loadFrontendVersion() {
  fetch('/frontend-version.xml', { cache: 'no-cache' })
    .then(async (res) => {
      if (!res.ok) throw new Error(`HTTP ${res.status}`)
      const text = await res.text()
      const parser = new DOMParser()
      const doc = parser.parseFromString(text, 'application/xml')
      const node = doc.querySelector('version')
      const val = node?.textContent?.trim()
      frontendVersion.value = val || ''
    })
    .catch(err => {
      console.warn('Failed to load frontend-version.xml', err)
      frontendVersion.value = import.meta.env.VITE_APP_VERSION || frontendVersion.value || ''
    })
}
</script>

<style scoped>
/* Root: fill width, allow page-level scrolling (no inner scrollbars) */
.portal {
  width: 100vw;
  max-width: 100%;
  min-height: 100vh;
  height: auto;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  background: linear-gradient(180deg, #FFF7EF 0%, #FFFFFF 100%);
  box-sizing: border-box;
  overflow: visible;
}

/* Header */
.portal-header { 
  /* header height clamps up to 120px max */
  --header-h: clamp(64px, 8vh, 120px);
  height: var(--header-h);
  min-height: var(--header-h);
  display: flex; align-items: center; justify-content: space-between; padding: 0 16px; background: #FFFFFF; border-bottom: 1px solid #F2C7A6; box-shadow: 0 2px 8px rgba(236,102,2,0.08);
}
.header-left { display: flex; align-items: center; gap: 12px; }
.home-btn { width: var(--header-h); height: var(--header-h); padding: 0; display: inline-flex; align-items: center; justify-content: center; font-size: 28px; }
.header-logo { height: var(--header-h); max-height: 120px; object-fit: contain; filter: drop-shadow(0 1px 3px rgba(236,102,2,0.25)); }
/* Home button image sizing */
.home-btn-img { max-height: calc(var(--header-h) - 18px); max-width: calc(var(--header-h) - 18px); object-fit: contain; display: block; }
.header-title {
  margin-left: 8px;
  /* keep requested compact header title style */
  font-size: 14px;
  font-weight: 600;
  letter-spacing: .5px;
  font-family: Calibri, 'Segoe UI', Arial, sans-serif;
  color: #EC6602;
  white-space: nowrap; overflow: hidden; text-overflow: ellipsis; /* prevent wrapping */
}
.header-right { display: flex; align-items: center; gap: 16px; }
/* Version display in the header */
.version-info { color: #6b6b6b; font-size: 0.85rem; background: #fff; border: 1px solid transparent; padding: 6px 8px; border-radius: 8px }
.version-info { white-space: nowrap }
.user-info { color: #82451F; font-weight: 600; background: #FFF3E8; border: 1px solid #F2C7A6; padding: 6px 10px; border-radius: 8px; }
.signout-btn { margin-left: 12px; background: transparent; border: 1px solid #E6C9B0; color: #82451F; padding: 6px 10px; border-radius: 8px; cursor: pointer; font-weight: 600; }
.signin-btn { margin-left: 6px; background: #EC6602; border: none; color: #fff; padding: 6px 10px; border-radius: 8px; cursor: pointer; font-weight: 600; }
.signin-btn:hover { opacity: 0.95 }
/* Profile icon next to sign-in */
.profile-icon { width: 24px; height: 24px; object-fit: cover; border-radius: 4px; margin-left: 6px; }
/* Dimmed appearance for guests */
.profile-icon.dimmed { opacity: 0.45; filter: grayscale(70%); }

/* Body */
.portal-body {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
  width: 100%;
  position: relative;
}
.portal-nav {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  padding: 14px 18px 10px;
  background: #FFF0E4;
  border-bottom: 1px solid #F2C7A6;
  box-shadow: inset 0 -1px 0 #F2C7A6;
  overflow-x: auto;
}
.portal-nav::-webkit-scrollbar { height: 6px; }
.portal-nav::-webkit-scrollbar-thumb { background: rgba(236,102,2,0.4); border-radius: 999px; }
.nav-link {
  display: inline-flex;
  align-items: center;
  padding: 8px 14px;
  color: #82451F;
  text-decoration: none;
  border-radius: 999px;
  background: #FFE6D3;
  box-shadow: 0 1px 4px rgba(236,102,2,0.08);
  font-weight: 600;
  font-size: 13px;
  white-space: nowrap;
}
.nav-link:hover { transform: translateY(-1px); background: #FFD9BB; }
.nav-link.active { background: #FFFFFF; border: 1px solid #F2C7A6; color: #A64E00; }
.nav-link.dimmed, .home-btn.dimmed { opacity: 0.45; pointer-events: none; }

/* Main Content */
.portal-content {
  flex: 1;
  min-width: 0;
  padding: 16px 18px 32px;
  overflow: visible;
}

/* Make sub-page titles a bit smaller for denser UI (apply to h2 and h3) */
.portal-content h2, .portal-content h3 {
  font-size: 1.1rem; /* approx 17.6px */
  margin-top: 0.25rem;
  margin-bottom: 0.6rem;
  /* remove bold */
  font-weight: 400;
  /* WordArt-like subtle style: gradient fill + shadow + slight skew */
  color: transparent;
  background: linear-gradient(90deg, #EC6602 0%, #82451F 60%);
  -webkit-background-clip: text;
  background-clip: text;
  text-shadow: 2px 2px 0 rgba(0,0,0,0.06), 0 6px 14px rgba(0,0,0,0.08);
  display: inline-block;
  transform: skewX(-4deg);
}

/* Footer */
.portal-footer { height: 40px; min-height: 40px; background: #FFFFFF; border-top: 1px solid #F2C7A6; display: flex; align-items: center; justify-content: center; color: #82451F; font-size: 0.9rem; }

/* Logon overlay (covers portal-body only so header/footer remain visible) */
.logon-overlay { position: absolute; inset: 0; background: rgba(0,0,0,0.45); display: flex; align-items: center; justify-content: center; z-index: 50; }
.logon-modal { width: 420px; background: #fff; border-radius: 12px; padding: 24px; box-shadow: 0 8px 24px rgba(0,0,0,0.25); text-align: center; }
.logon-modal h2 { margin: 0 0 8px 0; color: #EC6602; }
.logon-desc { margin: 0 0 16px 0; color: #5a3b27; }
.logon-input { width: 100%; padding: 10px 12px; border: 1px solid #E6C9B0; border-radius: 8px; margin-bottom: 16px; }
 .logon-actions{ display:flex; justify-content:center }
.btn { background: #EC6602; color: #fff; border: none; padding: 10px 18px; border-radius: 8px; font-weight: 700; cursor: pointer; }
.btn:hover { opacity: 0.95 }

/* Responsive tweaks */
@media (max-width: 1600px) {
  .portal-nav { gap: 6px; padding: 12px 16px; }
}

@media (max-width: 1366px) {
  .portal-nav { gap: 6px; padding: 10px 14px; }
  .header-logo { height: 56px; }
  .logon-modal { width: 360px }
}

@media (max-width: 1024px) {
  .portal-nav { overflow-x: scroll; }
  .portal-content { padding: 12px; }
  .header-title { font-size: 1rem; }
  .logon-modal { width: 320px }
}
</style>
