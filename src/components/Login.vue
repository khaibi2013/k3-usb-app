<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { Shield, ShieldAlert, KeyRound, Unlock, Settings2, Lock } from '@lucide/vue';

const emit = defineEmits(['authenticated']);

const isSetup = ref(false);
const password = ref('');
const confirmPassword = ref('');
const error = ref('');
const isLocked = ref(false);
const loading = ref(false);

const api = (window as any).electronAPI;

onMounted(async () => {
  const hasSetup = await api.checkSetup();
  isSetup.value = !hasSetup;
});

const handleSetup = async () => {
  if (password.value !== confirmPassword.value) {
    error.value = 'Mật khẩu xác nhận không khớp!'; return;
  }
  if (password.value.length < 6) {
    error.value = 'Mật khẩu phải từ 6 ký tự trở lên.'; return;
  }
  
  loading.value = true;
  await api.setupPassword(password.value);
  const res = await api.login(password.value);
  if (res.success) emit('authenticated');
  loading.value = false;
};

const handleLogin = async () => {
  error.value = '';
  loading.value = true;
  const res = await api.login(password.value);
  loading.value = false;
  
  if (res.success) {
    emit('authenticated');
  } else {
    error.value = res.error;
    if (res.locked) isLocked.value = true;
    password.value = '';
  }
};
</script>

<template>
  <div class="login-container">
    <div class="glass-panel login-box" :class="{ 'locked': isLocked }">
      <div class="header">
        <Lock v-if="isLocked" :size="56" color="var(--danger)" class="shake" />
        <Shield v-else-if="!isSetup" :size="56" color="var(--accent)" class="icon-pulse" />
        <Settings2 v-else :size="56" color="var(--accent)" />
        
        <h2 :class="{ 'text-danger': isLocked }">
          {{ isLocked ? 'THIẾT BỊ ĐÃ BỊ KHÓA' : (isSetup ? 'Thiết lập Két sắt' : 'Xác thực Bảo mật') }}
        </h2>
        <p class="subtitle" v-if="!isLocked">
          {{ isSetup ? 'Tạo mật khẩu chủ để mã hóa dữ liệu.' : 'IronVault Enterprise Edition' }}
        </p>
        <p class="subtitle text-danger" v-else>
          Đã vượt quá số lần đăng nhập. Dữ liệu bị khóa.
        </p>
      </div>

      <div class="form" v-if="!isLocked">
        <div class="input-group">
          <KeyRound class="input-icon" :size="18" />
          <input 
            v-model="password" type="password" class="input-field with-icon" 
            placeholder="Mật khẩu chủ" @keyup.enter="isSetup ? handleSetup() : handleLogin()"
          />
        </div>

        <div v-if="isSetup" class="input-group">
          <KeyRound class="input-icon" :size="18" />
          <input 
            v-model="confirmPassword" type="password" class="input-field with-icon" 
            placeholder="Xác nhận mật khẩu" @keyup.enter="handleSetup()"
          />
        </div>

        <div v-if="error" class="error-msg">
          <ShieldAlert :size="16" /> {{ error }}
        </div>

        <button class="btn w-full" :disabled="loading" @click="isSetup ? handleSetup() : handleLogin()">
          <Unlock :size="16" />
          {{ loading ? 'Đang xử lý...' : (isSetup ? 'Tạo Két sắt' : 'Mở Khóa') }}
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.login-container { display: flex; justify-content: center; align-items: center; height: 100%; }
.login-box { padding: 50px 40px; width: 100%; max-width: 420px; text-align: center; position: relative; overflow: hidden; }
.login-box::before { content: ''; position: absolute; top: 0; left: 0; right: 0; height: 4px; background: var(--accent); }
.login-box.locked::before { background: var(--danger); }
.header { margin-bottom: 35px; }
.header h2 { margin-top: 20px; font-size: 22px; font-weight: 700; letter-spacing: 0.5px; }
.subtitle { color: var(--text-muted); font-size: 14px; margin-top: 8px; }
.form { display: flex; flex-direction: column; gap: 16px; }
.input-group { position: relative; }
.input-icon { position: absolute; left: 14px; top: 50%; transform: translateY(-50%); color: var(--text-muted); }
.with-icon { padding-left: 42px; padding-top: 12px; padding-bottom: 12px; }
.w-full { width: 100%; padding: 14px; font-size: 15px; }
.error-msg { color: var(--danger); font-size: 13px; display: flex; align-items: center; justify-content: center; gap: 6px; background: rgba(239, 68, 68, 0.1); padding: 10px; border-radius: 6px; border: 1px solid rgba(239, 68, 68, 0.2); }
@keyframes pulse { 0% { transform: scale(1); filter: drop-shadow(0 0 0 rgba(16, 185, 129, 0)); } 50% { transform: scale(1.05); filter: drop-shadow(0 0 15px rgba(16, 185, 129, 0.5)); } 100% { transform: scale(1); filter: drop-shadow(0 0 0 rgba(16, 185, 129, 0)); } }
.icon-pulse { animation: pulse 2s infinite ease-in-out; }
@keyframes shake { 0%, 100% { transform: translateX(0); } 20%, 60% { transform: translateX(-5px); } 40%, 80% { transform: translateX(5px); } }
.shake { animation: shake 0.5s ease-in-out; }
</style>
