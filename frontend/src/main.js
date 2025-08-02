import { createApp } from 'vue'
import './style.css'
import App from './App.vue'
import { createRouter, createWebHistory } from 'vue-router'
import Entry from './components/Entry.vue'
import NavTabs from './components/NavTabs.vue'
import Planning from './components/Planning.vue'

const routes = [
  { path: '/', component: Entry },
  { path: '/worker', component: NavTabs },
  { path: '/planning', component: Planning }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

const app = createApp(App)
app.use(router)
app.mount('#app')
