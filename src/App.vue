<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';
import Login from './components/Login.vue';
import Dashboard from './components/Dashboard.vue';
import FileManager from './components/FileManager.vue';
import Settings from './components/Settings.vue';
import History from './components/History.vue';
import Scanner from './components/Scanner.vue';
import ToastManager from './components/ToastManager.vue';
import { Shield, FolderLock, Settings as SettingsIcon, LogOut, Activity, ChevronLeft, ChevronRight, History as HistoryIcon, Bug, ShieldCheck } from '@lucide/vue';
import './style.css';

const isAuthenticated = ref(false);
const activeTab = ref('dashboard');
const isSidebarCollapsed = ref(false);
let idleTimer: any = null;

const api = (window as any).electronAPI;

const resetIdleTimer = () => {
  if (!isAuthenticated.value) return;
  clearTimeout(idleTimer);
  idleTimer = setTimeout(() => {
    alert('Đã khóa Két sắt do bạn không thao tác trong 5 phút!');
    handleLogout();
  }, 5 * 60 * 1000); // 5 mins auto-lock
};

onMounted(async () => {
  const cfg = await api.getConfig();
  if (cfg.appTheme) {
    document.documentElement.setAttribute('data-theme', cfg.appTheme);
  }
  window.addEventListener('mousemove', resetIdleTimer);
  window.addEventListener('keydown', resetIdleTimer);
  window.addEventListener('click', resetIdleTimer);
});
onUnmounted(() => {
  window.removeEventListener('mousemove', resetIdleTimer);
  window.removeEventListener('keydown', resetIdleTimer);
  window.removeEventListener('click', resetIdleTimer);
  clearTimeout(idleTimer);
});

const onAuthenticated = () => {
  isAuthenticated.value = true;
  activeTab.value = 'dashboard';
  resetIdleTimer();
};

const handleLogout = async () => {
  await api.logout();
  isAuthenticated.value = false;
  activeTab.value = 'dashboard';
  clearTimeout(idleTimer);
};
</script>

<template>
  <div class="drag-bar"></div>
  <ToastManager />
  <div class="app-root">
    <!-- Login Screen -->
    <Transition name="fade" mode="out-in">
      <Login v-if="!isAuthenticated" @authenticated="onAuthenticated" />
      
      <!-- Main App Layout -->
      <div v-else class="main-layout">
        <!-- Sidebar Navigation -->
        <div class="sidebar glass-panel" :class="{ collapsed: isSidebarCollapsed }">
          <div class="sidebar-header">
            <div class="logo">
              <ShieldCheck v-if="!isSidebarCollapsed" :size="32" color="var(--accent)" style="filter: drop-shadow(0 0 8px rgba(16, 185, 129, 0.6));" />
              <ShieldCheck v-else :size="24" color="var(--accent)" />
              <div v-if="!isSidebarCollapsed" class="logo-text">
                <span class="logo-k3">K3</span>
                <span class="logo-security">SECURITY</span>
              </div>
            </div>
            <button class="icon-btn collapse-btn" @click="isSidebarCollapsed = !isSidebarCollapsed">
              <ChevronRight v-if="isSidebarCollapsed" :size="18" />
              <ChevronLeft v-else :size="18" />
            </button>
          </div>
          
          <nav class="nav-menu">
            <button :class="{ active: activeTab === 'dashboard' }" @click="activeTab = 'dashboard'" :title="isSidebarCollapsed ? 'Tổng quan' : ''">
              <Activity :size="18" /> <span v-if="!isSidebarCollapsed">Tổng quan</span>
            </button>
            <button :class="{ active: activeTab === 'files' }" @click="activeTab = 'files'" :title="isSidebarCollapsed ? 'Quản lý File' : ''">
              <FolderLock :size="18" /> <span v-if="!isSidebarCollapsed">Quản lý File</span>
            </button>
            <button :class="{ active: activeTab === 'history' }" @click="activeTab = 'history'" :title="isSidebarCollapsed ? 'Lịch sử' : ''">
              <HistoryIcon :size="18" /> <span v-if="!isSidebarCollapsed">Lịch sử</span>
            </button>
            <button :class="{ active: activeTab === 'scanner' }" @click="activeTab = 'scanner'" :title="isSidebarCollapsed ? 'Quét Virus' : ''">
              <Bug :size="18" /> <span v-if="!isSidebarCollapsed">Quét Virus</span>
            </button>
            <button :class="{ active: activeTab === 'settings' }" @click="activeTab = 'settings'" :title="isSidebarCollapsed ? 'Cài đặt' : ''">
              <SettingsIcon :size="18" /> <span v-if="!isSidebarCollapsed">Cài đặt</span>
            </button>
          </nav>
          
          <div class="sidebar-footer">
            <button class="logout-btn" @click="handleLogout" :title="isSidebarCollapsed ? 'Khóa Két' : ''">
              <LogOut :size="16" /> <span v-if="!isSidebarCollapsed">Khóa Két</span>
            </button>
          </div>
        </div>
        
        <!-- Content Area -->
        <div class="content-area">
          <Transition name="fade" mode="out-in">
            <Dashboard v-if="activeTab === 'dashboard'" />
            <FileManager v-else-if="activeTab === 'files'" />
            <History v-else-if="activeTab === 'history'" />
            <Scanner v-else-if="activeTab === 'scanner'" />
            <Settings v-else-if="activeTab === 'settings'" />
          </Transition>
        </div>
      </div>
    </Transition>
  </div>
</template>

<style scoped>
.drag-bar {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  height: 28px;
  -webkit-app-region: drag;
  z-index: 9999;
}

.app-root { height: 100vh; width: 100vw; padding: 20px; padding-top: 35px; }

.main-layout {
  display: flex;
  height: 100%;
  gap: 20px;
}

.sidebar {
  width: 250px;
  display: flex;
  flex-direction: column;
  padding: 20px;
  transition: width 0.3s ease;
  position: relative;
}

.sidebar.collapsed {
  width: 76px;
  padding: 20px 10px;
}

.collapse-btn {
  position: absolute;
  top: 25px;
  right: -12px;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  background: var(--bg-panel-hover);
  border: var(--glass-border);
  color: var(--text-main);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  z-index: 10;
  box-shadow: 0 2px 5px rgba(0,0,0,0.2);
}
.collapse-btn:hover { background: var(--accent); color: white; border-color: var(--accent); }

.logo {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 5px 0;
  margin-bottom: 30px;
}
.logo-text {
  display: flex;
  flex-direction: column;
  line-height: 1.1;
  user-select: none;
}
.logo-k3 {
  font-size: 22px;
  font-weight: 800;
  letter-spacing: 2px;
  color: var(--accent);
  text-shadow: 0 0 10px rgba(16, 185, 129, 0.4);
  background: linear-gradient(135deg, #10b981 0%, #3b82f6 100%);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
}
.logo-security {
  font-size: 9px;
  font-weight: 700;
  letter-spacing: 5px;
  color: var(--text-muted);
  text-transform: uppercase;
}

.brand {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 40px;
  height: 34px;
  overflow: hidden;
}
.sidebar.collapsed .brand { justify-content: center; }

.brand-text h2 { font-size: 18px; font-weight: 700; margin: 0; letter-spacing: 0.5px; white-space: nowrap; }
.badge { font-size: 10px; background: rgba(16, 185, 129, 0.2); color: var(--accent); padding: 2px 6px; border-radius: 4px; font-weight: 600; text-transform: uppercase; letter-spacing: 1px; }

.nav-menu { display: flex; flex-direction: column; gap: 8px; flex: 1; }

.nav-menu button {
  display: flex;
  align-items: center;
  gap: 12px;
  background: transparent;
  border: none;
  color: var(--text-muted);
  padding: 12px 16px;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
  text-align: left;
  white-space: nowrap;
}
.sidebar.collapsed .nav-menu button { padding: 12px; justify-content: center; }

.nav-menu button:hover { background: rgba(255,255,255,0.1); color: var(--text-main); transform: translateX(2px); }
.nav-menu button.active { background: linear-gradient(90deg, rgba(16, 185, 129, 0.2) 0%, rgba(16, 185, 129, 0.05) 100%); color: var(--accent); font-weight: 600; border-left: 3px solid var(--accent); border-radius: 4px 8px 8px 4px; }

.sidebar-footer { margin-top: auto; padding-top: 20px; border-top: 1px solid var(--border); }
.logout-btn {
  width: 100%; display: flex; align-items: center; justify-content: center; gap: 8px;
  background: rgba(239, 68, 68, 0.1); color: var(--danger); border: 1px solid transparent; padding: 10px; border-radius: 6px; cursor: pointer; transition: all 0.2s; font-weight: 500;
  white-space: nowrap; overflow: hidden;
}
.logout-btn:hover { background: var(--danger); color: white; }

.content-area { flex: 1; position: relative; overflow: hidden; }
</style>
