<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { Activity, HardDrive, ShieldCheck, ShieldAlert, EyeOff } from '@lucide/vue';

const stats = ref<any>(null);
const api = (window as any).electronAPI;

onMounted(async () => {
  stats.value = await api.getDashboardStats();
});

const formatBytes = (bytes: number) => {
  if (bytes === 0) return '0 Bytes';
  const k = 1024;
  const sizes = ['Bytes', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
};
</script>

<template>
  <div class="dashboard-container">
    <div class="page-title">
      <Activity :size="24" color="var(--accent)" /> Tổng quan Bảo mật
    </div>

    <div v-if="stats" class="stats-grid">
      <!-- Security Score -->
      <div class="glass-panel stat-card score-card">
        <div class="donut-chart-container">
          <svg viewBox="0 0 100 100" class="donut-svg">
            <circle cx="50" cy="50" r="40" class="donut-bg"></circle>
            <circle cx="50" cy="50" r="40" class="donut-fill" 
                    :stroke="stats.isDecoyMode ? 'var(--danger)' : 'var(--accent)'" 
                    :stroke-dasharray="251.2" 
                    :stroke-dashoffset="251.2 - (251.2 * stats.securityScore) / 100"></circle>
          </svg>
          <div class="donut-center">
            <div class="score-val" :class="stats.isDecoyMode ? 'text-danger' : 'text-success'">
              {{ stats.securityScore }}
            </div>
            <span style="font-size: 10px; color: var(--text-muted);">ĐIỂM</span>
          </div>
        </div>
        <div class="stat-info">
          <h3>Trạng thái Két sắt</h3>
          <p v-if="stats.isDecoyMode" class="text-danger" style="font-size: 12px; font-weight: bold; margin-top: 5px;">
            <EyeOff :size="12" style="vertical-align: middle;" /> KẾT SẮT GIẢ KÍCH HOẠT
          </p>
          <p v-else class="text-muted" style="font-size: 12px; margin-top: 5px;">Hệ thống an toàn. Mã hóa AES-256-GCM đang hoạt động.</p>
        </div>
      </div>

      <!-- Storage Info -->
      <div class="glass-panel stat-card">
        <div class="donut-chart-container">
          <svg viewBox="0 0 100 100" class="donut-svg">
            <circle cx="50" cy="50" r="40" class="donut-bg"></circle>
            <circle cx="50" cy="50" r="40" class="donut-fill" 
                    stroke="#3b82f6" 
                    :stroke-dasharray="251.2" 
                    :stroke-dashoffset="stats.usbTotalSize ? 251.2 - (251.2 * (stats.usbUsedSize / stats.usbTotalSize)) : 251.2"></circle>
          </svg>
          <div class="donut-center">
            <div class="score-val" style="color: #3b82f6; font-size: 20px;">
              {{ stats.usbTotalSize ? Math.round((stats.usbUsedSize / stats.usbTotalSize) * 100) : 0 }}%
            </div>
            <span style="font-size: 10px; color: var(--text-muted);">ĐÃ DÙNG</span>
          </div>
        </div>
        <div class="stat-info" style="flex: 1; width: 100%;">
          <h3>Dữ liệu đang bảo vệ</h3>
          <div style="font-size: 20px; font-weight: bold; margin-top: 5px;">{{ formatBytes(stats.totalSize) }}</div>
          <p class="text-muted" style="font-size: 12px; margin-top: 5px;">Gồm {{ stats.encryptedCount }} tệp tin.</p>
          
          <div v-if="stats.usbTotalSize" style="margin-top: 10px; font-size: 12px; color: var(--text-muted);">
             USB: {{ formatBytes(stats.usbUsedSize) }} / {{ formatBytes(stats.usbTotalSize) }}
          </div>
        </div>
      </div>
      
      <!-- System Info -->
      <div class="glass-panel stat-card" style="grid-column: span 2;">
        <h3>Trạng thái hệ thống</h3>
        <ul class="sys-list">
          <li><span class="dot green"></span> Chống Bruteforce (Max 10 lần sai): <strong>Kích hoạt</strong></li>
          <li><span class="dot green"></span> Tự động khóa (Idle 5 phút): <strong>Kích hoạt</strong></li>
          <li><span class="dot green"></span> Hủy dữ liệu an toàn (DoD 5220.22-M): <strong>Kích hoạt</strong></li>
          <li><span class="dot green"></span> Quét mã độc chủ động (Active Scanner): <strong>Kích hoạt (Heuristics)</strong></li>
        </ul>
      </div>
    </div>
  </div>
</template>

<style scoped>
.dashboard-container { padding: 10px; height: 100%; overflow-y: auto; }
.stats-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; }
.stat-card { padding: 25px; display: flex; gap: 20px; align-items: center; }
.stat-card.score-card { background: linear-gradient(135deg, rgba(16, 185, 129, 0.1), rgba(16, 185, 129, 0.02)); }
.stat-info h3 { font-size: 14px; color: var(--text-muted); font-weight: 500; margin-bottom: 5px; }
.score-val { font-size: 36px; font-weight: 800; line-height: 1; }

.donut-chart-container { position: relative; width: 80px; height: 80px; flex-shrink: 0; }
.donut-svg { width: 100%; height: 100%; transform: rotate(-90deg); }
.donut-bg { fill: none; stroke: rgba(255,255,255,0.1); stroke-width: 8; }
.donut-fill { fill: none; stroke-width: 8; stroke-linecap: round; transition: stroke-dashoffset 1s ease-in-out; }
.donut-center { position: absolute; top: 0; left: 0; right: 0; bottom: 0; display: flex; flex-direction: column; align-items: center; justify-content: center; }

.sys-list { list-style: none; margin-top: 15px; }
.sys-list li { margin-bottom: 12px; font-size: 14px; display: flex; align-items: center; gap: 10px; color: var(--text-muted); }
.sys-list li strong { color: var(--text-main); font-weight: 500; }
.dot { width: 8px; height: 8px; border-radius: 50%; display: inline-block; }
.dot.green { background: var(--accent); box-shadow: 0 0 8px var(--accent); }
</style>
