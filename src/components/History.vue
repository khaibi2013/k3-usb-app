<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { History as HistoryIcon } from '@lucide/vue';

const history = ref<any[]>([]);
const selectedIds = ref<Set<string>>(new Set());

const api = (window as any).electronAPI;

const loadHistory = async () => {
  history.value = await api.getUsbHistory();
};

onMounted(() => {
  loadHistory();
  api.onUsbHistoryUpdated(() => {
    loadHistory();
  });
});

const toggleSelect = (id: string) => {
  if (selectedIds.value.has(id)) selectedIds.value.delete(id);
  else selectedIds.value.add(id);
};

const deleteSelected = async () => {
  if (selectedIds.value.size === 0) return;
  if (confirm('Xóa các bản ghi lịch sử đã chọn?')) {
    await api.deleteUsbHistoryItems(Array.from(selectedIds.value));
    selectedIds.value.clear();
    await loadHistory();
  }
};

const deleteAll = async () => {
  if (confirm('CẢNH BÁO: Xóa TOÀN BỘ lịch sử kết nối USB?')) {
    await api.clearUsbHistory();
    selectedIds.value.clear();
    await loadHistory();
  }
};
</script>

<template>
  <div class="history-container">
    <div class="page-title">
      <HistoryIcon :size="24" color="var(--accent)" /> Lịch sử kết nối USB
    </div>

    <div class="glass-panel main-panel">
      <div class="panel-header">
        <div>
          <h3>Lịch sử kết nối USB</h3>
          <p class="text-muted" style="font-size: 13px; margin-top: 5px;">
            Hiển thị lịch sử cắm USB, xem chi tiết và xóa lịch sử theo từng thiết bị hoặc toàn bộ.
            Đã nạp {{ history.length }} bản ghi.
          </p>
        </div>
        <div class="actions">
          <button class="btn btn-outline" @click="loadHistory">Làm mới</button>
          <button class="btn btn-danger-outline" :disabled="selectedIds.size === 0" @click="deleteSelected">Xóa USB đã chọn</button>
          <button class="btn btn-danger" @click="deleteAll">Xóa toàn bộ lịch sử kết nối USB</button>
        </div>
      </div>

      <div class="table-container">
        <table class="history-table">
          <thead>
            <tr>
              <th width="40"></th>
              <th width="160">Thời gian</th>
              <th>Tên thiết bị</th>
              <th width="150">Loại thiết bị</th>
              <th width="150">Trạng thái</th>
              <th width="180">Số serial</th>
              <th>Device ID</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="history.length === 0">
              <td colspan="7" class="text-center text-muted py-4">Chưa có lịch sử kết nối USB nào được ghi nhận.</td>
            </tr>
            <tr v-for="h in history" :key="h.deviceId + h.time" :class="{ 'selected': selectedIds.has(h.deviceId) }" @click="toggleSelect(h.deviceId)">
              <td class="text-center">
                <input type="checkbox" :checked="selectedIds.has(h.deviceId)" @click.stop="toggleSelect(h.deviceId)" />
              </td>
              <td>{{ h.time }}</td>
              <td class="font-medium">{{ h.name }}</td>
              <td>{{ h.type }}</td>
              <td>
                <span class="status-badge" :class="h.status === 'Đang kết nối' ? 'active' : 'inactive'">
                  {{ h.status }}
                </span>
              </td>
              <td class="font-mono text-xs">{{ h.serial }}</td>
              <td class="font-mono text-xs truncate" :title="h.deviceId">{{ h.deviceId }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>

<style scoped>
.history-container { display: flex; flex-direction: column; height: 100%; padding: 10px; }
.main-panel { display: flex; flex-direction: column; flex: 1; overflow: hidden; }

.panel-header { padding: 20px; border-bottom: 1px solid var(--border); display: flex; justify-content: space-between; align-items: flex-end; }
.panel-header h3 { font-size: 18px; font-weight: 600; color: var(--text-main); }
.actions { display: flex; gap: 10px; }

.table-container { flex: 1; overflow: auto; }
.history-table { width: 100%; border-collapse: collapse; }
.history-table th { padding: 12px 10px; text-align: left; font-size: 13px; font-weight: 600; color: var(--text-muted); position: sticky; top: 0; background: var(--bg-panel); border-bottom: 1px solid var(--border); z-index: 1; white-space: nowrap; }
.history-table td { padding: 12px 10px; font-size: 13px; border-bottom: 1px solid rgba(255,255,255,0.02); color: var(--text-main); cursor: pointer; }
.history-table tr:hover td { background: rgba(255,255,255,0.03); }
.history-table tr.selected td { background: rgba(16, 185, 129, 0.1); }

.truncate { max-width: 150px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; display: inline-block; vertical-align: middle; }
.font-mono { font-family: 'Courier New', Courier, monospace; }
.text-xs { font-size: 11px; color: var(--text-muted); }
.font-medium { font-weight: 500; }
.text-center { text-align: center; }
.py-4 { padding-top: 20px !important; padding-bottom: 20px !important; }

.status-badge { padding: 4px 8px; border-radius: 4px; font-size: 12px; font-weight: 600; }
.status-badge.active { background: rgba(16, 185, 129, 0.2); color: #10b981; }
.status-badge.inactive { background: rgba(239, 68, 68, 0.1); color: var(--text-muted); }

input[type="checkbox"] { width: 16px; height: 16px; accent-color: var(--accent); cursor: pointer; }
</style>
