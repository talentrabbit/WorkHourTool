<template>
  <div class="product-register">
    <h3>Register New Product</h3>
    <div class="register-grid">
      <div>
        <label>SerialNo</label>
        <input v-model="form.serialNo" />
      </div>
      <div>
        <label>Modality Type</label>
        <input v-model="form.modalityType" />
      </div>
      <div>
        <label>Product Line</label>
        <input v-model="form.productLine" />
      </div>
      <div>
        <label>System Type</label>
        <input v-model="form.systemType" />
      </div>
      <div>
        <label>IvkNo</label>
        <input v-model="form.ivkNo" />
      </div>
      <div>
        <label>Project No</label>
        <input v-model="form.projectNo" />
      </div>
    </div>
    <div style="margin-top:12px;">
      <button class="assign-btn" @click="submit" :disabled="submitting">Create Product</button>
      <button class="cancel-btn" @click="reset" style="margin-left:8px;">Reset</button>
    </div>
    <div v-if="message" :class="{error: isError}" style="margin-top:12px">{{ message }}</div>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import axios from 'axios'

const form = ref({ serialNo: '', modalityType: '', productLine: '', systemType: '', ivkNo: '', projectNo: '' })
const submitting = ref(false)
const message = ref('')
const isError = ref(false)

function reset() {
  form.value = { serialNo: '', modalityType: '', productLine: '', systemType: '', ivkNo: '', projectNo: '' }
  message.value = ''
  isError.value = false
}

async function submit() {
  message.value = ''
  isError.value = false
  if (!form.value.serialNo || !form.value.serialNo.trim()) {
    message.value = 'SerialNo is required'
    isError.value = true
    return
  }
  submitting.value = true
  try {
    const res = await axios.post('/api/WorkHours/add-product', {
      SerialNo: form.value.serialNo.trim(),
      ModalityType: form.value.modalityType || null,
      ProductLine: form.value.productLine || null,
      SystemType: form.value.systemType || null,
      IvkNo: form.value.ivkNo || null,
      ProjectNo: form.value.projectNo || null
    })
    message.value = 'Product created: ' + (res.data?.serialNo || '')
    reset()
  } catch (err) {
    console.error('Failed to add product', err)
    if (err?.response?.status === 409) {
      message.value = err.response.data?.message || 'SerialNo already exists'
    } else {
      message.value = 'Failed to create product'
    }
    isError.value = true
  } finally {
    submitting.value = false
  }
}
</script>

<style scoped>
.product-register { max-width: 760px; margin: 1em auto; padding: 1.2em; background: #fff; border: 1px solid #f2c7a6; border-radius: 8px; }
.register-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0.6rem; align-items: start; }
label { font-weight: 700; color: #5a3b27; display:block; margin-bottom:0.2rem }
.register-grid input { width:100%; padding:0.4rem; border:1px solid #e6c9b0; border-radius:6px; box-sizing: border-box; font-size:0.95rem }
.assign-btn { background:#42b883; color:#fff; border:none; padding:0.6rem 1rem; border-radius:6px; cursor:pointer }
.cancel-btn { background:#fff; color:#82451F; border:1px solid #E6C9B0; padding:0.6rem 1rem; border-radius:6px }
.error { color:#b00020 }

/* Responsive: stack inputs on narrow screens to avoid overlap */
@media (max-width: 640px) {
  .register-grid { grid-template-columns: 1fr; gap: 0.8rem }
}
</style>
