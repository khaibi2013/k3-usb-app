<script setup lang="ts">
import { ref } from 'vue';
import { Settings as SettingsIcon, Save, EyeOff, Lock, Usb, Shield } from '@lucide/vue';

const decoyPassword = ref('');
const confirmDecoy = ref('');
const error = ref('');
const successMsg = ref('');
const loading = ref(false);

const decoySetup = ref(false);
const decoySizeMB = ref(0);

const isReadOnly = ref(false);
const readOnlyLoading = ref(false);

const cleanSystem = ref(true);
const showHidden = ref(false);
const autoClearHistory = ref(false);
const appTheme = ref('emerald');

const oldPassword = ref('');
const newPassword = ref('');
const pwdMsg = ref('');
const pwdError = ref('');

const hwidMsg = ref('');
const hwidError = ref('');

const api = (window as any).electronAPI;

import { onMounted } from 'vue';
onMounted(async () => {
  const cfg = await api.getConfig();
  if (cfg.cleanSystemJunk !== undefined) cleanSystem.value = cfg.cleanSystemJunk;
  if (cfg.showHidden !== undefined) showHidden.value = cfg.showHidden;
  if (cfg.autoClearHistory !== undefined) autoClearHistory.value = cfg.autoClearHistory;
  if (cfg.appTheme !== undefined) appTheme.value = cfg.appTheme;
  
  isReadOnly.value = await api.checkReadOnly();
  
  await loadDecoyInfo();
});

const loadDecoyInfo = async () => {
  const info = await api.getDecoyInfo();
  decoySetup.value = info.isSetup;
  decoySizeMB.value = info.sizeMB;
};

const handleSaveGeneral = async () => {
  await api.updateConfig({
    cleanSystemJunk: cleanSystem.value,
    showHidden: showHidden.value,
    autoClearHistory: autoClearHistory.value,
    appTheme: appTheme.value
  });
  document.documentElement.setAttribute('data-theme', appTheme.value);
  (window as any).showToast('Đã lưu Cài đặt Chung', 'success');
};

const handleChangePassword = async () => {
  pwdError.value = '';
  pwdMsg.value = '';
  if (!oldPassword.value || !newPassword.value) {
    pwdError.value = 'Vui lòng nhập đầy đủ mật khẩu.';
    return;
  }
  if (newPassword.value.length < 6) {
    pwdError.value = 'Mật khẩu mới phải từ 6 ký tự.';
    return;
  }
  const res = await api.changePassword(oldPassword.value, newPassword.value);
  if (res.success) {
    pwdMsg.value = 'Đổi mật khẩu thành công! Toàn bộ Két sắt đã được mã hóa lại bằng khoá mới.';
    oldPassword.value = '';
    newPassword.value = '';
  } else {
    pwdError.value = res.error;
  }
};

const handleEnableHwid = async () => {
  hwidError.value = '';
  hwidMsg.value = '';
  const confirmAction = confirm('CẢNH BÁO: Tính năng này sẽ KHÓA CHẾT Két sắt vào chiếc USB hiện tại. Nếu USB hỏng chip, dữ liệu KHÔNG THỂ KHÔI PHỤC trên USB khác! Bạn có chắc chắn muốn bật?');
  if (!confirmAction) return;
  
  const res = await api.enableHwidLock();
  if (res.success) {
    hwidMsg.value = 'Đã BẬT Khóa Phần cứng (Hardware ID Lock) thành công!';
  } else {
    hwidError.value = 'Lỗi: ' + res.error;
  }
};

const handleToggleReadOnly = async () => {
  readOnlyLoading.value = true;
  const confirmAction = confirm(isReadOnly.value ? 
      'Bạn sắp MỞ KHÓA ổ USB. USB sẽ cho phép ghi dữ liệu, nhưng có nguy cơ bị lây nhiễm virus nếu cắm vào máy lạ. Bạn có chắc chắn?' :
      'Bạn sắp KHÓA GHI ổ USB. Dữ liệu sẽ an toàn tuyệt đối, nhưng bạn KHÔNG THỂ tống thêm file mới vào Két sắt.\n\nLưu ý: Sẽ có một bảng đen (UAC) của Windows hiện lên yêu cầu quyền, hãy chọn Yes/Cho phép.\n\nBạn có chắc chắn muốn khóa?'
  );
  if (!confirmAction) {
      readOnlyLoading.value = false;
      return;
  }
  
  const res = await api.toggleReadOnly(!isReadOnly.value);
  if (res.success) {
      setTimeout(async () => {
          isReadOnly.value = await api.checkReadOnly();
          readOnlyLoading.value = false;
          alert(isReadOnly.value ? 'USB đã được KHÓA GHI an toàn.' : 'USB đã được MỞ KHÓA.');
      }, 5500); 
  } else {
      readOnlyLoading.value = false;
      alert('Lỗi: ' + res.error);
  }
};

const handleSetupDecoy = async () => {
  error.value = '';
  successMsg.value = '';
  if (!decoyPassword.value) return;
  if (decoyPassword.value !== confirmDecoy.value) {
    error.value = 'Mật khẩu xác nhận không khớp!'; return;
  }
  if (decoyPassword.value.length < 6) {
    error.value = 'Mật khẩu phải từ 6 ký tự trở lên.'; return;
  }
  
  loading.value = true;
  await api.setupDecoy(decoyPassword.value);
  successMsg.value = 'Đã thiết lập Két sắt giả thành công!';
  decoyPassword.value = '';
  confirmDecoy.value = '';
  loading.value = false;
  await loadDecoyInfo();
};

const handleWipeDecoy = async () => {
  if (!confirm('Hành động này sẽ XOÁ VĨNH VIỄN toàn bộ dữ liệu trong két sắt giả và gỡ bỏ mật khẩu giả. Bạn có chắc chắn?')) return;
  
  loading.value = true;
  await api.wipeDecoy();
  await loadDecoyInfo();
  (window as any).showToast('Đã xoá sạch Két sắt giả', 'success');
  loading.value = false;
};
</script>

<template>
  <div class="settings-container">
    <div class="page-title">
      <SettingsIcon :size="24" color="var(--text-main)" /> Cài đặt Bảo mật
    </div>

    <div class="glass-panel settings-card">
      <div class="card-header">
        <EyeOff :size="24" color="var(--accent)" />
        <div>
          <h3>Thiết lập Két sắt Giả (Decoy Vault)</h3>
          <p class="text-muted">Sử dụng tính năng Plausible Deniability. Tạo một mật khẩu thứ 2 để mở ra một két sắt giả không có dữ liệu quan trọng, dùng trong trường hợp bạn bị ép buộc phải mở phần mềm.</p>
        </div>
      </div>
      
      <div class="form">
        <input v-model="decoyPassword" type="password" class="input-field" placeholder="Mật khẩu giả" />
        <input v-model="confirmDecoy" type="password" class="input-field" placeholder="Xác nhận mật khẩu giả" />
        
        <p v-if="error" class="text-danger" style="font-size: 13px;">{{ error }}</p>
        <p v-if="successMsg" class="text-success" style="font-size: 13px;">{{ successMsg }}</p>
        
        <div style="display: flex; gap: 10px; align-items: center; margin-bottom: 15px;" v-if="decoySetup">
          <span style="color: var(--accent); font-weight: bold;">Trạng thái: Đang hoạt động</span>
          <span style="color: var(--text-muted); font-size: 14px;">(Dung lượng: {{ decoySizeMB.toFixed(2) }} MB)</span>
        </div>
        
        <div style="display: flex; gap: 10px;">
          <button class="btn" :disabled="loading || !decoyPassword" @click="handleSetupDecoy" style="flex: 1;">
            <Save :size="16" /> {{ decoySetup ? 'ĐỔI MẬT KHẨU GIẢ' : 'TẠO KÉT GIẢ' }}
          </button>
          
          <button v-if="decoySetup" class="btn btn-danger" :disabled="loading" @click="handleWipeDecoy">
            XOÁ KÉT GIẢ
          </button>
        </div>
      </div>
    </div>
    
    <div class="glass-panel settings-card">
      <div class="card-header">
        <Lock :size="24" color="#ef4444" />
        <div>
          <h3>Đổi mật khẩu bảo vệ</h3>
          <p class="text-muted">Thay đổi mật khẩu đăng nhập. Toàn bộ két sắt sẽ được mã hóa lại.</p>
        </div>
      </div>
      <div class="form">
        <div v-if="pwdError" class="text-danger" style="font-size: 13px;">{{ pwdError }}</div>
        <div v-if="pwdMsg" class="text-success" style="font-size: 13px;">{{ pwdMsg }}</div>
        
        <input type="password" v-model="oldPassword" class="input-field" placeholder="Nhập mật khẩu hiện tại" />
        <input type="password" v-model="newPassword" class="input-field" placeholder="Nhập mật khẩu mới" />
        
        <button class="btn btn-danger" @click="handleChangePassword">Xác nhận đổi mật khẩu</button>
      </div>
    </div>

    <div class="glass-panel settings-card">
      <div class="card-header">
        <Shield :size="24" :color="isReadOnly ? '#10b981' : '#f59e0b'" />
        <div>
          <h3>Chế độ Khóa Ghi USB (Chống Virus Tuyệt Đối)</h3>
          <p class="text-muted">Biến USB thành dạng Chỉ-đọc ở cấp độ hệ thống. Ngăn chặn 100% việc lây nhiễm virus hay bị làm ẩn file khi cắm vào máy Windows lạ.</p>
        </div>
      </div>
      <div class="form">
        <div style="padding: 15px; background: rgba(0,0,0,0.2); border-radius: 8px; border-left: 4px solid" :style="{ borderColor: isReadOnly ? '#10b981' : '#f59e0b' }">
          <h4 style="margin-bottom: 5px;" :style="{ color: isReadOnly ? '#10b981' : '#f59e0b' }">Trạng thái: {{ isReadOnly ? 'Đang Khóa (An Toàn)' : 'Chưa Khóa (Cho phép ghi)' }}</h4>
          <p style="font-size: 13px; color: var(--text-muted); margin-bottom: 15px; line-height: 1.4;">
            {{ isReadOnly ? 'USB của bạn hiện không thể bị lây nhiễm virus hay bị ghi đè dữ liệu.\nLưu ý: Để copy thêm tài liệu mới vào Két sắt, bạn cần tạm thời MỞ KHÓA.' : 'Khuyên dùng khi bạn phải mang USB cắm ra máy tính ngoài quán photocopy hoặc máy tính của người khác.' }}
          </p>
          <button class="btn" :class="isReadOnly ? 'btn-outline' : ''" :style="isReadOnly ? '' : 'background: rgba(16, 185, 129, 0.2); color: #10b981; border-color: #10b981;'" :disabled="readOnlyLoading" @click="handleToggleReadOnly">
            {{ readOnlyLoading ? 'Đang xử lý...' : (isReadOnly ? 'MỞ KHÓA (Cho phép ghi)' : 'KÍCH HOẠT KHÓA GHI') }}
          </button>
        </div>
      </div>
    </div>

    <div class="glass-panel settings-card">
      <div class="card-header">
        <Usb :size="24" color="#8b5cf6" />
        <div>
          <h3>Khóa Két bằng Phần cứng USB (Hardware ID Lock)</h3>
          <p class="text-muted">Tính năng cấp độ Quân Sự. Két sắt sẽ kiểm tra số Serial vật lý của chip USB. Hacker có copy dữ liệu sang máy/USB khác cũng vô dụng.</p>
        </div>
      </div>
      <div class="form">
        <div v-if="hwidError" class="text-danger" style="font-size: 13px;">{{ hwidError }}</div>
        <div v-if="hwidMsg" class="text-success" style="font-size: 13px; font-weight: bold;">{{ hwidMsg }}</div>
        <p class="text-warning" style="font-size: 13px; color: #f59e0b;">* Lưu ý: Khi bật, nếu ổ USB hiện tại bị cháy chip, bạn sẽ vĩnh viễn mất dữ liệu kể cả khi nhớ mật khẩu.</p>
        <button class="btn" style="background: rgba(139, 92, 246, 0.2); color: #8b5cf6; border-color: #8b5cf6;" @click="handleEnableHwid">KÍCH HOẠT KHÓA CỨNG</button>
      </div>
    </div>

    <div class="glass-panel settings-card">
      <div class="card-header">
        <SettingsIcon :size="24" color="#3b82f6" />
        <div>
          <h3>Cài đặt Chung</h3>
          <p class="text-muted">Tùy chỉnh hệ thống tập tin hiển thị.</p>
        </div>
      </div>
      <div class="form">
        <label class="toggle-row">
          <input type="checkbox" v-model="cleanSystem" />
          <span>Tự động ẩn file hệ thống/rác của Windows & MacOS (Thumbs.db, desktop.ini, .DS_Store...)</span>
        </label>
        <label class="toggle-row">
          <input type="checkbox" v-model="showHidden" />
          <span>Mặc định hiển thị Tệp và Thư mục ẩn</span>
        </label>
        <label class="toggle-row" style="margin-top: 10px; padding-top: 10px; border-top: 1px solid var(--border);">
          <input type="checkbox" v-model="autoClearHistory" />
          <span>Tự động xóa lịch sử kết nối của USB khi thoát phần mềm</span>
        </label>
        
        <div style="margin-top: 15px; padding-top: 15px; border-top: 1px solid var(--border);">
          <p style="font-size: 13px; color: var(--text-main); font-weight: 500; margin-bottom: 5px;">Giao diện ứng dụng (Theme)</p>
          <select v-model="appTheme" class="input-field" style="background: rgba(0,0,0,0.5); color: var(--text-main);">
            <option value="emerald">🟢 Emerald (Quân sự - Mặc định)</option>
            <option value="crimson">🔴 Crimson (Cảnh báo đỏ)</option>
            <option value="cyberpunk">🟡 Cyberpunk (Vàng Hacker)</option>
            <option value="neon">🔵 Neon Blue (Công nghệ)</option>
          </select>
        </div>
        

        
        <button class="btn" style="width: 150px; margin-top: 10px;" @click="handleSaveGeneral">
          <Save :size="16" /> Lưu thay đổi
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.settings-container { padding: 10px; height: 100%; overflow-y: auto; }
.settings-card { padding: 25px; margin-bottom: 20px; }
.card-header { display: flex; gap: 15px; margin-bottom: 25px; }
.card-header h3 { margin-bottom: 5px; color: var(--text-main); font-weight: 600; }
.card-header p { font-size: 13px; line-height: 1.5; }
.form { display: flex; flex-direction: column; gap: 15px; max-width: 500px; }
.toggle-row { display: flex; align-items: center; gap: 10px; font-size: 14px; cursor: pointer; color: var(--text-muted); }
.toggle-row input { width: 16px; height: 16px; accent-color: var(--accent); }
.toggle-row:hover { color: var(--text-main); }
</style>
