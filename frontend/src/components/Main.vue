<template>
  <div class="entry-container">
    <header class="hero">
      <img src="/entry.jpg" alt="Factory Hero" class="hero-img" />
      <h1 class="hero-title">SSME MI Digital Factory</h1>
      <p class="hero-desc">Empowering production managers and workers with digital tools for efficient manufacturing.</p>
    </header>
    <div class="entry-links">
      <router-link v-if="isAdmin || isManager" to="/planning" class="entry-link attention">
        <span class="icon">🚀</span>
        <span class="link-text">Production Planning</span>
      </router-link>

      <router-link v-if="isWorker || isAdmin" to="/worker" class="entry-link attention">
        <span class="icon">🛠️</span>
        <span class="link-text">Worker</span>
      </router-link>

      <router-link v-if="isAdmin || isManager" to="/maintenance" class="entry-link attention">
        <span class="icon">🧰</span>
        <span class="link-text">WorkHour Maintenance</span>
      </router-link>

      <!-- New: Register Product -->
      <router-link v-if="isAdmin" to="/product-register" class="entry-link attention">
        <span class="icon">📦</span>
        <span class="link-text">Register Product</span>
      </router-link>

      <!-- New: NCM Time -->
      <router-link v-if="isProcess || isAdmin" to="/ncm" class="entry-link attention">
        <span class="icon">🧾</span>
        <span class="link-text">NCM Time</span>
      </router-link>

      <!-- New: KanBan (in construction) -->
      <router-link to="/kanban" class="entry-link attention">
        <span class="icon">📋</span>
        <span class="link-text">KanBan</span>
      </router-link>
    </div>
  </div>
</template>

<script setup>
import { inject, computed } from 'vue'
const userRole = inject('userRole') || ''
const roleVal = computed(() => (typeof userRole === 'object' && 'value' in userRole) ? (userRole.value || '') : (userRole || ''))
const isWorker = computed(() => roleVal.value.toLowerCase() === 'worker')
const isManager = computed(() => roleVal.value.toLowerCase() === 'productionmanager')
const isAdmin = computed(() => roleVal.value.toLowerCase() === 'administrator')
const isProcess = computed(() => { const r = roleVal.value.toLowerCase(); return r === 'process' || r === 'processengineer' })
</script>

<style scoped>

  .entry-container {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    min-height: 100%;
    width: 100%;
    /* background removed to inherit from portal */
    padding: 0;
  }
  .hero {
    display: flex;
    flex-direction: column;
    align-items: center;
    margin-top: 2vw;
    margin-bottom: 2vw;
    width: 60vw;
    max-width: 1200px;
  }
  .hero-img {
    width: 50vw;
    max-width: 960px;
    min-width: 480px;
    margin-bottom: 1.2rem;
    border-radius: 18px;
    box-shadow: 0 4px 32px rgba(236, 102, 2, 0.15);
  }
.hero-title {
  font-size: 2.5rem;
  font-weight: 800;
  color: #EC6602;
  margin-bottom: 0.5rem;
}
.hero-desc {
  font-size: 1.15rem;
  color: #6b7280;
  margin-bottom: 0.5rem;
  text-align: center;
  max-width: 400px;
}
  /* Make entry links wrap into multiple rows when there are many visible buttons */
  .entry-links {
    display: flex;
    flex-wrap: wrap; /* allow wrapping to next line */
    gap: 1.5rem; /* consistent spacing between items */
    margin-top: 3vw;
    justify-content: center;
    width: 100%;
    max-width: 1200px;
    padding: 0 1rem;
  }
  .entry-link {
    flex: 1 1 260px; /* grow/shrink, prefer ~260px width */
    min-width: 220px;
    max-width: 340px;
    height: 70px;
    padding: 0 1.5rem;
    background: linear-gradient(90deg, #EC6602 0%, #FFA351 100%);
    color: #fff;
    border-radius: 12px;
    font-size: 1.35rem;
    font-weight: bold;
    text-decoration: none;
    box-shadow: 0 6px 18px rgba(236, 102, 2, 0.25);
    transition: transform 0.2s, box-shadow 0.2s, background 0.2s;
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 0.8rem;
    letter-spacing: 0.03em;
    border: 2px solid #EC6602;
  }
.entry-link.attention {
  animation: pulse 1.2s infinite alternate;
}
.entry-link:hover {
  background: linear-gradient(90deg, #D45500 0%, #EC6602 100%);
  transform: scale(1.08);
  box-shadow: 0 10px 28px rgba(236, 102, 2, 0.35);
}
.icon {
  font-size: 2rem;
  display: flex;
  align-items: center;
}
.link-text {
  font-size: 1.15rem;
  font-weight: 600;
}
@keyframes pulse {
  0% { box-shadow: 0 0 0 0 rgba(236, 102, 2, 0.5); }
  100% { box-shadow: 0 0 16px 6px rgba(236, 102, 2, 0.6); }
}
</style>
