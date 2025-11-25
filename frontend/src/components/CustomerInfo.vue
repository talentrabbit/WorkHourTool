<template>
  <div class="customer-page">
    <div class="page-header">
      <h2 class="title-bright">Customer Info</h2>
      <button class="btn" @click="openCreate">Add New Customer</button>
    </div>

    <div v-if="loading" class="loading">Loading...</div>
    <div v-else class="table-wrap">
      <table class="table">
        <thead>
          <tr>
            <th>SerialNo</th>
            <th>Customer</th>
            <th>Order No.</th>
            <th>Province/City</th>
            <th>Address</th>
            <th>Delivery Date</th>
            <th>ProductLine</th>
            <th>SystemType</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="o in orders" :key="o.id" class="clickable" @click="openEdit(o)">
            <td>{{ o.serialNo }}</td>
            <td>{{ o.customer }}</td>
            <td>{{ o.orderNumber }}</td>
            <td>{{ o.provinceCity }}</td>
            <td class="addr" :title="o.address">{{ o.address }}</td>
            <td>{{ o.deliveryDate }}</td>
            <td>{{ o.productLine }}</td>
            <td>{{ o.systemType }}</td>
          </tr>
        </tbody>
      </table>
      <div v-if="orders.length === 0" class="empty">No customer records.</div>
    </div>

    <div v-if="modalOpen" class="modal-overlay" @click.self="closeModal">
      <div class="modal">
        <div class="modal-header">
          <div class="title">{{ editing.id ? 'Edit Customer' : 'Add Customer' }}</div>
          <button class="close-btn" @click="closeModal">×</button>
        </div>
        <div class="modal-body">
          <div class="form-grid">
            <div class="form-row">
              <label>SerialNo</label>
              <input v-model="editing.serialNo" placeholder="System SerialNo" />
            </div>
            <div class="form-row">
              <label>Customer</label>
              <input v-model="editing.customer" />
            </div>
            <div class="form-row">
              <label>Order No.</label>
              <input v-model="editing.orderNumber" />
            </div>
            <div class="form-row">
              <label>Province/City</label>
              <input v-model="editing.provinceCity" />
            </div>
            <div class="form-row full">
              <label>Address</label>
              <textarea v-model="editing.address" rows="3"></textarea>
            </div>
            <div class="form-row">
              <label>Delivery Date</label>
              <input type="date" v-model="editing._deliveryDate" />
            </div>
          </div>
          <div class="hint">Note: Delivery Date is stored as text (YYYY-MM-DD).</div>
        </div>
        <div class="modal-footer">
          <button class="secondary" @click="closeModal">Cancel</button>
          <button class="danger" v-if="editing.id" @click="doDelete">Delete</button>
          <button class="btn" @click="doSave">Save</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import axios from 'axios'

const orders = ref([])
const loading = ref(false)
const modalOpen = ref(false)
const editing = ref({})

onMounted(() => {
  reload()
})

async function reload() {
  loading.value = true
  try {
    const res = await axios.get('/api/Orders')
    orders.value = Array.isArray(res.data) ? res.data : []
  } catch (e) {
    console.error('Failed to load orders', e)
    orders.value = []
  } finally {
    loading.value = false
  }
}

function openCreate() {
  editing.value = { serialNo: '', customer: '', orderNumber: '', provinceCity: '', address: '', _deliveryDate: '' }
  modalOpen.value = true
}

function openEdit(row) {
  editing.value = { ...row, _deliveryDate: normalizeDate(row.deliveryDate) }
  modalOpen.value = true
}

function closeModal() { modalOpen.value = false }

function normalizeDate(text) {
  if (!text) return ''
  // if already YYYY-MM-DD return as is; otherwise try parse
  const m = String(text).match(/^(\d{4})-(\d{2})-(\d{2})$/)
  if (m) return text
  const d = new Date(text)
  if (isNaN(d)) return ''
  const y = d.getFullYear()
  const mo = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${y}-${mo}-${day}`
}

async function doSave() {
  const payload = {
    SerialNo: editing.value.serialNo,
    Customer: editing.value.customer,
    OrderNumber: editing.value.orderNumber,
    ProvinceCity: editing.value.provinceCity,
    Address: editing.value.address,
    DeliveryDate: editing.value._deliveryDate || null
  }
  try {
    if (editing.value.id) {
      await axios.put(`/api/Orders/${editing.value.id}`, payload)
    } else {
      const res = await axios.post('/api/Orders', payload)
      if (res?.data?.id) editing.value.id = res.data.id
    }
    await reload()
    closeModal()
  } catch (e) {
    console.error('Save failed', e)
    alert(e?.response?.data?.message || e.message)
  }
}

async function doDelete() {
  if (!editing.value.id) return
  if (!confirm('Delete this customer record?')) return
  try {
    await axios.delete(`/api/Orders/${editing.value.id}`)
    await reload()
    closeModal()
  } catch (e) {
    console.error('Delete failed', e)
    alert(e?.response?.data?.message || e.message)
  }
}
</script>

<style scoped>
.customer-page { max-width: 96%; margin: 1rem 1rem; padding: 1rem; background: #fff; border-radius: 12px; box-shadow: 0 4px 14px rgba(0,0,0,0.06); font-size: 0.95rem; text-align: left; }
.page-header { display: flex; align-items: center; justify-content: flex-end; margin-bottom: 12px; position: relative; }
.btn { background: #EC6602; color: #fff; border: none; padding: 6px 10px; border-radius: 8px; cursor: pointer; font-weight: 700; font-size: 0.92rem; }
.btn:hover { opacity: 0.95 }
.table-wrap { overflow: auto; max-height: 65vh; }
.table { width: 100%; border-collapse: collapse; table-layout: fixed; }
.table th, .table td { padding: 6px 8px; border-bottom: 1px solid #eee; text-align: left; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; font-size: 0.92rem; }
.table .addr { max-width: 400px; }
.empty { margin-top: 1rem; color: #666 }
.loading { color: #82451F }

/* Modal styles reused */
.modal-overlay { position: fixed; inset: 0; background: rgba(0,0,0,0.35); display: flex; align-items: center; justify-content: center; z-index: 50; }
.modal { width: min(820px, 92vw); background: #fff; border-radius: 10px; box-shadow: 0 10px 30px rgba(0,0,0,0.2); display: flex; flex-direction: column; overflow: hidden; }
.modal-header { position: relative; background: #FFF4E6; padding: 14px 18px; border-bottom: 1px solid #f1dfd1; min-height: 45px; }
.modal-header .title { position: absolute; left: 50%; top: 50%; transform: translate(-50%, -50%); text-align: center; font-weight: 800; color: #8a4b1a; max-width: calc(100% - 140px); padding: 0 12px; line-height: 1.1; }
.modal-header .close-btn { position: absolute; right: 10px; top: 50%; transform: translateY(-50%); background: transparent; color: #8a4b1a; font-size: 22px; line-height: 1; padding: 4px 8px; border-radius: 6px; }
.modal-header .close-btn:hover { background: rgba(0,0,0,0.06); }
.modal-body { padding: 14px 16px; overflow: auto; }
.form-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 10px; }
.form-row { display: flex; flex-direction: column; gap: 6px; }
.form-row.full { grid-column: 1 / -1; }
.form-row > input, .form-row > textarea { border: 1px solid #ddd; border-radius: 6px; padding: 6px 8px; font-size: 0.95rem; }
.modal-footer { display: flex; justify-content: flex-end; gap: 10px; padding: 12px 16px; border-top: 1px solid #eee; }
.modal-footer .secondary { background: #e6e6e6; color: #333; }
.modal-footer .danger { background: #d33; }
.clickable { cursor: pointer; }
.clickable:hover { background: #fff8f1; }

/* Bright title style for page header — match Kanban h1 color but slightly smaller */
.title-bright {
  color: #EC6602;
  font-weight: 800;
  font-size: 1.5rem; /* slightly smaller than default h1 */
  margin: 0;
  position: absolute;
  left: 50%;
  transform: translateX(-50%);
  z-index: 2;
}
</style>
