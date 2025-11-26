<template>
  <div class="product-register">
    <!-- Header: New System button (styled like Planning switch button) -->
    <div style="display:flex; justify-content:flex-end; margin-bottom:8px;">
      <button class="add-nonproduct-btn switch-btn" @click="showRegisterDialog = true" aria-label="Create new system">
        <span class="switch-label">New System</span>
        <span class="forward-arrow" aria-hidden="true">+</span>
      </button>
    </div>
    <!-- Products Maintenance (moved above register form) -->
    <section class="collapsible" style="margin-top:0; margin-bottom:18px;">
      <header @click="toggleMaint" class="collapsible-header">
        <h3>Products Maintenance</h3>
        <span>{{ maintOpen ? '▾' : '▸' }}</span>
      </header>
      <div v-show="maintOpen" class="collapsible-body">
        <div class="actions">
            <button :disabled="!changedProducts.size" @click="saveProducts">Save</button>
            <button :disabled="!selectedProducts.size" @click="deleteProducts">Delete</button>
            <button @click="loadProducts">Refresh</button>
          </div>
          <vxe-table :data="products" border stripe round class="modern-vxe-table" @checkbox-change="onCheckChangeProd($event)" @checkbox-all="onCheckChangeProd($event)">
            <vxe-column type="checkbox" width="40" />
            <vxe-column field="serialNo" title="SerialNo" width="90">
              <template #default="{ row }">
                <input type="text" v-model="row.serialNo" class="cell-input" @input="() => markChangedProd(row.serialNo)" />
              </template>
            </vxe-column>
            <vxe-column field="modalityType" title="ModalityType" width="120">
              <template #default="{ row }">
                <input type="text" v-model="row.modalityType" class="cell-input" @input="() => markChangedProd(row.serialNo)" />
              </template>
            </vxe-column>
            <vxe-column field="productLine" title="ProductLine" width="120">
              <template #default="{ row }">
                <input type="text" v-model="row.productLine" class="cell-input" @input="() => markChangedProd(row.serialNo)" />
              </template>
            </vxe-column>
            <vxe-column field="systemType" title="SystemType" width="120">
              <template #default="{ row }">
                <input type="text" v-model="row.systemType" class="cell-input" @input="() => markChangedProd(row.serialNo)" />
              </template>
            </vxe-column>
            <vxe-column field="projectNo" title="ProjectNo" width="100">
              <template #default="{ row }">
                <input type="text" v-model="row.projectNo" class="cell-input" @input="() => markChangedProd(row.serialNo)" />
              </template>
            </vxe-column>
            <vxe-column field="ivkNo" title="IvkNo" width="100">
              <template #default="{ row }">
                <input type="text" v-model="row.ivkNo" class="cell-input" @input="() => markChangedProd(row.serialNo)" />
              </template>
            </vxe-column>
            <vxe-column field="systemState" title="SystemState" width="120">
              <template #default="{ row }">
                <select v-model="row.systemState" class="cell-input" @change="() => markChangedProd(row.serialNo)">
                  <option v-for="(opt, i) in processNames" :key="i" :value="opt">{{ opt }}</option>
                </select>
              </template>
            </vxe-column>
            <vxe-column field="unpackageHours" title="Unpackage" width="90">
              <template #default="{ row }">
                <input type="number" v-model.number="row.unpackageHours" class="cell-input eh-input" @input="() => markChangedProd(row.serialNo)" />
              </template>
            </vxe-column>
            <vxe-column field="assemblyHours" title="Assembly" width="90">
              <template #default="{ row }">
                <input type="number" v-model.number="row.assemblyHours" class="cell-input eh-input" @input="() => markChangedProd(row.serialNo)" />
              </template>
            </vxe-column>
            <vxe-column field="debugHours" title="Debug" width="90">
              <template #default="{ row }">
                <input type="number" v-model.number="row.debugHours" class="cell-input eh-input" @input="() => markChangedProd(row.serialNo)" />
              </template>
            </vxe-column>
            <vxe-column field="validationHours" title="Validation" width="100">
              <template #default="{ row }">
                <input type="number" v-model.number="row.validationHours" class="cell-input eh-input" @input="() => markChangedProd(row.serialNo)" />
              </template>
            </vxe-column>
            <vxe-column field="disassemblyHours" title="Disassembly" width="100">
              <template #default="{ row }">
                <input type="number" v-model.number="row.disassemblyHours" class="cell-input eh-input" @input="() => markChangedProd(row.serialNo)" />
              </template>
            </vxe-column>
            <vxe-column field="repackageHours" title="Repackage" width="100">
              <template #default="{ row }">
                <input type="number" v-model.number="row.repackageHours" class="cell-input eh-input" @input="() => markChangedProd(row.serialNo)" />
              </template>
            </vxe-column>
          </vxe-table>
      </div>
    </section>

    <!-- Register New System dialog -->
    <div v-if="showRegisterDialog" class="modal-overlay">
      <div class="modal">
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
        <div style="margin-top:12px; display:flex; gap:8px; justify-content:flex-end;">
          <button class="assign-btn" @click="submit" :disabled="submitting || !canSubmit">Create Product</button>
          <button class="cancel-btn" @click="() => { showRegisterDialog = false; reset(); }">Cancel</button>
        </div>
        <div v-if="message" :class="{error: isError}" style="margin-top:12px">{{ message }}</div>
      </div>
    </div>
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
// list of known process names (for SystemState dropdown in maintenance table)
const processNames = ref([])

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
  void loadProcessNames()
  void loadProducts()
})

async function loadProcessNames(){
  try{
    const res = await axios.get('/api/WorkHours/all-process-names')
    const names = Array.isArray(res.data) ? res.data.map(n => String(n)) : []
    // include NotStarted at the front and Finished at the end, avoid duplicates
    const set = new Set()
    set.add('NotStarted')
    for (const n of names) if (n) set.add(n)
    set.add('Finished')
    processNames.value = Array.from(set)
  }catch(e){
    console.warn('Failed to load process names', e)
    processNames.value = ['NotStarted','Finished']
  }
}

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

// close dialog on successful creation
watch(message, (m) => {
  if (m && !isError.value && showRegisterDialog.value) {
    // small delay to let user read message then close and reload list
    setTimeout(async () => {
      showRegisterDialog.value = false
      await loadProducts()
      // clear form for next open
      clearForm()
      message.value = ''
    }, 700)
  }
})

// --- Products maintenance logic ---
const maintOpen = ref(true)
function toggleMaint(){ maintOpen.value = !maintOpen.value }
const products = ref([])
const selectedProducts = ref(new Set())
const changedProducts = ref(new Set())
const showRegisterDialog = ref(false)
const prodFilter = null // filters removed per request

function onCheckChangeProd({ records }){
  const set = selectedProducts.value; set.clear(); for (const r of records) set.add(r.serialNo)
}
function markChangedProd(key){ changedProducts.value.add(key) }

async function loadProducts(){
  try{
    const res = await axios.get('/api/WorkHours/all-products')
    products.value = Array.isArray(res.data) ? res.data
      .filter(p => ((p.serialNo ?? p.SerialNo ?? '') !== '999999'))
      .map(p => ({
      id: p.id,
      projectNo: p.projectNo ?? p.ProjectNo ?? '',
      ivkNo: p.ivkNo ?? p.IvkNo ?? '',
      modalityType: p.modalityType ?? p.ModalityType ?? '',
      systemType: p.systemType ?? p.SystemType ?? '',
      serialNo: p.serialNo ?? p.SerialNo ?? '',
      productLine: p.productLine ?? p.ProductLine ?? '',
      unpackageHours: Number(p.unpackageHours ?? p.UnpackageHours ?? 0),
      assemblyHours: Number(p.assemblyHours ?? p.AssemblyHours ?? 0),
      debugHours: Number(p.debugHours ?? p.DebugHours ?? 0),
      validationHours: Number(p.validationHours ?? p.ValidationHours ?? 0),
      disassemblyHours: Number(p.disassemblyHours ?? p.DisassemblyHours ?? 0),
      repackageHours: Number(p.repackageHours ?? p.RepackageHours ?? 0),
      systemState: p.systemState ?? p.SystemState ?? ''
    })) : []
    selectedProducts.value.clear?.()
    changedProducts.value.clear?.()
  }catch(e){ console.error('Failed to load products', e) }
}

async function saveProducts(){
  if (!changedProducts.value.size) return
  const changedKeys = Array.from(changedProducts.value)
  const rows = products.value.filter(r => changedKeys.includes(r.serialNo))
  try{
    await Promise.all(rows.map(r => axios.put(`/api/WorkHours/product/${encodeURIComponent(r.serialNo)}`, {
      ProjectNo: r.projectNo || null,
      IvkNo: r.ivkNo || null,
      ModalityType: r.modalityType || null,
      SystemType: r.systemType || null,
      SerialNo: r.serialNo,
      ProductLine: r.productLine || null,
      UnpackageHours: r.unpackageHours ?? 0,
      AssemblyHours: r.assemblyHours ?? 0,
      DebugHours: r.debugHours ?? 0,
      ValidationHours: r.validationHours ?? 0,
      DisassemblyHours: r.disassemblyHours ?? 0,
      RepackageHours: r.repackageHours ?? 0,
      SystemState: r.systemState || null
    })))
    changedProducts.value.clear()
    await loadProducts()
  }catch(e){ console.error('Failed to save products', e) }
}

async function deleteProducts(){
  if (!selectedProducts.value.size) return
  const keys = Array.from(selectedProducts.value)
  try{
    await Promise.all(keys.map(sn => axios.delete(`/api/WorkHours/product/${encodeURIComponent(sn)}`)))
    selectedProducts.value.clear()
    await loadProducts()
  }catch(e){ console.error('Failed to delete products', e) }
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
/* Maintenance styles (shared feel with WorkHourMaintenance) */
.collapsible{ margin-top: 16px; border: 1px solid #ddd; border-radius: 6px }
.collapsible-header{ display:flex; justify-content:space-between; padding:8px; background:#f7f7f7; cursor:pointer }
.collapsible-body{ padding:12px }
.actions{ margin-bottom:8px }
.filters{ display:flex; gap:8px; align-items:center; margin-bottom:8px; flex-wrap:wrap }
.filter-input{ padding:6px 8px; border-radius:6px; border:1px solid #ddd }
.modern-vxe-table{ font-size:13px }
.cell-input{ width:100% }
.eh-input{ width:70px }

/* Reuse Planning-style switch button for New System */
.switch-btn { display: inline-flex; align-items: center; gap: 0.6rem; background: linear-gradient(90deg,#FFF4EA 0%,#FFF8F2 100%); border: 1px solid #F5D3B0; padding: 0.5rem 0.8rem; border-radius: 10px; cursor: pointer; box-shadow: 0 4px 10px rgba(236,102,2,0.08); }
.switch-btn:hover { transform: translateY(-2px); }
.switch-label { font-weight: 700; color: #6b3b1f; }
.forward-arrow { display:inline-flex; align-items:center; justify-content:center; background: #FFF3E8; color: #EC6602; font-weight: 800; border-radius: 999px; padding: 0.25rem 0.5rem; font-size: 1.1rem; box-shadow: 0 2px 6px rgba(236,102,2,0.12); }

/* Modal dialog for register form */
.modal-overlay{
  position:fixed; inset:0; background:rgba(0,0,0,0.35); display:flex; align-items:center; justify-content:center; z-index:9999;
}
.modal{ background:#fff; padding:16px; border-radius:10px; width: min(960px, 92%); box-shadow:0 8px 40px rgba(0,0,0,0.25); }
.modal h3{ margin-top:0 }
</style>
