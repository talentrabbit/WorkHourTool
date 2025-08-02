<script setup>
import { ref, defineEmits } from 'vue'

// Dropdown data arrays (to be fetched from backend in future)
const orderNumbers = ref([])
const productNames = ref([])
const seriesNos = ref([])
const processes = ref([])
const workSeats = ref([])
// Co-workers options (to be fetched from backend Process-Worker Table)
const coWorkers = ref(['Worker 2', 'Worker 3'])


// Form state
const form = ref({
  orderNumber: '',
  productName: '',
  seriesNo: '',
  process: '',
  workSeat: '',
  coWorkers: [], // array of selected co-worker IDs or names
  startTime: '',
  endTime: ''
})


function startWork(){
  // Placeholder for form submission logic
  console.log('Start Work:', form.value)
  emit('start-work-and-switch')
}
</script>

<template>
  <h2>Work Hours for Current Product</h2>
  <form class="product-form">
    <div class="form-row-columns">
      <div class="form-row">
        <label for="orderNumber">Order Number</label>
        <select id="orderNumber" v-model="form.orderNumber">
          <option value="" disabled>Select Order Number</option>
          <option v-for="num in orderNumbers" :key="num" :value="num">{{ num }}</option>
        </select>
      </div>
      <div class="form-row">
        <label for="productName">Product Name</label>
        <select id="productName" v-model="form.productName">
          <option value="" disabled>Select Product Name</option>
          <option v-for="name in productNames" :key="name" :value="name">{{ name }}</option>
        </select>
      </div>
      <div class="form-row">
        <label for="seriesNo">Series No.</label>
        <select id="seriesNo" v-model="form.seriesNo">
          <option value="" disabled>Select Series No.</option>
          <option v-for="series in seriesNos" :key="series" :value="series">{{ series }}</option>
        </select>
      </div>
      <div class="form-row">
        <label for="process">Process</label>
        <select id="process" v-model="form.process">
          <option value="" disabled>Select Process</option>
          <option v-for="proc in processes" :key="proc" :value="proc">{{ proc }}</option>
        </select>
      </div>
      <div class="form-row">
        <label for="workSeat">Work Seat</label>
        <select id="workSeat" v-model="form.workSeat">
          <option value="" disabled>Select Work Seat</option>
          <option v-for="seat in workSeats" :key="seat" :value="seat">{{ seat }}</option>
        </select>
      </div>
      <div class="form-row">
        <label for="coWorkers">Co-workers</label>
        <select id="coWorkers" v-model="form.coWorkers" multiple size="3">
          <option v-for="worker in coWorkers" :key="worker" :value="worker">{{ worker }}</option>
        </select>
        <small>Select one or more co-workers if this process requires multiple people.</small>
      </div>
    </div>
    <div class="form-row">
      <label for="startTime">Start Time</label>
      <input id="startTime" name="startTime" type="datetime-local" v-model="form.startTime" />
    </div>
    <div class="form-row">
      <label for="endTime">End Time</label>
      <input id="endTime" name="endTime" type="datetime-local" v-model="form.endTime" />
    </div>
    <div class="form-actions">
      <button type="button" class="start-work-btn" @click="startWork">Start Work</button>
    </div>
  </form>
</template>

<style scoped>
  .product-form {
    max-width: 600px;
    margin: 2em auto 0 auto;
    padding: 2em;
    background: #f9f9f9;
    border-radius: 8px;
    box-shadow: 0 2px 8px #0001;
  }
  .form-row-columns {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 1.5em;
    margin-bottom: 1em;
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
.form-row input,
.form-row select {
  padding: 0.5em;
  border: 1px solid #ccc;
  border-radius: 4px;
  font-size: 1em;
}

/* Make the Co-workers select control higher */
#coWorkers {
  min-height: 6em;
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
