import { createApp } from 'vue'
import './style.css'
import App from './App.vue'
import { createRouter, createWebHistory } from 'vue-router'
import Entry from './components/Entry.vue'
import NavTabs from './components/NavTabs.vue'
import Planning from './components/Planning.vue'
import WorkHourMaintenance from './components/WorkHourMaintenance.vue'
import VXETable from 'vxe-table'
import 'vxe-table/lib/style.css'
import axios from 'axios';

const routes = [
  { path: '/', component: Entry },
  { path: '/worker', component: NavTabs },
  { path: '/planning', component: Planning },
  { path: '/maintenance', component: WorkHourMaintenance }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

axios.defaults.baseURL = 'http://localhost:5063';

const app = createApp(App)
app.use(router)
app.use(VXETable) // Register VXETable plugin
app.mount('#app')
