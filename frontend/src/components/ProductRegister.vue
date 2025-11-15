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
        <select v-model="form.modalityType" @change="onFieldChange('modalityType')">
          <option value="">-- Select --</option>
          <option v-for="(opt, i) in modalityOptions" :key="i" :value="opt">{{ opt }}</option>
        </select>
      </div>
      <div>
        <label>Product Line</label>
        <select v-model="form.productLine" @change="onFieldChange('productLine')">
          <option value="">-- Select --</option>
          <option v-for="(opt, i) in productLineOptions" :key="i" :value="opt">{{ opt }}</option>
        </select>
      </div>
      <div>
        <label>System Type</label>
        <select v-model="form.systemType" @change="onFieldChange('systemType')">
          <option value="">-- Select --</option>
          <option v-for="(opt, i) in systemTypeOptions" :key="i" :value="opt">{{ opt }}</option>
        </select>
      </div>
      <div>
        <label>Project No</label>
        <select v-model="form.projectNo" @change="onFieldChange('projectNo')">
          <option value="">-- Select --</option>
          <option v-for="(opt, i) in projectNoOptions" :key="i" :value="opt">{{ opt }}</option>
        </select>
      </div>
      <div>
        <label>IvkNo</label>
        <select v-model="form.ivkNo" @change="onFieldChange('ivkNo')">
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
import { ref, onMounted, computed, watch, nextTick } from 'vue'
import axios from 'axios'

const form = ref({ serialNo: '', modalityType: '', productLine: '', systemType: '', ivkNo: '', projectNo: '' })
const submitting = ref(false)
const message = ref('')
const isError = ref(false)

// Option arrays will be populated by applyFilters()

const allDefs = ref([])
const lastChanged = ref('')

function onFieldChange(name) {
  lastChanged.value = name
  // call central filter to refresh options
  applyFilters()
  // clear lastChanged after a short window so subsequent updates can run normally
  nextTick(() => setTimeout(() => { lastChanged.value = '' }, 50))
}

const modalityOptions = ref([])
const productLineOptions = ref([])
const systemTypeOptions = ref([])
const projectNoOptions = ref([])
const ivkOptions = ref([])

const canSubmit = computed(() => {
  const f = form.value
  const sn = (f.serialNo || '').trim()
  // SerialNo must be 4-6 digits
  const validSerial = /^[0-9]{4,6}$/.test(sn)
  return Boolean(validSerial && f.modalityType && f.productLine && f.systemType && f.projectNo)
})

function reset() {
  // clear form selections
  form.value = { serialNo: '', modalityType: '', productLine: '', systemType: '', ivkNo: '', projectNo: '' }
  // clear transient state
  message.value = ''
  isError.value = false
  lastChanged.value = ''

  // Recompute options from existing in-memory definitions (no network call)
  applyFilters()
}

// Clear only the form fields but keep any message (used after successful submit so user sees confirmation)
function clearForm() {
  form.value = { serialNo: '', modalityType: '', productLine: '', systemType: '', ivkNo: '', projectNo: '' }
  // keep message and isError as-is
  lastChanged.value = ''
  // recompute options based on cleared form
  applyFilters()
}

// Ensure only digits are allowed in SerialNo input
function onSerialInput(event) {
  const raw = event?.target?.value || ''
  const cleaned = raw.replace(/\D+/g, '')
  if (form.value.serialNo !== cleaned) {
    form.value.serialNo = cleaned
  }
  // Validate length: if non-empty but not 4-6 digits, show an inline error
  if (cleaned.length > 0 && !(cleaned.length >= 4 && cleaned.length <= 6)) {
    message.value = 'Invalid SerialNo'
    isError.value = true
  } else {
    // Only clear the invalid message if it was the one we set earlier
    if (message.value === 'Invalid SerialNo') {
      message.value = ''
      isError.value = false
    }
  }
}

async function loadDefinitions() {
  try {
    const res = await axios.get('/api/WorkHours/get-product-definitions')
    const defs = Array.isArray(res.data) ? res.data : []
    // store full definitions; computed option lists will derive filtered options
    allDefs.value = defs.map(d => ({
      modalityType: d.ModalityType ?? d.modalityType ?? '',
      productLine: d.ProductLine ?? d.productLine ?? '',
      systemType: d.SystemType ?? d.systemType ?? '',
      ivkNo: d.IvkNo ?? d.ivkNo ?? '',
      projectNo: d.ProjectNo ?? d.projectNo ?? ''
    }))
    applyFilters()
  } catch (err) {
    console.warn('Failed to load product definitions', err)
    allDefs.value = []
  }
}

onMounted(() => {
  void loadDefinitions()
})

// If a selection becomes invalid because of a new filter, clear it
// central filter function: apply current form as filters (empty = ignored)
function applyFilters() {
  const f = form.value
  // Filter definitions by active selections (non-empty)
  const filtered = allDefs.value.filter(d => {
    if (f.modalityType && d.modalityType !== f.modalityType) return false
    if (f.productLine && d.productLine !== f.productLine) return false
    if (f.systemType && d.systemType !== f.systemType) return false
    if (f.ivkNo && d.ivkNo !== f.ivkNo) return false
    if (f.projectNo && d.projectNo !== f.projectNo) return false
    return true
  })

  // Build option sets from filtered defs
  const mSet = new Set(filtered.map(d => d.modalityType).filter(Boolean))
  const pSet = new Set(filtered.map(d => d.productLine).filter(Boolean))
  const sSet = new Set(filtered.map(d => d.systemType).filter(Boolean))
  const prSet = new Set(filtered.map(d => d.projectNo).filter(Boolean))
  const iSet = new Set(filtered.map(d => d.ivkNo).filter(Boolean))

  modalityOptions.value = Array.from(mSet)
  productLineOptions.value = Array.from(pSet)
  systemTypeOptions.value = Array.from(sSet)
  projectNoOptions.value = Array.from(prSet)
  ivkOptions.value = Array.from(iSet)

  // Clear selections that are no longer valid, but keep the field that was just changed
  if (f.modalityType && lastChanged.value !== 'modalityType' && !modalityOptions.value.includes(f.modalityType)) f.modalityType = ''
  if (f.productLine && lastChanged.value !== 'productLine' && !productLineOptions.value.includes(f.productLine)) f.productLine = ''
  if (f.systemType && lastChanged.value !== 'systemType' && !systemTypeOptions.value.includes(f.systemType)) f.systemType = ''
  if (f.ivkNo && lastChanged.value !== 'ivkNo' && !ivkOptions.value.includes(f.ivkNo)) f.ivkNo = ''
  if (f.projectNo && lastChanged.value !== 'projectNo' && !projectNoOptions.value.includes(f.projectNo)) f.projectNo = ''

  // Auto-select single-option lists (but don't override user's just-changed field)
  if (modalityOptions.value.length === 1 && !form.value.modalityType && lastChanged.value !== 'modalityType') form.value.modalityType = modalityOptions.value[0]
  if (productLineOptions.value.length === 1 && !form.value.productLine && lastChanged.value !== 'productLine') form.value.productLine = productLineOptions.value[0]
  if (systemTypeOptions.value.length === 1 && !form.value.systemType && lastChanged.value !== 'systemType') form.value.systemType = systemTypeOptions.value[0]
  if (projectNoOptions.value.length === 1 && !form.value.projectNo && lastChanged.value !== 'projectNo') form.value.projectNo = projectNoOptions.value[0]
  if (ivkOptions.value.length === 1 && !form.value.ivkNo && lastChanged.value !== 'ivkNo') form.value.ivkNo = ivkOptions.value[0]
}

// Auto-select when a filtered option list has only one choice
watch(modalityOptions, (opts) => {
  if (Array.isArray(opts) && opts.length === 1 && form.value.modalityType !== opts[0]) {
    form.value.modalityType = opts[0]
  }
})
watch(productLineOptions, (opts) => {
  if (Array.isArray(opts) && opts.length === 1 && form.value.productLine !== opts[0]) {
    form.value.productLine = opts[0]
  }
})
watch(systemTypeOptions, (opts) => {
  if (Array.isArray(opts) && opts.length === 1 && form.value.systemType !== opts[0]) {
    form.value.systemType = opts[0]
  }
})
watch(projectNoOptions, (opts) => {
  if (Array.isArray(opts) && opts.length === 1 && form.value.projectNo !== opts[0]) {
    form.value.projectNo = opts[0]
  }
})
watch(ivkOptions, (opts) => {
  if (Array.isArray(opts) && opts.length === 1 && form.value.ivkNo !== opts[0]) {
    form.value.ivkNo = opts[0]
  }
})

async function submit() {
  message.value = ''
  isError.value = false
  const sn = (form.value.serialNo || '').trim()
  if (!sn) {
    message.value = 'SerialNo is required'
    isError.value = true
    return
  }
  if (!/^[0-9]{4,6}$/.test(sn)) {
    message.value = 'Invalid SerialNo'
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
  // Keep the success message visible while clearing the form fields
  clearForm()
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
.product-register {   max-width: 90%; width: 75vw;   margin: 1em auto; padding: 1.2em; background: #fff; border: 1px solid #f2c7a6; border-radius: 8px; }
.register-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0.6rem; align-items: start; }
label { font-weight: 700; color: #5a3b27; display:block; margin-bottom:0.2rem }
.register-grid input, .register-grid select { width:100%; padding:0.4rem; border:1px solid #e6c9b0; border-radius:6px; box-sizing: border-box; font-size:0.95rem }
.assign-btn { background:#42b883; color:#fff; border:none; padding:0.6rem 1rem; border-radius:6px; cursor:pointer }
.assign-btn:not(:disabled):hover { background:#3aa97a }
.assign-btn:focus-visible { outline:2px solid #2e865f; outline-offset:2px }
/* Make disabled state clearly different */
.assign-btn[disabled],
.assign-btn:disabled { background:#a7cdbb; color:#f5f7f6; cursor:not-allowed; opacity:0.65 }
.cancel-btn { background:#fff; color:#82451F; border:1px solid #E6C9B0; padding:0.6rem 1rem; border-radius:6px }
.cancel-btn:hover { background:#faf6f3 }
.error { color:#b00020 }

/* Responsive: stack inputs on narrow screens to avoid overlap */
@media (max-width: 640px) {
  .register-grid { grid-template-columns: 1fr; gap: 0.8rem }
}
</style>
