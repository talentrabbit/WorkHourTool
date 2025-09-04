import { createApp } from 'vue'
import './style.css'
import App from './App.vue'
import { createRouter, createWebHistory } from 'vue-router'
import Main from './components/Main.vue'
import WorkHourTool from './components/WorkHourTool.vue'
import Planning from './components/Planning.vue'
import WorkHourMaintenance from './components/WorkHourMaintenance.vue'
import VXETable from 'vxe-table'
import 'vxe-table/lib/style.css'
import axios from 'axios';

const routes = [
  { path: '/', component: Main },
  { path: '/main', component: Main },
  { path: '/worker', component: WorkHourTool },
  { path: '/planning', component: Planning },
  { path: '/maintenance', component: WorkHourMaintenance }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

axios.defaults.baseURL = 'http://shai571a:5063';

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

const app = createApp(App)
app.use(router)
app.use(VXETable) // Register VXETable plugin
app.mount('#app')
