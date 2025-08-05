<template>
  <div class="planning-container">
    <h2>Production Planning</h2>
    <div class="search-section">
      <input v-model="serialNo" placeholder="Enter SerialNo" class="search-input" />
      <button @click="searchProduct" class="search-btn">Search</button>
    </div>
    <div v-if="product" class="product-info">
      <h3>Product Info</h3>
      <div><strong>SerialNo:</strong> {{ product.serialNo }}</div>
      <div><strong>ProjectNo:</strong> {{ product.projectNo }}</div>
      <div><strong>SystemType:</strong> {{ product.systemType }}</div>
      <div><strong>Current State:</strong> {{ productState }}</div>
      <div class="assign-section">
        <h4>Assign Task</h4>
        <input v-model="task.workerName" placeholder="Worker Name" />
        <input v-model="task.process" placeholder="Process" />
        <input type="date" v-model="task.date" />
        <input type="time" v-model="task.beginTime" />
        <input type="time" v-model="task.endTime" />
        <button @click="assignTask">Assign</button>
      </div>
    </div>
    <div v-if="searchError" class="error">{{ searchError }}</div>

    <div class="all-products-section">
      <h3>All Systems Production State</h3>
      <vxe-table :data="allProducts" border>
        <vxe-column field="serialNo" title="SerialNo" width="120" />
        <vxe-column field="projectNo" title="ProjectNo" width="120" />
        <vxe-column field="systemType" title="SystemType" width="120" />
        <vxe-column field="state" title="Production State" width="150" />
      </vxe-table>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import axios from 'axios'
import { VXETable } from 'vxe-table'

const serialNo = ref('')
const product = ref(null)
const productState = ref('')
const searchError = ref('')
const task = ref({
  workerName: '',
  process: '',
  date: '',
  beginTime: '',
  endTime: ''
})

const allProducts = ref([])

async function fetchAllProducts() {
  // You may need to implement an API endpoint to get all products with their WorkHours
  const res = await axios.get('/api/Products')
  allProducts.value = res.data.map(p => ({
    serialNo: p.serialNo,
    projectNo: p.projectNo,
    systemType: p.systemType,
    state: p.workHours?.length ? 'In Progress' : 'Not Started'
  }))
}

onMounted(() => {
  fetchAllProducts()
})

async function searchProduct() {
  searchError.value = ''
  product.value = null
  productState.value = ''
  try {
    const res = await axios.get(`/api/WorkHours/product-status/${serialNo.value}`)
    product.value = res.data
    productState.value = res.data.WorkHours?.length ? 'In Progress' : 'Not Started'
  } catch (err) {
    searchError.value = err.response?.data?.message || 'Product not found.'
  }
}

async function assignTask() {
  if (!product.value) return
  await axios.post('/api/WorkHours', {
    WorkerName: task.value.workerName,
    MainTime: 0,
    IssueTime: 0,
    SerialNo: product.value.serialNo,
    Process: task.value.process,
    Date: task.value.date,
    BeginTime: task.value.beginTime,
    EndTime: task.value.endTime
  })
  alert('Task assigned!')
}
</script>
/* Add vxe-table styles if needed */

<style scoped>

.planning-container {
  max-width: 900px;
  width: 70vw;
  margin: 4vw auto;
  padding: 2.5vw;
  background: #f1f5f9;
  border-radius: 18px;
  box-shadow: 0 4px 24px rgba(0,0,0,0.07);
}
.search-section {
  display: flex;
  gap: 2vw;
  margin-bottom: 2vw;
}
.search-input {
  flex: 1;
  padding: 0.7vw;
  border-radius: 8px;
  border: 1px solid #cbd5e1;
  font-size: 1.1em;
}
.search-btn {
  background: #2563eb;
  color: #fff;
  border: none;
  border-radius: 8px;
  padding: 0.7vw 2vw;
  cursor: pointer;
  font-size: 1.1em;
}
.search-btn:hover {
  background: #1e40af;
}
.product-info {
  margin-top: 2vw;
  background: #fff;
  padding: 1.5vw;
  border-radius: 12px;
  box-shadow: 0 2px 12px rgba(0,0,0,0.05);
}
.assign-section {
  margin-top: 2vw;
  display: flex;
  flex-direction: column;
  gap: 1vw;
}
.error {
  color: #dc2626;
  margin-top: 1vw;
}
</style>
