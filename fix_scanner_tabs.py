import re

with open('src/components/Scanner.vue', 'r', encoding='utf-8') as f:
    vue = f.read()

if "const activeTab = ref('results');" not in vue:
    vue = vue.replace("const currentFile = ref('');", "const currentFile = ref('');\nconst activeTab = ref('results');\nconst quarantineList = ref<any[]>([]);\nconst logsList = ref<any[]>([]);")

# Replace tabs HTML
tabs_html = """
      <div class="tabs">
        <div class="tab" :class="{ active: activeTab === 'results' }" @click="activeTab = 'results'">Kết quả quét</div>
        <div class="tab" :class="{ active: activeTab === 'quarantine' }" @click="activeTab = 'quarantine'">Khu cách ly</div>
        <div class="tab" :class="{ active: activeTab === 'logs' }" @click="activeTab = 'logs'">Nhật ký</div>
      </div>
"""
old_tabs_regex = r'<div class="tabs">[\s\S]*?</div>'
vue = re.sub(old_tabs_regex, tabs_html.strip(), vue, count=1)

# Modify table actions
actions_html = """
      <div class="table-actions" v-if="activeTab === 'results'">
        <button class="btn btn-outline btn-sm" @click="handleAction('quarantine')"><ShieldAlert :size="14" /> Cách ly đã chọn</button>
        <button class="btn btn-danger btn-sm" @click="handleAction('delete')"><Trash2 :size="14" /> Xóa tệp đã chọn</button>
      </div>
"""
old_actions_regex = r'<div class="table-actions">[\s\S]*?</div>'
vue = re.sub(old_actions_regex, actions_html.strip(), vue, count=1)

# Modify table wrapper to handle tabs
wrapper_html = """
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
            </tr>
          </thead>
          <tbody>
            <tr v-if="quarantineList.length === 0">
              <td colspan="3" style="text-align:center; padding: 20px; color: var(--text-muted)">Khu cách ly hiện đang trống. K3 bảo vệ máy tính bạn an toàn tuyệt đối!</td>
            </tr>
            <tr v-for="q in quarantineList" :key="q.id">
              <td>{{ q.name }}</td>
              <td class="text-muted" style="font-size: 12px;">{{ q.originalPath }}</td>
              <td>{{ q.date }}</td>
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
"""
old_wrapper_regex = r'<div class="table-wrapper">[\s\S]*?</div>\s*</div>\s*</div>\s*</template>'
vue = re.sub(old_wrapper_regex, wrapper_html.strip() + '\n    </div>\n  </div>\n</template>', vue, count=1)

with open('src/components/Scanner.vue', 'w', encoding='utf-8') as f:
    f.write(vue)

