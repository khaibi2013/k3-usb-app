<script setup lang="ts">
import { toasts } from '../utils/toast';
import { CheckCircle, AlertTriangle, Info } from '@lucide/vue';
</script>

<template>
  <div class="toast-container">
    <TransitionGroup name="toast">
      <div v-for="t in toasts" :key="t.id" class="toast-item glass-panel" :class="t.type">
        <CheckCircle v-if="t.type === 'success'" :size="18" color="var(--accent)" />
        <AlertTriangle v-else-if="t.type === 'error'" :size="18" color="var(--danger)" />
        <Info v-else :size="18" color="#3b82f6" />
        <span>{{ t.message }}</span>
      </div>
    </TransitionGroup>
  </div>
</template>

<style scoped>
.toast-container {
  position: fixed;
  bottom: 20px;
  right: 20px;
  display: flex;
  flex-direction: column;
  gap: 10px;
  z-index: 10000;
  pointer-events: none;
}

.toast-item {
  padding: 12px 20px;
  border-radius: 8px;
  color: white;
  font-size: 14px;
  box-shadow: 0 4px 12px rgba(0,0,0,0.5);
  pointer-events: auto;
  border-left: 4px solid var(--accent);
  display: flex;
  align-items: center;
  gap: 10px;
  background: var(--bg-panel);
}

.toast-item.success { border-left-color: var(--accent); }
.toast-item.error { border-left-color: var(--danger); }
.toast-item.info { border-left-color: #3b82f6; }

.toast-enter-active, .toast-leave-active { transition: all 0.3s ease; }
.toast-enter-from { opacity: 0; transform: translateX(30px); }
.toast-leave-to { opacity: 0; transform: translateX(30px); }
</style>
