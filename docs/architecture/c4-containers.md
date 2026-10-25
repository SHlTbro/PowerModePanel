# 可运行边界与技术职责

![一个桌面程序与按需脚本子进程](../../assets/diagrams/c4-containers.png)

这里的“容器”采用 C4 定义，表示真正可运行的应用或工具进程，不是 Docker。项目内有一个 C# 桌面 EXE，以及按按钮启动的 Windows PowerShell 子进程。`FanController`、监测和后台工具属于同一 EXE 内的代码职责，不能视为独立服务。

| 可运行单元或外部系统 | 技术 | 具体职责 |
|---|---|---|
| PowerModePanel.exe | C#、.NET Framework、WinForms | 操作界面、风扇 native I/O、WMI 采样、进程处理、结果与错误反馈。 |
| PowerShell 子进程 | Windows PowerShell、两个 `.ps1` | 运行图书馆、游戏、工作、满血性能等档位脚本。 |
| Windows | WMI、Win32、`powercfg` | 提供本地系统数据、内存和进程操作、电源策略设置。 |
| OEM Control Center | `InsydeDCHU.dll`、Winapi 委托 | 提供 OEM 风扇配置、固件包与风扇命令接口；由厂商安装。 |
| NVIDIA 驱动工具 | `nvidia-smi` | GPU 温度、利用率、P-State 查询；功耗请求是否生效取决于驱动。 |

| 调用方 → 目标 | 实际动作 | 协议或技术 | 代码依据 |
|---|---|---|---|
| 使用者 → EXE | 选择操作 | WinForms Click 事件 | [MainForm](../../PowerModePanel.cs#L116) |
| EXE → PowerShell | 执行随包模式脚本，收集 stdout/stderr | `Process.Start`、`powershell.exe -File` | [RunPowerShell](../../PowerModePanel.cs#L913) |
| EXE → OEM | 风扇状态读取、曲线与模式写入 | native DLL、Winapi delegates | [FanController 构造与 I/O](../../PowerModePanel.cs#L1072) |
| EXE → Windows | 温度/频率采样及进程操作 | WMI、Win32；关闭模式也直接使用 `powercfg` | [采样](../../PowerModePanel.cs#L934)、[电源恢复](../../PowerModePanel.cs#L369) |
| EXE → NVIDIA | 读取 GPU 温度、使用率和 P-State | `nvidia-smi --query-gpu` | [GetGpuInfo](../../PowerModePanel.cs#L1011) |
| PowerShell → Windows | 设置 CPU 电源参数并激活方案 | `powercfg /setacvalueindex`、`/setdcvalueindex`、`/setactive` | [LibraryMode.ps1](../../LibraryMode.ps1)、[PerformanceCoolMode.ps1](../../PerformanceCoolMode.ps1) |
| PowerShell → NVIDIA | 请求 GPU 功耗限制 | `nvidia-smi -pl` | [LibraryMode.ps1](../../LibraryMode.ps1) |

`BackgroundWhitelist.txt` 是 EXE 读写的本地文本配置，决定清后台时保护哪些进程；它不是数据库或额外运行进程。两个脚本与 EXE 同目录。OEM DLL 不属于源码分发包，程序从厂商的已安装位置加载。

没有引入 Component、Dynamic 或 Deployment 图：这些层级对当前单机工具不会增加首次理解所需的信息。完整风扇流程另行展示其拒绝、回读和恢复分支。

[Archify JSON](../../assets/diagrams/c4-containers.json) · [本地交互 HTML](../../assets/diagrams/c4-containers.html) · [主题自适应 SVG](../../assets/diagrams/c4-containers.svg) · [C4 Mermaid 源](../../assets/diagrams/c4-containers.mmd)
