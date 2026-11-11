<script setup>
import { computed, inject, ref, watch } from 'vue'
import axios from 'axios'

// Negotiate authenticates connections: do not relay it through Vite's shared proxy.
// Production stays same-origin. Override the development origin if the backend port differs.
const adminApiOrigin = import.meta.env.DEV
  ? (import.meta.env.VITE_ADMIN_API_ORIGIN || `http://${window.location.hostname}:5063`)
  : ''
const adminApi = axios.create({ baseURL: adminApiOrigin, withCredentials: true })
const authenticationUrl = `${adminApiOrigin}/api/users`
const userRoles = inject('userRoles', ref([]))
const isAdmin = computed(() => userRoles.value.some(role => role.toLowerCase() === 'administrator'))
const availableRoles = ['Administrator', 'ProductionManager', 'ProcessEngineer', 'Worker']
const users = ref([])
const search = ref('')
const loading = ref(false)
const usersLoaded = ref(false)
const authenticationRequired = ref(false)
const saving = ref(false)
const deleting = ref(false)
const updatingId = ref(null)
const busy = computed(() => saving.value || deleting.value || updatingId.value !== null)
const error = ref('')
const success = ref('')
const pendingDelete = ref(null)
const emptyForm = () => ({ gid: '', fullName: '', mail: '', roles: [] })
const form = ref(emptyForm())
let loadSequence = 0

function normalizeUser(user) {
  const raw = user.role ?? user.Role
  const assigned = user.roles ?? user.Roles
  // Prefer the complete stored field, including legacy roles omitted by canonical API parsing.
  const source = typeof raw === 'string' && raw.trim() ? [raw]
    : Array.isArray(assigned) ? assigned : [assigned || '']
  const roles = []
  const seen = new Set()
  for (const value of source) {
    for (const part of String(value || '').split(/[,;|\r\n]+/)) {
      const text = part.trim()
      if (!text) continue
      const key = text.toLowerCase().replace(/[\s_-]/g, '').replace(/s$/, '')
      const role = availableRoles.find(item => item.toLowerCase() === key) || text
      if (!seen.has(role.toLowerCase())) {
        seen.add(role.toLowerCase())
        roles.push(role)
      }
    }
  }
  const mail = user.mail || ''
  return { ...user, mail, roles, originalMail: mail, originalRoles: [...roles] }
}

function rowChanged(user) {
  return user.mail !== user.originalMail ||
    JSON.stringify([...user.roles].sort()) !== JSON.stringify([...user.originalRoles].sort())
}

function validRow(user) {
  const mail = user.mail.trim()
  return user.roles.length > 0 && user.roles.every(role => availableRoles.includes(role)) &&
    mail.length <= 200 && (!mail || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(mail))
}

async function updateUser(user) {
  if (busy.value || !isAdmin.value || !usersLoaded.value || !rowChanged(user) || !validRow(user)) return
  updatingId.value = user.id
  error.value = ''
  success.value = ''
  try {
    const { data } = await adminApi.put(`/api/users/${user.id}`, {
      mail: user.mail.trim(), roles: [...user.roles]
    })
    if (!isAdmin.value) return
    users.value = users.value.map(row => row.id === user.id ? normalizeUser(data) : row)
    success.value = `${data.fullName} was updated.`
  } catch (err) {
    if (isAdmin.value) error.value = errorMessage(err)
  } finally {
    updatingId.value = null
  }
}

function addRole(user, event) {
  const role = event.target.value
  if (role && !busy.value && !user.roles.includes(role)) user.roles.push(role)
  event.target.value = ''
}

const filteredUsers = computed(() => {
  const query = search.value.trim().toLowerCase()
  return users.value.filter(user => [user.gid, user.fullName, user.mail, ...(user.roles || [])]
    .some(value => String(value || '').toLowerCase().includes(query)))
})

function errorMessage(err) {
  if ([401, 403].includes(err.response?.status)) usersLoaded.value = false
  if (err.response?.status === 401) {
    authenticationRequired.value = true
    return 'Windows authentication is required. Open backend authentication below, sign in with your administrator Windows account, then click Refresh. Selecting a name in the portal does not authenticate that account.'
  }
  if (err.response?.status === 403) {
    return 'Your authenticated Windows account does not match an Administrator in the user directory. Check its GID, email, or full directory display name.'
  }
  if (!err.response) {
    authenticationRequired.value = true
    return 'Cannot reach or authenticate with the backend. Check that the development backend is running on the configured origin and allows this frontend origin. Open backend authentication below to check Windows sign-in, then Refresh.'
  }
  const data = err.response?.data
  if (data?.errors) return Object.values(data.errors).flat().join(' ')
  return data?.detail || data?.message || 'The request failed. Please try again.'
}

async function loadUsers() {
  const sequence = ++loadSequence
  loading.value = true
  usersLoaded.value = false
  authenticationRequired.value = false
  error.value = ''
  try {
    const { data } = await adminApi.get('/api/users')
    if (!Array.isArray(data)) throw new Error('Invalid user directory response')
    if (sequence === loadSequence && isAdmin.value) {
      users.value = data.map(normalizeUser)
      usersLoaded.value = true
    }
  } catch (err) {
    if (sequence === loadSequence && isAdmin.value) error.value = errorMessage(err)
  } finally {
    if (sequence === loadSequence) loading.value = false
  }
}

async function addUser() {
  if (busy.value || !isAdmin.value || !usersLoaded.value) return
  saving.value = true
  error.value = ''
  success.value = ''
  try {
    const { data } = await adminApi.post('/api/users', {
      gid: form.value.gid.trim(), fullName: form.value.fullName.trim(),
      mail: form.value.mail.trim(), roles: [...form.value.roles]
    })
    if (!isAdmin.value) return
    users.value = [...users.value, normalizeUser(data)].sort((a, b) => a.fullName.localeCompare(b.fullName))
    form.value = emptyForm()
    success.value = `${data.fullName} was added.`
  } catch (err) {
    if (isAdmin.value) error.value = errorMessage(err)
  } finally {
    saving.value = false
  }
}

async function removeUser() {
  if (busy.value || !pendingDelete.value || !isAdmin.value || !usersLoaded.value) return
  const target = pendingDelete.value
  deleting.value = true
  error.value = ''
  success.value = ''
  try {
    await adminApi.delete(`/api/users/${target.id}`)
    if (!isAdmin.value) return
    users.value = users.value.filter(user => user.id !== target.id)
    success.value = `${target.fullName} was removed.`
  } catch (err) {
    if (isAdmin.value) error.value = errorMessage(err)
  } finally {
    deleting.value = false
    pendingDelete.value = null
  }
}

watch(isAdmin, allowed => {
  if (allowed) void loadUsers()
  else {
    ++loadSequence
    loading.value = false
    usersLoaded.value = false
    authenticationRequired.value = false
    users.value = []
    pendingDelete.value = null
    form.value = emptyForm()
    error.value = ''
    success.value = ''
  }
}, { immediate: true })
</script>

<template>
  <section v-if="isAdmin" class="admin-users">
    <header class="page-heading">
      <div><h2>User Administration</h2><p>Manage factory access and assign one or more roles to each user.</p></div>
      <span class="admin-badge">Administrator only</span>
    </header>
    <p v-if="error" class="notice error" role="alert">{{ error }}</p>
    <p v-if="authenticationRequired" class="notice"><a :href="authenticationUrl" target="_blank" rel="noopener noreferrer">Open backend authentication</a> · After signing in, return here and click Refresh.</p>
    <p v-if="success" class="notice success" role="status">{{ success }}</p>

    <form class="panel add-user" @submit.prevent="addUser">
      <h3>Add user</h3>
      <div class="fields">
        <label>Company account ID (GID, optional)<input v-model="form.gid" maxlength="100" placeholder="Windows account ID" /></label>
        <label>Full name<input v-model="form.fullName" required maxlength="200" placeholder="Full display name" /></label>
        <label>Email (optional)<input v-model="form.mail" type="email" maxlength="200" placeholder="name@company.com" /></label>
      </div>
      <fieldset><legend>Roles — select one or more</legend>
        <div class="role-options"><label v-for="role in availableRoles" :key="role"><input v-model="form.roles" type="checkbox" :value="role" />{{ role }}</label></div>
      </fieldset>
      <div class="form-footer"><span>Only full name and at least one role are required.</span><button type="submit" :disabled="busy || loading || !usersLoaded || !form.roles.length || !form.fullName.trim()">{{ saving ? 'Adding…' : 'Add user' }}</button></div>
    </form>

    <section class="panel user-list" aria-label="Users">
      <div class="list-heading"><h3>Users <span class="count">{{ users.length }}</span></h3>
        <div class="list-controls"><input v-model="search" type="search" aria-label="Search users" placeholder="Search name, account, email, or role" /><button type="button" class="secondary" :disabled="loading || busy" @click="loadUsers">Refresh</button></div>
      </div>
      <p v-if="loading" role="status">Loading users…</p>
      <p v-else-if="!usersLoaded" role="status">User directory could not be loaded. Resolve the error above, then click Refresh.</p>
      <div v-else class="table-scroll"><table><thead><tr><th>Full name</th><th>Account ID</th><th>Email</th><th>Roles</th><th>Action</th></tr></thead>
        <tbody><tr v-for="user in filteredUsers" :key="user.id" :class="{ 'row-dirty': rowChanged(user) }">
          <td>{{ user.fullName }}</td>
          <td>{{ user.gid || '—' }}</td>
          <td><input v-model="user.mail" type="email" maxlength="200" class="row-email" :aria-label="`Email for ${user.fullName}`" placeholder="Email (optional)" :disabled="busy" /></td>
          <td>
            <div class="role-tags"><span v-for="role in user.roles" :key="role" class="editable-role">{{ role }}<button type="button" class="role-remove" :aria-label="`Remove ${role} role from ${user.fullName}`" :disabled="busy" @click="user.roles = user.roles.filter(item => item !== role)">×</button></span></div>
            <select v-if="availableRoles.some(role => !user.roles.includes(role))" class="role-add" :aria-label="`Add role to ${user.fullName}`" :disabled="busy" @change="addRole(user, $event)"><option value="">+ Add role</option><option v-for="role in availableRoles.filter(role => !user.roles.includes(role))" :key="role" :value="role">{{ role }}</option></select>
            <small v-if="!user.roles.length" class="row-validation">At least one role is required.</small>
          </td>
          <td><div class="row-actions"><button type="button" class="remove" :disabled="busy" :aria-label="`Remove ${user.fullName}`" @click="pendingDelete = user">Remove</button><button v-if="rowChanged(user)" type="button" :disabled="busy || !validRow(user)" :aria-label="`Update ${user.fullName}`" @click="updateUser(user)">{{ updatingId === user.id ? 'Updating…' : 'Update' }}</button></div></td>
        </tr>
          <tr v-if="!filteredUsers.length"><td colspan="5" class="empty">{{ users.length ? 'No matching users.' : 'No users found.' }}</td></tr>
        </tbody>
      </table></div>
    </section>

    <div v-if="pendingDelete" class="confirm-overlay" @click.self="!deleting && (pendingDelete = null)" @keydown.esc="!deleting && (pendingDelete = null)">
      <section class="confirm-dialog" role="alertdialog" aria-modal="true" aria-labelledby="remove-title" aria-describedby="remove-description">
        <h3 id="remove-title">Remove user?</h3><p id="remove-description">Remove <strong>{{ pendingDelete.fullName }}</strong> ({{ pendingDelete.gid || 'no account ID' }}) from the user directory? Existing work records will not be deleted.</p>
        <div class="confirm-actions"><button type="button" class="secondary" :disabled="deleting" autofocus @click="pendingDelete = null">Cancel</button><button type="button" class="remove" :disabled="deleting" @click="removeUser">{{ deleting ? 'Removing…' : 'Remove user' }}</button></div>
      </section>
    </div>
  </section>
  <section v-else class="admin-users"><h2>Administrator access required</h2><p>This page is only available to administrators.</p><router-link to="/">Return home</router-link></section>
</template>

<style scoped>
.admin-users { width: 100%; color: #5b3312; }
.page-heading, .list-heading, .form-footer, .list-controls { display: flex; align-items: center; justify-content: space-between; gap: 16px; flex-wrap: wrap; }
h2 { color: #a64e00; margin: 0 0 8px; }
h3, p { margin: 0; }
.page-heading { margin-bottom: 20px; }
.page-heading p, .form-footer > span { color: #795f4d; font-size: .9rem; }
.admin-badge, .count { background: #fff0e4; border-radius: 20px; padding: 5px 12px; font-size: .85rem; }
.panel { background: #fff; padding: 20px; border: 1px solid #f2d5bf; border-radius: 12px; margin-bottom: 20px; box-shadow: 0 4px 16px #82451f08; }
.fields { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 16px; margin: 16px 0; }
.fields label { display: flex; flex-direction: column; gap: 6px; font-size: .9rem; }
input:not([type=checkbox]) { min-width: 0; box-sizing: border-box; padding: 9px 10px; border: 1px solid #dfcbbb; border-radius: 6px; font: inherit; background: white; color: #40352e; }
input:focus-visible, button:focus-visible { outline: 2px solid #ec6602; outline-offset: 2px; }
fieldset { margin: 0 0 16px; padding: 12px; border: 1px solid #eedbcc; border-radius: 8px; }
legend { padding: 0 6px; font-size: .9rem; }
.role-options, .role-tags { display: flex; flex-wrap: wrap; gap: 8px 18px; }
.role-options label { display: flex; align-items: center; gap: 6px; }
.role-options input { accent-color: #ec6602; }
button { padding: 9px 16px; background: #ec6602; color: white; border: 1px solid transparent; border-radius: 6px; cursor: pointer; font: inherit; }
button:disabled { opacity: .5; cursor: not-allowed; }
.secondary { background: #fff7f0; border-color: #e6c9b0; color: #82451f; }
.remove { background: #fff1ef; border-color: #efc3bd; color: #ad3025; }
.list-heading { margin-bottom: 16px; }
.list-controls { flex: 1; justify-content: flex-end; }
.list-controls input { flex: 1; max-width: 400px; }
.table-scroll { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; text-align: left; }
th, td { padding: 12px; border-bottom: 1px solid #f0e2d7; }
th { background: #fff7f0; font-size: .9rem; }
td { overflow-wrap: anywhere; }
.role-tags { gap: 5px; }
.role-tags span { padding: 4px 8px; border-radius: 5px; background: #fff0e4; font-size: .8rem; }
.role-tags .editable-role { position: relative; padding: 7px 24px 7px 8px; }
.role-tags .role-remove { position: absolute; top: -3px; right: -3px; display: grid; place-items: center; width: 18px; height: 18px; padding: 0; border-radius: 50%; background: #82451f; color: white; font-size: 14px; line-height: 1; }
.row-email { width: 100%; min-width: 180px !important; }
.role-add { margin-top: 8px; padding: 5px 8px; border: 1px solid #dfcbbb; border-radius: 6px; background: white; color: #82451f; }
.row-actions { display: flex; gap: 8px; }
.row-dirty { background: #fffbf5; }
.row-validation { display: block; margin-top: 6px; color: #a52e22; }
.notice { padding: 12px 16px; border-radius: 8px; margin-bottom: 16px; }
.error { background: #fff0ee; color: #a52e22; }
.success { background: #edf8ef; color: #236137; }
.empty { text-align: center; color: #795f4d; }
.confirm-overlay { position: fixed; inset: 0; background: #0006; display: flex; align-items: center; justify-content: center; padding: 20px; z-index: 1000; }
.confirm-dialog { background: white; width: min(460px, 100%); padding: 24px; border-radius: 12px; box-shadow: 0 12px 40px #0003; }
.confirm-dialog p { margin: 16px 0 24px; line-height: 1.6; overflow-wrap: anywhere; }
.confirm-actions { display: flex; gap: 10px; justify-content: flex-end; }
@media (max-width: 800px) { .fields { grid-template-columns: 1fr; } .panel { padding: 16px; } .list-controls { width: 100%; } .list-controls input { width: 100%; max-width: none; } }
</style>