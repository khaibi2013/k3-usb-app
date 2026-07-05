<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch } from 'vue';
import { Bug, FolderSearch, FileSearch, StopCircle, Trash2, ShieldAlert, RefreshCw } from '@lucide/vue';

const api = (window as any).electronAPI;

const isScanning = ref(false);
const scannedFiles = ref(0);
const threats = ref<any[]>([]);
const currentFile = ref('');
const activeTab = ref('results');
const quarantineList = ref<any[]>([]);
const logsList = ref<any[]>([]);

let startTime = 0;
let timerInterval: any = null;
const elapsedStr = ref('00:00:00');

const formatTime = (ms: number) => {
  const totalSeconds = Math.floor(ms / 1000);
  const h = String(Math.floor(totalSeconds / 3600)).padStart(2, '0');
  const m = String(Math.floor((totalSeconds % 3600) / 60)).padStart(2, '0');
  const s = String(totalSeconds % 60).padStart(2, '0');
  return `${h}:${m}:${s}`;
};

const handleProgress = (data: any) => {
  currentFile.value = data.name;
  scannedFiles.value++;
};

const handleThreat = (data: any) => {
  threats.value.push({
    id: Date.now() + Math.random().toString(),
    name: data.name,
    path: data.path,
    threat: data.threat,
    status: 'Phát hiện',
    selected: true
  });
};

const handleDone = () => {
  isScanning.value = false;
  clearInterval(timerInterval);
  currentFile.value = 'Quét hoàn tất';
};

const loadData = async () => {
  if (activeTab.value === 'quarantine') {
    quarantineList.value = await api.getQuarantineList();
  } else if (activeTab.value === 'logs') {
    logsList.value = await api.getScannerLogs();
  }
};
watch(activeTab, loadData);
onMounted(() => {
  loadData();
  api.onScannerProgress(handleProgress);
  api.onScannerThreatFound(handleThreat);
  api.onScannerDone(handleDone);
});

onUnmounted(() => {
  if (isScanning.value) {
    api.scannerStop();
  }
});

const startScan = async (paths: string[]) => {
  if (paths.length === 0) return;
  threats.value = [];
  scannedFiles.value = 0;
  isScanning.value = true;
  startTime = Date.now();
  
  timerInterval = setInterval(() => {
    elapsedStr.value = formatTime(Date.now() - startTime);
  }, 1000);

  await api.scannerStart(paths);
};

const chooseFolder = async () => {
  const res = await api.showOpenDialog({ properties: ['openDirectory'] });
  if (res && res.length) {
    startScan(res);
  }
};

const chooseFile = async () => {
  const res = await api.showOpenDialog({ properties: ['openFile', 'multiSelections'] });
  if (res && res.length) {
    startScan(res);
  }
};

const stopScan = () => {
  api.scannerStop();
  handleDone();
};

const toggleAll = (e: Event) => {
  const checked = (e.target as HTMLInputElement).checked;
  threats.value.forEach(t => t.selected = checked);
};

const handleQuarantineAction = async (action: string, id: string) => {
  const msg = action === 'delete' ? 'Bạn có chắc chắn muốn xóa vĩnh viễn tệp này khỏi khu cách ly?' : 'Khôi phục tệp này ra màn hình Desktop?';
  if (!confirm(msg)) return;
  try {
    const res = await api.quarantineAction(action, id);
    if (res.success) {
      alert(action === 'delete' ? 'Đã xóa vĩnh viễn!' : 'Đã khôi phục thành công ra Desktop!');
      await loadData();
    } else {
      alert('Lỗi: ' + res.error);
    }
  } catch (err) {
    alert('Vui lòng ấn Cmd + R để tải lại ứng dụng trước khi sử dụng tính năng này!');
  }
};

const handleAction = async (action: string) => {
  const selectedPaths = threats.value.filter(t => t.selected).map(t => t.path);
  if (selectedPaths.length === 0) return;
  
  const msg = action === 'delete' ? 'Bạn có chắc chắn muốn xóa vĩnh viễn các tệp nhiễm?' : 'Cách ly các tệp nhiễm?';
  if (!confirm(msg)) return;

  const res = await api.scannerAction(action, selectedPaths);
  if (res.success) {
    alert('Đã xử lý thành công!');
    threats.value = threats.value.filter(t => !t.selected);
    await loadData();
  } else {
    alert('Lỗi xử lý: ' + res.error);
  }
};

</script>

<template>
  <div class="scanner-container">
    <div class="page-title">
      <Bug :size="24" color="#ef4444" /> Quét mã độc (Anti-Virus)
    </div>

    <!-- Toolbar -->
    <div class="glass-panel toolbar">
      <button class="btn btn-outline" @click="chooseFolder" :disabled="isScanning">
        <FolderSearch :size="16" /> Chọn thư mục...
      </button>
      <button class="btn btn-outline" @click="chooseFile" :disabled="isScanning">
        <FileSearch :size="16" /> Chọn tệp...
      </button>
      <div style="flex:1"></div>
      <button class="btn" style="background: var(--danger)" v-if="isScanning" @click="stopScan">
        <StopCircle :size="16" /> Dừng quét
      </button>
      <button class="btn btn-primary" v-else @click="chooseFolder">Quét toàn diện</button>
    </div>

    <!-- Progress -->
    <div class="glass-panel progress-box">
      <div class="progress-info">
        <p><strong>[K3-AV]</strong> Đã quét {{ scannedFiles }} tệp, phát hiện <span style="color:var(--danger)">{{ threats.length }}</span> tệp nghi nhiễm.</p>
        <p class="text-muted" style="margin-top:5px; font-size:12px;">Đang quét: {{ currentFile || 'Đang chờ...' }}</p>
      </div>
      <div class="progress-bar-container">
        <div class="progress-bar" :class="{ 'scanning': isScanning }"></div>
      </div>
      <div class="stats-row">
        <span>Sạch: {{ scannedFiles - threats.length }}</span> |
        <span class="text-danger">Nhiễm: {{ threats.length }}</span> |
        <span>Bỏ qua: 0</span> |
        <span>Tổng: {{ scannedFiles }}</span> |
        <span>Thời gian: {{ elapsedStr }}</span>
      </div>
    </div>

    <!-- Results Table -->
    <div class="glass-panel table-container">
      <div class="tabs">
        <div class="tab" :class="{ active: activeTab === 'results' }" @click="activeTab = 'results'">Kết quả quét</div>
        <div class="tab" :class="{ active: activeTab === 'quarantine' }" @click="activeTab = 'quarantine'">Khu cách ly</div>
        <div class="tab" :class="{ active: activeTab === 'logs' }" @click="activeTab = 'logs'">Nhật ký</div>
      </div>
      
      <div class="table-actions" v-if="activeTab === 'results'">
        <button class="btn btn-outline btn-sm" @click="handleAction('quarantine')"><ShieldAlert :size="14" /> Cách ly đã chọn</button>
        <button class="btn btn-danger btn-sm" @click="handleAction('delete')"><Trash2 :size="14" /> Xóa tệp đã chọn</button>
      </div>

      <div class="table-wrapper" v-if="activeTab === 'results'">
        <table class="data-table">
          <thead>
            <tr>
              <th style="width:40px"><input type="checkbox" @change="toggleAll" checked /></th>
              <th>Tên tệp</th>
              <th>Đường dẫn</th>
              <th style="width:100px">Trạng thái</th>
              <th>Tên virus / dấu hiệu</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="threats.length === 0">
              <td colspan="5" style="text-align:center; padding: 20px; color: var(--text-muted)">Không có tệp nhiễm nào</td>
            </tr>
            <tr v-for="t in threats" :key="t.id" class="threat-row">
              <td><input type="checkbox" v-model="t.selected" /></td>
              <td>{{ t.name }}</td>
              <td class="text-muted" style="font-size: 12px;" :title="t.path">{{ t.path }}</td>
              <td class="text-danger">{{ t.status }}</td>
              <td class="text-danger">{{ t.threat }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="table-wrapper" v-if="activeTab === 'quarantine'">
        <table class="data-table">
          <thead>
            <tr>
              <th>Tên tệp gốc</th>
              <th>Đường dẫn gốc</th>
              <th>Ngày cách ly</th>
              <th style="width: 150px"></th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="quarantineList.length === 0">
              <td colspan="4" style="text-align:center; padding: 20px; color: var(--text-muted)">Khu cách ly hiện đang trống. K3 bảo vệ máy tính bạn an toàn tuyệt đối!</td>
            </tr>
            <tr v-for="q in quarantineList" :key="q.id">
              <td>{{ q.name }}</td>
              <td class="text-muted" style="font-size: 12px;">{{ q.originalPath }}</td>
              <td>{{ q.date }}</td>
              <td style="text-align: right; white-space: nowrap;">
                <button class="btn btn-outline btn-sm action-btn restore-btn" @click="handleQuarantineAction('restore', q.id)" title="Khôi phục ra Desktop">
                  <RefreshCw :size="14" /> Khôi phục
                </button>
                <button class="btn btn-outline btn-sm action-btn delete-btn" @click="handleQuarantineAction('delete', q.id)" title="Xóa vĩnh viễn">
                  <Trash2 :size="14" /> Xóa hẳn
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="table-wrapper" v-if="activeTab === 'logs'">
        <table class="data-table">
          <thead>
            <tr>
              <th>Thời gian</th>
              <th>Sự kiện</th>
              <th>Chi tiết</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="logsList.length === 0">
              <td colspan="3" style="text-align:center; padding: 20px; color: var(--text-muted)">Chưa có nhật ký hoạt động nào.</td>
            </tr>
            <tr v-for="l in logsList" :key="l.id">
              <td>{{ l.time }}</td>
              <td>{{ l.event }}</td>
              <td class="text-muted">{{ l.details }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>

<style scoped>
.scanner-container { padding: 10px; height: 100%; display: flex; flex-direction: column; gap: 15px; }

.toolbar { display: flex; gap: 10px; padding: 15px; align-items: center; }
.btn-outline { background: transparent; border: 1px solid var(--border); color: var(--text-main); }
.btn-outline:hover { background: rgba(255,255,255,0.05); }

.progress-box { padding: 15px; }
.progress-info { font-size: 13px; margin-bottom: 10px; }
.progress-bar-container { height: 16px; background: rgba(0,0,0,0.3); border-radius: 8px; overflow: hidden; margin-bottom: 10px; }
.progress-bar { width: 100%; height: 100%; background: var(--accent); opacity: 0; transition: opacity 0.3s; }
.progress-bar.scanning {
  opacity: 1;
  background: linear-gradient(90deg, #10b981 0%, #3b82f6 50%, #10b981 100%);
  background-size: 200% 100%;
  animation: loading 2s infinite linear;
}
@keyframes loading { 0% { background-position: 100% 0; } 100% { background-position: -100% 0; } }
.stats-row { font-size: 12px; color: var(--text-muted); }

.table-container { flex: 1; display: flex; flex-direction: column; overflow: hidden; padding: 0; }
.tabs { display: flex; border-bottom: 1px solid var(--border); background: rgba(0,0,0,0.2); }
.tab { padding: 12px 20px; font-size: 14px; cursor: pointer; border-bottom: 2px solid transparent; }
.tab.active { border-bottom-color: var(--accent); color: var(--accent); font-weight: 500; background: rgba(255,255,255,0.03); }

.table-actions { padding: 10px 15px; display: flex; justify-content: flex-end; gap: 10px; border-bottom: 1px solid var(--border); }
.btn-sm { padding: 6px 12px; font-size: 12px; }

.table-wrapper { flex: 1; overflow-y: auto; }
.data-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 13px; }
.data-table th { padding: 10px 15px; background: rgba(0,0,0,0.3); color: var(--text-muted); font-weight: 500; position: sticky; top: 0; }
.data-table td { padding: 10px 15px; border-bottom: 1px solid rgba(255,255,255,0.05); }
.threat-row { background: rgba(239, 68, 68, 0.05); }
.threat-row:hover { background: rgba(239, 68, 68, 0.1); }

.action-btn {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 4px 10px;
  border-radius: 6px;
  font-weight: 500;
  transition: all 0.2s ease;
  background: rgba(0,0,0,0.2);
}
.restore-btn { color: #10b981; border-color: rgba(16, 185, 129, 0.3); }
.restore-btn:hover { background: rgba(16, 185, 129, 0.1); border-color: #10b981; transform: translateY(-1px); }
.delete-btn { color: #ef4444; border-color: rgba(239, 68, 68, 0.3); }
.delete-btn:hover { background: rgba(239, 68, 68, 0.1); border-color: #ef4444; transform: translateY(-1px); }
</style>
