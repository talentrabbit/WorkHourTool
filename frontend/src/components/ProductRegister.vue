<template>
  <div class="product-register">
    <h3>Register New System</h3>
    <div class="register-grid">
      <div>
        <label>SerialNo</label>
        <input v-model="form.serialNo" inputmode="numeric" pattern="[0-9]*" @input="onSerialInput" aria-label="Serial number (digits only)" />
      </div>
      <div>
        <label>Modality Type</label>
        <select v-model="form.modalityType">
          <option value="">-- Select --</option>
          <option v-for="(opt, i) in modalityOptions" :key="i" :value="opt">{{ opt }}</option>
        </select>
      </div>
      <div>
        <label>Product Line</label>
        <select v-model="form.productLine">
          <option value="">-- Select --</option>
          <option v-for="(opt, i) in productLineOptions" :key="i" :value="opt">{{ opt }}</option>
        </select>
      </div>
      <div>
        <label>System Type</label>
        <select v-model="form.systemType">
          <option value="">-- Select --</option>
          <option v-for="(opt, i) in systemTypeOptions" :key="i" :value="opt">{{ opt }}</option>
        </select>
      </div>
      <div>
        <label>IvkNo</label>
        <select v-model="form.ivkNo">
          <option value="">-- Select --</option>
          <option v-for="(opt, i) in ivkOptions" :key="i" :value="opt">{{ opt }}</option>
        </select>
      </div>
    </div>
    
    <div style="margin-top:12px;">
      <button class="assign-btn" @click="submit" :disabled="submitting || !canSubmit">Create Product</button>
      <button class="cancel-btn" @click="reset" style="margin-left:8px;">Reset</button>
    </div>
    <div v-if="message" :class="{error: isError}" style="margin-top:12px">{{ message }}</div>
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from 'vue'
import axios from 'axios'

const form = ref({ serialNo: '', modalityType: '', productLine: '', systemType: '', ivkNo: '' })
const submitting = ref(false)
const message = ref('')
const isError = ref(false)

const modalityOptions = ref([])
const productLineOptions = ref([])
const systemTypeOptions = ref([])
const ivkOptions = ref([])

const canSubmit = computed(() => {
  const f = form.value
  return Boolean(f.serialNo && f.serialNo.trim() && f.modalityType && f.productLine && f.systemType)
})

function reset() {
  form.value = { serialNo: '', modalityType: '', productLine: '', systemType: '', ivkNo: '' }
  message.value = ''
  isError.value = false
}

// Ensure only digits are allowed in SerialNo input
function onSerialInput(event) {
  const raw = event?.target?.value || ''
  const cleaned = raw.replace(/\D+/g, '')
  if (cleaned !== raw) {
    // update the bound model with cleaned value
    form.value.serialNo = cleaned
  }
}

async function loadDefinitions() {
  try {
    const res = await axios.get('/api/WorkHours/get-product-definitions')
    const defs = Array.isArray(res.data) ? res.data : []
    modalityOptions.value = Array.from(new Set(defs.map(d => d.modalityType).filter(Boolean)))
    productLineOptions.value = Array.from(new Set(defs.map(d => d.productLine).filter(Boolean)))
    systemTypeOptions.value = Array.from(new Set(defs.map(d => d.systemType).filter(Boolean)))
    ivkOptions.value = Array.from(new Set(defs.map(d => d.ivkNo).filter(Boolean)))
  } catch (err) {
    console.warn('Failed to load product definitions', err)
    modalityOptions.value = []
    productLineOptions.value = []
    systemTypeOptions.value = []
    ivkOptions.value = []
  }
}

onMounted(() => {
  void loadDefinitions()
})

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
      ProjectNo: null
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
.register-grid input, .register-grid select { width:100%; padding:0.4rem; border:1px solid #e6c9b0; border-radius:6px; box-sizing: border-box; font-size:0.95rem }
.assign-btn { background:#42b883; color:#fff; border:none; padding:0.6rem 1rem; border-radius:6px; cursor:pointer }
.cancel-btn { background:#fff; color:#82451F; border:1px solid #E6C9B0; padding:0.6rem 1rem; border-radius:6px }
.error { color:#b00020 }

/* Responsive: stack inputs on narrow screens to avoid overlap */
@media (max-width: 640px) {
  .register-grid { grid-template-columns: 1fr; gap: 0.8rem }
}
</style>
