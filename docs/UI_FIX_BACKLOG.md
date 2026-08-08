# UI 修复任务清单

## 目的

本文档记录 2026-08-02 UI 审查发现的问题，并将其拆成可独立认领的修复任务。新 Session 不需要重新分析整个 UI；读取项目约束和本文档后，选择一项适合自身能力的待认领任务即可。

本文档只负责开发协作，不改变产品中的路线优先级，也不代表所有设计候选都已获批准。实现仍以 `AGENTS.md`、`docs/PRODUCT_SPEC.md`、`docs/ACCEPTANCE.md`、`docs/UX_DESIGN.md` 和 `docs/IMPLEMENTATION_PLAN.md` 为准。

## 使用方式

### 方式一：手动指定任务

向新 Session 发送：

> 阅读 `AGENTS.md` 和 `docs/UI_FIX_BACKLOG.md`，认领并完成 `UI-XXX`。开始前阅读该任务要求的产品文档，只修改任务范围内的文件，完成后运行任务列出的验证命令并回填结果。

### 方式二：让 Session 自行认领

向新 Session 发送：

> 阅读 `AGENTS.md` 和 `docs/UI_FIX_BACKLOG.md`。根据当前模型能力，从「待认领」任务中选择一项不高于自身推荐模型等级、依赖已经满足的任务。先在文档中填写认领信息，再开始实现。一次只认领一项，完成并验证后回填结果。

自行认领只适合一次运行一个 Session。多个 Session 并发时，应由你手动分配不同任务编号；否则它们可能同时认领同一项任务。

## 能力等级

| 等级 | 适合的模型 | 适用任务 |
| --- | --- | --- |
| A | 轻量模型 | 文案、间距、样式和范围明确的机械修改 |
| B | 标准模型 | 单个交互流程、响应式布局和有限的跨文件修改 |
| C | 强推理模型 | 状态不变量、持久化、疑难启动故障和高风险跨层修改 |

模型等级不足时不要认领。任务代码量小但涉及数据完整性或执行状态时，仍按高等级处理。

## 状态约定

- `待认领`：可以开始，且依赖已经满足。
- `进行中`：已有 Session 认领，不要重复处理。
- `阻塞`：缺少决策、环境或前置任务。
- `已完成`：实现和验证均已完成，并已记录证据。

认领时填写：

```text
状态：进行中
认领者：<Session 标题或线程 ID>
认领时间：<YYYY-MM-DD HH:mm，含时区>
```

完成时填写实际验证命令和结果。不能用「应该通过」代替运行证据。

## 当前基线

- `dotnet build ExecutionContinuity.slnx --no-restore --verbosity minimal`：通过，0 个警告，0 个错误。
- `dotnet test ExecutionContinuity.slnx --no-build --verbosity minimal`：通过，App 24、Domain 22、Persistence 5，共 51 个测试。
- 实际窗口基线：隔离 fixture 下的 `Daily` 窗口探针通过；UI-004 已完成 default、520x680 和 1280x760 的真实窗口验收与截图。尚未实现的任务不包含在该验收结论中；仓库中的 Stitch 图片仍只是参考图。
- 工作区存在未提交修改。任何 Session 都必须先检查 `git status --short`，不得覆盖无关改动。

## 任务总览

| 编号 | 任务 | 体量 | 风险 | 推荐等级 | 状态 | 依赖 |
| --- | --- | --- | --- | --- | --- | --- |
| UI-001 | 排查应用启动后没有主窗口 | 大 | 高 | C | 已完成 | 无 |
| UI-002 | 为归档内容提供可逆恢复界面 | 大 | 高 | C | 待认领 | 无 |
| UI-003 | 锁定 Capture 抽屉的原始上下文 | 中 | 高 | C | 已完成 | 无 |
| UI-004 | 实现真正的窄窗口导航与自适应布局 | 大 | 中 | B | 已完成 | UI-001 建议先完成 |
| UI-005 | 按规格重构路线列表的信息层级 | 大 | 中 | B | 待认领 | UI-001 建议先完成 |
| UI-006 | 将 Inbox 和语言设置占位内容改为真实控件 | 大 | 中 | B | 待认领 | UI-001 建议先完成 |
| UI-007 | 修复 Guide 主动作卡片内边距 | 小 | 低 | A | 已完成 | 无 |
| UI-008 | 补齐关键 UI 行为的自动化与手工验收脚本 | 中 | 中 | B | 待认领 | 跟随对应修复任务 |

## UI-001：排查应用启动后没有主窗口

- **状态：** 已完成
- **认领者：** Codex UI-001 startup regression audit
- **认领时间：** 2026-08-04 00:39 +08:00
- **体量：** 大
- **风险：** 高
- **推荐等级：** C

### 现象

解决方案可以构建，自动化测试也能通过，但启动 `ExecutionContinuity.App.exe` 后没有出现可捕获的主窗口。一次复测中进程保持存活并锁住可执行文件；另一次复测中进程随后退出。`%LOCALAPPDATA%\ExecutionContinuity\startup-error.txt` 没有记录本次故障。

### 完成标准

- 找到可复现的启动失败原因，或证明问题只存在于当前自动化环境。
- 正常启动后出现标题为 `Daily` 的主窗口。
- 启动异常能留下本次有效诊断信息，不依赖过期日志。
- 提供稳定的手工启动检查步骤；能自动化时补充相应测试。
- 重新运行完整构建和测试。

### 边界

- 不要为了让窗口出现而删除用户数据库。
- 不要绕过持久化初始化错误或吞掉异常。
- 不要顺带重构全部窗口结构。

### 结果

- 状态：已完成
- 修改文件：`src/ExecutionContinuity.App/ExecutionContinuity.App.csproj`、`src/ExecutionContinuity.App/Program.cs`、`src/ExecutionContinuity.App/App.xaml.cs`、`src/ExecutionContinuity.App/StartupDiagnostics.cs`、`tests/ExecutionContinuity.App.Tests/ExecutionSessionTests.cs`、`docs/MANUAL_ACCEPTANCE.md`。
- 自动化验证：目标诊断测试先失败后通过；`dotnet test tests/ExecutionContinuity.App.Tests/ExecutionContinuity.App.Tests.csproj --no-restore --verbosity minimal`，11/11 通过；`dotnet build ExecutionContinuity.slnx --no-restore --verbosity minimal`，0 个警告、0 个错误；`dotnet test ExecutionContinuity.slnx --no-build --verbosity minimal`，38/38 通过；`git diff --check` 通过。
- 手工验证：直接启动 Debug `ExecutionContinuity.App.exe` 后，以进程内顶层窗口枚举确认可见 WinUI 窗口标题为 `Daily`，尺寸为 `960x680`。此前“无主窗口”来自自动化探针将同进程的输入法浮窗误识别为主窗口；源码、项目配置和文档不含 OneDrive、Explorer 或 shell 启动调用。
- 未解决风险：如果 Windows App SDK 在进入 `Program.Main` 前发生原生启动故障，托管诊断文件无法捕获该故障；本次未复现此类故障。
- 后续任务：可认领 `UI-004`、`UI-005` 或 `UI-006`；`UI-007` 可重新执行默认窗口与窄窗口布局验收。

#### 2026-08-04 回归审计

- 状态：已完成
- 根因：本次无法复现应用启动退出。直接启动 Debug `ExecutionContinuity.App.exe` 后，`Daily` 主窗口约 500ms 出现且进程持续运行超过 10 秒；系统窗口树也识别到同一 `Daily` 窗口。失败证据中的 `dotnet ExecutionContinuity.App.dll` 是无效的 WinUI 启动路径，本次返回 `0xC000027B`，且发生在托管诊断入口之前；该退出码不能代表 exe 启动失败。Windows 自动化直接按路径启动还曾被错误路由到 OneDrive，因此也不能单独作为应用退出证据。
- 修改文件：`tests/Verify-ReleaseWindow.ps1` 增加 `ExpectedWindowTitle`，只有非零主窗口句柄且标题精确为 `Daily` 才通过，并在超时时输出最后句柄和标题；`tests/ExecutionContinuity.App.Tests/ExecutionSessionTests.cs` 增加探针契约回归测试；`docs/MANUAL_ACCEPTANCE.md` 固化正确的 exe/探针启动命令并排除 DLL 宿主方式。
- 自动化验证：`Startup_window_probe_requires_the_Daily_window_title` 先因脚本缺少标题约束失败，修改后 1/1 通过；加固探针对同一 Debug exe 连续三轮均返回不同 PID/句柄和 `Title='Daily'`；`dotnet build ExecutionContinuity.slnx --no-restore --verbosity minimal` 通过，0 警告、0 错误；`dotnet test ExecutionContinuity.slnx --no-build --verbosity minimal` 通过，Domain 22/22、Persistence 5/5、App 15/15，共 42/42；最终构建后探针再次返回 `PASS ... Title='Daily'`。
- 手工验证：直接 exe 进程 PID 31152 在 40 次、每 250ms 的采样中从 500ms 起持续暴露标题为 `Daily` 的主窗口；`sky.list_apps()`、`sky.get_window_state()` 和实际截图均确认真实 WinUI 窗口标题与内容正常。旧 `startup-error.txt` 仍是 2026-07-27 的 `ExecutionShellWindow` Release 历史记录，本次没有新托管异常，因此时间戳未变化。
- 未解决风险：自动化平台的通用 `launch_app` 仍可能错误解析本地路径；启动验收必须使用仓库脚本或直接 exe。Windows App SDK 在 `Program.Main` 前的原生故障仍无法由托管诊断文件捕获。
- 后续任务：`UI-007` 可以重新执行默认窗口和窄窗口的真实截图验收；不需要再修改启动代码或 Guide Padding。

## UI-002：为归档内容提供可逆恢复界面

- **状态：** 待认领
- **认领者：**
- **认领时间：**
- **体量：** 大
- **风险：** 高
- **推荐等级：** C

### 问题

归档页目前只有说明文字，没有归档列表和恢复操作。用户可以归档路线或想法，但无法从 UI 中恢复。这违反「归档可逆」的产品约束。

### 完成标准

- 规划模式的归档页分别显示已归档路线和已归档 Inbox 条目。
- 每项提供明确的恢复操作；恢复只改变归档状态，不激活路线、不移动当前步骤、不删除快照。
- 恢复后的内容回到对应规划列表，原始内容和时间戳保持不变。
- 持久化失败时不显示成功状态，界面与内存状态保持原样。
- 为领域状态转换和持久化恢复补充自动化测试，并提供 UI 手工验收步骤。

### 边界

- 不增加隐式删除或自动清理。
- 恢复路线时不得自动设置 `activeRouteId`。
- 不在 Guide 或 Capture 中暴露路线选择。

### 结果

待填写。

## UI-003：锁定 Capture 抽屉的原始上下文

- **状态：** 已完成
- **认领者：** Codex UI-003 capture context isolation
- **认领时间：** 2026-08-03 22:16 +08:00
- **体量：** 中
- **风险：** 高
- **推荐等级：** C

### 问题

Capture 抽屉只覆盖内容区右侧。抽屉打开后，顶部 Guide/Planning 切换和设置入口仍可操作。切换模式会改变底层页面；打开设置会直接关闭抽屉，并可能丢失尚未保存的输入。保存后也无法保证回到打开抽屉时的页面、动作和滚动位置。

### 完成标准

- 打开抽屉时记录当前模式、规划目的地、当前动作和必要的视图位置。
- 抽屉打开期间，底层导航和会改变上下文的操作不可触发。
- 保存成功必须等待持久化确认，然后关闭抽屉并恢复原上下文。
- 保存失败保持抽屉打开并保留输入，不改变路线状态或已有快照。
- 只有明确点击「取消」才丢弃未保存输入。
- 增加覆盖保存成功、保存失败、取消和底层导航隔离的测试或手工验收脚本。

### 边界

- Capture 只能新增 Inbox 条目。
- 不得修改 `activeRouteId`、`currentStepId`、路线生命周期、步骤完成状态或已有执行快照。
- 不把 Capture 变成规划模式或路线选择入口。

### 结果

- 状态：已完成
- 修改文件：`src/ExecutionContinuity.App/CapturePresentation.cs` 新增可测试的 Capture 来源上下文锁；`src/ExecutionContinuity.App/MainWindow.xaml` 将抽屉改为覆盖完整窗口、循环 Tab 焦点的模态层并把状态条置于其上；`src/ExecutionContinuity.App/MainWindow.xaml.cs` 记录并恢复模式、设置状态、规划目的地、当前 route/step/mode/action 和滚动偏移，隔离底层导航，且只在持久化成功或明确取消时释放上下文；`tests/ExecutionContinuity.App.Tests/ExecutionSessionTests.cs` 增加上下文锁和模态层回归测试；`docs/MANUAL_ACCEPTANCE.md` 增加 Capture 上下文隔离验收脚本。
- 自动化验证：修改前 App 基线 `dotnet test tests/ExecutionContinuity.App.Tests/ExecutionContinuity.App.Tests.csproj --no-restore --verbosity minimal` 为 11/11 通过；新增上下文测试先因类型缺失失败，再以 `dotnet test tests/ExecutionContinuity.App.Tests/ExecutionContinuity.App.Tests.csproj --no-restore --filter FullyQualifiedName~Capture_ --verbosity minimal` 验证 4/4 通过；状态条层级和初始折叠断言均先失败后通过；App 全量测试 14/14 通过；`dotnet build ExecutionContinuity.slnx --no-restore --verbosity minimal` 通过，0 警告、0 错误；`dotnet test ExecutionContinuity.slnx --no-build --verbosity minimal` 通过，App 14、Domain 22、Persistence 5，共 41/41；`git diff --check` 退出码 0（仅显示仓库既有的 LF 到 CRLF 提示）。
- 手工验证：启动 Debug `ExecutionContinuity.App.exe` 后确认可见窗口标题为 `Daily`。在无活动路线状态打开 Capture，输入未保存文本后点击遮罩下的「进入规划」未触发导航；连续三次 Tab 后焦点回到 Capture 输入框，文本保持；点击「取消」后抽屉关闭、未保存文本消失并返回原空状态。测试进程已关闭，未修改或删除已有本地数据。
- 未解决风险：真实本地 Inbox 已含用户数据，为避免污染或覆盖数据，没有在该库执行成功保存和 Planning 滚动位置的真实窗口验收；成功/失败持久化边界、上下文释放与滚动恢复由自动化测试、代码路径和新增手工脚本覆盖。Windows 自动化初次直接启动被错误路由到 OneDrive，改为直接启动本仓库 exe 后完成上述验收。
- 后续任务：`UI-008` 可在具备隔离测试数据库的桌面 UI 驱动中补充 Capture 成功保存、写失败输入保留及 Planning 滚动位置的端到端自动化；不要通过清空现有用户数据库制造测试前置条件。

## UI-004：实现真正的窄窗口导航与自适应布局

- **状态：** 已完成
- **认领者：** Codex UI-004 responsive navigation
- **认领时间：** 2026-08-05 00:00 +08:00
- **体量：** 大
- **风险：** 中
- **推荐等级：** B

### 问题

Guide、无活动路线、设置、Capture 和底部按钮使用固定宽度。小窗口下可能横向裁切，而页面禁用了水平滚动。规划紧凑模式只是把列表和详情纵向堆叠，没有实现规格要求的列表到详情流程。

### 完成标准

- Guide、空状态、设置、状态栏、Capture 和底部操作栏使用带最大宽度的响应式尺寸，不产生横向裁切。
- Capture 在窄窗口中变成全宽面板。
- Routes 和 Inbox 在窄窗口中使用列表到详情流程，并提供明确返回操作。
- 返回列表时恢复分组、选中项和滚动位置；不改变任何领域状态。
- 在默认窗口、窄窗口和较宽桌面窗口执行手工截图验收。

### 边界

- 不通过缩小字体解决溢出。
- 不在 Guide 中增加路线列表或其他选择。
- 不把列表和详情简单堆叠成一个长页面。

### 结果

- 状态：已完成
- 根因与修复：PowerShell 启动的窗口不能稳定交给桌面自动化，因此 `tests/ExecutionContinuity.UiFixture/Program.cs` 增加无参数交互启动模式，由桌面自动化直接启动 fixture launcher，在同一 Windows session 中创建隔离数据库、设置子进程环境并启动真实 app；launcher 记录父子 PID、SessionId、exe、数据库和 trace 路径。窄窗详情空白的直接根因是 Routes/Inbox 详情被移动到第 0 列，同时第 0 列在详情模式被设为 0 宽；`MainWindow.xaml.cs` 将两个详情 surface 固定在第 1 个 `Star` 列。列表无法滚动的根因是内部 `ScrollViewer` 位于无限高度测量的 `StackPanel`；`MainWindow.xaml` 将 Routes 改为 `Auto/*` Grid、Inbox 改为 `Auto/Auto/*` Grid。fixture 扩充为 10 条路线和 13 条 Inbox 项，支持滚动恢复验收。
- 自动化验证：`Compact_detail_surfaces_stay_in_the_star_sized_detail_column` 与 `Planning_lists_give_inner_scroll_viewers_a_constrained_grid_row` 均先失败后通过；最终 `dotnet build ExecutionContinuity.slnx --no-restore --verbosity minimal` 通过，0 个警告、0 个错误；`dotnet test ExecutionContinuity.slnx --no-build --verbosity minimal` 通过，App 24/24、Domain 22/22、Persistence 5/5，共 51/51；`dotnet build tests\ExecutionContinuity.UiFixture\ExecutionContinuity.UiFixture.csproj --no-restore --verbosity minimal` 通过，0 个警告、0 个错误；隔离数据库窗口探针输出 `PASS ProcessId=40876 MainWindowHandle=1250236 Title='Daily' DatabasePath='C:\Users\47596\Documents\Daily\.ui-review\ui004-fixture\execution-continuity.db'`；`git diff --check` 退出码 0，仅显示既有 LF 到 CRLF 提示。
- 手工验证：Computer Use 直接启动测试 launcher 后，`.ui-review/ui004-fixture/interactive-launcher-evidence.txt` 记录 launcher PID 42640 与 app PID 4532 均位于 SessionId 1，且 app 使用隔离数据库。真实窗口在 default、520x680 和 1280x760 三种请求尺寸下完成检查。520x680 下 Routes 从滚动后的 route 05 进入可见详情并由 `返回路线列表` 回到 route 04/05 的原滚动位置；Inbox 从滚动后的 entry 03 进入可见详情并由 `返回收件箱列表` 回到 entry 03 被选中且仍在原滚动位置。全宽 Capture、Settings、Guide、底部栏均无横向裁切。证据为 `.ui-review/ui_004_final_default_routes.png`、`.ui-review/ui_004_final_default_inbox.png`、`.ui-review/ui_004_final_520x680_guide.png`、`.ui-review/ui_004_final_520x680_routes_list_scrolled.png`、`.ui-review/ui_004_final_520x680_routes_detail.png`、`.ui-review/ui_004_final_520x680_routes_back.png`、`.ui-review/ui_004_final_520x680_inbox_list_scrolled.png`、`.ui-review/ui_004_final_520x680_inbox_detail_scrolled.png`、`.ui-review/ui_004_final_520x680_inbox_back_scrolled.png`、`.ui-review/ui_004_final_520x680_capture.png`、`.ui-review/ui_004_final_520x680_settings.png`、`.ui-review/ui_004_final_1280x760_routes.png` 和 `.ui-review/ui_004_final_1280x760_inbox_detail.png`。
- 领域与数据边界：验收只执行导航、滚动、查看、返回、Capture 取消和 Settings 查看，没有触发激活、保存、归档、转换或完成命令；状态模型测试保留选择和滚动上下文且不调用领域状态转换。所有本轮 app 启动均由 fixture launcher 或带显式 `-DatabasePath` 的探针完成，默认 LocalAppData 数据库未被读取、修改或删除。
- 未解决风险：无 UI-004 阻塞项。截图像素尺寸受 Windows DPI 缩放影响；实际请求尺寸分别为 default、520x680 和 1280x760。
- 后续任务：无。

## UI-005：按规格重构路线列表的信息层级

- **状态：** 待认领
- **认领者：**
- **认领时间：**
- **体量：** 大
- **风险：** 中
- **推荐等级：** B

### 问题

页面虽然显示「按状态」，实际路线只按标题排序。列表没有 Current、Paused、Draft 分组，也没有暂停时间、步骤进度和返回上下文展开。暂停路线没有按最近暂停时间优先。

### 完成标准

- 提供稳定的「按状态 / 按项目」分组控件；首次默认按状态。
- 按状态显示 Current、Paused 和 Draft，Paused 按最近暂停时间排序。
- 暂停路线显示标题、保留的下一动作、暂停时间、步骤进度和一个恢复命令。
- 展开后只读显示完成标准、边界、暂停备注和预设备选动作。
- 选择或展开路线不会修改路线状态；切换分组不会激活或排序路线步骤。
- 若按项目所需领域数据尚未实现，先记录明确阻塞，不要伪造项目归属。

### 边界

- 不根据内容推断优先级或推荐路线。
- Guide 不显示这些路线选择。
- 路线切换仍必须先持久化旧路线的完整返回锚点。

### 结果

待填写。

## UI-006：将 Inbox 和语言设置占位内容改为真实控件

- **状态：** 待认领
- **认领者：**
- **认领时间：**
- **体量：** 大
- **风险：** 中
- **推荐等级：** B

### 问题

Inbox 的「全部 / 未整理 / 已整理」目前只是静态文字，不能筛选；Inbox 和 Routes 都没有本地搜索。设置页只显示「简体中文（默认）」，没有「跟随系统 / 简体中文 / English」选择控件。

### 完成标准

- Inbox 使用可操作的分段控件过滤全部、未整理和已整理条目。
- Inbox 搜索同时匹配不可变原文和整理后内容，且不返回归档条目。
- Routes 搜索匹配路线标题、保留的下一动作和项目名。
- 设置提供「跟随系统 / 简体中文 / English」，选择能够持久化并即时更新界面文案。
- 用户填写的路线、步骤、备注和 Capture 原文绝不自动翻译。
- 如果整理后内容或项目字段尚未进入领域模型，应拆出阻塞任务，不用 UI 假数据掩盖缺口。

### 边界

- 不添加自动 AI 整理。
- 不把语言选择放进 Guide 主执行区域。
- 不让搜索、过滤或语言切换改变路线生命周期。

### 结果

待填写。

## UI-007：修复 Guide 主动作卡片内边距

- **状态：** 已完成
- **认领者：** Codex UI-007 continuation acceptance
- **认领时间：** 2026-08-03 23:30 +08:00
- **体量：** 小
- **风险：** 低
- **推荐等级：** A

### 问题

Guide 主动作卡片的 `Border` 没有 `Padding`，标题和动作文字会贴近边框，与 Stitch 参考图及其他内容卡片的留白不一致。

### 完成标准

- 为主动作卡片增加与当前间距体系一致的内边距。
- 长动作文本正常换行，不覆盖边框或后续内容。
- 默认窗口和窄窗口下检查卡片布局。
- 运行 App 测试和完整构建。

### 边界

- 不改变动作文字、完成标准或边界内容。
- 不顺带重做整个 Guide 视觉系统。

### 结果

- 状态：已完成
- 修改文件：`src/ExecutionContinuity.App/MainWindow.xaml` 增加 Guide 主动作卡片 `Padding="20"`；`tests/ExecutionContinuity.App.Tests/ExecutionSessionTests.cs` 增加内边距回归断言。
- 自动化验证：目标 Padding 回归测试已通过；本轮 `dotnet build ExecutionContinuity.slnx --no-restore --verbosity minimal` 通过，0 警告、0 错误；`dotnet test ExecutionContinuity.slnx --no-build --verbosity minimal` 通过，Domain 22/22、Persistence 5/5、App 19/19，共 46/46。
- 手工验证：使用隔离 active-route fixture 启动真实 `Daily` 窗口。请求 `520x680` 时，`.ui-review/ui_007_fixture_520x680_guide.png` 显示主动作卡片具有一致内边距、`Open the current route detail` 正常换行，完成标准、边界和底部 Capture 没有覆盖；请求 `1280x760` 时，`.ui-review/ui_007_fixture_1280x760_guide.png` 显示相同卡片在宽窗口内布局正常。子进程路径和请求尺寸见 `.ui-review/ui004-fixture/launcher-evidence.txt`；窗口探针输出 `PASS ... Title='Daily'`。
- 未解决风险：无。
- 后续任务：无。

## UI-008：补齐关键 UI 行为的自动化与手工验收脚本

- **状态：** 待认领
- **认领者：**
- **认领时间：**
- **体量：** 中
- **风险：** 中
- **推荐等级：** B

### 问题

现有 App 测试主要检查展示模型和 XAML 字符串是否存在，不能证明真实窗口能够启动，也没有覆盖窄窗口导航、Capture 上下文隔离、归档恢复和语言切换。

### 完成标准

- 将每个已修复任务中可测试的验收标准映射到自动化测试或 `docs/MANUAL_ACCEPTANCE.md`。
- 自动化覆盖状态变化和持久化边界；真实视觉、窗口尺寸和焦点行为使用明确的手工步骤。
- 手工步骤包含前置数据、操作、预期结果和失败时需要保留的证据。
- 完成全量构建和测试，并记录测试数量。

### 边界

- 不用只检查字符串存在的测试代替行为验证。
- 不为尚未实现的功能写会无条件通过的占位测试。
- 不在没有实际运行证据时声明 UI 验收完成。

### 结果

待填写。

## Session 完成回填模板

```markdown
### 结果

- 状态：已完成 / 阻塞
- 修改文件：
- 自动化验证：
- 手工验证：
- 未解决风险：
- 后续任务：
```
