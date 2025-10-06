import { createApp } from 'vue'
import './style.css'
import App from './App.vue'
import { createRouter, createWebHistory } from 'vue-router'
import Main from './components/Main.vue'
import WorkHourTool from './components/WorkHourTool.vue'
import Planning from './components/Planning.vue'
import WorkHourMaintenance from './components/WorkHourMaintenance.vue'
import Ncm from './components/Ncm.vue'
import ProductRegister from './components/ProductRegister.vue'
import Kanban from './components/Kanban.vue'
import VXETable from 'vxe-table'
import 'vxe-table/lib/style.css'
import axios from 'axios'

// Determine API base URL:
// - In development, use an empty base so Vite dev server proxy forwards /api requests
// - In production, use same-origin (empty) so requests go to the server that serves the SPA
const mode = import.meta.env.MODE || 'development'
const apiBase = ''
// Ensure axios uses a string (empty means relative requests -> same-origin in prod, proxy in dev)
axios.defaults.baseURL = apiBase

console.info(`API base URL: ${axios.defaults.baseURL || '(relative / same-origin)'} (mode: ${mode})`)

const routes = [
  { path: '/', component: Main },
  { path: '/main', component: Main },
  { path: '/worker', component: WorkHourTool },
  { path: '/planning', component: Planning },
  { path: '/product-register', component: ProductRegister },
  { path: '/maintenance', component: WorkHourMaintenance }, 
  { path: '/ncm', component: Ncm }, 
  { path: '/kanban', component: Kanban }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

// Startup parameter: ?devUser=Name or VITE_DEV_USER env
const urlParams = new URLSearchParams(window.location.search)
const devUser = urlParams.get('devUser') || import.meta.env.VITE_DEV_USER
if (devUser) {
  axios.interceptors.request.use(cfg => {
    cfg.headers = cfg.headers || {}
    cfg.headers['X-Dev-User'] = devUser
    return cfg
  })
}

// log final resolved API base
console.info(`Final API base URL: ${axios.defaults.baseURL || '(relative / same-origin)'} (mode: ${mode})`)

const app = createApp(App)
app.use(router)
app.use(VXETable) // Register VXETable plugin
app.mount('#app')
