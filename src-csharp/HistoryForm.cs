using System;
using System.Drawing;
using System.Windows.Forms;
using System.Management;
using System.Diagnostics.Eventing.Reader;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace AnToanUSB
{
    public class HistoryForm : Form
    {
        private ListView lvUsbHistory;
        private ListView lvNetworkHistory;
        private Label lblSubTitle;
        public HistoryForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Quản lý lịch sử kết nối";
            this.Size = new Size(1000, 600);
            this.MinimumSize = new Size(900, 560);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.White;

            Panel topPanel = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.White };
            Label lblTitle = new Label { Text = "Quản lý lịch sử kết nối USB / Mạng", Font = new Font("Segoe UI", 16, FontStyle.Bold), Location = new Point(20, 10), AutoSize = true };
            lblSubTitle = new Label { Text = "Đang nạp dữ liệu...", Font = new Font("Segoe UI", 9, FontStyle.Regular), ForeColor = Color.Gray, Location = new Point(22, 40), AutoSize = true };
            topPanel.Controls.AddRange(new Control[] { lblTitle, lblSubTitle });

            TabControl tabControl = new TabControl { Dock = DockStyle.Fill, ItemSize = new Size(150, 30), Font = new Font("Segoe UI", 9) };
            TabPage tabUSB = new TabPage("Quản lý kết nối USB") { BackColor = Color.White };
            TabPage tabNetwork = new TabPage("Quản lý kết nối Mạng") { BackColor = Color.White };

            // --- TAB USB ---
            Label lblTabTitle = new Label { Text = "Lịch sử kết nối USB", Font = new Font("Segoe UI", 12, FontStyle.Bold), Location = new Point(10, 10), AutoSize = true };
            Label lblTabDesc = new Label { Text = "Hiển thị lịch sử USB, xem chi tiết USB được chọn và xóa lịch sử theo từng thiết bị hoặc toàn bộ.", Font = new Font("Segoe UI", 9), ForeColor = Color.Gray, Location = new Point(12, 35), AutoSize = true };
            
            FlowLayoutPanel btnPanel = new FlowLayoutPanel { Location = new Point(10, 60), Width = 950, Height = 40 };
            
            Button btnView = new Button { Text = "Nạp Lịch sử", Width = 100, Height = 30, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            Button btnDetails = new Button { Text = "Chi tiết", Width = 100, Height = 30, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            Button btnDeleteSelected = new Button { Text = "Xóa USB đã chọn", Width = 150, Height = 30, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.DarkRed, FlatStyle = FlatStyle.Flat };
            btnDeleteSelected.FlatAppearance.BorderColor = Color.DarkRed;
            Button btnDeleteAll = new Button { Text = "Xóa toàn bộ lịch sử", Width = 180, Height = 30, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.DarkRed, FlatStyle = FlatStyle.Flat };
            btnDeleteAll.FlatAppearance.BorderColor = Color.DarkRed;

            btnPanel.Controls.AddRange(new Control[] { btnView, btnDetails, btnDeleteSelected, btnDeleteAll });

            lvUsbHistory = new ListView { Location = new Point(10, 110), Width = 950, Height = 380, View = View.Details, FullRowSelect = true, GridLines = true, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            lvUsbHistory.Columns.Add("Thời gian", 150);
            lvUsbHistory.Columns.Add("Tên thiết bị (Friendly Name)", 250);
            lvUsbHistory.Columns.Add("Nhà sản xuất (Vendor)", 150);
            lvUsbHistory.Columns.Add("Số serial", 200);
            lvUsbHistory.Columns.Add("Hardware ID", 300);
            
            btnView.Click += (s, e) => LoadRealUsbHistory();
            btnDetails.Click += (s, e) => ShowSelectedUsbDetails();
            btnDeleteSelected.Click += (s, e) => DeleteSelectedUsbHistory();
            
            btnDeleteAll.Click += (s, e) => {
                if (!IsAdministrator()) {
                    MessageBox.Show("Cần chạy ứng dụng bằng quyền Administrator (Run as Administrator) để xóa khóa Registry hệ thống!", "Từ chối truy cập", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (MessageBox.Show("Bạn có chắc chắn muốn xóa toàn bộ lịch sử USB trong Registry (USBSTOR và USB)? Việc này có thể cần khởi động lại Windows để cập nhật đầy đủ.", "Cảnh báo Xóa", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) {
                    try {
                        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum", true)) {
                            TryDeleteSubKeyTree(key, "USBSTOR");
                            TryDeleteSubKeyTree(key, "USB");
                        }
                        MessageBox.Show("Đã gửi lệnh xóa lịch sử USB trong Registry.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        LoadRealUsbHistory();
                    } catch (Exception ex) {
                        MessageBox.Show("Lỗi khi xóa Registry: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            };

            tabUSB.Controls.AddRange(new Control[] { lblTabTitle, lblTabDesc, btnPanel, lvUsbHistory });
            
            // --- TAB NETWORK ---
            Label lblNetTitle = new Label { Text = "Lịch sử kết nối Mạng", Font = new Font("Segoe UI", 12, FontStyle.Bold), Location = new Point(10, 10), AutoSize = true };
            Label lblNetDesc = new Label { Text = "Đọc trực tiếp từ Windows Event Logs (Microsoft-Windows-NetworkProfile).", Font = new Font("Segoe UI", 9), ForeColor = Color.Gray, Location = new Point(12, 35), AutoSize = true };
            
            FlowLayoutPanel netBtnPanel = new FlowLayoutPanel { Location = new Point(10, 60), Width = 950, Height = 40 };
            Button btnViewNet = new Button { Text = "Tải Lịch sử Mạng", Width = 150, Height = 30, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            Button btnInternetStatus = new Button { Text = "Trạng thái Internet", Width = 160, Height = 30, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            Button btnClearNet = new Button { Text = "Xóa lịch sử mạng", Width = 150, Height = 30, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.DarkRed, FlatStyle = FlatStyle.Flat };
            btnClearNet.FlatAppearance.BorderColor = Color.DarkRed;
            netBtnPanel.Controls.AddRange(new Control[] { btnViewNet, btnInternetStatus, btnClearNet });
            
            lvNetworkHistory = new ListView { Location = new Point(10, 110), Width = 950, Height = 380, View = View.Details, FullRowSelect = true, GridLines = true, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            lvNetworkHistory.Columns.Add("Thời gian", 150);
            lvNetworkHistory.Columns.Add("Sự kiện", 120);
            lvNetworkHistory.Columns.Add("Event ID", 80);
            lvNetworkHistory.Columns.Add("Chi tiết (Network Profile)", 600);

            btnViewNet.Click += (s, e) => LoadRealNetworkHistory();
            btnInternetStatus.Click += (s, e) => LoadInternetStatus();

            btnClearNet.Click += (s, e) => {
                if (!IsAdministrator()) {
                    MessageBox.Show("Cần chạy ứng dụng bằng quyền Administrator (Run as Administrator) để xóa Event Log!", "Từ chối truy cập", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (MessageBox.Show("Xóa toàn bộ Event Logs của NetworkProfile?", "Cảnh báo Xóa", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) {
                    try {
                        System.Diagnostics.Eventing.Reader.EventLogSession.GlobalSession.ClearLog("Microsoft-Windows-NetworkProfile/Operational");
                        MessageBox.Show("Đã dọn dẹp Event Log mạng thành công!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        LoadRealNetworkHistory();
                    } catch (Exception ex) {
                        MessageBox.Show("Lỗi khi xóa Event Log: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            };

            tabNetwork.Controls.AddRange(new Control[] { lblNetTitle, lblNetDesc, netBtnPanel, lvNetworkHistory });

            tabControl.TabPages.Add(tabUSB);
            tabControl.TabPages.Add(tabNetwork);

            this.Controls.Add(tabControl);
            this.Controls.Add(topPanel);

            this.Load += (s, e) => {
                LoadRealUsbHistory();
                LoadRealNetworkHistory();
            };
        }

        private bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private void TryDeleteSubKeyTree(RegistryKey parent, string name)
        {
            try
            {
                if (parent.OpenSubKey(name) != null) parent.DeleteSubKeyTree(name);
            }
            catch { }
        }

        private async void LoadRealUsbHistory()
        {
            lvUsbHistory.Items.Clear();
            lblSubTitle.Text = "Đang nạp dữ liệu...";
            try {
                var rows = await Task.Run(() => CollectUsbHistoryRows());
                foreach (var row in rows)
                {
                    var item = new ListViewItem(row.Columns);
                    item.Tag = row.RegistryPath;
                    if (row.IsCurrent) item.ForeColor = Color.DarkGreen;
                    lvUsbHistory.Items.Add(item);
                }
            } catch (Exception ex) {
                lvUsbHistory.Items.Add(new ListViewItem(new[] { "Error", ex.Message, "", "", "" }));
            }
            lblSubTitle.Text = string.Format("Đã nạp {0} bản ghi USB / {1} bản ghi Mạng.", lvUsbHistory.Items.Count, lvNetworkHistory.Items.Count);
        }

        private class UsbHistoryRow
        {
            public string[] Columns;
            public string RegistryPath;
            public bool IsCurrent;
        }

        private List<UsbHistoryRow> CollectUsbHistoryRows()
        {
            var rows = new List<UsbHistoryRow>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(@"SELECT * FROM Win32_PnPEntity WHERE PNPClass='USB' OR DeviceID LIKE 'USB%' OR DeviceID LIKE '%USBSTOR%'"))
                using (ManagementObjectCollection collection = searcher.Get())
                {
                    foreach (ManagementObject device in collection)
                    {
                        string id = GetWmiValue(device, "DeviceID");
                        if (string.IsNullOrEmpty(id)) continue;
                        string name = FirstNonEmpty(GetWmiValue(device, "Name"), GetWmiValue(device, "Caption"), "Unknown Device");
                        string mfg = FirstNonEmpty(GetWmiValue(device, "Manufacturer"), "Unknown Vendor");
                        string serial = ExtractSerial(id);
                        string key = id;
                        if (seen.Add(key))
                        {
                            rows.Add(new UsbHistoryRow
                            {
                                Columns = new[] { "Hiện tại", name, mfg, serial, id },
                                RegistryPath = @"SYSTEM\CurrentControlSet\Enum\" + id.Replace('/', '\\'),
                                IsCurrent = true
                            });
                        }
                    }
                }
            }
            catch { }

            CollectUsbRegistryBranch(rows, seen, @"SYSTEM\CurrentControlSet\Enum\USBSTOR", "USBSTOR");
            CollectUsbRegistryBranch(rows, seen, @"SYSTEM\CurrentControlSet\Enum\USB", "USB");

            return rows;
        }

        private void CollectUsbRegistryBranch(List<UsbHistoryRow> rows, HashSet<string> seen, string branchPath, string prefix)
        {
            using (RegistryKey root = Registry.LocalMachine.OpenSubKey(branchPath))
            {
                if (root == null) return;

                foreach (string deviceKeyName in root.GetSubKeyNames())
                {
                    using (RegistryKey deviceKey = root.OpenSubKey(deviceKeyName))
                    {
                        if (deviceKey == null) continue;
                        foreach (string instanceKeyName in deviceKey.GetSubKeyNames())
                        {
                            using (RegistryKey instanceKey = deviceKey.OpenSubKey(instanceKeyName))
                            {
                                if (instanceKey == null) continue;

                                string hardwareId = prefix + "\\" + deviceKeyName + "\\" + instanceKeyName;
                                if (!seen.Add(hardwareId)) continue;

                                string friendlyName = FirstNonEmpty(
                                    ReadRegistryValue(instanceKey, "FriendlyName"),
                                    ReadRegistryValue(instanceKey, "DeviceDesc"),
                                    deviceKeyName);
                                friendlyName = CleanupRegistryText(friendlyName);

                                string mfg = CleanupRegistryText(FirstNonEmpty(ReadRegistryValue(instanceKey, "Mfg"), "Unknown Vendor"));
                                string serial = instanceKeyName;

                                rows.Add(new UsbHistoryRow
                                {
                                    Columns = new[] { "Registry", friendlyName, mfg, serial, hardwareId },
                                    RegistryPath = branchPath + "\\" + deviceKeyName + "\\" + instanceKeyName,
                                    IsCurrent = false
                                });
                            }
                        }
                    }
                }
            }
        }

        private string GetWmiValue(ManagementObject obj, string key)
        {
            try
            {
                object value = obj[key];
                return value == null ? "" : value.ToString();
            }
            catch { return ""; }
        }

        private string ReadRegistryValue(RegistryKey key, string valueName)
        {
            object value = key.GetValue(valueName);
            if (value == null) return "";
            if (value is string[]) return string.Join(", ", (string[])value);
            return value.ToString();
        }

        private string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
                if (!string.IsNullOrWhiteSpace(value)) return value;
            return "";
        }

        private string ExtractSerial(string deviceId)
        {
            string[] parts = deviceId.Split('\\');
            return parts.Length > 2 ? parts[2] : "N/A";
        }

        private string CleanupRegistryText(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            int idx = value.LastIndexOf(';');
            if (idx >= 0 && idx < value.Length - 1) value = value.Substring(idx + 1);
            return value.Replace("@", "").Trim();
        }

        private void ShowSelectedUsbDetails()
        {
            if (lvUsbHistory.SelectedItems.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn một thiết bị USB trong danh sách.", "Chi tiết USB", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ListViewItem item = lvUsbHistory.SelectedItems[0];
            string details = string.Format(
                "Thời gian/Nguồn: {0}\nTên thiết bị: {1}\nNhà sản xuất: {2}\nSố serial: {3}\nHardware ID: {4}",
                item.SubItems[0].Text,
                item.SubItems[1].Text,
                item.SubItems[2].Text,
                item.SubItems[3].Text,
                item.SubItems[4].Text);

            details += "\nRegistry path: HKLM\\" + (item.Tag == null ? "" : item.Tag.ToString());

            MessageBox.Show(details, "Chi tiết USB", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void DeleteSelectedUsbHistory()
        {
            if (lvUsbHistory.SelectedItems.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn một thiết bị USB cần xóa lịch sử.", "Xóa USB đã chọn", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!IsAdministrator())
            {
                MessageBox.Show("Cần chạy ứng dụng bằng quyền Administrator để xóa lịch sử USB trong Registry.", "Từ chối truy cập", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ListViewItem item = lvUsbHistory.SelectedItems[0];
            string registryPath = item.Tag == null ? "" : item.Tag.ToString();
            if (string.IsNullOrEmpty(registryPath) || !registryPath.StartsWith(@"SYSTEM\CurrentControlSet\Enum\", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Không xác định được Registry path của thiết bị này.", "Xóa USB đã chọn", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show("Xóa lịch sử thiết bị USB đã chọn khỏi Registry?\n\n" + item.SubItems[1].Text, "Xác nhận xóa", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try
            {
                string parentPath = registryPath.Substring(0, registryPath.LastIndexOf('\\'));
                string childName = registryPath.Substring(registryPath.LastIndexOf('\\') + 1);
                using (RegistryKey parent = Registry.LocalMachine.OpenSubKey(parentPath, true))
                {
                    if (parent == null) throw new Exception("Không mở được khóa cha: HKLM\\" + parentPath);
                    parent.DeleteSubKeyTree(childName);
                }
                MessageBox.Show("Đã xóa lịch sử USB đã chọn.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadRealUsbHistory();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể xóa thiết bị đã chọn: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadRealNetworkHistory()
        {
            lvNetworkHistory.Items.Clear();
            int count = 0;
            try {
                EventLogQuery query = new EventLogQuery("Microsoft-Windows-NetworkProfile/Operational", PathType.LogName);
                query.ReverseDirection = true; // Newest first
                
                using (EventLogReader reader = new EventLogReader(query)) {
                    EventRecord record;
                    while ((record = reader.ReadEvent()) != null && count < 200) { // Limit to 200 for performance
                        if (record.Id == 10000 || record.Id == 10001 || record.Id == 4004 || record.Id == 4002) {
                            string time = record.TimeCreated.HasValue ? record.TimeCreated.Value.ToString("dd/MM/yyyy HH:mm:ss") : "";
                            string eventType = "";
                            if (record.Id == 10000) eventType = "Kết nối";
                            else if (record.Id == 10001) eventType = "Ngắt kết nối";
                            else if (record.Id == 4004) eventType = "Thay đổi State";
                            else eventType = "Xác định Network";

                            // We capture the raw message which usually contains Network Guid/Name
                            string desc = record.FormatDescription() ?? "";
                            desc = desc.Replace("\r", " ").Replace("\n", " ").Trim();
                            if (desc.Length > 200) desc = desc.Substring(0, 200) + "...";

                            lvNetworkHistory.Items.Add(new ListViewItem(new[] { time, eventType, record.Id.ToString(), desc }));
                            count++;
                        }
                    }
                }
            } catch (Exception ex) {
                lvNetworkHistory.Items.Add(new ListViewItem(new[] { "Error", "Permission Denied?", "", ex.Message }));
            }
            lblSubTitle.Text = string.Format("Đã nạp {0} bản ghi USB / {1} bản ghi Mạng.", lvUsbHistory.Items.Count, count);
        }

        private void LoadInternetStatus()
        {
            lvNetworkHistory.Items.Clear();
            int count = 0;
            bool online = NetworkInterface.GetIsNetworkAvailable();
            lvNetworkHistory.Items.Add(new ListViewItem(new[] {
                DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                online ? "Internet OK" : "Offline",
                "STATUS",
                online ? "Windows báo có kết nối mạng khả dụng." : "Windows không phát hiện kết nối mạng khả dụng."
            }));
            count++;

            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                try
                {
                    IPInterfaceProperties props = ni.GetIPProperties();
                    string ipList = "";
                    foreach (UnicastIPAddressInformation ip in props.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            if (ipList.Length > 0) ipList += ", ";
                            ipList += ip.Address.ToString();
                        }
                    }

                    string gatewayList = "";
                    foreach (GatewayIPAddressInformation gw in props.GatewayAddresses)
                    {
                        if (gw.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            if (gatewayList.Length > 0) gatewayList += ", ";
                            gatewayList += gw.Address.ToString();
                        }
                    }

                    string dnsList = "";
                    foreach (System.Net.IPAddress dns in props.DnsAddresses)
                    {
                        if (dns.AddressFamily == AddressFamily.InterNetwork)
                        {
                            if (dnsList.Length > 0) dnsList += ", ";
                            dnsList += dns.ToString();
                        }
                    }

                    string desc = string.Format("Tên: {0} | Loại: {1} | IP: {2} | Gateway: {3} | DNS: {4}",
                        ni.Name,
                        ni.NetworkInterfaceType,
                        string.IsNullOrEmpty(ipList) ? "N/A" : ipList,
                        string.IsNullOrEmpty(gatewayList) ? "N/A" : gatewayList,
                        string.IsNullOrEmpty(dnsList) ? "N/A" : dnsList);

                    lvNetworkHistory.Items.Add(new ListViewItem(new[] {
                        DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                        "Đang hoạt động",
                        "NIC",
                        desc
                    }));
                    count++;
                }
                catch { }
            }

            lblSubTitle.Text = string.Format("Đã nạp {0} bản ghi USB / {1} dòng trạng thái Internet.", lvUsbHistory.Items.Count, count);
        }
    }
    }
