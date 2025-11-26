<template>
  <div class="portal">
    <header class="portal-header">
      <div class="header-left">
        <img :src="logoUrl" alt="Company Logo" class="header-logo" />
        <router-link to="/" :class="['home-btn', {dimmed: isCountingTimerActive}]" title="Home" aria-label="Home">🏠</router-link>
      </div>
      <div class="header-title">SSME MI Digital Factory</div>
      <div class="header-right">
        <div class="version-info" title="Frontend / Backend version">FE: {{ frontendVersion }}
          <span v-if="backendVersion"> | BE: {{ backendVersion }}</span>
        </div>
        <div class="user-info">{{ username }}</div>
        <button v-if="username === 'Guest'" class="signin-btn" @click="openLogon" title="Sign in">Sign in</button>
        <button v-else class="signout-btn" @click="doSignOut" title="Sign out">Sign out</button>
      </div>
    </header>
    <div class="portal-body">
      <aside  class="portal-nav">
        <nav>
          <router-link v-if="isAdmin || isManager" to="/planning" :class="['nav-link',{dimmed: isCountingTimerActive}]" active-class="active">Planning</router-link>
          <router-link v-if="isAdmin" to="/product-register" :class="['nav-link',{dimmed: isCountingTimerActive}]" active-class="active">Products</router-link>
          <router-link v-if="isWorker || isAdmin" to="/worker" :class="['nav-link',{dimmed: isCountingTimerActive}]" active-class="active">Work Hour Tool</router-link>
          <router-link v-if="isProcess || isAdmin" to="/ncm" :class="['nav-link',{dimmed: isCountingTimerActive}]" active-class="active">NCM Time</router-link>
          <router-link v-if="isAdmin || isManager" to="/maintenance" :class="['nav-link',{dimmed: isCountingTimerActive}]" active-class="active">Maintenance</router-link>
          <!-- Kanban entry hidden while feature is under construction -->
          <router-link v-if="false" to="/kanban" :class="['nav-link',{dimmed: isCountingTimerActive}]" active-class="active">Kanban</router-link>
          <router-link v-if="isAdmin || isManager" to="/orders" :class="['nav-link',{dimmed: isCountingTimerActive}]" active-class="active">Order Info</router-link>
        </nav>
      </aside>
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
import logoUrl from '../company-logo.png?url'
import { useRouter } from 'vue-router'

const username = ref(localStorage.getItem('username') || 'Guest')
const userRole = ref(localStorage.getItem('userRole') || '')
provide('username', username)
provide('userRole', userRole)

// global flag to indicate a counting timer is active (provided to children)
import { ref as vueRef } from 'vue'
const isCountingTimerActive = vueRef(false)
provide('isCountingTimerActive', isCountingTimerActive)

const workerNames = ref([])
const router = useRouter()

// Frontend version provided at build time via Vite env (VITE_APP_VERSION)
const frontendVersion = ref(import.meta.env.VITE_APP_VERSION || '')
const backendVersion = ref('')

// Role-derived flags
const isWorker = computed(() => (userRole.value || '').toLowerCase() === 'worker')
const isManager = computed(() => (userRole.value || '').toLowerCase() === 'productionmanager')
const isAdmin = computed(() => (userRole.value || '').toLowerCase() === 'administrator')
// Process role (accept both 'process' and 'processengineer' values returned by find-user)
const isProcess = computed(() => {
  const r = (userRole.value || '').toLowerCase()
  return r === 'process' || r === 'processengineer'
})

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
    userRole.value = data.role || (Array.isArray(data.roles) && data.roles[0]) || ''
    try { localStorage.setItem('username', username.value) } catch {}
    try { localStorage.setItem('userRole', userRole.value) } catch {}

    // Navigate based on role but DO NOT force a full page reload.
    // Full reload caused an infinite refresh loop in some dev setups (blocked localStorage or proxy behavior).
    const r = (userRole.value || '').toLowerCase()
    try {
      if (r === 'productionmanager') {
        await router.push('/planning')
      } else if (r === 'worker') {
        await router.push('/worker')
      } else if (r === 'administrator') {
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
  username.value = 'Guest'
  userRole.value = ''
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
</script>

<style scoped>
/* Root: fill width, allow page-level scrolling (no inner scrollbars) */
.portal {
  width: 100vw;
  max-width: 100%; /* fill full screen width */
  min-height: 100vh; /* at least viewport height; grows with content */
  height: auto; /* allow natural growth so page scroll is used */
  margin: 0;
  padding-left: 12px; /* keep a small left gutter so content isn't flush against the window */
  display: flex;
  flex-direction: column;
  background: linear-gradient(180deg, #FFF7EF 0%, #FFFFFF 100%);
  box-sizing: border-box;
  overflow: visible; /* don't trap scroll; let body handle it */
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
.header-title {
  flex: 1; /* let title take remaining space between left/right */
  text-align: center;
  font-size: clamp(16px, 2vw, 28px); /* responsive, capped */
  font-weight: 800; color: #EC6602; letter-spacing: 0.02em;
  white-space: nowrap; overflow: hidden; text-overflow: ellipsis; /* prevent wrapping */
}
.header-right { display: flex; align-items: center; gap: 16px; }
/* Version display in the header */
.version-info { color: #6b6b6b; font-size: 0.85rem; background: #fff; border: 1px solid transparent; padding: 6px 8px; border-radius: 8px }
.version-info { white-space: nowrap }
.user-info { color: #82451F; font-weight: 600; background: #FFF3E8; border: 1px solid #F2C7A6; padding: 6px 10px; border-radius: 8px; }
.signout-btn { margin-left: 12px; background: transparent; border: 1px solid #E6C9B0; color: #82451F; padding: 6px 10px; border-radius: 8px; cursor: pointer; font-weight: 600; }
.signin-btn { margin-left: 12px; background: #EC6602; border: none; color: #fff; padding: 6px 10px; border-radius: 8px; cursor: pointer; font-weight: 600; }
.signin-btn:hover { opacity: 0.95 }

/* Body */
.portal-body { flex: 1; display: flex; min-height: 0; width: 100%; position: relative; }
.portal-nav { width: 200px; min-width: 200px; background: #FFF0E4; border-right: 1px solid #F2C7A6; padding: 14px 8px; box-shadow: inset -1px 0 0 #F2C7A6; overflow-y: auto; }
.portal-nav nav { display: flex; flex-direction: column; gap: 8px; }
.nav-link { display: block; padding: 10px 12px; color: #82451F; text-decoration: none; border-radius: 8px; background: #FFE6D3; box-shadow: 0 1px 4px rgba(236,102,2,0.08); font-weight: 600; }
.nav-link:hover { transform: translateY(-1px); background: #FFD9BB; }
.nav-link.active { background: #FFFFFF; border: 1px solid #F2C7A6; color: #A64E00; }
.nav-link.dimmed, .home-btn.dimmed { opacity: 0.45; pointer-events: none; }

/* Main Content */
.portal-content { flex: 1; min-width: 0; padding: 0px; overflow: visible; }

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
  .portal { padding: 0 8px; }
  .portal-nav { width: 180px; min-width: 180px; }
}

@media (max-width: 1366px) {
  .portal { padding: 0 8px; }
  .portal-nav { width: 170px; min-width: 170px; }
  .header-logo { height: 56px; }
  .logon-modal { width: 360px }
}

@media (max-width: 1024px) {
  .portal-nav { display: none; }
  .portal-content { padding: 12px; }
  .header-title { font-size: 1rem; }
  .logon-modal { width: 320px }
}
</style>
