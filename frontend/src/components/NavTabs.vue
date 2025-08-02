<script setup>
import { ref, provide, watch } from 'vue'
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
import { onMounted, onUnmounted } from 'vue'
let heroInterval = null
onMounted(() => {
  heroInterval = setInterval(nextHero, 5000)
})
onUnmounted(() => {
  if (heroInterval) clearInterval(heroInterval)
})
</script>

<template>
  <div class="nav-layout">
    <div class="nav-header">
      <img src="/company-logo.png" alt="Company Trademark" class="company-logo" />
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
        <WorkSeat @start-work-and-switch="handleStartWorkAndSwitch" />
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
.nav-layout {
  display: flex;
  flex-direction: column;
  min-height: 100vh;
}
.nav-header {
  display: flex;
  align-items: center;
  padding: 1em 2em 0.5em 1em;
}
.nav-header-spacer {
  flex: 1;
}
.company-logo {
  width: 33vw;
  max-width: 400px;
  min-width: 120px;
  height: auto;
  object-fit: contain;
  margin-right: 1em;
}
.title-graph {
  width: 33vw;
  max-width: 400px;
  min-width: 120px;
  height: auto;
  object-fit: contain;
  margin-left: 1em;
  margin-right: 0.5em;
}
.hero-section {
  flex: 1;
}
.hero-section h1 {
  margin: 0;
  font-size: 2em;
  color: #42b883;
}
.hero-section p {
  margin: 0.2em 0 0 0;
  color: #888;
}
.hero-section {
  flex: 1;
  min-height: 320px;
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  justify-content: center;
  position: relative;
}
.hero-section {
  flex: 1;
  min-height: 340px;
  max-height: 340px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  position: relative;
}
.hero-carousel {
  width: 100%;
  height: 340px;
  min-height: 340px;
  max-height: 340px;
  margin-bottom: 1em;
  overflow: hidden;
  border-radius: 12px;
  box-shadow: 0 2px 8px #0002;
  background: #eee;
  display: flex;
  align-items: flex-start;
  justify-content: center;
}
.hero-img {
  width: 100%;
  height: 340px;
  min-height: 340px;
  max-height: 340px;
  object-fit: cover;
  object-position: top;
  transition: opacity 0.5s;
}
.hero-section h1 {
  margin: 0;
  font-size: 2.5em;
  color: #42b883;
}
.hero-section p {
  margin: 0.5em 0 0 0;
  color: #888;
  font-size: 1.2em;
}
  
.title-graph {
  width: 18vw;
  max-width: 200px;
  min-width: 100px;
  height: auto;
  object-fit: contain;
  margin-left: 0.5em;
}
.hero-section {
  flex: 1;
}
.hero-section h1 {
  margin: 0;
  font-size: 2em;
  color: #42b883;
}
.hero-section p {
  margin: 0.2em 0 0 0;
  color: #888;
}

.hero-section {
  flex: 1;
  min-height: 420px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  position: relative;
}
.hero-carousel {
  width: 100%;
  height: 340px;
  margin-bottom: 0.7em;
  overflow: hidden;
  border-radius: 16px;
  box-shadow: 0 4px 16px #0002;
  background: #eee;
  display: flex;
  align-items: center;
  justify-content: center;
}
.hero-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  transition: opacity 0.5s;
}
.hero-section h1 {
  margin: 0.2em 0 0 0;
  font-size: 1.5em;
  color: #42b883;
}
.hero-section p {
  margin: 0.3em 0 0 0;
  color: #888;
  font-size: 1em;
}
.nav-tabs-horizontal {
  display: flex;
  gap: 1em;
  background: #f5f5f5;
  padding: 0.5em 1em;
  border-bottom: 1px solid #ddd;
}
.nav-tabs-horizontal button {
  padding: 0.75em 1.5em;
  border: none;
  background: #eee;
  cursor: pointer;
  border-radius: 4px 4px 0 0;
  font-size: 1em;
}
.nav-tabs-horizontal button.active {
  background: #fff;
  border-bottom: 2px solid #42b883;
  font-weight: bold;
}
.tab-content {
  flex: 1;
  padding: 2em;
  background: #fff;
  border-radius: 0 0 4px 4px;
  box-shadow: 0 2px 8px #0001;
  min-height: 400px;
  overflow-y: auto;
}
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
