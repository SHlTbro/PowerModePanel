# 架构与可编辑图

PowerModePanel 是本地 Windows 桌面工具。首页已经展开核心说明；这里保存图的可编辑来源和逐条关系说明，方便维护者继续核对实现。

| 读者问题 | 静态图 | 可选本地交互图 | 可编辑来源 | 说明 |
|---|---|---|---|---|
| 软件连接谁？ | [系统上下文 PNG](../../assets/diagrams/c4-context.png) · [SVG](../../assets/diagrams/c4-context.svg) | [下载 HTML](../../assets/diagrams/c4-context.html) | [Archify JSON](../../assets/diagrams/c4-context.json) · [C4 Mermaid](../../assets/diagrams/c4-context.mmd) | [系统上下文](c4-context.md) |
| 哪些单元真正运行？ | [运行结构 PNG](../../assets/diagrams/c4-containers.png) · [SVG](../../assets/diagrams/c4-containers.svg) | [下载 HTML](../../assets/diagrams/c4-containers.html) | [Archify JSON](../../assets/diagrams/c4-containers.json) · [C4 Mermaid](../../assets/diagrams/c4-containers.mmd) | [容器与技术职责](c4-containers.md) |
| 何时拒绝降低风扇？ | [写入前检查 PNG](../../assets/diagrams/fan-preflight.png) · [SVG](../../assets/diagrams/fan-preflight.svg) | [下载 HTML](../../assets/diagrams/fan-preflight.html) | [Archify JSON](../../assets/diagrams/fan-preflight.json) | [完整风扇流程](fan-workflow.md) |
| 写入失败如何恢复？ | [写入与恢复 PNG](../../assets/diagrams/fan-apply-recovery.png) · [SVG](../../assets/diagrams/fan-apply-recovery.svg) | [下载 HTML](../../assets/diagrams/fan-apply-recovery.html) | [Archify JSON](../../assets/diagrams/fan-apply-recovery.json) | [完整风扇流程](fan-workflow.md) |

HTML 是自包含文件，下载后用现代桌面浏览器打开。GitHub 文件预览不会执行其中的交互；本项目没有发布在线交互站点。浅色 PNG 适合直接阅读，SVG 随宿主明暗主题显示。两份 Mermaid 源分别使用真正的 `C4Context` 与 `C4Container` 语法，便于继续编辑 C4 语义；本页采用 Archify 的清晰排版作为阅读预览。

图中的箭头均从调用方指向被调用方，表示实际操作，不表示独立服务数量。风扇控制类属于桌面 EXE 内部；白名单属于文本配置文件。图中没有云端、数据库或网络 API。

Archify 生成的交互查看器和静态图使用其 MIT 代码及 JetBrains Mono 字体；第三方许可见项目根目录的 [THIRD_PARTY_NOTICES.md](../../THIRD_PARTY_NOTICES.md)。
