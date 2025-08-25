<template>
  <div class="portal">
    <header class="portal-header">
      <div class="header-left">
        <img :src="logoUrl" alt="Company Logo" class="header-logo" />
        <router-link to="/" class="home-btn" title="Home" aria-label="Home">🏠</router-link>
      </div>
      <div class="header-title">SSME MI Digital Factory</div>
      <div class="header-right">
        <div class="user-info">Signed in as {{ username }}</div>
      </div>
    </header>
    <div class="portal-body">
      <aside class="portal-nav">
        <nav>
          <router-link v-if="!isWorker" to="/planning" class="nav-link" active-class="active">Production Planning</router-link>
          <router-link to="/worker" class="nav-link" active-class="active">Work Hour Tool</router-link>
          <router-link v-if="!isWorker" to="/maintenance" class="nav-link" active-class="active">WorkHour Maintenance</router-link>
        </nav>
      </aside>
      <main class="portal-content">
        <router-view />
      </main>
    </div>
    <footer class="portal-footer">
      <span>© 2025 SSME MI • Internal Portal</span>
    </footer>
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from 'vue'
import axios from 'axios'
import logoUrl from '../company-logo.png?url'
const username = ref(localStorage.getItem('username') || 'Guest')
const workerNames = ref([])
const isWorker = computed(() => {
  const norm = s => (s || '').toLowerCase().replace(/[^a-z0-9]/g, '')
  const u = norm(username.value)
  return workerNames.value.some(w => u.includes(norm(w)))
})

onMounted(async () => {
  try {
    const res = await axios.get('/api/auth/current-user', { withCredentials: true })
    if (res?.data?.user) {
      username.value = res.data.user
    }
  } catch {}
  try {
    const workers = await axios.get('/api/workhours/all-worker-names')
    workerNames.value = Array.isArray(workers.data) ? workers.data : []
  } catch {}
})
</script>

<style scoped>
/* Root: fixed to viewport 16:9, scalable */
.portal { width: 100vw; height: 100vh; display: flex; flex-direction: column; background: linear-gradient(180deg, #FFF7EF 0%, #FFFFFF 100%); }

/* Header */
.portal-header { height: 72px; min-height: 72px; display: flex; align-items: center; justify-content: space-between; padding: 0 16px; background: #FFFFFF; border-bottom: 1px solid #F2C7A6; box-shadow: 0 2px 8px rgba(236,102,2,0.08); }
.header-left { display: flex; align-items: center; gap: 12px; }
.home-btn { width: 64px; height: 64px; padding: 0; display: inline-flex; align-items: center; justify-content: center; font-size: 28px; }
.header-logo { height: 72px; object-fit: contain; filter: drop-shadow(0 1px 3px rgba(236,102,2,0.25)); }
.header-title { font-size: 1.25rem; font-weight: 800; color: #EC6602; letter-spacing: 0.02em; }
.header-right { display: flex; align-items: center; gap: 16px; }
.user-info { color: #82451F; font-weight: 600; background: #FFF3E8; border: 1px solid #F2C7A6; padding: 6px 10px; border-radius: 8px; }

/* Body */
.portal-body { flex: 1; display: flex; min-height: 0; }
.portal-nav { width: 260px; min-width: 240px; background: #FFF0E4; border-right: 1px solid #F2C7A6; padding: 16px 10px; box-shadow: inset -1px 0 0 #F2C7A6; overflow-y: auto; }
.portal-nav nav { display: flex; flex-direction: column; gap: 8px; }
.nav-link { display: block; padding: 10px 12px; color: #82451F; text-decoration: none; border-radius: 8px; background: #FFE6D3; box-shadow: 0 1px 4px rgba(236,102,2,0.08); font-weight: 600; }
.nav-link:hover { transform: translateY(-1px); background: #FFD9BB; }
.nav-link.active { background: #FFFFFF; border: 1px solid #F2C7A6; color: #A64E00; }

/* Main Content */
.portal-content { flex: 1; min-width: 0; padding: 16px; overflow: auto; }

/* Footer */
.portal-footer { height: 40px; min-height: 40px; background: #FFFFFF; border-top: 1px solid #F2C7A6; display: flex; align-items: center; justify-content: center; color: #82451F; font-size: 0.9rem; }
</style>
