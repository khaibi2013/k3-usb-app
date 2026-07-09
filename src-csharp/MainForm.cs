using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using System.Threading;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;

namespace AnToanUSB
{
    public class MainForm : Form
    {
        private enum SelectionMode { All, None, Invert, Encrypted, Plain }

        private MenuStrip menuStrip;
        private ToolStripMenuItem menuSelection;
        private ToolStripMenuItem menuFunction;
        private ToolStripMenuItem menuHelp;
        
        private ToolStrip toolStrip;
        private ToolStripButton btnFolder;
        private ToolStripButton btnView;
        private ToolStripButton btnDriveSettings;
        private ToolStripButton btnShield;
        private ToolStripButton btnDatabase;
        private ToolStripButton btnSecurityCenter;
        private ToolStripButton btnReadOnlyToggle;

        private TextBox txtSearch;
        private Button btnEncryptUSB;
        private ComboBox cbLanguage;
        
        private TreeView treeLeft;
        private ListView listMiddle;
        private ListView listRight;
        
        private Button btnTransferRight;
        private Button btnTransferLeft;
        
        private ContextMenuStrip ctxRight;
        private ContextMenuStrip ctxMiddle;
        
        private FileSystemWatcher watcher;
        private string vaultPath;
        private string currentMiddlePath = "";
        private string currentRightPath = "";
        private bool showHiddenFiles = false;
        private CancellationTokenSource searchCts;
        private int searchMatchCount = 0;
        private List<string> clipboardPaths = new List<string>();
        private bool clipboardIsCut = false;
        private DateTime lastDevicePrompt = DateTime.MinValue;

        private ImageList sysIconsSmall;
        private ImageList sysIconsLarge;

        public MainForm()
        {
            sysIconsSmall = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
            sysIconsLarge = new ImageList { ImageSize = new Size(32, 32), ColorDepth = ColorDepth.Depth32Bit };
            
            // Default icons
            sysIconsSmall.Images.Add("folder", IconExtractor.GetFileIcon("folder", true, true) ?? new Bitmap(16, 16));
            sysIconsSmall.Images.Add("drive", IconExtractor.GetNiceDriveIcon(null, true) ?? new Bitmap(16, 16));
            sysIconsSmall.Images.Add("pc", IconExtractor.GetSystemIcon("imageres.dll", 109, true) ?? IconExtractor.GetSystemIcon("shell32.dll", 15, true) ?? new Bitmap(16, 16));
            sysIconsSmall.Images.Add("file", IconExtractor.GetFileIcon("file", true, true) ?? new Bitmap(16, 16));
            sysIconsLarge.Images.Add("folder", IconExtractor.GetFileIcon("folder", false, true) ?? new Bitmap(32, 32));
            sysIconsLarge.Images.Add("drive", IconExtractor.GetNiceDriveIcon(null, false) ?? new Bitmap(32, 32));
            sysIconsLarge.Images.Add("pc", IconExtractor.GetSystemIcon("imageres.dll", 109, false) ?? IconExtractor.GetSystemIcon("shell32.dll", 15, false) ?? new Bitmap(32, 32));
            sysIconsLarge.Images.Add("file", IconExtractor.GetFileIcon("file", false, true) ?? new Bitmap(32, 32));

            InitializeComponent();
            ApplyLanguage();
            LanguageManager.LanguageChanged += ApplyLanguage;
            
            vaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigManager.IsDecoyMode ? ".vault_decoy" : ".vault");
            if (!Directory.Exists(vaultPath)) Directory.CreateDirectory(vaultPath);
            showHiddenFiles = ConfigManager.ShowHidden;

            SetupAutoEncrypt();
            Shown += (s, e) => BeginInvoke(new Action(LoadInitialDataFast));
        }

        private void LoadInitialDataFast()
        {
            Cursor oldCursor = Cursor;
            Cursor = Cursors.WaitCursor;
            try
            {
                LoadTreeDrives();
                LoadRightPane();
            }
            finally
            {
                Cursor = oldCursor;
            }
        }

        private void SetupAutoEncrypt()
        {
            ConfigManager.LoadConfig();
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                watcher = null;
            }

            if (string.IsNullOrEmpty(ConfigManager.AutoEncryptFolder)) return;

            string watchPath = ConfigManager.AutoEncryptFolder == "Toàn bộ USB"
                ? AppDomain.CurrentDomain.BaseDirectory
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigManager.AutoEncryptFolder);

            if (!Directory.Exists(watchPath)) Directory.CreateDirectory(watchPath);

            watcher = new FileSystemWatcher(watchPath);
            watcher.IncludeSubdirectories = true;
            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size | NotifyFilters.LastWrite;
            FileSystemEventHandler queueHandler = (s, e) => QueueAutoEncryptPath(e.FullPath);
            RenamedEventHandler renameHandler = (s, e) => QueueAutoEncryptPath(e.FullPath);
            watcher.Created += queueHandler;
            watcher.Changed += queueHandler;
            watcher.Renamed += renameHandler;
            watcher.EnableRaisingEvents = true;
        }

        private void QueueAutoEncryptPath(string path)
        {
            ThreadPool.QueueUserWorkItem(_ => ProcessAutoEncryptPath(path));
        }

        private void ProcessAutoEncryptPath(string path)
        {
            if (Directory.Exists(path))
            {
                Thread.Sleep(1500);
                ProcessAutoEncryptDirectory(path);
                return;
            }

            ProcessFileQueue(path);
        }

        private void ProcessAutoEncryptDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath)) return;
            EnsureVaultDirectoryForSource(directoryPath);

            for (int i = 0; i < 6; i++)
            {
                try
                {
                    foreach (string file in Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories))
                        ProcessFileQueue(file);
                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(500);
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(500);
                }
            }
        }

        private bool ProcessFileQueue(string filePath)
        {
            if (!File.Exists(filePath)) return false;
            if (ShouldSkipAutoEncrypt(filePath)) return false;

            int retries = 20;
            while (retries > 0)
            {
                try
                {
                    var fi = new FileInfo(filePath);
                    if (ConfigManager.MaxFileSizeBytes >= 0 && fi.Length > ConfigManager.MaxFileSizeBytes)
                        return false;

                    ScanResult scan = AntivirusScanner.ScanFileReal(filePath);
                    if (scan.Status != "Sạch")
                    {
                        try { CryptoEngine.SecureShredFile(filePath); } catch { try { File.Delete(filePath); } catch { } }
                        this.Invoke(new Action(() => MessageBox.Show("Đã chặn và xóa an toàn file nghi nhiễm trong USB:\n" + Path.GetFileName(filePath) + "\n\nPhát hiện: " + scan.VirusName, "K3-AV cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
                        return false;
                    }
                    
                    string dest = GetUniqueVaultEncryptedPath(filePath);
                    CryptoEngine.EncryptFile(filePath, dest);
                    CryptoEngine.SecureShredFile(filePath);
                    
                    this.Invoke(new Action(() => LoadRightPane()));
                    return true; 
                }
                catch (IOException ex)
                {
                    if (ex.HResult == unchecked((int)0x80070020))
                    {
                        Thread.Sleep(500);
                        retries--;
                    }
                    else
                    {
                        Thread.Sleep(500);
                        retries--;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(500);
                    retries--;
                }
                catch { break; }
            }
            return false;
        }

        private string GetUniqueVaultEncryptedPath(string sourceFilePath)
        {
            string relativePath = GetAutoEncryptRelativePath(sourceFilePath);
            string safeName = string.IsNullOrEmpty(relativePath) ? Path.GetFileName(sourceFilePath) : relativePath;
            if (string.IsNullOrEmpty(safeName)) safeName = "file";

            string dest = Path.Combine(vaultPath, safeName + ".k3enc");
            string destDir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
            if (!File.Exists(dest)) return dest;

            string dir = Path.GetDirectoryName(dest);
            string fileName = Path.GetFileName(safeName);
            string name = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            int counter = 1;
            do
            {
                dest = Path.Combine(string.IsNullOrEmpty(dir) ? vaultPath : dir, string.Format("{0} ({1}){2}.k3enc", name, counter, ext));
                counter++;
            } while (File.Exists(dest));
            return dest;
        }

        private bool EnsureVaultDirectoryForSource(string sourceDirectoryPath)
        {
            string relativePath = GetAutoEncryptRelativePath(sourceDirectoryPath);
            if (string.IsNullOrEmpty(relativePath)) return false;

            string destDir = Path.Combine(vaultPath, relativePath);
            if (Directory.Exists(destDir)) return false;
            Directory.CreateDirectory(destDir);
            return true;
        }

        private string GetAutoEncryptRelativePath(string sourcePath)
        {
            try
            {
                string watchPath = GetAutoEncryptWatchPath();
                if (string.IsNullOrEmpty(watchPath)) return Path.GetFileName(sourcePath);

                string fullSource = Path.GetFullPath(sourcePath);
                string fullWatch = Path.GetFullPath(watchPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!fullSource.StartsWith(fullWatch, StringComparison.OrdinalIgnoreCase)) return Path.GetFileName(sourcePath);

                return fullSource.Substring(fullWatch.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return Path.GetFileName(sourcePath);
            }
        }

        private string GetAutoEncryptWatchPath()
        {
            ConfigManager.LoadConfig();
            if (string.IsNullOrEmpty(ConfigManager.AutoEncryptFolder)) return "";
            return ConfigManager.AutoEncryptFolder == "Toàn bộ USB"
                ? AppDomain.CurrentDomain.BaseDirectory
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigManager.AutoEncryptFolder);
        }

        private bool ShouldSkipAutoEncrypt(string filePath)
        {
            string fullPath = Path.GetFullPath(filePath);
            string basePath = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
            string realVault = Path.GetFullPath(Path.Combine(basePath, ".vault"));
            string decoyVault = Path.GetFullPath(Path.Combine(basePath, ".vault_decoy"));

            if (fullPath.EndsWith(".k3enc", StringComparison.OrdinalIgnoreCase)) return true;
            if (IsInsidePath(fullPath, realVault) || IsInsidePath(fullPath, decoyVault)) return true;
            if (string.Equals(fullPath, Path.GetFullPath(Application.ExecutablePath), StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(Path.GetFileName(fullPath), ".vault_config.json", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private bool IsInsidePath(string filePath, string directoryPath)
        {
            string dir = directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return filePath.StartsWith(dir, StringComparison.OrdinalIgnoreCase);
        }

        private void LoadTreeDrives()
        {
            treeLeft.BeginUpdate();
            try
            {
                treeLeft.Nodes.Clear();
                TreeNode pcNode = new TreeNode("My Computer");
                pcNode.ImageKey = "pc";
                pcNode.SelectedImageKey = "pc";
                treeLeft.Nodes.Add(pcNode);

                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.IsReady)
                    {
                        TreeNode dNode = new TreeNode(string.Format("{0} ({1})", drive.VolumeLabel, drive.Name.Replace("\\", "")));
                        dNode.Tag = drive.Name;
                        
                        Image driveIcon = IconExtractor.GetNiceDriveIcon(drive, true);
                        if (driveIcon != null) {
                            string key = "drive_" + drive.Name;
                            if (!sysIconsSmall.Images.ContainsKey(key)) sysIconsSmall.Images.Add(key, driveIcon);
                            dNode.ImageKey = key;
                            dNode.SelectedImageKey = key;
                        } else {
                            dNode.ImageKey = "drive";
                            dNode.SelectedImageKey = "drive";
                        }

                        dNode.Nodes.Add("..."); 
                        pcNode.Nodes.Add(dNode);
                    }
                }
                pcNode.Expand();
            }
            finally
            {
                treeLeft.EndUpdate();
            }
        }

        private void TreeLeft_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node.Nodes.Count == 1 && e.Node.Nodes[0].Text == "...")
            {
                e.Node.Nodes.Clear();
                string path = e.Node.Tag != null ? e.Node.Tag.ToString() : null;
                if (string.IsNullOrEmpty(path)) return;

                try
                {
                    foreach (var dir in Directory.GetDirectories(path))
                    {
                        var di = new DirectoryInfo(dir);
                        if ((di.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden && !showHiddenFiles) continue;
                        
                        TreeNode n = new TreeNode(Path.GetFileName(dir));
                        n.Tag = dir;
                        n.ImageKey = "folder";
                        n.SelectedImageKey = "folder";
                        n.Nodes.Add("...");
                        e.Node.Nodes.Add(n);
                    }
                }
                catch { } 
            }
        }

        private void TreeLeft_AfterSelect(object sender, TreeViewEventArgs e)
        {
            string path = e.Node.Tag != null ? e.Node.Tag.ToString() : null;
            if (!string.IsNullOrEmpty(path))
            {
                LoadMiddlePane(path);
            }
        }

        private void CreateNewFolder(string basePath, Action reloadAction)
        {
            if (string.IsNullOrEmpty(basePath)) return;
            string baseName = "New Folder";
            string targetDir = Path.Combine(basePath, baseName);
            int counter = 1;
            while (Directory.Exists(targetDir) || File.Exists(targetDir))
            {
                targetDir = Path.Combine(basePath, string.Format("{0} ({1})", baseName, counter));
                counter++;
            }
            try
            {
                Directory.CreateDirectory(targetDir);
                reloadAction();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể tạo thư mục: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadMiddlePane(string path = null)
        {
            if (path == null) path = currentMiddlePath;
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                listMiddle.BeginUpdate();
                listMiddle.Items.Clear();
                currentMiddlePath = path;

                var upItem = new ListViewItem(new[] { "..", "Go Back", "", "" });
                string parentDir = Path.GetDirectoryName(path);
                upItem.Tag = parentDir ?? "";
                upItem.ImageKey = "folder";
                listMiddle.Items.Add(upItem);

                foreach (var d in Directory.GetDirectories(path))
                {
                    var di = new DirectoryInfo(d);
                    if ((di.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden && !showHiddenFiles) continue;

                    var item = new ListViewItem(new[] { Path.GetFileName(d), "Thư mục", "", di.LastWriteTime.ToString() });
                    item.Tag = d;
                    item.ImageKey = "folder";
                    listMiddle.Items.Add(item);
                }
                foreach (var f in Directory.GetFiles(path))
                {
                    var fi = new FileInfo(f);
                    if ((fi.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden && !showHiddenFiles) continue;

                    var item = new ListViewItem(new[] { fi.Name, "File", (fi.Length / 1024).ToString() + " KB", fi.LastWriteTime.ToString() });
                    item.Tag = f;
                    
                    string ext = Path.GetExtension(f).ToLower();
                    if (string.IsNullOrEmpty(ext)) item.ImageKey = "file";
                    else 
                    {
                        if (!sysIconsSmall.Images.ContainsKey(ext))
                        {
                            Image img = IconExtractor.GetFileIcon(ext, true, false);
                            if (img != null) sysIconsSmall.Images.Add(ext, img);
                        }
                        item.ImageKey = sysIconsSmall.Images.ContainsKey(ext) ? ext : "file";
                    }
                    listMiddle.Items.Add(item);
                }
            }
            catch (UnauthorizedAccessException) { MessageBox.Show("Không có quyền truy cập!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            catch { }
            finally { listMiddle.EndUpdate(); }
        }

        private void LoadRightPane(string path = null)
        {
            if (path == null) path = string.IsNullOrEmpty(currentRightPath) ? vaultPath : currentRightPath;
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                listRight.BeginUpdate();
                listRight.Items.Clear();
                currentRightPath = path;

                if (path != vaultPath)
                {
                    var upItem = new ListViewItem(new[] { "..", "Go Back", "", "" });
                    upItem.Tag = Path.GetDirectoryName(path) ?? vaultPath;
                    upItem.ImageKey = "folder";
                    listRight.Items.Add(upItem);
                }

                foreach (var d in Directory.GetDirectories(path))
                {
                    var di = new DirectoryInfo(d);
                    var item = new ListViewItem(new[] { Path.GetFileName(d), "Thư mục", "", Directory.GetLastWriteTime(d).ToString() });
                    item.Tag = d; 
                    item.ImageKey = "folder";
                    listRight.Items.Add(item);
                }

                foreach (var f in Directory.GetFiles(path))
                {
                    if (f.EndsWith(".k3enc"))
                    {
                        var fi = new FileInfo(f);
                        string originalName = Path.GetFileNameWithoutExtension(f); 
                        var item = new ListViewItem(new[] { originalName, "File Mã hóa", (fi.Length / 1024).ToString() + " KB", fi.LastWriteTime.ToShortDateString() });
                        item.Tag = f;

                        string ext = Path.GetExtension(originalName).ToLower();
                        if (string.IsNullOrEmpty(ext)) item.ImageKey = "file";
                        else 
                        {
                            if (!sysIconsSmall.Images.ContainsKey(ext))
                            {
                                Image img = IconExtractor.GetFileIcon(ext, true, true);
                                if (img != null) sysIconsSmall.Images.Add(ext, img);
                            }
                            item.ImageKey = sysIconsSmall.Images.ContainsKey(ext) ? ext : "file";
                        }
                        listRight.Items.Add(item);
                    }
                    else
                    {
                        var fi = new FileInfo(f);
                        var item = new ListViewItem(new[] { fi.Name, "File Chưa mã hóa", (fi.Length / 1024).ToString() + " KB", fi.LastWriteTime.ToShortDateString() });
                        item.Tag = f;
                        item.ForeColor = Color.DarkOrange;

                        string ext = Path.GetExtension(f).ToLower();
                        if (string.IsNullOrEmpty(ext)) item.ImageKey = "file";
                        else
                        {
                            if (!sysIconsSmall.Images.ContainsKey(ext))
                            {
                                Image img = IconExtractor.GetFileIcon(ext, true, true);
                                if (img != null) sysIconsSmall.Images.Add(ext, img);
                            }
                            item.ImageKey = sysIconsSmall.Images.ContainsKey(ext) ? ext : "file";
                        }
                        listRight.Items.Add(item);
                    }
                }
            }
            catch { }
            finally { listRight.EndUpdate(); }
        }

        private void DrawCircularButton(object sender, PaintEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.Clear(btn.Parent.BackColor);

            bool hovered = btn.ClientRectangle.Contains(btn.PointToClient(Cursor.Position));
            Rectangle shadowRect = new Rectangle(4, 5, btn.Width - 8, btn.Height - 8);
            Rectangle outerRect = new Rectangle(2, 1, btn.Width - 5, btn.Height - 5);
            Rectangle innerRect = new Rectangle(10, 9, btn.Width - 21, btn.Height - 21);

            using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(24, 15, 184, 145)))
            {
                e.Graphics.FillEllipse(shadowBrush, shadowRect);
            }

            using (SolidBrush brush = new SolidBrush(hovered ? Color.FromArgb(225, 245, 238) : Color.White))
            {
                e.Graphics.FillEllipse(brush, outerRect);
            }
            using (Pen pen = new Pen(hovered ? Theme.TealDark : Theme.Teal, 2.2f))
            {
                e.Graphics.DrawEllipse(pen, outerRect);
            }

            using (SolidBrush coreBrush = new SolidBrush(hovered ? Theme.TealDark : Theme.Teal))
            {
                e.Graphics.FillEllipse(coreBrush, innerRect);
            }

            using (Pen arrowPen = new Pen(Color.White, 2.8f))
            {
                arrowPen.StartCap = LineCap.Round;
                arrowPen.EndCap = LineCap.Round;
                int midY = innerRect.Top + innerRect.Height / 2;
                int left = innerRect.Left + 8;
                int right = innerRect.Right - 8;

                if (btn.Name == "btnTransferRight")
                {
                    e.Graphics.DrawLine(arrowPen, left, midY, right, midY);
                    e.Graphics.DrawLine(arrowPen, right - 5, midY - 5, right, midY);
                    e.Graphics.DrawLine(arrowPen, right - 5, midY + 5, right, midY);
                }
                else
                {
                    e.Graphics.DrawLine(arrowPen, right, midY, left, midY);
                    e.Graphics.DrawLine(arrowPen, left + 5, midY - 5, left, midY);
                    e.Graphics.DrawLine(arrowPen, left + 5, midY + 5, left, midY);
                }
            }
        }

        private void InitializeComponent()
        {
            this.Text = "USB An Toàn K3 (Safe Mode)";
            this.Size = new Size(1200, 700);
            this.MinimumSize = new Size(980, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            Theme.Apply(this);

            menuStrip = new MenuStrip();
            menuSelection = new ToolStripMenuItem("Lựa chọn");
            menuFunction = new ToolStripMenuItem("Chức năng");
            menuHelp = new ToolStripMenuItem("Trợ giúp");

            var mnuSelectAll = new ToolStripMenuItem("Chọn tất cả") { ShortcutKeys = Keys.Control | Keys.A };
            var mnuSelectNone = new ToolStripMenuItem("Bỏ chọn tất cả");
            var mnuInvertSelection = new ToolStripMenuItem("Đảo lựa chọn");
            var mnuSelectEncrypted = new ToolStripMenuItem("Chọn file đã mã hóa");
            var mnuSelectPlain = new ToolStripMenuItem("Chọn file chưa mã hóa");
            var mnuSelectionRefresh = new ToolStripMenuItem("Làm mới") { ShortcutKeys = Keys.F5 };
            var mnuSelectionOpenPath = new ToolStripMenuItem("Mở đường dẫn");
            var mnuSelectionCopyPath = new ToolStripMenuItem("Sao chép đường dẫn");

            mnuSelectAll.Click += (s, e) => SelectItems(SelectionMode.All);
            mnuSelectNone.Click += (s, e) => SelectItems(SelectionMode.None);
            mnuInvertSelection.Click += (s, e) => SelectItems(SelectionMode.Invert);
            mnuSelectEncrypted.Click += (s, e) => SelectItems(SelectionMode.Encrypted);
            mnuSelectPlain.Click += (s, e) => SelectItems(SelectionMode.Plain);
            mnuSelectionRefresh.Click += (s, e) => RefreshActivePane();
            mnuSelectionOpenPath.Click += (s, e) => OpenSelectedPath(GetActiveListView());
            mnuSelectionCopyPath.Click += (s, e) => CopySelectedPaths();

            menuSelection.DropDownItems.AddRange(new ToolStripItem[] {
                mnuSelectAll, mnuSelectNone, mnuInvertSelection,
                new ToolStripSeparator(),
                mnuSelectEncrypted, mnuSelectPlain,
                new ToolStripSeparator(),
                mnuSelectionRefresh, mnuSelectionOpenPath, mnuSelectionCopyPath
            });
            
            var mnuHistory = new ToolStripMenuItem("Quản lý Lịch sử kết nối USB/Mạng");
            mnuHistory.Click += (s, e) => new HistoryForm().ShowDialog(this);
            
            var mnuScanner = new ToolStripMenuItem("Trình Quét Mã độc (Antivirus)");
            mnuScanner.Click += (s, e) => new AntivirusForm().ShowDialog(this);
            var mnuRescue = new ToolStripMenuItem("Cứu hộ USB");
            mnuRescue.Click += (s, e) => new UsbRescueForm().ShowDialog(this);

            menuFunction.DropDownItems.Add(mnuHistory);
            menuFunction.DropDownItems.Add(mnuScanner);
            menuFunction.DropDownItems.Add(mnuRescue);
            
            menuStrip.Items.AddRange(new ToolStripItem[] { menuSelection, menuFunction, menuHelp });
            Theme.StyleMenuStrip(menuStrip);
            
            toolStrip = new ToolStrip { BackColor = Color.White, GripStyle = ToolStripGripStyle.Hidden, ImageScalingSize = new Size(32, 32), Padding = new Padding(10, 5, 10, 5) };
            Theme.StyleToolStrip(toolStrip);
            
            ToolStripButton btnSettings = new ToolStripButton { Image = CustomIcons.GetSettingsGearIcon(), ToolTipText = "Cài đặt chung", Text = "Cài đặt", DisplayStyle = ToolStripItemDisplayStyle.ImageAndText, TextImageRelation = TextImageRelation.ImageAboveText, Font = new Font("Segoe UI", 8.2f, FontStyle.Bold), ForeColor = Theme.TextInv, AutoSize = false, Width = 88, Height = 60 };
            btnSettings.Click += (s, e) => {
                using (var form = new SettingsForm())
                    form.ShowDialog(this);
                ConfigManager.LoadConfig();
                showHiddenFiles = ConfigManager.ShowHidden;
                SetupAutoEncrypt();
                LoadMiddlePane(currentMiddlePath);
                LoadRightPane(currentRightPath);
            };
            
            btnFolder = MakeToolbarButton("Thư mục", CustomIcons.GetAddFolderIcon(), "Tạo Thư mục mới");
            btnFolder.Click += (s, e) => {
                using (var dialog = new ActionChoiceDialog(
                    "Tạo thư mục mới",
                    "Chọn vị trí bạn muốn tạo thư mục.",
                    new ActionChoice("local", "Máy tính", "Tạo trong thư mục đang mở ở cột giữa.", CustomIcons.GetAddFolderIcon(), Theme.Teal),
                    new ActionChoice("vault", "Két sắt USB", "Tạo trong thư mục hiện tại của két bên phải.", CustomIcons.GetShieldCheckIcon(), Theme.Amber)))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    if (dialog.SelectedId == "local") {
                    if (!string.IsNullOrEmpty(currentMiddlePath)) CreateNewFolder(currentMiddlePath, () => LoadMiddlePane(currentMiddlePath));
                    else MessageBox.Show("Vui lòng chọn một thư mục trên Máy tính ở cột bên trái trước!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    } else if (dialog.SelectedId == "vault") {
                    string targetVaultDir = string.IsNullOrEmpty(currentRightPath) ? vaultPath : currentRightPath;
                    CreateNewFolder(targetVaultDir, () => LoadRightPane(currentRightPath));
                    }
                }
            };

            btnView = MakeToolbarButton("Ẩn/hiện", CustomIcons.GetViewIcon(), "Hiển thị File ẩn");
            btnView.Click += (s, e) => {
                showHiddenFiles = !showHiddenFiles;
                btnView.ToolTipText = showHiddenFiles ? "Đang hiện File ẩn (Bấm để ẩn)" : "Hiển thị File ẩn";
                if (!string.IsNullOrEmpty(currentMiddlePath)) LoadMiddlePane(currentMiddlePath);
                MessageBox.Show(showHiddenFiles ? "Đã BẬT chế độ hiển thị File ẩn trên máy tính!" : "Đã TẮT chế độ hiển thị File ẩn!", "Góc nhìn", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            btnDriveSettings = MakeToolbarButton("Công cụ", CustomIcons.GetToolboxIcon(), "Format Két sắt / Sửa lỗi USB");
            btnDriveSettings.Click += (s, e) => {
                using (var dialog = new ActionChoiceDialog(
                    "Công cụ USB",
                    "Chọn tác vụ bảo trì cho USB An Toàn K3.",
                    new ActionChoice("format", "Format két sắt", "Xóa an toàn .vault, .vault_decoy và BaoMat rồi tạo lại.", CustomIcons.GetCleanupIcon(), Theme.Danger),
                    new ActionChoice("repair", "Sửa lỗi USB", "Chạy kiểm tra ổ đĩa bằng CHKDSK của Windows.", CustomIcons.GetToolboxIcon(), Theme.Teal),
                    new ActionChoice("optimize", "Tối ưu ổ", "Chạy Optimize/Defrag theo đúng loại ổ bằng công cụ Windows.", CustomIcons.GetSettingsGearIcon(), Theme.Amber)))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    if (dialog.SelectedId == "format") {
                    var confirm = MessageBox.Show("CẢNH BÁO: Thao tác này sẽ xóa sạch dữ liệu trong .vault, .vault_decoy và thư mục BaoMat bằng xóa an toàn. Tiếp tục?", "Xác nhận Format Két sắt", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (confirm == DialogResult.Yes) {
                        SecureFormatVaultData();
                        MessageBox.Show("Đã format sạch vùng dữ liệu an toàn và tạo lại Két sắt.", "Format Két sắt", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    } else if (dialog.SelectedId == "repair") {
                    RunDriveRepair();
                    } else if (dialog.SelectedId == "optimize") {
                    RunDriveOptimize();
                    }
                }
            };

            btnSecurityCenter = MakeToolbarButton("Bảo mật", CustomIcons.GetShieldCheckIcon(), "Trung tâm bảo mật và kiểm tra toàn vẹn");
            btnSecurityCenter.Click += (s, e) => new SecurityCenterForm(vaultPath).ShowDialog(this);

            btnReadOnlyToggle = MakeToolbarButton(UsbHelper.IsReadOnlyEnabled() ? "Mở ghi" : "Chặn ghi", CustomIcons.GetLockToggleIcon(UsbHelper.IsReadOnlyEnabled()), "Bật/tắt chặn ghi USB");
            btnReadOnlyToggle.Click += (s, e) => ToggleReadOnly();

            btnShield = MakeToolbarButton("Khóa USB", CustomIcons.GetUsbLockIcon(), "Ngắt kết nối an toàn");
            btnShield.Click += (s, e) => {
                CryptoEngine.Logout();
                if (watcher != null) watcher.EnableRaisingEvents = false;
                if (ConfigManager.WipeHistory) CleanupRecentHistory();
                MessageBox.Show("Đã đóng và khóa Két sắt thành công. Bạn có thể rút USB ra an toàn!", "Ngắt kết nối", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Application.Exit();
            };

            btnDatabase = MakeToolbarButton("Dọn dẹp", CustomIcons.GetCleanupIcon(), "Dọn dẹp & Quản lý lịch sử");
            btnDatabase.Click += (s, e) => new PrivacyCleanupForm().ShowDialog(this);
            
            toolStrip.Items.AddRange(new ToolStripItem[] { btnFolder, btnView, btnSecurityCenter, btnReadOnlyToggle, btnDriveSettings, btnDatabase, btnShield, new ToolStripSeparator(), btnSettings });

            StatusStrip statusStrip = new StatusStrip { BackColor = Theme.TealDark };
            ToolStripStatusLabel lblCapacity = new ToolStripStatusLabel { Text = "Đang tính toán dung lượng...", ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            statusStrip.Items.Add(lblCapacity);
            this.Controls.Add(statusStrip);

            // Timer to update capacity
            System.Windows.Forms.Timer capacityTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            capacityTimer.Tick += (s, e) => {
                try {
                    string rootPath = Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory);
                    DriveInfo d = new DriveInfo(rootPath);
                    if (d.IsReady) {
                        lblCapacity.Text = string.Format("Dung lượng USB: {0} GB trống / {1} GB tổng cộng", d.AvailableFreeSpace / 1073741824, d.TotalSize / 1073741824);
                    }
                } catch { lblCapacity.Text = "Không thể đọc dung lượng"; }
            };
            capacityTimer.Start();

            Panel topPanel = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = Theme.Mist };
            CheckBox chkSearchLocal = new CheckBox { Text = "Tìm trên Máy tính", Location = new Point(600, 16), AutoSize = true, Font = Theme.FontBody, ForeColor = Theme.TextDark };
            txtSearch = new TextBox { Location = new Point(400, 13), Width = 190, Font = new Font("Segoe UI", 10), BorderStyle = BorderStyle.FixedSingle };
            txtSearch.TextChanged += async (s, e) => {
                string q = txtSearch.Text.ToLower().Trim();
                if (searchCts != null) searchCts.Cancel();
                
                if (string.IsNullOrEmpty(q)) {
                    if (chkSearchLocal.Checked) LoadMiddlePane(currentMiddlePath);
                    else LoadRightPane(currentRightPath);
                    return;
                }

                searchCts = new CancellationTokenSource();
                var token = searchCts.Token;
                searchMatchCount = 0;

                if (chkSearchLocal.Checked) {
                    listMiddle.Items.Clear();
                    await System.Threading.Tasks.Task.Run(() => PerformGlobalSearchLocal(q, token), token);
                } else {
                    listRight.Items.Clear();
                    await System.Threading.Tasks.Task.Run(() => PerformGlobalSearchVault(q, token), token);
                }
            };
            Label lblSearch = new Label { Text = "Tìm kiếm:", Location = new Point(330, 16), AutoSize = true, Font = Theme.FontHeading, ForeColor = Theme.TextMute };
            
            btnEncryptUSB = new Button { Location = new Point(870, 8), Width = 132, Height = 34, BackColor = Theme.Teal, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = Theme.FontHeading };
            try { btnEncryptUSB.Image = IconExtractor.GetSystemIcon("shell32.dll", 77, true); btnEncryptUSB.TextImageRelation = TextImageRelation.ImageBeforeText; } catch { }
            btnEncryptUSB.Click += (s, e) => EncryptAutoFolderNow();
            var encryptButtonTip = new ToolTip();
            encryptButtonTip.SetToolTip(btnEncryptUSB, "Quét thư mục tự mã hóa trong Cài đặt chung và đưa dữ liệu vào Két sắt.");

            cbLanguage = new ComboBox { Location = new Point(1012, 10), Width = 108, DropDownStyle = ComboBoxStyle.DropDownList };
            cbLanguage.Items.AddRange(new string[] { "Tiếng Việt", "English" });
            cbLanguage.SelectedIndex = 0;
            cbLanguage.SelectedIndexChanged += (s, e) => { LanguageManager.SwitchLanguage(cbLanguage.SelectedIndex == 0 ? "vi" : "en"); };
            topPanel.Controls.Add(chkSearchLocal);
            topPanel.Controls.Add(lblSearch);
            topPanel.Controls.Add(txtSearch);
            topPanel.Controls.Add(btnEncryptUSB);
            topPanel.Controls.Add(cbLanguage);
            topPanel.Resize += (s, e) => {
                int right = topPanel.ClientSize.Width - 12;
                cbLanguage.Left = Math.Max(760, right - cbLanguage.Width);
                btnEncryptUSB.Left = cbLanguage.Left - btnEncryptUSB.Width - 10;
                chkSearchLocal.Left = btnEncryptUSB.Left - chkSearchLocal.Width - 14;
                lblSearch.Left = 24;
                txtSearch.Left = lblSearch.Right + 8;
                txtSearch.Width = Math.Max(180, chkSearchLocal.Left - txtSearch.Left - 14);
                chkSearchLocal.Top = 16;
                btnEncryptUSB.Top = 8;
                cbLanguage.Top = 10;
            };

            TableLayoutPanel mainLayout = new TableLayoutPanel();
            mainLayout.Dock = DockStyle.Fill;
            mainLayout.ColumnCount = 4;
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24F)); 
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F)); 
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72F)); 
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F)); 
            mainLayout.RowCount = 1;
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            treeLeft = new TreeView { Dock = DockStyle.Fill, ShowLines = true, ImageList = sysIconsSmall, BorderStyle = BorderStyle.None, Font = Theme.FontBody, BackColor = Theme.White, ForeColor = Theme.TextDark };
            treeLeft.BeforeExpand += TreeLeft_BeforeExpand;
            treeLeft.AfterSelect += TreeLeft_AfterSelect;

            listMiddle = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, AllowDrop = true, GridLines = false, SmallImageList = sysIconsSmall, BorderStyle = BorderStyle.None, Font = Theme.FontBody, BackColor = Theme.White, ForeColor = Theme.TextDark };
            listMiddle.Columns.Add("Tên", 150);
            listMiddle.Columns.Add("Loại", 80);
            listMiddle.Columns.Add("Kích thước", 80);
            listMiddle.Columns.Add("Ngày sửa cuối", 150);
            listMiddle.Resize += (s, e) => ResizeFileListColumns(listMiddle);
            listMiddle.DoubleClick += (s, e) => {
                if (listMiddle.SelectedItems.Count > 0)
                {
                    string path = listMiddle.SelectedItems[0].Tag.ToString();
                    if (!string.IsNullOrEmpty(path) && (Directory.Exists(path) || Directory.GetLogicalDrives().Contains(path))) 
                        LoadMiddlePane(path);
                }
            };
            listMiddle.ItemDrag += (s, e) => BeginListViewDrag(listMiddle);
            listMiddle.DragEnter += (s, e) => HandleFileDragEnter(e);
            listMiddle.DragDrop += (s, e) => DropPathsToLocalPane(e);
            listMiddle.KeyDown += (s, e) => HandleListShortcut(listMiddle, false, e);

            ctxMiddle = new ContextMenuStrip();
            var mnuMiddleCopy = new ToolStripMenuItem("Copy") { ShortcutKeys = Keys.Control | Keys.C };
            var mnuMiddleCut = new ToolStripMenuItem("Cut") { ShortcutKeys = Keys.Control | Keys.X };
            var mnuMiddlePaste = new ToolStripMenuItem("Paste") { ShortcutKeys = Keys.Control | Keys.V };
            mnuMiddleCopy.Click += (s, e) => CaptureClipboardSelection(listMiddle, false);
            mnuMiddleCut.Click += (s, e) => CaptureClipboardSelection(listMiddle, true);
            mnuMiddlePaste.Click += (s, e) => PasteClipboardTo(currentMiddlePath, () => LoadMiddlePane(currentMiddlePath));
            ctxMiddle.Items.Add(mnuMiddleCopy);
            ctxMiddle.Items.Add(mnuMiddleCut);
            ctxMiddle.Items.Add(mnuMiddlePaste);
            ctxMiddle.Items.Add(new ToolStripSeparator());
            
            var mnuMiddleEncrypt = new ToolStripMenuItem("Mã hóa");
            try { mnuMiddleEncrypt.Image = IconExtractor.GetSystemIcon("shell32.dll", 47, true); } catch { }
            
            var mnuPasteEncrypt = new ToolStripMenuItem("Dán và Mã hóa") { ShortcutKeys = Keys.Control | Keys.M };
            var mnuEncryptFile = new ToolStripMenuItem("Mã hóa tệp");
            var mnuEncryptCustom = new ToolStripMenuItem("Mã hóa với khóa tùy chọn");
            mnuPasteEncrypt.Click += (s, e) => PasteClipboardEncryptedTo(string.IsNullOrEmpty(currentRightPath) ? vaultPath : currentRightPath);
            mnuEncryptCustom.Click += (s, e) => EncryptSelectedWithCustomPassword(listMiddle, () => LoadMiddlePane(currentMiddlePath));
            try { mnuPasteEncrypt.Image = IconExtractor.GetSystemIcon("shell32.dll", 262, true); } catch { }
            try { mnuEncryptFile.Image = IconExtractor.GetSystemIcon("shell32.dll", 47, true); } catch { }
            try { mnuEncryptCustom.Image = IconExtractor.GetSystemIcon("shell32.dll", 104, true); } catch { }

            mnuMiddleEncrypt.DropDownItems.Add(mnuPasteEncrypt);
            mnuMiddleEncrypt.DropDownItems.Add(mnuEncryptFile);
            mnuMiddleEncrypt.DropDownItems.Add(mnuEncryptCustom);
            ctxMiddle.Items.Add(mnuMiddleEncrypt);
            
            var mnuMiddleDecrypt = new ToolStripMenuItem("Giải mã");
            ctxMiddle.Items.Add(mnuMiddleDecrypt);
            ctxMiddle.Items.Add(new ToolStripSeparator());
            
            var mnuMiddleDelete = new ToolStripMenuItem("Delete") { ShortcutKeys = Keys.Delete };
            var mnuMiddleSecureDelete = new ToolStripMenuItem("Secure Delete");
            try { mnuMiddleDelete.Image = IconExtractor.GetSystemIcon("shell32.dll", 131, true); } catch { }
            try { mnuMiddleSecureDelete.Image = IconExtractor.GetSystemIcon("shell32.dll", 31, true); } catch { }
            
            mnuMiddleDelete.Click += (s, e) => {
                if (listMiddle.SelectedItems.Count > 0) {
                    if (MessageBox.Show("Bạn có chắc chắn muốn xóa tệp/thư mục này?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) {
                        try {
                            DeleteSelectedItems(listMiddle, false);
                            LoadMiddlePane(currentMiddlePath);
                        } catch (Exception ex) { MessageBox.Show("Lỗi: " + ex.Message); }
                    }
                }
            };
            
            mnuMiddleSecureDelete.Click += (s, e) => {
                if (listMiddle.SelectedItems.Count > 0) {
                    if (MessageBox.Show("CẢNH BÁO: Bạn đang dùng Xóa An Toàn 3 vòng (DoD 5220.22-M). Dữ liệu sẽ BỊ NGHIỀN NÁT và KHÔNG THỂ KHÔI PHỤC!\nTiếp tục?", "Cảnh báo Đỏ", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) {
                        try {
                            DeleteSelectedItems(listMiddle, true);
                            LoadMiddlePane(currentMiddlePath);
                            MessageBox.Show("Đã Xóa An Toàn thành công!", "Bảo mật", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        } catch (Exception ex) { MessageBox.Show("Lỗi: " + ex.Message); }
                    }
                }
            };
            
            mnuEncryptFile.Click += (s, e) => {
                if (listMiddle.SelectedItems.Count > 0) {
                    try {
                        int count = 0;
                        foreach (ListViewItem selected in listMiddle.SelectedItems)
                        {
                            string p = selected.Tag != null ? selected.Tag.ToString() : "";
                            if (!File.Exists(p)) continue;
                            if (p.EndsWith(".k3enc", StringComparison.OrdinalIgnoreCase)) continue;
                            string dest = GetAvailablePath(p + ".k3enc");
                            CryptoEngine.EncryptFile(p, dest);
                            CryptoEngine.SecureShredFile(p);
                            count++;
                        }
                        LoadMiddlePane(currentMiddlePath);
                        MessageBox.Show(string.Format("Đã mã hóa {0} tệp thành công!", count), "Mã hóa", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    } catch (Exception ex) { MessageBox.Show("Lỗi mã hóa: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                }
            };

            mnuMiddleDecrypt.Click += (s, e) => {
                if (listMiddle.SelectedItems.Count > 0) {
                    try {
                        string p = listMiddle.SelectedItems[0].Tag.ToString();
                        if (File.Exists(p) && p.EndsWith(".k3enc")) {
                            DecryptAnyEncryptedFile(p, Path.GetDirectoryName(p));
                            File.Delete(p);
                            LoadMiddlePane(currentMiddlePath);
                            MessageBox.Show("Đã giải mã tệp thành công!", "Giải mã", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        } else {
                            MessageBox.Show("Vui lòng chọn một tệp đã mã hóa (.k3enc).", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    } catch (Exception ex) { MessageBox.Show("Lỗi giải mã: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                }
            };

            ctxMiddle.Items.Add(mnuMiddleDelete);
            ctxMiddle.Items.Add(mnuMiddleSecureDelete);
            ctxMiddle.Items.Add(new ToolStripSeparator());
            var mnuMiddleRename = new ToolStripMenuItem("Rename") { ShortcutKeys = Keys.F2 };
            mnuMiddleRename.Click += (s, e) => RenameSelectedItem(listMiddle, () => LoadMiddlePane(currentMiddlePath));
            ctxMiddle.Items.Add(mnuMiddleRename);
            
            var mnuMiddleNewFolder = new ToolStripMenuItem("New Folder");
            mnuMiddleNewFolder.Click += (s, e) => {
                if (!string.IsNullOrEmpty(currentMiddlePath)) {
                    CreateNewFolder(currentMiddlePath, () => LoadMiddlePane(currentMiddlePath));
                }
            };
            var mnuMiddleNewNote = new ToolStripMenuItem("Tạo ghi chú");
            mnuMiddleNewNote.Click += (s, e) => CreateTextNote(false);
            var mnuMiddleEditNote = new ToolStripMenuItem("Sửa ghi chú");
            mnuMiddleEditNote.Click += (s, e) => EditSelectedTextNote(listMiddle, false);
            var mnuMiddleRefresh = new ToolStripMenuItem("Refresh");
            mnuMiddleRefresh.Click += (s, e) => {
                if (!string.IsNullOrEmpty(currentMiddlePath)) LoadMiddlePane(currentMiddlePath);
            };
            var mnuMiddleScanVirus = new ToolStripMenuItem("Quét virus");
            mnuMiddleScanVirus.Click += (s, e) => ScanSelectedPath(listMiddle);
            var mnuMiddleOpenPath = new ToolStripMenuItem("Mở đường dẫn");
            mnuMiddleOpenPath.Click += (s, e) => OpenSelectedPath(listMiddle);
            ctxMiddle.Items.Add(mnuMiddleNewFolder);
            ctxMiddle.Items.Add(mnuMiddleNewNote);
            ctxMiddle.Items.Add(mnuMiddleEditNote);
            ctxMiddle.Items.Add(mnuMiddleRefresh);
            ctxMiddle.Items.Add(mnuMiddleScanVirus);
            ctxMiddle.Items.Add(new ToolStripSeparator());
            ctxMiddle.Items.Add(mnuMiddleOpenPath);
            listMiddle.ContextMenuStrip = ctxMiddle;

            Panel middlePanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Mist, Padding = new Padding(8, 0, 8, 0) };
            btnTransferRight = new Button { Name = "btnTransferRight", Size = new Size(42, 42), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, TabStop = false };
            btnTransferRight.FlatAppearance.BorderSize = 0;
            btnTransferRight.Paint += DrawCircularButton;
            btnTransferRight.MouseEnter += (s, e) => btnTransferRight.Invalidate();
            btnTransferRight.MouseLeave += (s, e) => btnTransferRight.Invalidate();
            
            btnTransferLeft = new Button { Name = "btnTransferLeft", Size = new Size(42, 42), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, TabStop = false };
            btnTransferLeft.FlatAppearance.BorderSize = 0;
            btnTransferLeft.Paint += DrawCircularButton;
            btnTransferLeft.MouseEnter += (s, e) => btnTransferLeft.Invalidate();
            btnTransferLeft.MouseLeave += (s, e) => btnTransferLeft.Invalidate();

            var transferTip = new ToolTip();
            transferTip.SetToolTip(btnTransferRight, "Đưa file đã chọn vào Két sắt USB");
            transferTip.SetToolTip(btnTransferLeft, "Giải mã / đưa file đã chọn ra Máy tính");

            middlePanel.Resize += (s, e) => {
                int buttonSize = btnTransferRight.Width;
                btnTransferRight.Location = new Point((middlePanel.Width - buttonSize) / 2, (middlePanel.Height / 2) - 52);
                btnTransferLeft.Location = new Point((middlePanel.Width - buttonSize) / 2, (middlePanel.Height / 2) + 10);
            };
            
            btnTransferRight.Click += (s, e) => {
                if (listMiddle.SelectedItems.Count > 0)
                {
                    try
                    {
                        string targetVaultDir = string.IsNullOrEmpty(currentRightPath) ? vaultPath : currentRightPath;
                        EncryptListSelectionToVault(listMiddle, targetVaultDir);
                        LoadRightPane(currentRightPath);
                        MessageBox.Show(LanguageManager.GetString("Ctx_Encrypt") + " thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex) { MessageBox.Show("Lỗi mã hóa: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                }
            };
            
            btnTransferLeft.Click += (s, e) => {
                if (listRight.SelectedItems.Count > 0)
                {
                    try
                    {
                        string outDir = string.IsNullOrEmpty(currentMiddlePath) ? AppDomain.CurrentDomain.BaseDirectory : currentMiddlePath;
                        foreach (ListViewItem item in listRight.SelectedItems)
                        {
                            string path = item.Tag != null ? item.Tag.ToString() : "";
                            if (File.Exists(path)) {
                                if (ConfigManager.AutoDecrypt || path.EndsWith(".k3enc", StringComparison.OrdinalIgnoreCase)) DecryptAnyEncryptedFile(path, outDir);
                                else File.Copy(path, GetAvailablePath(Path.Combine(outDir, Path.GetFileName(path))));
                            }
                            else if (Directory.Exists(path)) {
                                string folderName = new DirectoryInfo(path).Name;
                                string targetDir = Path.Combine(outDir, folderName);
                                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                                
                                foreach (var file in Directory.GetFiles(path, "*.k3enc", SearchOption.AllDirectories))
                                {
                                    string rel = file.Substring(path.Length).TrimStart('\\', '/');
                                    string relDir = Path.GetDirectoryName(rel) ?? "";
                                    string destDir = Path.Combine(targetDir, relDir);
                                    if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                                    DecryptAnyEncryptedFile(file, destDir);
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(currentMiddlePath)) LoadMiddlePane(currentMiddlePath);
                        MessageBox.Show(LanguageManager.GetString("Ctx_Decrypt") + " thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex) { MessageBox.Show("Lỗi giải mã: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                }
            };

            middlePanel.Controls.Add(btnTransferRight);
            middlePanel.Controls.Add(btnTransferLeft);

            listRight = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, AllowDrop = true, GridLines = false, SmallImageList = sysIconsSmall, BorderStyle = BorderStyle.None, Font = Theme.FontBody, BackColor = Color.FromArgb(249, 250, 251), ForeColor = Theme.TextDark };
            listRight.Columns.Add("Tên", 150);
            listRight.Columns.Add("Loại", 80);
            listRight.Columns.Add("Kích thước", 80);
            listRight.Columns.Add("Ngày sửa cuối", 150);
            listRight.Resize += (s, e) => ResizeFileListColumns(listRight);
            listRight.DoubleClick += (s, e) => {
                if (listRight.SelectedItems.Count > 0)
                {
                    string path = listRight.SelectedItems[0].Tag.ToString();
                    if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) 
                        LoadRightPane(path);
                }
            };
            listRight.ItemDrag += (s, e) => BeginListViewDrag(listRight);
            listRight.DragEnter += (s, e) => HandleFileDragEnter(e);
            listRight.DragDrop += (s, e) => DropPathsToVaultPane(e);
            listRight.KeyDown += (s, e) => HandleListShortcut(listRight, true, e);

            mainLayout.Controls.Add(treeLeft, 0, 0);
            mainLayout.Controls.Add(listMiddle, 1, 0);
            mainLayout.Controls.Add(middlePanel, 2, 0);
            mainLayout.Controls.Add(listRight, 3, 0);

            ctxRight = new ContextMenuStrip();
            var mnuRightCopy = new ToolStripMenuItem("Copy") { ShortcutKeys = Keys.Control | Keys.C };
            var mnuRightCut = new ToolStripMenuItem("Cut") { ShortcutKeys = Keys.Control | Keys.X };
            var mnuRightPaste = new ToolStripMenuItem("Paste (mã hóa)") { ShortcutKeys = Keys.Control | Keys.V };
            mnuRightCopy.Click += (s, e) => CaptureClipboardSelection(listRight, false);
            mnuRightCut.Click += (s, e) => CaptureClipboardSelection(listRight, true);
            mnuRightPaste.Click += (s, e) => PasteClipboardEncryptedTo(string.IsNullOrEmpty(currentRightPath) ? vaultPath : currentRightPath);
            ctxRight.Items.Add(mnuRightCopy);
            ctxRight.Items.Add(mnuRightCut);
            ctxRight.Items.Add(mnuRightPaste);
            ctxRight.Items.Add(new ToolStripSeparator());
            var mnuEncrypt = new ToolStripMenuItem("Mã hóa");
            var mnuRightEncryptFile = new ToolStripMenuItem("Mã hóa tệp");
            var mnuRightEncryptCustom = new ToolStripMenuItem("Mã hóa với khóa tùy chọn");
            mnuRightEncryptCustom.Click += (s, e) => EncryptSelectedWithCustomPassword(listRight, () => LoadRightPane(currentRightPath));
            mnuEncrypt.DropDownItems.Add(mnuRightEncryptFile);
            mnuEncrypt.DropDownItems.Add(mnuRightEncryptCustom);
            ctxRight.Items.Add(mnuEncrypt);
            var mnuRightDecrypt = new ToolStripMenuItem("Giải mã");
            ctxRight.Items.Add(mnuRightDecrypt);
            ctxRight.Items.Add(new ToolStripSeparator());
            var mnuRightDelete = new ToolStripMenuItem("Delete") { ShortcutKeys = Keys.Delete };
            var mnuRightSecureDelete = new ToolStripMenuItem("Secure Delete");
            try { mnuRightDelete.Image = IconExtractor.GetSystemIcon("shell32.dll", 131, true); } catch { }
            try { mnuRightSecureDelete.Image = IconExtractor.GetSystemIcon("shell32.dll", 31, true); } catch { }

            mnuRightEncryptFile.Click += (s, e) => {
                if (listRight.SelectedItems.Count > 0) {
                    try {
                        int count = 0;
                        foreach (ListViewItem selected in listRight.SelectedItems)
                        {
                            string p = selected.Tag != null ? selected.Tag.ToString() : "";
                            if (!File.Exists(p) || p.EndsWith(".k3enc", StringComparison.OrdinalIgnoreCase)) continue;
                            string dest = GetAvailablePath(p + ".k3enc");
                            CryptoEngine.EncryptFile(p, dest);
                            CryptoEngine.SecureShredFile(p);
                            count++;
                        }
                        LoadRightPane(currentRightPath);
                        MessageBox.Show(string.Format("Đã mã hóa {0} tệp thành công!", count), "Mã hóa", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    } catch (Exception ex) { MessageBox.Show("Lỗi mã hóa: " + ex.Message); }
                }
            };
            
            mnuRightDecrypt.Click += (s, e) => {
                if (listRight.SelectedItems.Count > 0) {
                    try {
                        string outDir = GetDecryptOutputDirectory();
                        if (string.IsNullOrEmpty(outDir)) return;

                        int count = 0;
                        foreach (ListViewItem selected in listRight.SelectedItems)
                        {
                            string p = selected.Tag != null ? selected.Tag.ToString() : "";
                            if (File.Exists(p) && p.EndsWith(".k3enc")) {
                                DecryptAnyEncryptedFile(p, outDir);
                                count++;
                            }
                            else if (Directory.Exists(p))
                            {
                                string folderName = new DirectoryInfo(p).Name;
                                string targetDir = Path.Combine(outDir, folderName);
                                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                                foreach (var file in Directory.GetFiles(p, "*.k3enc", SearchOption.AllDirectories))
                                {
                                    string rel = file.Substring(p.Length).TrimStart('\\', '/');
                                    string relDir = Path.GetDirectoryName(rel) ?? "";
                                    string destDir = Path.Combine(targetDir, relDir);
                                    if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                                    DecryptAnyEncryptedFile(file, destDir);
                                    count++;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(currentMiddlePath)) LoadMiddlePane(currentMiddlePath);
                        LoadRightPane(currentRightPath);
                        MessageBox.Show(string.Format("Đã giải mã {0} tệp ra máy tính. Bản mã hóa trong Két sắt vẫn được giữ lại.", count), "Giải mã", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    } catch (Exception ex) { MessageBox.Show("Lỗi giải mã: " + ex.Message); }
                }
            };

            mnuRightDelete.Click += (s, e) => {
                if (listRight.SelectedItems.Count > 0) {
                    if (MessageBox.Show("Bạn có chắc chắn muốn xóa khỏi két sắt?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) {
                        try {
                            DeleteSelectedItems(listRight, false);
                            LoadRightPane(currentRightPath);
                        } catch (Exception ex) { MessageBox.Show("Lỗi: " + ex.Message); }
                    }
                }
            };

            mnuRightSecureDelete.Click += (s, e) => {
                if (listRight.SelectedItems.Count > 0) {
                    if (MessageBox.Show("CẢNH BÁO: Xóa An Toàn 3 vòng (DoD 5220.22-M) khỏi két sắt. KHÔNG THỂ KHÔI PHỤC!\nTiếp tục?", "Cảnh báo Đỏ", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) {
                        try {
                            DeleteSelectedItems(listRight, true);
                            LoadRightPane(currentRightPath);
                            MessageBox.Show("Đã Xóa An Toàn thành công!", "Bảo mật", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        } catch (Exception ex) { MessageBox.Show("Lỗi: " + ex.Message); }
                    }
                }
            };

            ctxRight.Items.Add(mnuRightDelete);
            ctxRight.Items.Add(mnuRightSecureDelete);
            ctxRight.Items.Add(new ToolStripSeparator());
            var mnuRightNewFolder = new ToolStripMenuItem("New Folder");
            mnuRightNewFolder.Click += (s, e) => {
                string targetVaultDir = string.IsNullOrEmpty(currentRightPath) ? vaultPath : currentRightPath;
                CreateNewFolder(targetVaultDir, () => LoadRightPane(currentRightPath));
            };
            var mnuRightNewNote = new ToolStripMenuItem("Tạo ghi chú");
            mnuRightNewNote.Click += (s, e) => CreateTextNote(true);
            var mnuRightEditNote = new ToolStripMenuItem("Sửa ghi chú");
            mnuRightEditNote.Click += (s, e) => EditSelectedTextNote(listRight, true);
            var mnuRightRename = new ToolStripMenuItem("Rename") { ShortcutKeys = Keys.F2 };
            mnuRightRename.Click += (s, e) => RenameSelectedItem(listRight, () => LoadRightPane(currentRightPath));
            ctxRight.Items.Add(mnuRightRename);
            ctxRight.Items.Add(mnuRightNewFolder);
            ctxRight.Items.Add(mnuRightNewNote);
            ctxRight.Items.Add(mnuRightEditNote);
            var mnuRightRefresh = new ToolStripMenuItem("Refresh");
            mnuRightRefresh.Click += (s, e) => LoadRightPane(currentRightPath);
            ctxRight.Items.Add(mnuRightRefresh);
            var mnuRightScanVirus = new ToolStripMenuItem("Quét virus");
            mnuRightScanVirus.Click += (s, e) => ScanSelectedPath(listRight);
            ctxRight.Items.Add(mnuRightScanVirus);
            ctxRight.Items.Add(new ToolStripSeparator());
            var mnuRightOpenPath = new ToolStripMenuItem("Mở đường dẫn");
            mnuRightOpenPath.Click += (s, e) => OpenSelectedPath(listRight);
            ctxRight.Items.Add(mnuRightOpenPath);
            listRight.ContextMenuStrip = ctxRight;

            // Old status strip removed

            this.Controls.Add(mainLayout);
            this.Controls.Add(topPanel);
            this.Controls.Add(toolStrip);
            this.Controls.Add(menuStrip);
            this.Controls.Add(statusStrip);
            this.MainMenuStrip = menuStrip;
            this.Resize += (s, e) => {
                ResizeFileListColumns(listMiddle);
                ResizeFileListColumns(listRight);
            };
        }

        private ToolStripButton MakeToolbarButton(string text, Image image, string tooltip)
        {
            return new ToolStripButton
            {
                Text = text,
                Image = image,
                ToolTipText = tooltip,
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                TextImageRelation = TextImageRelation.ImageAboveText,
                Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
                ForeColor = Theme.TextInv,
                AutoSize = false,
                Width = 88,
                Height = 60
            };
        }

        private void ResizeFileListColumns(ListView list)
        {
            if (list == null || list.Columns.Count < 4 || list.ClientSize.Width <= 0) return;

            int typeWidth = 82;
            int sizeWidth = 86;
            int dateWidth = list.ClientSize.Width < 520 ? 118 : 150;
            int nameWidth = Math.Max(150, list.ClientSize.Width - typeWidth - sizeWidth - dateWidth - 8);

            list.BeginUpdate();
            try
            {
                list.Columns[0].Width = nameWidth;
                list.Columns[1].Width = typeWidth;
                list.Columns[2].Width = sizeWidth;
                list.Columns[3].Width = dateWidth;
            }
            finally
            {
                list.EndUpdate();
            }
        }

        private ListView GetActiveListView()
        {
            if (listRight != null && (listRight.Focused || listRight.SelectedItems.Count > 0)) return listRight;
            if (listMiddle != null && (listMiddle.Focused || listMiddle.SelectedItems.Count > 0)) return listMiddle;
            return listMiddle ?? listRight;
        }

        private void SelectItems(SelectionMode mode)
        {
            ListView list = GetActiveListView();
            if (list == null) return;

            foreach (ListViewItem item in list.Items)
            {
                bool isUp = item.Text == "..";
                if (isUp && mode != SelectionMode.None) continue;

                bool select;
                switch (mode)
                {
                    case SelectionMode.All:
                        select = true;
                        break;
                    case SelectionMode.None:
                        select = false;
                        break;
                    case SelectionMode.Invert:
                        select = !item.Selected;
                        break;
                    case SelectionMode.Encrypted:
                        select = item.Tag != null && item.Tag.ToString().EndsWith(".k3enc", StringComparison.OrdinalIgnoreCase);
                        break;
                    case SelectionMode.Plain:
                        select = item.Tag != null && File.Exists(item.Tag.ToString()) && !item.Tag.ToString().EndsWith(".k3enc", StringComparison.OrdinalIgnoreCase);
                        break;
                    default:
                        select = false;
                        break;
                }
                item.Selected = select;
            }
            list.Focus();
        }

        private void RefreshActivePane()
        {
            if (listRight != null && (listRight.Focused || listRight.SelectedItems.Count > 0)) LoadRightPane(currentRightPath);
            else if (!string.IsNullOrEmpty(currentMiddlePath)) LoadMiddlePane(currentMiddlePath);
            else LoadRightPane(currentRightPath);
        }

        private void CopySelectedPaths()
        {
            ListView list = GetActiveListView();
            if (list == null || list.SelectedItems.Count == 0) return;

            var sb = new System.Text.StringBuilder();
            foreach (ListViewItem item in list.SelectedItems)
            {
                if (item.Tag != null) sb.AppendLine(item.Tag.ToString());
            }
            if (sb.Length > 0) Clipboard.SetText(sb.ToString().TrimEnd());
        }

        private void ToggleReadOnly()
        {
            bool next = !UsbHelper.IsReadOnlyEnabled();
            try
            {
                UsbHelper.SetReadOnly(next);
                btnReadOnlyToggle.Text = next ? "Mở ghi" : "Chặn ghi";
                btnReadOnlyToggle.Image = CustomIcons.GetLockToggleIcon(next);
                MessageBox.Show(next
                    ? "Đã bật chặn ghi USB bằng Windows policy và DiskPart. Nếu Explorer vẫn hiển thị ghi được, hãy rút/cắm lại USB để Windows nạp lại trạng thái."
                    : "Đã tắt chặn ghi USB. Nếu Windows vẫn báo chỉ đọc, hãy rút/cắm lại USB để áp dụng đầy đủ.",
                    "Chặn ghi USB", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Chặn ghi USB", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CaptureClipboardSelection(ListView sourceList, bool cut)
        {
            clipboardPaths.Clear();
            foreach (ListViewItem item in sourceList.SelectedItems)
            {
                string path = item.Tag != null ? item.Tag.ToString() : "";
                if (File.Exists(path) || Directory.Exists(path)) clipboardPaths.Add(path);
            }
            clipboardIsCut = cut;
        }

        private string[] GetSelectedPaths(ListView sourceList)
        {
            List<string> paths = new List<string>();
            foreach (ListViewItem item in sourceList.SelectedItems)
            {
                if (item.Text == "..") continue;
                string path = item.Tag != null ? item.Tag.ToString() : "";
                if (File.Exists(path) || Directory.Exists(path)) paths.Add(path);
            }
            return paths.ToArray();
        }

        private void BeginListViewDrag(ListView sourceList)
        {
            string[] paths = GetSelectedPaths(sourceList);
            if (paths.Length == 0) return;
            sourceList.DoDragDrop(new DataObject(DataFormats.FileDrop, paths), DragDropEffects.Copy);
        }

        private void HandleFileDragEnter(DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void DropPathsToVaultPane(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            string targetVaultDir = string.IsNullOrEmpty(currentRightPath) ? vaultPath : currentRightPath;
            int count = EncryptPathsToVault(paths, targetVaultDir, false);
            LoadRightPane(currentRightPath);
            MessageBox.Show(string.Format("Đã mã hóa {0} tệp vào Két sắt.", count), "Kéo thả mã hóa", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void DropPathsToLocalPane(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            if (string.IsNullOrEmpty(currentMiddlePath) || !Directory.Exists(currentMiddlePath)) return;
            string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            int count = DecryptOrCopyPathsToLocal(paths, currentMiddlePath);
            LoadMiddlePane(currentMiddlePath);
            MessageBox.Show(string.Format("Đã đưa {0} tệp ra thư mục máy tính.", count), "Kéo thả", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void HandleListShortcut(ListView list, bool isVaultPane, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C)
            {
                CaptureClipboardSelection(list, false);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.X)
            {
                CaptureClipboardSelection(list, true);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.V)
            {
                if (isVaultPane) PasteClipboardEncryptedTo(string.IsNullOrEmpty(currentRightPath) ? vaultPath : currentRightPath);
                else PasteClipboardTo(currentMiddlePath, () => LoadMiddlePane(currentMiddlePath));
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.M)
            {
                PasteClipboardEncryptedTo(string.IsNullOrEmpty(currentRightPath) ? vaultPath : currentRightPath);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                if (MessageBox.Show("Bạn có chắc chắn muốn xóa các mục đã chọn?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    DeleteSelectedItems(list, false);
                    if (isVaultPane) LoadRightPane(currentRightPath); else LoadMiddlePane(currentMiddlePath);
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F2)
            {
                RenameSelectedItem(list, () => { if (isVaultPane) LoadRightPane(currentRightPath); else LoadMiddlePane(currentMiddlePath); });
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F5)
            {
                if (isVaultPane) LoadRightPane(currentRightPath); else LoadMiddlePane(currentMiddlePath);
                e.Handled = true;
            }
        }

        private void DeleteSelectedItems(ListView list, bool secure)
        {
            foreach (string path in GetSelectedPaths(list))
            {
                if (secure) SecureDeletePath(path);
                else
                {
                    if (File.Exists(path)) File.Delete(path);
                    else if (Directory.Exists(path)) Directory.Delete(path, true);
                }
            }
        }

        private void SecureDeletePath(string path)
        {
            if (File.Exists(path))
            {
                CryptoEngine.SecureShredFile(path);
                return;
            }

            if (!Directory.Exists(path)) return;
            foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                try { CryptoEngine.SecureShredFile(file); } catch { }
            }
            Directory.Delete(path, true);
        }

        private void EncryptListSelectionToVault(ListView list, string targetVaultDir)
        {
            EncryptPathsToVault(GetSelectedPaths(list), targetVaultDir, false);
        }

        private int EncryptPathsToVault(IEnumerable<string> paths, string targetVaultDir, bool removeSource)
        {
            if (string.IsNullOrEmpty(targetVaultDir)) targetVaultDir = vaultPath;
            Directory.CreateDirectory(targetVaultDir);

            int count = 0;
            foreach (string source in paths)
            {
                if (File.Exists(source))
                {
                    if (source.EndsWith(".k3enc", StringComparison.OrdinalIgnoreCase) && IsInsidePath(source, vaultPath)) continue;
                    if (!IsFileCleanForUsbImport(source, true)) continue;
                    string dest = GetAvailablePath(Path.Combine(targetVaultDir, Path.GetFileName(source) + ".k3enc"));
                    CryptoEngine.EncryptFile(source, dest);
                    if (removeSource) CryptoEngine.SecureShredFile(source);
                    count++;
                }
                else if (Directory.Exists(source))
                {
                    string folderRoot = GetAvailablePath(Path.Combine(targetVaultDir, Path.GetFileName(source)));
                    Directory.CreateDirectory(folderRoot);
                    foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                    {
                        string relDir = dir.Substring(source.Length).TrimStart('\\', '/');
                        Directory.CreateDirectory(Path.Combine(folderRoot, relDir));
                    }
                    bool allFilesAccepted = true;
                    foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                    {
                        if (!IsFileCleanForUsbImport(file, true))
                        {
                            allFilesAccepted = false;
                            continue;
                        }
                        string rel = file.Substring(source.Length).TrimStart('\\', '/');
                        string destDir = Path.Combine(folderRoot, Path.GetDirectoryName(rel) ?? "");
                        Directory.CreateDirectory(destDir);
                        string dest = GetAvailablePath(Path.Combine(destDir, Path.GetFileName(file) + ".k3enc"));
                        CryptoEngine.EncryptFile(file, dest);
                        count++;
                    }
                    if (removeSource && allFilesAccepted) SecureDeletePath(source);
                }
            }
            return count;
        }

        private bool IsFileCleanForUsbImport(string filePath, bool showMessage)
        {
            if (!File.Exists(filePath)) return false;
            if (TrustedFileManager.IsTrusted(filePath)) return true;
            ScanResult result = AntivirusScanner.ScanFileReal(filePath);
            if (result.Status == "Sạch") return true;

            if (showMessage)
            {
                string reason = result.Status == "Lỗi" ? "Không thể quét an toàn" : result.VirusName;
                return ShowThreatAlert(filePath, reason, true);
            }
            return false;
        }

        private bool ShowThreatAlert(string filePath, string reason, bool allowQuarantine)
        {
            try
            {
                using (ThreatAlertDialog dialog = new ThreatAlertDialog(filePath, reason, allowQuarantine))
                {
                    dialog.ShowDialog(this);
                    dialog.ExecuteSelectedAction();
                    return dialog.SelectedAction == ThreatAlertAction.TrustFile && TrustedFileManager.IsTrusted(filePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("K3-AV đã chặn file trước khi đưa vào USB/Két sắt:\n\n" + filePath + "\n\nLý do: " + reason + "\n\nKhông thể thực hiện thao tác phụ: " + ex.Message, "K3-AV cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return false;
        }

        private int DecryptOrCopyPathsToLocal(IEnumerable<string> paths, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            int count = 0;
            foreach (string source in paths)
            {
                if (File.Exists(source))
                {
                    if (source.EndsWith(".k3enc", StringComparison.OrdinalIgnoreCase))
                    {
                        DecryptAnyEncryptedFile(source, targetDir);
                    }
                    else
                    {
                        File.Copy(source, GetAvailablePath(Path.Combine(targetDir, Path.GetFileName(source))));
                    }
                    count++;
                }
                else if (Directory.Exists(source))
                {
                    string folderRoot = GetAvailablePath(Path.Combine(targetDir, Path.GetFileName(source)));
                    Directory.CreateDirectory(folderRoot);
                    foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                    {
                        string relDir = dir.Substring(source.Length).TrimStart('\\', '/');
                        Directory.CreateDirectory(Path.Combine(folderRoot, relDir));
                    }
                    foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                    {
                        string rel = file.Substring(source.Length).TrimStart('\\', '/');
                        string destDir = Path.Combine(folderRoot, Path.GetDirectoryName(rel) ?? "");
                        Directory.CreateDirectory(destDir);
                        if (file.EndsWith(".k3enc", StringComparison.OrdinalIgnoreCase))
                            DecryptAnyEncryptedFile(file, destDir);
                        else
                            File.Copy(file, GetAvailablePath(Path.Combine(destDir, Path.GetFileName(file))));
                        count++;
                    }
                }
            }
            return count;
        }

        private void PasteClipboardTo(string targetDir, Action reloadAction)
        {
            if (clipboardPaths.Count == 0 || string.IsNullOrEmpty(targetDir)) return;
            Directory.CreateDirectory(targetDir);

            foreach (string source in clipboardPaths.ToArray())
            {
                string target = Path.Combine(targetDir, Path.GetFileName(source));
                if (File.Exists(source))
                {
                    bool shouldDecrypt = source.EndsWith(".k3enc", StringComparison.OrdinalIgnoreCase) && (ConfigManager.AutoDecrypt || IsInsidePath(source, vaultPath));
                    if (shouldDecrypt)
                    {
                        DecryptAnyEncryptedFile(source, targetDir);
                        if (clipboardIsCut) CryptoEngine.SecureShredFile(source);
                    }
                    else
                    {
                        target = GetAvailablePath(target);
                        if (clipboardIsCut) File.Move(source, target);
                        else File.Copy(source, target, false);
                    }
                }
                else if (Directory.Exists(source))
                {
                    bool shouldDecryptDirectory = ConfigManager.AutoDecrypt || IsInsidePath(source, vaultPath);
                    if (shouldDecryptDirectory)
                    {
                        DecryptOrCopyPathsToLocal(new[] { source }, targetDir);
                        if (clipboardIsCut) SecureDeletePath(source);
                    }
                    else
                    {
                        target = GetAvailablePath(target);
                        CopyDirectory(source, target);
                        if (clipboardIsCut) Directory.Delete(source, true);
                    }
                }
            }

            if (clipboardIsCut) clipboardPaths.Clear();
            reloadAction();
        }

        private void PasteClipboardEncryptedTo(string targetVaultDir)
        {
            if (clipboardPaths.Count == 0 || string.IsNullOrEmpty(targetVaultDir)) return;
            Directory.CreateDirectory(targetVaultDir);

            EncryptPathsToVault(clipboardPaths.ToArray(), targetVaultDir, clipboardIsCut);

            if (clipboardIsCut) clipboardPaths.Clear();
            LoadRightPane(currentRightPath);
        }

        private void RenameSelectedItem(ListView list, Action reloadAction)
        {
            if (list.SelectedItems.Count == 0) return;
            string path = list.SelectedItems[0].Tag != null ? list.SelectedItems[0].Tag.ToString() : "";
            if (!File.Exists(path) && !Directory.Exists(path)) return;

            string currentName = Path.GetFileName(path);
            string newName = PromptForText("Đổi tên", "Tên mới:", currentName);
            if (string.IsNullOrWhiteSpace(newName) || newName == currentName) return;
            if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show("Tên mới chứa ký tự không hợp lệ.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string target = Path.Combine(Path.GetDirectoryName(path), newName);
            if (File.Exists(target) || Directory.Exists(target))
            {
                MessageBox.Show("Đã tồn tại tệp hoặc thư mục cùng tên.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (File.Exists(path)) File.Move(path, target);
            else Directory.Move(path, target);
            reloadAction();
        }

        private string PromptForText(string title, string label, string value)
        {
            using (Form dialog = new Form())
            using (TextBox input = new TextBox())
            using (Button ok = new Button())
            using (Button cancel = new Button())
            using (Label lbl = new Label())
            {
                dialog.Text = title;
                dialog.Size = new Size(420, 155);
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;

                lbl.Text = label;
                lbl.Location = new Point(16, 18);
                lbl.AutoSize = true;
                input.Text = value;
                input.Location = new Point(16, 42);
                input.Width = 370;
                ok.Text = "OK";
                ok.Location = new Point(230, 78);
                ok.DialogResult = DialogResult.OK;
                cancel.Text = "Hủy";
                cancel.Location = new Point(310, 78);
                cancel.DialogResult = DialogResult.Cancel;
                dialog.Controls.AddRange(new Control[] { lbl, input, ok, cancel });
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;
                return dialog.ShowDialog(this) == DialogResult.OK ? input.Text.Trim() : null;
            }
        }

        private void OpenSelectedPath(ListView list)
        {
            if (list.SelectedItems.Count == 0) return;
            string path = list.SelectedItems[0].Tag != null ? list.SelectedItems[0].Tag.ToString() : "";
            if (File.Exists(path)) path = Path.GetDirectoryName(path);
            if (!Directory.Exists(path)) return;
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }

        private void ScanSelectedPath(ListView list)
        {
            if (list == null || list.SelectedItems.Count == 0) return;
            string path = list.SelectedItems[0].Tag != null ? list.SelectedItems[0].Tag.ToString() : "";
            if (!File.Exists(path) && !Directory.Exists(path)) return;
            using (var form = new AntivirusForm(path))
                form.ShowDialog(this);
        }

        private void CreateTextNote(bool inVault)
        {
            string baseDir = inVault
                ? (string.IsNullOrEmpty(currentRightPath) ? vaultPath : currentRightPath)
                : currentMiddlePath;
            if (string.IsNullOrEmpty(baseDir) || !Directory.Exists(baseDir))
            {
                MessageBox.Show("Vui lòng chọn thư mục đích trước.", "Tạo ghi chú", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string name = PromptForText("Tạo ghi chú", "Tên file ghi chú:", "note.txt");
            if (string.IsNullOrWhiteSpace(name)) return;
            if (Path.GetExtension(name) == "") name += ".txt";
            if (!IsTextNoteName(name))
            {
                MessageBox.Show("Chỉ hỗ trợ tạo file text như .txt, .md, .log, .ini, .json, .xml, .csv, .yml.", "Tạo ghi chú", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string title = inVault ? "Ghi chú trong Két - " + name : "Ghi chú - " + name;
            using (var editor = new TextNoteEditorForm(title, ""))
            {
                if (editor.ShowDialog(this) != DialogResult.OK || !editor.Saved) return;

                if (inVault)
                {
                    string tempDir = CreateTempEditorDir();
                    string tempFile = Path.Combine(tempDir, Path.GetFileName(name));
                    string tempEnc = tempFile + ".k3enc";
                    try
                    {
                        File.WriteAllText(tempFile, editor.EditedText, new UTF8Encoding(true));
                        string dest = GetAvailablePath(Path.Combine(baseDir, Path.GetFileName(name) + ".k3enc"));
                        CryptoEngine.EncryptFile(tempFile, tempEnc);
                        File.Copy(tempEnc, dest, false);
                    }
                    finally
                    {
                        DeleteTempEditorDir(tempDir);
                    }
                    LoadRightPane(currentRightPath);
                }
                else
                {
                    string dest = GetAvailablePath(Path.Combine(baseDir, Path.GetFileName(name)));
                    File.WriteAllText(dest, editor.EditedText, new UTF8Encoding(true));
                    LoadMiddlePane(currentMiddlePath);
                }
            }
        }

        private void EditSelectedTextNote(ListView list, bool inVault)
        {
            if (list == null || list.SelectedItems.Count == 0) return;
            string path = list.SelectedItems[0].Tag != null ? list.SelectedItems[0].Tag.ToString() : "";
            if (!File.Exists(path))
            {
                MessageBox.Show("Vui lòng chọn một file text/ghi chú.", "Sửa ghi chú", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool encrypted = path.EndsWith(".k3enc", StringComparison.OrdinalIgnoreCase);
            string displayName = encrypted ? Path.GetFileNameWithoutExtension(path) : Path.GetFileName(path);
            if (!IsTextNoteName(displayName))
            {
                MessageBox.Show("Chỉ hỗ trợ sửa file text: .txt, .md, .log, .ini, .json, .xml, .csv, .yml.", "Sửa ghi chú", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string tempDir = null;
            string editFile = path;
            try
            {
                if (encrypted)
                {
                    if (CryptoEngine.IsCustomEncryptedFile(path))
                    {
                        MessageBox.Show("File mã hóa bằng mật khẩu riêng chưa hỗ trợ sửa trực tiếp. Hãy giải mã ra máy rồi sửa.", "Sửa ghi chú", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    tempDir = CreateTempEditorDir();
                    CryptoEngine.DecryptFile(path, tempDir);
                    string[] files = Directory.GetFiles(tempDir);
                    if (files.Length == 0) throw new IOException("Không giải mã được file tạm.");
                    editFile = files[0];
                }

                if (new FileInfo(editFile).Length > 5 * 1024 * 1024)
                {
                    MessageBox.Show("File text quá lớn để sửa trực tiếp trong app.", "Sửa ghi chú", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string text = File.ReadAllText(editFile, Encoding.UTF8);
                using (var editor = new TextNoteEditorForm("Sửa ghi chú - " + displayName, text))
                {
                    if (editor.ShowDialog(this) != DialogResult.OK || !editor.Saved) return;
                    File.WriteAllText(editFile, editor.EditedText, new UTF8Encoding(true));

                    if (encrypted)
                    {
                        string tempEnc = editFile + ".k3enc";
                        CryptoEngine.EncryptFile(editFile, tempEnc);
                        File.Copy(tempEnc, path, true);
                    }
                }

                if (inVault) LoadRightPane(currentRightPath);
                else LoadMiddlePane(currentMiddlePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể sửa ghi chú: " + ex.Message, "Sửa ghi chú", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempDir)) DeleteTempEditorDir(tempDir);
            }
        }

        private bool IsTextNoteName(string fileName)
        {
            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext == ".txt" || ext == ".md" || ext == ".log" || ext == ".ini" || ext == ".json" ||
                   ext == ".xml" || ext == ".csv" || ext == ".yml" || ext == ".yaml" || ext == ".config";
        }

        private string CreateTempEditorDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "K3NoteEditor", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private void DeleteTempEditorDir(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                foreach (string file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try { CryptoEngine.SecureShredFile(file); } catch { try { File.Delete(file); } catch { } }
                }
                Directory.Delete(dir, true);
            }
            catch { }
        }

        private void EncryptSelectedWithCustomPassword(ListView list, Action reloadAction)
        {
            if (list.SelectedItems.Count == 0) return;

            string password = PromptForPassword("Mã hóa với khóa tùy chọn", "Nhập mật khẩu riêng:", true);
            if (string.IsNullOrEmpty(password)) return;

            int count = 0;
            foreach (ListViewItem item in list.SelectedItems)
            {
                string path = item.Tag != null ? item.Tag.ToString() : "";
                if (!File.Exists(path))
                {
                    MessageBox.Show("Chỉ hỗ trợ mã hóa với khóa tùy chọn cho từng tệp.", "Mã hóa", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    continue;
                }
                if (path.EndsWith(".k3enc", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Tệp này đã được mã hóa.", "Mã hóa", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    continue;
                }

                string dest = GetAvailablePath(path + ".k3enc");
                CryptoEngine.EncryptFileWithPassword(path, dest, password);
                CryptoEngine.SecureShredFile(path);
                count++;
            }

            reloadAction();
            if (count > 0) MessageBox.Show(string.Format("Đã mã hóa {0} tệp bằng mật khẩu riêng.", count), "Mã hóa", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void DecryptAnyEncryptedFile(string path, string destDir)
        {
            if (CryptoEngine.IsCustomEncryptedFile(path))
            {
                string password = PromptForPassword("Giải mã bằng mật khẩu riêng", "Nhập mật khẩu riêng:", false);
                if (string.IsNullOrEmpty(password)) throw new OperationCanceledException("Đã hủy nhập mật khẩu riêng.");
                CryptoEngine.DecryptFileWithPassword(path, destDir, password);
            }
            else
            {
                CryptoEngine.DecryptFile(path, destDir);
            }
        }

        private string GetDecryptOutputDirectory()
        {
            if (!string.IsNullOrEmpty(currentMiddlePath) && Directory.Exists(currentMiddlePath))
                return currentMiddlePath;

            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Chọn thư mục trên máy tính để nhận file giải mã";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    return dialog.SelectedPath;
            }
            return null;
        }

        private string PromptForPassword(string title, string label, bool confirmPassword)
        {
            using (Form dialog = new Form())
            using (Label lbl = new Label())
            using (TextBox txt = new TextBox())
            using (Label lbl2 = new Label())
            using (TextBox txt2 = new TextBox())
            using (Button ok = new Button())
            using (Button cancel = new Button())
            {
                dialog.Text = title;
                dialog.Size = new Size(420, confirmPassword ? 210 : 160);
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;

                lbl.Text = label;
                lbl.Location = new Point(18, 18);
                lbl.AutoSize = true;
                txt.Location = new Point(18, 42);
                txt.Width = 360;
                txt.PasswordChar = '●';

                lbl2.Text = "Nhập lại mật khẩu riêng:";
                lbl2.Location = new Point(18, 76);
                lbl2.AutoSize = true;
                txt2.Location = new Point(18, 100);
                txt2.Width = 360;
                txt2.PasswordChar = '●';
                lbl2.Visible = confirmPassword;
                txt2.Visible = confirmPassword;

                ok.Text = "OK";
                ok.Location = new Point(218, confirmPassword ? 136 : 78);
                ok.DialogResult = DialogResult.OK;
                cancel.Text = "Hủy";
                cancel.Location = new Point(302, confirmPassword ? 136 : 78);
                cancel.DialogResult = DialogResult.Cancel;

                dialog.Controls.AddRange(new Control[] { lbl, txt, lbl2, txt2, ok, cancel });
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;

                if (dialog.ShowDialog(this) != DialogResult.OK) return null;
                if (txt.Text.Length < 6)
                {
                    MessageBox.Show("Mật khẩu riêng cần ít nhất 6 ký tự.", title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return null;
                }
                if (confirmPassword && txt.Text != txt2.Text)
                {
                    MessageBox.Show("Hai lần nhập mật khẩu riêng không khớp.", title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return null;
                }
                return txt.Text;
            }
        }

        private void EncryptAutoFolderNow()
        {
            ConfigManager.LoadConfig();
            string folder = ConfigManager.AutoEncryptFolder == "Toàn bộ USB"
                ? AppDomain.CurrentDomain.BaseDirectory
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, string.IsNullOrEmpty(ConfigManager.AutoEncryptFolder) ? "BaoMat" : ConfigManager.AutoEncryptFolder);

            if (!Directory.Exists(folder))
            {
                MessageBox.Show("Không tìm thấy thư mục cần mã hóa.", "Mã hóa USB", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int encryptedCount = 0;
            int createdFolderCount = 0;
            bool watcherWasEnabled = watcher != null && watcher.EnableRaisingEvents;
            if (watcher != null) watcher.EnableRaisingEvents = false;
            try
            {
                foreach (string dir in Directory.GetDirectories(folder, "*", SearchOption.AllDirectories))
                {
                    if (EnsureVaultDirectoryForSource(dir)) createdFolderCount++;
                }

                foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                {
                    if (ShouldSkipAutoEncrypt(file)) continue;
                    if (ProcessFileQueue(file)) encryptedCount++;
                }
            }
            finally
            {
                if (watcher != null) watcher.EnableRaisingEvents = watcherWasEnabled;
            }
            LoadRightPane(currentRightPath);
            if (encryptedCount == 0 && createdFolderCount == 0)
            {
                MessageBox.Show("Không có tệp hoặc thư mục mới cần đưa vào Két sắt.\n\nThư mục đang quét: " + folder, "Đưa vào Két sắt", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show(string.Format("Đã đưa vào Két sắt:\n- {0} tệp đã mã hóa\n- {1} thư mục đã tạo\n\nNguồn: {2}", encryptedCount, createdFolderCount, folder), "Đưa vào Két sắt", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SecureFormatVaultData()
        {
            if (watcher != null) watcher.EnableRaisingEvents = false;
            foreach (string dir in new[] { ".vault", ".vault_decoy", "BaoMat" })
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dir);
                SecureDeleteDirectory(path);
                Directory.CreateDirectory(path);
            }
            currentRightPath = vaultPath;
            LoadRightPane(vaultPath);
            if (watcher != null) watcher.EnableRaisingEvents = true;
        }

        private void SecureDeleteDirectory(string path)
        {
            if (!Directory.Exists(path)) return;
            foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                CryptoEngine.SecureShredFile(file);
            Directory.Delete(path, true);
        }

        private void CleanupAutoEncryptFolder()
        {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BaoMat");
            if (!Directory.Exists(folder)) return;
            foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                CryptoEngine.SecureShredFile(file);
        }

        private void CleanupMacMetadata()
        {
            foreach (string file in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "._*", SearchOption.AllDirectories))
            {
                try { File.Delete(file); } catch { }
            }
            foreach (string file in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, ".DS_Store", SearchOption.AllDirectories))
            {
                try { File.Delete(file); } catch { }
            }
        }

        private void CleanupRecentHistory()
        {
            string recentPath = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
            if (!Directory.Exists(recentPath)) return;
            foreach (string file in Directory.GetFiles(recentPath))
            {
                try { File.Delete(file); } catch { }
            }
        }

        private void RunDriveRepair()
        {
            try
            {
                string root = GetCurrentDriveLetter();
                string output = RunWindowsDiskTool("chkdsk.exe", root + " /scan", 180000);
                ShowDiskToolOutput("Sửa lỗi USB", output);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể chạy kiểm tra ổ đĩa: " + ex.Message, "Sửa chữa USB", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunDriveOptimize()
        {
            try
            {
                string root = GetCurrentDriveLetter();
                DriveInfo drive = new DriveInfo(root + "\\");
                string note = drive.DriveType == DriveType.Removable
                    ? "Ổ hiện tại là Removable/USB. Một số USB flash không cần chống phân mảnh; Windows sẽ tự tối ưu phù hợp nếu hỗ trợ."
                    : "Windows sẽ tự chọn tối ưu phù hợp: defrag cho HDD, trim/retrim cho SSD nếu hỗ trợ.";

                if (MessageBox.Show(note + "\n\nTiếp tục tối ưu ổ " + root + "?", "Tối ưu ổ", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                string output = RunWindowsDiskTool("defrag.exe", root + " /O /U /V", 300000);
                ShowDiskToolOutput("Tối ưu / Chống phân mảnh ổ", output);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể chạy tối ưu ổ: " + ex.Message, "Tối ưu ổ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GetCurrentDriveLetter()
        {
            string root = Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory);
            if (string.IsNullOrEmpty(root)) throw new InvalidOperationException("Không xác định được ổ đang chạy ứng dụng.");
            return root.TrimEnd('\\');
        }

        private string RunWindowsDiskTool(string fileName, string arguments, int timeoutMs)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process proc = Process.Start(psi))
            {
                if (!proc.WaitForExit(timeoutMs))
                {
                    try { proc.Kill(); } catch { }
                    return "Quá thời gian chờ khi chạy: " + fileName + " " + arguments;
                }

                string output = "";
                try { output = proc.StandardOutput.ReadToEnd(); } catch { }
                string error = "";
                try { error = proc.StandardError.ReadToEnd(); } catch { }
                if (!string.IsNullOrWhiteSpace(error)) output += "\r\n" + error;
                output += "\r\nExitCode: " + proc.ExitCode;
                return string.IsNullOrWhiteSpace(output) ? "Không có output từ công cụ Windows." : output.Trim();
            }
        }

        private void ShowDiskToolOutput(string title, string output)
        {
            if (string.IsNullOrWhiteSpace(output)) output = "Không có output.";
            MessageBox.Show(output.Length > 1800 ? output.Substring(0, 1800) + "..." : output, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (string dir in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dir.Replace(sourceDir, targetDir));
            foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
                File.Copy(file, file.Replace(sourceDir, targetDir), false);
        }

        private string GetAvailablePath(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return path;
            string dir = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            int i = 1;
            string candidate;
            do
            {
                candidate = Path.Combine(dir, string.Format("{0} ({1}){2}", name, i, ext));
                i++;
            } while (File.Exists(candidate) || Directory.Exists(candidate));
            return candidate;
        }

        private void ApplyLanguage()
        {
            this.Text = LanguageManager.GetString("AppTitle");
            menuSelection.Text = LanguageManager.GetString("Menu_Selection");
            menuFunction.Text = LanguageManager.GetString("Menu_Function");
            menuHelp.Text = LanguageManager.GetString("Menu_Help");
            
            btnEncryptUSB.Text = LanguageManager.GetString("Btn_EncryptUSB");

            ctxRight.Items[0].Text = LanguageManager.GetString("Ctx_Copy");
            ctxRight.Items[1].Text = LanguageManager.GetString("Ctx_Cut");
            ctxRight.Items[2].Text = LanguageManager.GetString("Ctx_Paste") + " (mã hóa)";
            
            ToolStripMenuItem encryptMnu = (ToolStripMenuItem)ctxRight.Items[4];
            encryptMnu.Text = LanguageManager.GetString("Ctx_Encrypt");
            encryptMnu.DropDownItems[0].Text = LanguageManager.GetString("Ctx_Encrypt_File");
            encryptMnu.DropDownItems[1].Text = LanguageManager.GetString("Ctx_Encrypt_Custom");
            
            ctxRight.Items[5].Text = LanguageManager.GetString("Ctx_Decrypt");
            ctxRight.Items[7].Text = LanguageManager.GetString("Ctx_Delete");
            ctxRight.Items[8].Text = LanguageManager.GetString("Ctx_SecureDelete");
            ctxRight.Items[10].Text = LanguageManager.GetString("Ctx_Rename");
            ctxRight.Items[11].Text = LanguageManager.GetString("Ctx_NewFolder");
            ctxRight.Items[12].Text = "Tạo ghi chú";
            ctxRight.Items[13].Text = "Sửa ghi chú";
            ctxRight.Items[14].Text = LanguageManager.GetString("Ctx_Refresh");
            ctxRight.Items[15].Text = "Quét virus";
            ctxRight.Items[17].Text = LanguageManager.GetString("Ctx_OpenPath");
        }
        private void PerformGlobalSearchLocal(string query, CancellationToken token)
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                SearchDirectoryRecursive(drive.Name, query, true, token);
                if (token.IsCancellationRequested || searchMatchCount > 300) break;
            }
        }

        private void PerformGlobalSearchVault(string query, CancellationToken token)
        {
            SearchDirectoryRecursive(vaultPath, query, false, token);
        }

        private void SearchDirectoryRecursive(string dir, string query, bool isLocal, CancellationToken token)
        {
            if (token.IsCancellationRequested || searchMatchCount > 300) return;
            try
            {
                foreach (var f in Directory.GetFiles(dir))
                {
                    if (token.IsCancellationRequested || searchMatchCount > 300) return;
                    string fileName = Path.GetFileName(f);
                    
                    if (isLocal)
                    {
                        if (fileName.ToLower().Contains(query))
                        {
                            var fi = new FileInfo(f);
                            if ((fi.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden && !showHiddenFiles) continue;
                            
                            this.BeginInvoke(new Action(() => {
                                var item = new ListViewItem(new[] { fileName, "File", (fi.Length / 1024).ToString() + " KB", fi.LastWriteTime.ToString() });
                                item.Tag = f;
                                string ext = Path.GetExtension(f).ToLower();
                                if (string.IsNullOrEmpty(ext)) item.ImageKey = "file";
                                else {
                                    if (!sysIconsSmall.Images.ContainsKey(ext)) {
                                        Image img = IconExtractor.GetFileIcon(ext, true, false);
                                        if (img != null) sysIconsSmall.Images.Add(ext, img);
                                    }
                                    item.ImageKey = sysIconsSmall.Images.ContainsKey(ext) ? ext : "file";
                                }
                                listMiddle.Items.Add(item);
                            }));
                            searchMatchCount++;
                        }
                    }
                    else
                    {
                        if (f.EndsWith(".k3enc"))
                        {
                            string orig = Path.GetFileNameWithoutExtension(f);
                            if (orig.ToLower().Contains(query))
                            {
                                var fi = new FileInfo(f);
                                this.BeginInvoke(new Action(() => {
                                    var item = new ListViewItem(new[] { orig, "File Mã hóa", (fi.Length / 1024).ToString() + " KB", fi.LastWriteTime.ToString() });
                                    item.Tag = f;
                                    string ext = Path.GetExtension(orig).ToLower();
                                    if (string.IsNullOrEmpty(ext)) item.ImageKey = "file";
                                    else {
                                        if (!sysIconsSmall.Images.ContainsKey(ext)) {
                                            Image img = IconExtractor.GetFileIcon(ext, true, true);
                                            if (img != null) sysIconsSmall.Images.Add(ext, img);
                                        }
                                        item.ImageKey = sysIconsSmall.Images.ContainsKey(ext) ? ext : "file";
                                    }
                                    listRight.Items.Add(item);
                                }));
                                searchMatchCount++;
                            }
                        }
                    }
                }

                foreach (var d in Directory.GetDirectories(dir))
                {
                    if (token.IsCancellationRequested || searchMatchCount > 300) return;
                    string dirName = Path.GetFileName(d);
                    if (dirName.ToLower().Contains(query))
                    {
                        this.BeginInvoke(new Action(() => {
                            var di = new DirectoryInfo(d);
                            var item = new ListViewItem(new[] { dirName, "Thư mục", "", di.LastWriteTime.ToString() });
                            item.Tag = d;
                            item.ImageKey = "folder";
                            if (isLocal) listMiddle.Items.Add(item);
                            else listRight.Items.Add(item);
                        }));
                        searchMatchCount++;
                    }
                    SearchDirectoryRecursive(d, query, isLocal, token);
                }
            }
            catch { }
        }

        private void SecureDeleteFile(string path)
        {
            if (!File.Exists(path)) return;
            
            // DoD 5220.22-M style 3-pass overwrite
            FileInfo fi = new FileInfo(path);
            long length = fi.Length;
            int bufferSize = 4096;
            byte[] zeros = new byte[bufferSize];
            byte[] ones = new byte[bufferSize];
            for (int i = 0; i < bufferSize; i++) ones[i] = 0xFF;
            byte[] randoms = new byte[bufferSize];
            Random rnd = new Random();

            // Unlock file
            File.SetAttributes(path, FileAttributes.Normal);

            // Pass 1: 0x00
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Write)) {
                for (long i = 0; i < length; i += bufferSize) {
                    fs.Write(zeros, 0, (int)Math.Min(bufferSize, length - i));
                }
            }
            // Pass 2: 0xFF
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Write)) {
                for (long i = 0; i < length; i += bufferSize) {
                    fs.Write(ones, 0, (int)Math.Min(bufferSize, length - i));
                }
            }
            // Pass 3: Random
            rnd.NextBytes(randoms);
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Write)) {
                for (long i = 0; i < length; i += bufferSize) {
                    fs.Write(randoms, 0, (int)Math.Min(bufferSize, length - i));
                }
            }
            // Scramble filename and delete
            string directory = Path.GetDirectoryName(path);
            string scrambledPath = Path.Combine(directory, Guid.NewGuid().ToString() + ".tmp");
            File.Move(path, scrambledPath);
            File.Delete(scrambledPath);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_DEVICECHANGE = 0x0219;
            const int DBT_DEVICEARRIVAL = 0x8000;
            if (m.Msg == WM_DEVICECHANGE && m.WParam.ToInt32() == DBT_DEVICEARRIVAL)
            {
                if ((DateTime.Now - lastDevicePrompt).TotalSeconds > 8)
                {
                    lastDevicePrompt = DateTime.Now;
                    BeginInvoke(new Action(() => PromptScanNewUsb()));
                }
            }
            base.WndProc(ref m);
        }

        private void PromptScanNewUsb()
        {
            if (MessageBox.Show("Windows vừa báo có thiết bị lưu trữ/USB mới được cắm.\nBạn muốn mở Cứu hộ USB để quét và kiểm tra không?", "Auto-scan USB", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                new UsbRescueForm().ShowDialog(this);
            }
        }
    }
}
