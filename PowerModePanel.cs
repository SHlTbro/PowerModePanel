using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;
using System.Windows.Forms;

namespace PowerModePanel
{
    public class MainForm : Form
    {
        private TextBox output;
        private Button libraryButton;
        private Button gameButton;
        private Button workButton;
        private Button maxButton;
        private Button fanButton;
        private Button fanQuietButton;
        private Button fanAutomaticButton;
        private Label fanModeValue;
        private FanController fanController;
        private Button statusButton;
        private Button cleanButton;
        private Button offButton;
        private Label currentModeValue;
        private Label cpuTempValue;
        private Label gpuTempValue;
        private Label cpuClockValue;
        private Label gpuUtilValue;
        private MetricGraph graph;
        private System.Windows.Forms.Timer refreshTimer;
        private bool refreshBusy;
        private string baseDir;

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        public MainForm()
        {
            baseDir = AppDomain.CurrentDomain.BaseDirectory;
            Text = "屑通Power Mode Panel";
            Width = 860;
            Height = 850;
            MinimumSize = new Size(850, 830);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 10F);
            BackColor = Color.FromArgb(242, 245, 249);
            string iconPath = Path.Combine(baseDir, "PowerModePanel.ico");
            if (File.Exists(iconPath)) Icon = new Icon(iconPath);

            var title = new Label();
            title.Text = "屑通Power Mode Panel";
            title.Font = new Font("Microsoft YaHei UI", 22F, FontStyle.Bold);
            title.AutoSize = true;
            title.Left = 24;
            title.Top = 18;
            title.ForeColor = Color.FromArgb(25, 31, 42);

            var subtitle = new Label();
            subtitle.Text = "HASEE CV15S · i7-12650H + RTX 4060 · 安静、游戏、工作一键切换";
            subtitle.AutoSize = true;
            subtitle.Left = 27;
            subtitle.Top = 66;
            subtitle.ForeColor = Color.FromArgb(82, 92, 108);

            libraryButton = MakeButton("图书馆静音", 26, 106, Color.FromArgb(34, 150, 128));
            gameButton = MakeButton("游戏降温", 292, 106, Color.FromArgb(55, 118, 224));
            workButton = MakeButton("工作降温", 558, 106, Color.FromArgb(112, 86, 205));
            maxButton = MakeButton("满血性能", 26, 166, Color.FromArgb(215, 86, 68));
            fanButton = MakeButton("风扇面板", 292, 166, Color.FromArgb(72, 83, 98));
            statusButton = MakeButton("释放内存", 558, 166, Color.FromArgb(72, 83, 98));

            cleanButton = MakeButton("清后台", 26, 226, Color.FromArgb(116, 91, 70));
            offButton = MakeButton("关闭模式", 292, 226, Color.FromArgb(96, 105, 120));
            currentModeValue = MakeModeCard("当前模式", "检测中", 558, 226, Color.FromArgb(42, 135, 190));
            fanQuietButton = MakeButton("仅风扇静音", 26, 286, Color.FromArgb(34, 150, 128));
            fanQuietButton.Name = "fanQuiet";
            fanAutomaticButton = MakeButton("恢复自动风扇", 292, 286, Color.FromArgb(72, 83, 98));
            fanAutomaticButton.Name = "fanAutomatic";
            fanModeValue = MakeModeCard("风扇策略", "独立调节", 558, 286, Color.FromArgb(34, 150, 128));

            cpuTempValue = MakeMetricCard("CPU 温度", "-- °C", 26, 362, Color.FromArgb(230, 89, 72));
            gpuTempValue = MakeMetricCard("GPU 温度", "-- °C", 226, 362, Color.FromArgb(245, 150, 60));
            cpuClockValue = MakeMetricCard("CPU 频率", "-- GHz", 426, 362, Color.FromArgb(58, 125, 225));
            gpuUtilValue = MakeMetricCard("GPU 使用率", "-- %", 626, 362, Color.FromArgb(92, 170, 102));

            graph = new MetricGraph();
            graph.Left = 26;
            graph.Top = 468;
            graph.Width = 798;
            graph.Height = 200;
            graph.BackColor = Color.FromArgb(20, 24, 31);

            output = new TextBox();
            output.Left = 26;
            output.Top = 690;
            output.Width = 798;
            output.Height = 96;
            output.Multiline = true;
            output.ReadOnly = true;
            output.ScrollBars = ScrollBars.Vertical;
            output.Font = new Font("Consolas", 9F);
            output.BackColor = Color.FromArgb(255, 255, 255);
            output.ForeColor = Color.FromArgb(30, 35, 44);
            output.BorderStyle = BorderStyle.FixedSingle;

            libraryButton.Click += delegate { RunMode("图书馆静音", "LibraryMode.ps1", "on"); };
            gameButton.Click += delegate { RunMode("游戏降温", "PerformanceCoolMode.ps1", "game"); };
            workButton.Click += delegate { RunMode("工作降温", "PerformanceCoolMode.ps1", "work"); };
            maxButton.Click += delegate { RunMode("满血性能", "PerformanceCoolMode.ps1", "max"); };
            fanButton.Click += delegate { OpenFanPanel(); };
            fanQuietButton.Click += delegate { ApplyFanMode(true); };
            fanAutomaticButton.Click += delegate { ApplyFanMode(false); };
            statusButton.Click += delegate { TrimMemory(); };
            cleanButton.Click += delegate { CleanBackgroundProcesses(); };
            offButton.Click += delegate { DisableAllModes(); };

            Controls.Add(title);
            Controls.Add(subtitle);
            Controls.Add(libraryButton);
            Controls.Add(gameButton);
            Controls.Add(workButton);
            Controls.Add(maxButton);
            Controls.Add(fanButton);
            Controls.Add(fanQuietButton);
            Controls.Add(fanAutomaticButton);
            Controls.Add(statusButton);
            Controls.Add(cleanButton);
            Controls.Add(offButton);
            Controls.Add(graph);
            Controls.Add(output);

            refreshTimer = new System.Windows.Forms.Timer();
            refreshTimer.Interval = 1000;
            refreshTimer.Tick += delegate { RefreshLiveMetrics(false); };

            Shown += delegate
            {
                ReadFanModeStatus();
                RefreshLiveMetrics(true);
                refreshTimer.Start();
            };
        }

        private Button MakeButton(string text, int left, int top, Color color)
        {
            var button = new Button();
            button.Text = text;
            button.Left = left;
            button.Top = top;
            button.Width = 240;
            button.Height = 46;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            return button;
        }

        private Label MakeModeCard(string title, string value, int left, int top, Color accent)
        {
            var panel = new Panel();
            panel.Left = left;
            panel.Top = top;
            panel.Width = 240;
            panel.Height = 46;
            panel.BackColor = Color.White;
            panel.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(Color.FromArgb(226, 232, 240)))
                using (var brush = new SolidBrush(accent))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
                    e.Graphics.FillRectangle(brush, 0, 0, 5, panel.Height);
                }
            };

            var titleLabel = new Label();
            titleLabel.Text = title;
            titleLabel.Left = 16;
            titleLabel.Top = 4;
            titleLabel.Width = 88;
            titleLabel.Height = 18;
            titleLabel.ForeColor = Color.FromArgb(92, 102, 118);
            titleLabel.Font = new Font("Microsoft YaHei UI", 8.5F);

            var valueLabel = new Label();
            valueLabel.Text = value;
            valueLabel.Left = 16;
            valueLabel.Top = 20;
            valueLabel.Width = 205;
            valueLabel.Height = 24;
            valueLabel.ForeColor = Color.FromArgb(25, 31, 42);
            valueLabel.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold);

            panel.Controls.Add(titleLabel);
            panel.Controls.Add(valueLabel);
            Controls.Add(panel);
            return valueLabel;
        }
        private Label MakeMetricCard(string title, string value, int left, int top, Color accent)
        {
            var panel = new Panel();
            panel.Left = left;
            panel.Top = top;
            panel.Width = 178;
            panel.Height = 78;
            panel.BackColor = Color.White;
            panel.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(Color.FromArgb(226, 232, 240)))
                using (var brush = new SolidBrush(accent))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
                    e.Graphics.FillRectangle(brush, 0, 0, 5, panel.Height);
                }
            };

            var titleLabel = new Label();
            titleLabel.Text = title;
            titleLabel.Left = 16;
            titleLabel.Top = 10;
            titleLabel.Width = 140;
            titleLabel.Height = 20;
            titleLabel.ForeColor = Color.FromArgb(92, 102, 118);
            titleLabel.Font = new Font("Microsoft YaHei UI", 9F);

            var valueLabel = new Label();
            valueLabel.Text = value;
            valueLabel.Left = 16;
            valueLabel.Top = 34;
            valueLabel.Width = 150;
            valueLabel.Height = 34;
            valueLabel.ForeColor = Color.FromArgb(25, 31, 42);
            valueLabel.Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold);

            panel.Controls.Add(titleLabel);
            panel.Controls.Add(valueLabel);
            Controls.Add(panel);
            return valueLabel;
        }

        private void RunMode(string title, string scriptName, string arg)
        {
            SetButtons(false);
            try
            {
                string script = Path.Combine(baseDir, scriptName);
                if (!File.Exists(script))
                {
                    MessageBox.Show("找不到脚本: " + script, "屑通Power Mode Panel", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                output.Text = "正在应用 " + title + "..." + Environment.NewLine + Environment.NewLine;
                output.AppendText(RunPowerShell(script, arg));
                RefreshLiveMetrics(true);
            }
            finally
            {
                SetButtons(true);
            }
        }

        private void RefreshLiveMetrics(bool writeLog)
        {
            if (refreshBusy) return;
            refreshBusy = true;

            ThreadPool.QueueUserWorkItem(delegate
            {
                MetricsSnapshot snapshot = CollectMetricsSnapshot();
                try
                {
                    if (!IsDisposed && IsHandleCreated)
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            ApplyMetricsSnapshot(snapshot, writeLog);
                            refreshBusy = false;
                        });
                    }
                    else
                    {
                        refreshBusy = false;
                    }
                }
                catch
                {
                    refreshBusy = false;
                }
            });
        }

        private MetricsSnapshot CollectMetricsSnapshot()
        {
            MetricsSnapshot snapshot = new MetricsSnapshot();
            snapshot.CpuTemp = GetCpuTemperature();
            snapshot.CpuClock = GetCpuClock();
            snapshot.Gpu = GetGpuInfo();
            snapshot.ModeName = DetectCurrentMode();
            snapshot.CapturedAt = DateTime.Now;
            return snapshot;
        }

        private void ApplyMetricsSnapshot(MetricsSnapshot snapshot, bool writeLog)
        {
            cpuTempValue.Text = FormatValue(snapshot.CpuTemp, "0.0", "°C");
            cpuClockValue.Text = FormatValue(snapshot.CpuClock, "0.00", "GHz");
            gpuTempValue.Text = FormatValue(snapshot.Gpu.Temp, "0", "°C");
            gpuUtilValue.Text = FormatValue(snapshot.Gpu.Util, "0", "%");
            graph.Add(snapshot.CpuTemp, snapshot.Gpu.Temp, snapshot.CpuClock, snapshot.Gpu.Util);
            currentModeValue.Text = snapshot.ModeName;

            if (writeLog)
            {
                var builder = new StringBuilder();
                builder.AppendLine("当前状态  " + snapshot.CapturedAt.ToString("HH:mm:ss"));
                builder.AppendLine("CPU 温度: " + cpuTempValue.Text);
                builder.AppendLine("GPU 温度: " + gpuTempValue.Text);
                builder.AppendLine("CPU 频率: " + cpuClockValue.Text);
                builder.AppendLine("GPU 使用率: " + gpuUtilValue.Text + "  " + snapshot.Gpu.PState);
                builder.AppendLine("当前模式: " + snapshot.ModeName);
                builder.AppendLine("提示: 现在后台采样，每 1 秒刷新；nvidia-smi 偶发慢时不会卡住界面。");
                output.Text = builder.ToString();
            }
        }

        private string FormatValue(double value, string format, string unit)
        {
            if (double.IsNaN(value)) return "-- " + unit;
            return value.ToString(format, CultureInfo.InvariantCulture) + " " + unit;
        }

        [DllImport("psapi.dll")]
        private static extern bool EmptyWorkingSet(IntPtr processHandle);

        private const string BalancedScheme = "381b4222-f694-41f0-9685-ff5bb260df2e";
        private const string SubProcessor = "54533251-82be-4824-96c1-47b60b740d00";
        private const string SubPciExpress = "501a4d13-42af-4429-9fd1-a8218c268e20";
        private const string SubWireless = "19cbb8fa-5279-450e-9fac-8a3d5fedd0c1";
        private const string ProcThrottleMin = "893dee8e-2bef-41e0-89c6-b55d0929964c";
        private const string ProcThrottleMax = "bc5038f7-23e0-4960-96da-33abaf5935ec";
        private const string ProcFreqMax = "75b0ae3f-bce0-45a7-8c89-c9611c25e100";
        private const string ProcFreqMax1 = "75b0ae3f-bce0-45a7-8c89-c9611c25e101";
        private const string PerfBoostMode = "be337238-0d82-4146-a960-4f3749d470c7";
        private const string PerfBoostPol = "45bcc044-d885-43e2-8605-ee0ec6e96b59";
        private const string PerfEpp = "36687f9e-e3a5-4dbf-b1dc-15eb381c6863";
        private const string PerfEpp1 = "36687f9e-e3a5-4dbf-b1dc-15eb381c6864";
        private const string SysCoolPol = "94d3a615-a899-4ac5-ae2b-e4d8f634367f";
        private const string PerfIncThreshold = "06cadf0e-64ed-448a-8927-ce7bf90eb35d";
        private const string PerfDecThreshold = "12a0ab44-fe28-4fa9-b3bd-4b64f44960a6";
        private const string LatencyHintPerf = "619b7505-003b-4e82-b7a6-4dd29c300971";
        private const string HeteroClass0Floor = "fddc842b-8364-4edc-94cf-c17f60de1c80";
        private const string Aspm = "ee12f906-d277-404b-b6da-e5fa1a576df5";
        private const string WirelessPower = "12bbebe6-58d6-4636-95bb-3217ef867c1a";

        private void DisableAllModes()
        {
            SetButtons(false);
            try
            {
                SetPower(SubProcessor, ProcThrottleMin, "5");
                SetPower(SubProcessor, ProcThrottleMax, "100");
                SetPower(SubProcessor, ProcFreqMax, "0");
                SetPower(SubProcessor, ProcFreqMax1, "0");
                SetPower(SubProcessor, PerfBoostMode, "2");
                SetPower(SubProcessor, PerfBoostPol, "60");
                SetPower(SubProcessor, PerfEpp, "33");
                SetPower(SubProcessor, PerfEpp1, "33");
                SetPower(SubProcessor, SysCoolPol, "1");
                SetPower(SubProcessor, PerfIncThreshold, "60");
                SetPower(SubProcessor, PerfDecThreshold, "20");
                SetPower(SubProcessor, LatencyHintPerf, "99");
                SetPower(SubProcessor, HeteroClass0Floor, "100");
                SetPower(SubPciExpress, Aspm, "2");
                SetPower(SubWireless, WirelessPower, "0");
                RunCommand("powercfg", "/setactive " + BalancedScheme);
                output.Text = "已关闭所有自定义模式，恢复普通均衡策略。" + Environment.NewLine +
                              "说明: 这会取消图书馆/游戏/工作/满血档的频率限制和偏置。" + Environment.NewLine;
                RefreshLiveMetrics(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "关闭模式", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetButtons(true);
            }
        }

        private void SetPower(string subGuid, string settingGuid, string value)
        {
            RunCommand("powercfg", "/setacvalueindex " + BalancedScheme + " " + subGuid + " " + settingGuid + " " + value);
            RunCommand("powercfg", "/setdcvalueindex " + BalancedScheme + " " + subGuid + " " + settingGuid + " " + value);
        }

        private string DetectCurrentMode()
        {
            int maxP = GetPowerValue(SubProcessor, ProcFreqMax);
            int maxE = GetPowerValue(SubProcessor, ProcFreqMax1);
            int boost = GetPowerValue(SubProcessor, PerfBoostMode);
            int maxState = GetPowerValue(SubProcessor, ProcThrottleMax);
            int epp = GetPowerValue(SubProcessor, PerfEpp);

            if (boost == 0 || maxState <= 63) return "图书馆静音";
            if (maxP == 3600 && maxE == 3200 && boost == 6) return "游戏降温";
            if (maxP == 4000 && maxE == 3500 && boost == 4) return "工作降温";
            if (maxP == 0 && maxE == 0 && boost == 2 && epp <= 15) return "满血性能";
            if (maxP == 0 && maxE == 0 && boost == 2 && maxState == 100) return "普通均衡";
            return "自定义/未知";
        }

        private int GetPowerValue(string subGuid, string settingGuid)
        {
            try
            {
                string keyPath = "SYSTEM\\CurrentControlSet\\Control\\Power\\User\\PowerSchemes\\" + BalancedScheme + "\\" + subGuid + "\\" + settingGuid;
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath))
                {
                    if (key == null) return -1;
                    object value = key.GetValue("ACSettingIndex");
                    if (value == null) return -1;
                    return Convert.ToInt32(value);
                }
            }
            catch { }
            return -1;
        }
        private void TrimMemory()
        {
            SetButtons(false);
            long before = GetAvailableMemoryBytes();
            int trimmed = 0;
            int failed = 0;

            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    if (process.Id == 0) continue;
                    if (process.HasExited) continue;
                    if (EmptyWorkingSet(process.Handle)) trimmed++;
                    else failed++;
                }
                catch
                {
                    failed++;
                }
                finally
                {
                    try { process.Dispose(); } catch { }
                }
            }

            long after = GetAvailableMemoryBytes();
            double freedMb = double.NaN;
            if (before >= 0 && after >= 0) freedMb = Math.Max(0, (after - before) / 1024.0 / 1024.0);

            var builder = new StringBuilder();
            builder.AppendLine("内存清理完成");
            builder.AppendLine("已整理进程: " + trimmed);
            builder.AppendLine("无法访问/跳过: " + failed);
            if (!double.IsNaN(freedMb)) builder.AppendLine("可用物理内存增加: " + freedMb.ToString("0", CultureInfo.InvariantCulture) + " MB");
            builder.AppendLine("说明: 不会结束程序，只是让 Windows 回收暂时不用的工作集内存。");
            builder.AppendLine("提示: 内存清理不能直接降 90°C，高温主要由 CPU/GPU 负载和风扇策略决定。");
            output.Text = builder.ToString();
            RefreshLiveMetrics(false);
            SetButtons(true);
        }

        private long GetAvailableMemoryBytes()
        {
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher("SELECT FreePhysicalMemory FROM Win32_OperatingSystem"))
                {
                    foreach (System.Management.ManagementObject item in searcher.Get())
                    {
                        return Convert.ToInt64(item["FreePhysicalMemory"]) * 1024L;
                    }
                }
            }
            catch { }
            return -1;
        }
        private void CleanBackgroundProcesses()
        {
            EnsureBackgroundWhitelistFile();
            HashSet<string> whitelist = LoadWhitelistProcessNames();
            HashSet<string> protectedNames = GetProtectedProcessNames();

            List<ProcessCandidate> candidates = new List<ProcessCandidate>();
            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    string name = process.ProcessName;
                    if (process.Id == 0 || process.Id == 4) { process.Dispose(); continue; }
                    if (process.Id == Process.GetCurrentProcess().Id) { process.Dispose(); continue; }
                    if (whitelist.Contains(name)) { process.Dispose(); continue; }
                    if (protectedNames.Contains(name)) { process.Dispose(); continue; }
                    if (IsWindowsSystemProcess(process)) { process.Dispose(); continue; }

                    candidates.Add(new ProcessCandidate
                    {
                        Process = process,
                        Name = name,
                        Id = process.Id,
                        MemoryMb = process.WorkingSet64 / 1024.0 / 1024.0,
                        Path = SafeGetProcessPath(process)
                    });
                }
                catch
                {
                    try { process.Dispose(); } catch { }
                }
            }

            if (candidates.Count == 0)
            {
                output.Text = "没有发现可清理的非白名单后台进程。" + Environment.NewLine +
                              "只需要维护 BackgroundWhitelist.txt；不在白名单且不是系统关键进程的普通进程都会作为候选。" + Environment.NewLine;
                return;
            }

            List<ProcessCandidate> selected = ShowBackgroundCleanupDialog(candidates);
            if (selected == null || selected.Count == 0)
            {
                foreach (ProcessCandidate candidate in candidates) candidate.Dispose();
                output.Text = "已取消清后台，未结束任何进程。" + Environment.NewLine;
                return;
            }

            int closed = 0;
            int failed = 0;
            foreach (ProcessCandidate candidate in candidates)
            {
                try
                {
                    if (!selected.Contains(candidate)) continue;
                    Process process = candidate.Process;
                    if (!process.HasExited)
                    {
                        process.CloseMainWindow();
                        if (!process.WaitForExit(1200)) process.Kill();
                        closed++;
                    }
                }
                catch
                {
                    failed++;
                }
                finally
                {
                    candidate.Dispose();
                }
            }

            output.Text = "清后台完成" + Environment.NewLine +
                          "已结束: " + closed + Environment.NewLine +
                          "失败/无权限: " + failed + Environment.NewLine +
                          "白名单文件: BackgroundWhitelist.txt" + Environment.NewLine;
            RefreshLiveMetrics(false);
        }

        private List<ProcessCandidate> ShowBackgroundCleanupDialog(List<ProcessCandidate> candidates)
        {
            using (Form dialog = new Form())
            using (ListView list = new ListView())
            using (ImageList icons = new ImageList())
            using (Button okButton = new Button())
            using (Button cancelButton = new Button())
            using (Button selectAllButton = new Button())
            using (Button selectNoneButton = new Button())
            using (Button addWhitelistButton = new Button())
            using (Button openListButton = new Button())
            using (Label hint = new Label())
            {
                dialog.Text = "清后台 - 选择要结束的进程";
                dialog.Width = 980;
                dialog.Height = 560;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Font = new Font("Microsoft YaHei UI", 10F);
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                if (Icon != null) dialog.Icon = Icon;

                hint.Text = "带图标显示进程来源；勾选后可清理，也可以直接加入白名单并从本次清理中移除。";
                hint.Left = 14;
                hint.Top = 12;
                hint.Width = 940;
                hint.Height = 34;

                icons.ImageSize = new Size(18, 18);
                icons.ColorDepth = ColorDepth.Depth32Bit;
                list.Left = 14;
                list.Top = 52;
                list.Width = 940;
                list.Height = 380;
                list.View = View.Details;
                list.CheckBoxes = true;
                list.FullRowSelect = true;
                list.GridLines = true;
                list.MultiSelect = true;
                list.SmallImageList = icons;
                list.HideSelection = false;
                list.Columns.Add("进程", 230);
                list.Columns.Add("PID", 75);
                list.Columns.Add("内存", 90);
                list.Columns.Add("路径", 520);

                candidates.Sort(delegate(ProcessCandidate a, ProcessCandidate b)
                {
                    int result = b.MemoryMb.CompareTo(a.MemoryMb);
                    if (result != 0) return result;
                    return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                });

                foreach (ProcessCandidate candidate in candidates)
                {
                    int imageIndex = AddProcessIcon(icons, candidate.Path);
                    ListViewItem item = new ListViewItem(candidate.Name, imageIndex);
                    item.SubItems.Add(candidate.Id.ToString(CultureInfo.InvariantCulture));
                    item.SubItems.Add(candidate.MemoryMb.ToString("0", CultureInfo.InvariantCulture) + " MB");
                    item.SubItems.Add(string.IsNullOrEmpty(candidate.Path) ? "路径不可访问" : candidate.Path);
                    item.Tag = candidate;
                    item.Checked = true;
                    list.Items.Add(item);
                }

                selectAllButton.Text = "全选";
                selectAllButton.Left = 14;
                selectAllButton.Top = 446;
                selectAllButton.Width = 86;
                selectAllButton.Click += delegate
                {
                    foreach (ListViewItem item in list.Items) item.Checked = true;
                };

                selectNoneButton.Text = "全不选";
                selectNoneButton.Left = 108;
                selectNoneButton.Top = 446;
                selectNoneButton.Width = 86;
                selectNoneButton.Click += delegate
                {
                    foreach (ListViewItem item in list.Items) item.Checked = false;
                };

                addWhitelistButton.Text = "选中加入白名单";
                addWhitelistButton.Left = 202;
                addWhitelistButton.Top = 446;
                addWhitelistButton.Width = 136;
                addWhitelistButton.Click += delegate
                {
                    List<ProcessCandidate> toWhitelist = new List<ProcessCandidate>();
                    foreach (ListViewItem item in list.SelectedItems)
                    {
                        ProcessCandidate candidate = item.Tag as ProcessCandidate;
                        if (candidate != null) toWhitelist.Add(candidate);
                    }
                    if (toWhitelist.Count == 0)
                    {
                        MessageBox.Show("请先点击选中要保留的进程行。", "加入白名单", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    AddProcessesToWhitelist(toWhitelist);
                    foreach (ListViewItem item in list.SelectedItems) item.Remove();
                };

                openListButton.Text = "编辑白名单";
                openListButton.Left = 348;
                openListButton.Top = 446;
                openListButton.Width = 112;
                openListButton.Click += delegate { OpenListFile("BackgroundWhitelist.txt"); };

                okButton.Text = "清理勾选项";
                okButton.Left = 750;
                okButton.Top = 446;
                okButton.Width = 100;
                okButton.DialogResult = DialogResult.OK;

                cancelButton.Text = "取消";
                cancelButton.Left = 858;
                cancelButton.Top = 446;
                cancelButton.Width = 96;
                cancelButton.DialogResult = DialogResult.Cancel;

                dialog.Controls.Add(hint);
                dialog.Controls.Add(list);
                dialog.Controls.Add(selectAllButton);
                dialog.Controls.Add(selectNoneButton);
                dialog.Controls.Add(addWhitelistButton);
                dialog.Controls.Add(openListButton);
                dialog.Controls.Add(okButton);
                dialog.Controls.Add(cancelButton);
                dialog.AcceptButton = okButton;
                dialog.CancelButton = cancelButton;

                if (dialog.ShowDialog(this) != DialogResult.OK) return null;

                List<ProcessCandidate> selected = new List<ProcessCandidate>();
                foreach (ListViewItem item in list.Items)
                {
                    if (!item.Checked) continue;
                    ProcessCandidate candidate = item.Tag as ProcessCandidate;
                    if (candidate != null) selected.Add(candidate);
                }
                return selected;
            }
        }

        private int AddProcessIcon(ImageList icons, string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    using (Icon icon = Icon.ExtractAssociatedIcon(path))
                    {
                        if (icon != null)
                        {
                            icons.Images.Add(new Bitmap(icon.ToBitmap()));
                            return icons.Images.Count - 1;
                        }
                    }
                }
            }
            catch { }
            icons.Images.Add(SystemIcons.Application.ToBitmap());
            return icons.Images.Count - 1;
        }

        private void AddProcessesToWhitelist(List<ProcessCandidate> candidates)
        {
            EnsureBackgroundWhitelistFile();
            string file = Path.Combine(baseDir, "BackgroundWhitelist.txt");
            HashSet<string> existing = LoadWhitelistProcessNames();
            using (StreamWriter writer = new StreamWriter(file, true, Encoding.UTF8))
            {
                foreach (ProcessCandidate candidate in candidates)
                {
                    if (existing.Contains(candidate.Name)) continue;
                    writer.WriteLine(candidate.Name);
                    existing.Add(candidate.Name);
                }
            }
            output.Text = "已加入白名单: " + candidates.Count + " 个进程。" + Environment.NewLine +
                          "本次清理列表已移除这些进程。" + Environment.NewLine;
        }

        private void EnsureBackgroundWhitelistFile()
        {
            EnsureWhitelistFile();
        }

        private void EnsureWhitelistFile()
        {
            string path = Path.Combine(baseDir, "BackgroundWhitelist.txt");
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# 屑通Power Mode Panel 清后台白名单");
            builder.AppendLine("# 规则: 每行一个进程名，不需要 .exe；# 开头为注释");
            builder.AppendLine("# 清后台逻辑: 不在白名单、且不是系统关键进程的普通用户进程，都会出现在候选窗口。");
            builder.AppendLine("# 想保护某个软件，就把任务管理器里的进程名加到这里。");
            builder.AppendLine();
            foreach (string item in GetDefaultWhitelist()) builder.AppendLine(item);

            if (!File.Exists(path))
            {
                File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
                return;
            }

            string current = File.ReadAllText(path, Encoding.UTF8);
            if (current.IndexOf("清后台白名单", StringComparison.OrdinalIgnoreCase) < 0)
            {
                File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
            }
        }

        private HashSet<string> LoadWhitelistProcessNames()
        {
            EnsureWhitelistFile();
            HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rawLine in File.ReadAllLines(Path.Combine(baseDir, "BackgroundWhitelist.txt"), Encoding.UTF8))
            {
                string line = rawLine.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("#")) continue;
                if (line.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) line = line.Substring(0, line.Length - 4);
                set.Add(line);
            }
            return set;
        }

        private string[] GetDefaultWhitelist()
        {
            return new string[] {
                "PowerModePanel", "Codex", "Code", "explorer", "System", "Registry", "Idle", "SearchApp", "SearchHost",
                "League of Legends", "LeagueClient", "LeagueClientUx", "LeagueClientUxRender",
                "RiotClientServices", "WeGame", "ControlCenter30", "FanSpeedSetting",
                "NVDisplay.Container", "nvcontainer", "NVIDIA Share", "NVIDIA Web Helper",
                "Steam", "steamwebhelper", "EpicGamesLauncher", "Battle.net", "Agent",
                "Discord", "ToDesk", "ToDesk_Service", "AnyDesk", "SunloginClient",
                "Everything", "PowerToys", "OneDrive"
            };
        }

        private HashSet<string> GetProtectedProcessNames()
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                "System", "Idle", "Registry", "smss", "csrss", "wininit", "winlogon", "services",
                "lsass", "svchost", "fontdrvhost", "dwm", "WUDFHost", "WmiPrvSE", "spoolsv",
                "audiodg", "conhost", "sihost", "taskhostw", "SearchIndexer", "SearchHost", "SearchApp",
                "StartMenuExperienceHost", "ShellExperienceHost", "RuntimeBroker", "SecurityHealthService",
                "MsMpEng", "NisSrv", "dllhost", "ctfmon", "ApplicationFrameHost", "TextInputHost",
                "SystemSettings", "powershell", "cmd", "WindowsTerminal", "OpenConsole"
            };
        }

        private bool IsWindowsSystemProcess(Process process)
        {
            string path = SafeGetProcessPath(process);
            if (string.IsNullOrEmpty(path)) return false;
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\') + "\\";
            string system32 = Path.Combine(windows, "System32") + "\\";
            string syswow64 = Path.Combine(windows, "SysWOW64") + "\\";
            string systemApps = Path.Combine(windows, "SystemApps") + "\\";
            return path.StartsWith(system32, StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith(systemApps, StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith(syswow64, StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith(Path.Combine(windows, "WinSxS") + "\\", StringComparison.OrdinalIgnoreCase);
        }

        private string SafeGetProcessPath(Process process)
        {
            try
            {
                return process.MainModule.FileName;
            }
            catch { }
            return "";
        }

        private void OpenListFile(string fileName)
        {
            EnsureBackgroundWhitelistFile();
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(baseDir, fileName),
                UseShellExecute = true
            });
        }
        private void OpenFanPanel()
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = "clevofan:", UseShellExecute = true });
                output.AppendText(Environment.NewLine + "已打开风扇面板。建议游戏用 Auto/Performance，图书馆用 Silent。" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "风扇面板", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyFanMode(bool quiet)
        {
            SetButtons(false);
            try
            {
                if (fanController == null) fanController = new FanController();
                if (quiet) fanController.SetQuiet();
                else fanController.SetAutomatic();
                fanModeValue.Text = quiet ? "低噪曲线" : "自动风扇";
                output.Text = quiet ? "已启用独立低噪风扇曲线。低温稳定后逐步降速，升温时会自动加速。" : "已恢复厂商自动风扇策略和原有自定义曲线。";
                output.AppendText(Environment.NewLine + "未修改整机性能模式、CPU 电源策略或 GPU 设置。" + Environment.NewLine +
                    "低负载时约 25%；CPU 超过 75°C、GPU 超过 70°C 后自动加速，分别在 85°C / 80°C 达到全速。" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                output.Text = "风扇设置失败：" + ex.Message + Environment.NewLine;
                MessageBox.Show(ex.Message, "独立风扇控制", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { SetButtons(true); }
        }

        private void ReadFanModeStatus()
        {
            try
            {
                if (fanController == null) fanController = new FanController();
                byte mode = fanController.GetMode();
                fanModeValue.Text = mode == 6 && fanController.IsQuiet() ? "低噪曲线" : mode == 0 ? "自动风扇" : "其他预设";
            }
            catch { fanModeValue.Text = "驱动不可用"; }
        }

        private string RunPowerShell(string script, string arg)
        {
            var startInfo = new ProcessStartInfo();
            startInfo.FileName = "powershell.exe";
            startInfo.Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\" " + arg;
            startInfo.WorkingDirectory = baseDir;
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.StandardOutputEncoding = Encoding.UTF8;
            startInfo.StandardErrorEncoding = Encoding.UTF8;

            using (var process = Process.Start(startInfo))
            {
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return stdout + (string.IsNullOrWhiteSpace(stderr) ? "" : Environment.NewLine + stderr);
            }
        }

        private double GetCpuTemperature()
        {
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher("root\\wmi", "SELECT * FROM MSAcpi_ThermalZoneTemperature"))
                {
                    foreach (System.Management.ManagementObject item in searcher.Get())
                    {
                        double raw = Convert.ToDouble(item["CurrentTemperature"]);
                        return Math.Round(raw / 10.0 - 273.15, 1);
                    }
                }
            }
            catch { }
            return double.NaN;
        }

        private double GetCpuClock()
        {
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher("SELECT CurrentClockSpeed FROM Win32_Processor"))
                {
                    foreach (System.Management.ManagementObject item in searcher.Get())
                    {
                        double baseMhz = GetCpuBaseMhz();
                        double perfPercent = GetCpuPerformancePercent();
                        if (!double.IsNaN(baseMhz) && !double.IsNaN(perfPercent))
                        {
                            return Math.Round(baseMhz * perfPercent / 100.0 / 1000.0, 2);
                        }
                        return Math.Round(Convert.ToDouble(item["CurrentClockSpeed"]) / 1000.0, 2);
                    }
                }
            }
            catch { }
            return double.NaN;
        }

        private double GetCpuBaseMhz()
        {
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher("SELECT MaxClockSpeed FROM Win32_Processor"))
                {
                    foreach (System.Management.ManagementObject item in searcher.Get())
                    {
                        return Convert.ToDouble(item["MaxClockSpeed"]);
                    }
                }
            }
            catch { }
            return double.NaN;
        }

        private double GetCpuPerformancePercent()
        {
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher("SELECT Name, PercentProcessorPerformance FROM Win32_PerfFormattedData_Counters_ProcessorInformation"))
                {
                    double total = double.NaN;
                    double bestCore = double.NaN;
                    foreach (System.Management.ManagementObject item in searcher.Get())
                    {
                        string name = Convert.ToString(item["Name"]);
                        double value = Convert.ToDouble(item["PercentProcessorPerformance"]);
                        if (name == "_Total" || name == "0,_Total") total = value;
                        if (!name.EndsWith("_Total") && (double.IsNaN(bestCore) || value > bestCore)) bestCore = value;
                    }
                    if (!double.IsNaN(total)) return total;
                    return bestCore;
                }
            }
            catch { }
            return double.NaN;
        }
        private GpuInfo GetGpuInfo()
        {
            var info = new GpuInfo();
            try
            {
                string result = RunCommand("nvidia-smi", "--query-gpu=temperature.gpu,utilization.gpu,pstate --format=csv,noheader,nounits").Trim();
                string[] parts = result.Split(',');
                if (parts.Length >= 3)
                {
                    double temp;
                    double util;
                    if (double.TryParse(parts[0].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out temp)) info.Temp = temp;
                    if (double.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out util)) info.Util = util;
                    info.PState = parts[2].Trim();
                }
            }
            catch { }
            return info;
        }

        private string RunCommand(string fileName, string arguments)
        {
            var startInfo = new ProcessStartInfo();
            startInfo.FileName = fileName;
            startInfo.Arguments = arguments;
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            using (var process = Process.Start(startInfo))
            {
                string stdout = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return stdout;
            }
        }

        private void SetButtons(bool enabled)
        {
            libraryButton.Enabled = enabled;
            gameButton.Enabled = enabled;
            workButton.Enabled = enabled;
            maxButton.Enabled = enabled;
            fanButton.Enabled = enabled;
            fanQuietButton.Enabled = enabled;
            fanAutomaticButton.Enabled = enabled;
            statusButton.Enabled = enabled;
            cleanButton.Enabled = enabled;
            offButton.Enabled = enabled;
        }
    }

    // Uses the OEM custom-curve packet and fan command. Firmware regulates the fans;
    // no polling loop, raw EC access, or performance profile writes are required.
    public sealed class FanController
    {
        private readonly Func<int, byte[], int> writeCommand;
        private readonly Func<int, int, int, byte[]> readSettings;
        private readonly Action<int, int, byte> writeSetting;
        private readonly Func<int, byte[]> readPackage;

        public FanController()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "ControlCenter", "InsydeDCHU.dll");
            if (!File.Exists(path)) throw new InvalidOperationException("未找到已安装的 Control Center 风扇驱动。请先安装厂商 Control Center 3.0。");
            IntPtr module = LoadLibraryEx(path, IntPtr.Zero, 0x1100);
            if (module == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "无法加载厂商风扇驱动。");
            try
            {
                NativeCommand command = (NativeCommand)Marshal.GetDelegateForFunctionPointer(Export(module, "SetDCHU_Data"), typeof(NativeCommand));
                NativeSettings read = (NativeSettings)Marshal.GetDelegateForFunctionPointer(Export(module, "ReadAppSettings"), typeof(NativeSettings));
                NativeSettings write = (NativeSettings)Marshal.GetDelegateForFunctionPointer(Export(module, "WriteAppSettings"), typeof(NativeSettings));
                NativePackage package = (NativePackage)Marshal.GetDelegateForFunctionPointer(Export(module, "GetDCHU_Data_Buffer"), typeof(NativePackage));
                writeCommand = delegate(int id, byte[] bytes) { return command(id, bytes, bytes.Length); };
                readSettings = delegate(int page, int index, int length)
                {
                    byte[] bytes = new byte[length];
                    RequireSuccess(read(page, index, length, bytes), "读取厂商风扇状态");
                    return bytes;
                };
                writeSetting = delegate(int page, int index, byte value)
                {
                    RequireSuccess(write(page, index, 1, new byte[] { value }), "保存厂商风扇状态");
                };
                readPackage = delegate(int id)
                {
                    byte[] bytes = new byte[256];
                    RequireSuccess(package(id, bytes), "读取厂商风扇曲线");
                    return bytes;
                };
                // Kept loaded for the process lifetime, as required by the native delegates.
            }
            catch { FreeLibrary(module); throw; }
        }

        internal FanController(Func<int, byte[], int> command, Func<int, int, int, byte[]> read,
            Action<int, int, byte> write, Func<int, byte[]> package)
        {
            if (command == null || read == null || write == null || package == null) throw new ArgumentNullException("native I/O");
            writeCommand = command;
            readSettings = read;
            writeSetting = write;
            readPackage = package;
        }

        public byte GetMode() { return Read(4, 5, 1)[0]; }
        public bool IsQuiet() { return GetMode() == 6 && IsQuietCurve(Packet(13)); }
        public void SetQuiet() { Apply(true); }
        public void SetAutomatic() { Apply(false); }

        private void Apply(bool quiet)
        {
            byte original = GetMode();
            byte performance = Read(1, 1, 1)[0];
            if (original > 6) throw new InvalidOperationException("厂商返回了未知的风扇模式，未执行写入。");
            byte[] profile = Read(4, 0, 256), firmware = Packet(13);
            if (profile[6] != 2 || firmware[12] != 2)
                throw new InvalidOperationException("此风扇布局尚未验证，未执行写入。");
            byte[] savedCurve = SavedCurve(profile);
            byte[] originalCurve = original == 6 && IsQuietCurve(firmware) ? QuietCurve(firmware, savedCurve, firmware[19]) : savedCurve;
            byte[] desired = quiet ? QuietCurve(firmware, savedCurve, 64) : savedCurve;
            byte mode = quiet ? (byte)6 : (byte)0;
            if (quiet)
            {
                byte[] temperature = Packet(12);
                if (temperature[18] == 0 || temperature[18] >= 85 || temperature[21] >= 80)
                    throw new InvalidOperationException("温度过高或温度读取无效，未降低风扇转速。请先使用自动风扇降温。");
            }
            bool changed = false;
            try
            {
                // Set before invoking native I/O so partial native failures also restore the curve.
                changed = true;
                SetCurve(desired);
                SetHardwareMode(mode);
                writeSetting(4, 5, mode);
                if (GetMode() != mode) throw new InvalidOperationException("厂商风扇状态未确认更新。");
                if (!MatchesCurve(Packet(13), desired)) throw new InvalidOperationException("固件未确认应用风扇曲线。");
                if (Read(1, 1, 1)[0] != performance) throw new InvalidOperationException("检测到整机性能模式同时发生变化。");
            }
            catch (Exception error)
            {
                if (!changed) throw;
                try
                {
                    SetCurve(originalCurve); SetHardwareMode(original); writeSetting(4, 5, original);
                    if (GetMode() != original || !MatchesCurve(Packet(13), originalCurve))
                        throw new InvalidOperationException("原风扇曲线恢复回读不一致。");
                }
                catch (Exception restoreError)
                {
                    throw new InvalidOperationException("风扇设置失败，且原风扇模式未能恢复。请使用厂商风扇面板恢复自动模式。" +
                        Environment.NewLine + error.Message + Environment.NewLine + restoreError.Message, error);
                }
                throw new InvalidOperationException("风扇设置失败，已恢复原风扇模式。" + Environment.NewLine + error.Message, error);
            }
        }

        private byte[] Read(int page, int index, int length)
        {
            byte[] bytes = readSettings(page, index, length);
            if (bytes == null || bytes.Length != length) throw new InvalidOperationException("厂商设置数据长度无效。");
            return bytes;
        }
        private byte[] Packet(int id)
        {
            byte[] bytes = readPackage(id);
            if (bytes == null || bytes.Length != 256) throw new InvalidOperationException("厂商风扇数据长度无效。");
            return bytes;
        }
        private void SetCurve(byte[] curve) { RequireSuccess(writeCommand(14, curve), "应用风扇曲线"); }
        private static bool MatchesCurve(byte[] firmware, byte[] curve)
        {
            for (int fan = 0; fan < 2; fan++)
                for (int point = 0; point < 4; point++)
                    if (firmware[18 + fan * 8 + point] != curve[2 + fan * 4 + point]) return false;
            return true;
        }
        private static bool IsQuietCurve(byte[] firmware)
        {
            // Recognize the earlier 29% curve too, so a failed upgrade restores it unchanged.
            byte duty = firmware[19];
            return firmware[12] == 2 && (duty == 64 || duty == 74) && firmware[18] == 75 && firmware[20] == 85 && firmware[21] == 255 &&
                firmware[26] == 70 && firmware[27] == duty && firmware[28] == 80 && firmware[29] == 255;
        }
        private static byte[] SavedCurve(byte[] profile)
        {
            byte[] curve = new byte[256];
            // Read_FanInfo uses 18 bytes per fan. Preserve the OEM's cached slopes exactly,
            // including the unused third channel, instead of deriving them from hardware defaults.
            for (int fan = 0; fan < 3; fan++)
            {
                int s = 16 + fan * 18, d = 2 + fan * 4, r = 14 + fan * 6;
                if (profile[s + 1] > 100 || profile[s + 2] > 100 || profile[s + 7] > 100 || profile[s + 8] > 100)
                    throw new InvalidOperationException("厂商保存的风扇曲线无效，未执行写入。");
                curve[d] = profile[s + 7]; curve[d + 1] = (byte)Math.Round(profile[s + 1] / 100.0 * 255);
                curve[d + 2] = profile[s + 8]; curve[d + 3] = (byte)Math.Round(profile[s + 2] / 100.0 * 255);
                for (int segment = 0; segment < 3; segment++)
                {
                    curve[r + segment * 2] = profile[s + 13 + segment * 2];
                    curve[r + segment * 2 + 1] = profile[s + 12 + segment * 2];
                }
            }
            return curve;
        }
        private static byte[] QuietCurve(byte[] firmware, byte[] saved, byte targetDuty)
        {
            byte[] curve = (byte[])saved.Clone();
            for (int fan = 0; fan < 2; fan++)
            {
                int s = 16 + fan * 8, d = 2 + fan * 4, r = 14 + fan * 6;
                int start = firmware[s], minimum = firmware[s + 1], knee = fan == 0 ? 75 : 70;
                if (minimum > targetDuty || start >= knee)
                    throw new InvalidOperationException("此硬件的最低风量或温度范围不支持已验证的低噪曲线。");
                int[] temperatures = { start, knee, knee + 10, 100 };
                int[] duties = { (int)Math.Round(minimum * 100.0 / 255), (int)Math.Round(targetDuty * 100.0 / 255), 100, 100 };
                curve[d] = (byte)knee; curve[d + 1] = targetDuty; curve[d + 2] = (byte)(knee + 10); curve[d + 3] = 255;
                // The installed FnKey.Device.FAN.Load_Rxx scales percent/degree by 2.55*16.
                for (int segment = 0; segment < 3; segment++)
                {
                    int slope = (int)Math.Round((double)(duties[segment + 1] - duties[segment]) /
                        (temperatures[segment + 1] - temperatures[segment]) * 2.55 * 16);
                    curve[r + segment * 2] = (byte)(slope >> 8); curve[r + segment * 2 + 1] = (byte)slope;
                }
            }
            return curve;
        }

        private void SetHardwareMode(byte mode)
        {
            // SetWMI(121, 1, mode): mode in byte 0, subcommand in byte 3.
            RequireSuccess(writeCommand(121, new byte[] { mode, 0, 0, 1 }), "切换风扇策略");
        }
        private static void RequireSuccess(int result, string action)
        {
            if (result < 0) throw new InvalidOperationException(action + "失败，厂商返回 0x" + result.ToString("X8") + "。");
        }
        private static IntPtr Export(IntPtr module, string name)
        {
            IntPtr address = GetProcAddress(module, name);
            if (address == IntPtr.Zero) throw new InvalidOperationException("厂商风扇驱动缺少接口：" + name);
            return address;
        }
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int NativeCommand(int command, [In] byte[] data, int length);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int NativeSettings(int page, int index, int length, [In, Out] byte[] data);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int NativePackage(int command, [Out] byte[] data);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "LoadLibraryExW")]
        private static extern IntPtr LoadLibraryEx(string file, IntPtr reserved, uint flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);
        [DllImport("kernel32.dll")]
        private static extern bool FreeLibrary(IntPtr module);
    }

    public class ProcessCandidate : IDisposable
    {
        public Process Process;
        public string Name;
        public int Id;
        public double MemoryMb;
        public string Path;

        public override string ToString()
        {
            return Name + "  PID " + Id + "  " + MemoryMb.ToString("0", CultureInfo.InvariantCulture) + " MB" + (string.IsNullOrEmpty(Path) ? "" : "  " + Path);
        }

        public void Dispose()
        {
            try
            {
                if (Process != null) Process.Dispose();
            }
            catch { }
        }
    }
    public class MetricsSnapshot
    {
        public double CpuTemp = double.NaN;
        public double CpuClock = double.NaN;
        public GpuInfo Gpu = new GpuInfo();
        public DateTime CapturedAt = DateTime.MinValue;
        public string ModeName = "检测中";
    }
    public class GpuInfo
    {
        public double Temp = double.NaN;
        public double Util = double.NaN;
        public string PState = "";
    }

    public class MetricGraph : Panel
    {
        private readonly List<double> cpuTemps = new List<double>();
        private readonly List<double> gpuTemps = new List<double>();
        private readonly List<double> cpuClocks = new List<double>();
        private readonly List<double> gpuUtils = new List<double>();
        private const int MaxPoints = 80;

        public void Add(double cpuTemp, double gpuTemp, double cpuClock, double gpuUtil)
        {
            AddPoint(cpuTemps, cpuTemp);
            AddPoint(gpuTemps, gpuTemp);
            AddPoint(cpuClocks, cpuClock * 20.0);
            AddPoint(gpuUtils, gpuUtil);
            Invalidate();
        }

        private void AddPoint(List<double> list, double value)
        {
            if (!double.IsNaN(value)) list.Add(value);
            if (list.Count > MaxPoints) list.RemoveAt(0);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Rectangle area = new Rectangle(50, 24, Width - 74, Height - 58);

            using (var textBrush = new SolidBrush(Color.FromArgb(220, 230, 240)))
            using (var gridPen = new Pen(Color.FromArgb(50, 255, 255, 255)))
            using (var axisPen = new Pen(Color.FromArgb(90, 255, 255, 255)))
            {
                e.Graphics.DrawString("实时曲线  CPU温度 / GPU温度 / CPU频率x20 / GPU使用率", Font, textBrush, 14, 6);
                for (int i = 0; i <= 4; i++)
                {
                    int y = area.Top + i * area.Height / 4;
                    e.Graphics.DrawLine(gridPen, area.Left, y, area.Right, y);
                    e.Graphics.DrawString((100 - i * 25).ToString(), Font, textBrush, 12, y - 8);
                }
                e.Graphics.DrawRectangle(axisPen, area);
            }

            DrawSeries(e.Graphics, area, cpuTemps, Color.FromArgb(255, 95, 85));
            DrawSeries(e.Graphics, area, gpuTemps, Color.FromArgb(255, 176, 72));
            DrawSeries(e.Graphics, area, cpuClocks, Color.FromArgb(95, 150, 255));
            DrawSeries(e.Graphics, area, gpuUtils, Color.FromArgb(105, 210, 120));

            DrawLegend(e.Graphics);
        }

        private void DrawSeries(Graphics graphics, Rectangle area, List<double> values, Color color)
        {
            if (values.Count < 2) return;
            using (var pen = new Pen(color, 2F))
            {
                PointF[] points = new PointF[values.Count];
                for (int i = 0; i < values.Count; i++)
                {
                    double clamped = Math.Max(0, Math.Min(100, values[i]));
                    float x = area.Left + (float)i * area.Width / Math.Max(1, MaxPoints - 1);
                    float y = area.Bottom - (float)(clamped / 100.0 * area.Height);
                    points[i] = new PointF(x, y);
                }
                graphics.DrawLines(pen, points);
            }
        }

        private void DrawLegend(Graphics graphics)
        {
            DrawLegendItem(graphics, "CPU温度", 58, Height - 26, Color.FromArgb(255, 95, 85));
            DrawLegendItem(graphics, "GPU温度", 158, Height - 26, Color.FromArgb(255, 176, 72));
            DrawLegendItem(graphics, "CPU频率", 258, Height - 26, Color.FromArgb(95, 150, 255));
            DrawLegendItem(graphics, "GPU使用率", 358, Height - 26, Color.FromArgb(105, 210, 120));
        }

        private void DrawLegendItem(Graphics graphics, string text, int x, int y, Color color)
        {
            using (var brush = new SolidBrush(color))
            using (var textBrush = new SolidBrush(Color.FromArgb(220, 230, 240)))
            {
                graphics.FillRectangle(brush, x, y + 4, 22, 4);
                graphics.DrawString(text, Font, textBrush, x + 28, y - 4);
            }
        }
    }
}

















