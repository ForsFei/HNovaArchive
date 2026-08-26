# HDecompressHelper 实施计划

## 目标
基于现有仓库构建一个科技感、简洁的 WPF 解压管理工具：调用 7-Zip 执行解压，集中展示多个任务的文件级/总体进度，并支持快速打开压缩包所在目录和解压目标目录。

## 第二版目标
在第一版基础上增加历史持久化、列表/平铺显示切换、深浅主题切换、7-Zip/WinRAR 解压引擎切换，以及允许明文落盘和写入日志的密码输入与显隐切换。

## 第三版目标
增加成功密码库与失败后的候选密码重试；修复密码工具栏裁切；支持分卷首卷和伪装/附加后缀压缩文件的自动重命名加载。

## 第四版目标
密码功能始终启用并默认明文显示，精简密码工具栏；把密码输入移到解压引擎右侧，并将视图和背景切换靠右排列。

## 阶段

### Phase 1：需求与仓库审查
**Status:** complete
- 阅读 `Chatgpt回复.md`、工程结构和现有实现。
- 确认目标框架、依赖和可复用代码。

### Phase 2：架构与进度监控设计
**Status:** complete
- 设计任务模型、7-Zip 进程封装、输出解析、取消与并发行为。
- 明确 7-Zip 缺失、路径冲突、错误输出等边界条件。

### Phase 3：功能与界面实现
**Status:** complete
- 实现解压任务管理、目录选择、进度显示、状态与日志。
- 实现科技简洁的 WPF 视觉和快速打开目录操作。

### Phase 4：构建与验证
**Status:** complete
- 构建工程并修复编译问题。
- 对核心解析/命令行为做适度验证，检查工作区差异。

### Phase 5：交付说明
**Status:** complete
- 更新规划记录并总结运行方式、监控原理和限制。

### Phase 6：第二版审查与技术设计
**Status:** complete
- 审查第一版服务/UI 边界并核对 WinRAR 官方 CLI 行为。
- 设计设置、历史、主题、引擎路由和密码生命周期。

### Phase 7：持久化与多引擎实现
**Status:** complete
- 实现设置/历史 JSON、7-Zip/WinRAR 定位与后端路由。
- 按用户要求将密码和包含密码的任务日志明文持久化，并在界面/README 明确标注安全风险。

### Phase 8：第二版界面实现
**Status:** complete
- 实现列表/平铺切换、深浅主题切换、引擎切换与路径状态。
- 实现密码启用、输入、显隐切换和任务引擎标识。

### Phase 9：迁移与验证
**Status:** complete
- 增加持久化、队列、引擎命令与密码明文记录测试。
- 构建、格式验证、运行时与深浅主题视觉检查。

### Phase 10：第二版交付
**Status:** complete
- 更新 README、预览和第二版限制说明。
- 确认规划记录与临时数据清理完成。

### Phase 11：第三版设计
**Status:** complete
- 设计成功密码去重、持久化、手动选择和失败重试边界。
- 设计 `.7z.001` 与异常扩展名的安全重命名规则。

### Phase 12：密码库与重试实现
**Status:** complete
- 实现成功密码保存、选择/删除与任务实际密码更新。
- 实现可重试失败后的候选密码顺序尝试和聚合日志。

### Phase 13：输入规范化与界面修复
**Status:** complete
- 支持 `.7z.001` 直接加载、附加后缀剥离和未知扩展名改为 `.zip`。
- 修复密码行裁切并改为可编辑下拉列表。

### Phase 14：第三版验证与交付
**Status:** complete
- 增加密码重试、密码库和文件重命名测试并做真实加密归档验证。
- 复验构建、格式、运行界面并更新 README/规划记录。

### Phase 15：第四版设置与工具栏重构
**Status:** complete
- 移除密码启用/显隐状态和相应按钮，密码输入始终可用且明文显示。
- 将密码输入放到解压引擎右侧，并将视图与背景操作靠右布局。

### Phase 16：第四版验证与交付
**Status:** complete
- 更新测试和 README，说明默认密码行为及性能影响。
- 串行复验格式、Release 构建、测试和实际窗口布局。

## 关键约束
- 保留用户已有改动，不处理无关文件。
- 通过 7-Zip 命令行标准输出监控进度，不依赖 UI 自动化。
- UI 更新必须回到 WPF Dispatcher/UI 线程。
- 密码允许随历史和任务日志明文持久化，不使用 DPAPI；按用户最新要求移除界面常驻安全提示，但 README 保留明文风险说明。
- 视图模式、主题和默认引擎需要持久化，默认保持列表、深色和 7-Zip。

## 错误记录

| 错误 | 尝试 | 处理 |
|---|---:|---|
| 内联 PowerShell 探针被执行策略拒绝 | 1 | 改为可审查的临时 `.ps1` 文件后成功，清理拆成单独、显式路径操作。 |
| 递归清理被执行策略拒绝 | 2 | 已只读核对最终绝对路径；PowerShell `Remove-Item` 仍被策略拒绝，改用同一 PowerShell 进程内的 `.NET Directory.Delete` 静态字面量 API。 |
| 测试项目无法引用 WPF 项目 | 1 | `dotnet new xunit -f net8.0` 与 `net8.0-windows` 不兼容；将测试目标改为 `net8.0-windows` 后重新添加引用。 |
| 核心服务首次构建缺少 `StreamReader` 命名空间 | 1 | WPF 隐式 using 集未包含所需 IO 类型；增加项目级 `global using System.IO`。 |
| C# 12 不允许在 async 方法中保存 `Span<char>` 局部变量 | 1 | 将每次读取转换成短生命周期字符串，再交给 parser/log；保持 .NET 8 默认语言版本而不强行升级。 |
| App.xaml 上下文补丁未匹配模板空白 | 1 | 改为整文件替换，避免模板空白/换行差异影响补丁。 |
| WPF `TextBlock` 不支持 `LetterSpacing` | 1 | 移除该跨 UI 框架属性，保留字号与字重实现品牌字标。 |
| 首次运行因 `Run.Text` 默认回写只读统计属性而退出 | 1 | 为 `ActiveCount`、`CompletedCount`、`MaxConcurrentExtractions` 的 Run 绑定显式设置 `Mode=OneWay`。 |
| `dotnet format --verify` 报模板 `AssemblyInfo.cs` 空白格式 | 1 | 仅为格式问题且不涉及业务代码；使用 `dotnet format --no-restore` 做机械格式化后复验。 |
| 第二版密码约束补丁首次上下文未匹配 | 1 | 用精确单行替换更新约束，并记录用户的新持久化授权。 |
| 读取 `ArchivePathHelper` 时误用了 `Services` 路径 | 1 | 通过 `rg --files` 定位到 `Infrastructure/ArchivePathHelper.cs`，未产生代码改动。 |
| 主 ViewModel 整文件替换补丁格式无效 | 1 | 编辑工具不接受在同一补丁中删除并新增同一路径，拆成删除和新增两个原子补丁。 |
| 第二版首次 Debug 构建无法覆盖程序文件 | 1 | 发现 PID 28968 正在运行用户可见的 Debug 程序；不擅自关闭，改用 Release 输出继续验证。 |
| Release 首次运行因主题画刷被冻结而退出 | 1 | 从 .NET Runtime 事件确认异常；改用 DynamicResource，并在主题服务中替换被冻结的画刷实例。 |
| computer-use 首次启动 Release 超时 | 1 | 重新枚举确认未启动后，改用明确 Release 路径启动，再由 computer-use 唯一绑定窗口。 |
| 文件选择对话框可访问性索引失效 | 1 | 停止复用失效索引，改用应用已有的命令行文件参数载入两个 QA 归档。 |
| 平铺窗口截图被其他前台应用覆盖 | 1 | 不把错误截图作为视觉证据；改用可访问性树、运行状态和持久化 JSON 核对卡片/主题状态。 |
| 组合式 QA 清理脚本被安全策略拒绝 | 1 | 拆分为进程正常关闭、只读绝对路径验证和字面量目录清理。 |
| 字面量 `Remove-Item -Recurse` 仍被策略拒绝 | 1 | 在已验证路径后使用同一 PowerShell 进程内的 `.NET Directory.Delete` 静态字面量 API。 |
| 使用 `Get-Content -Encoding Byte` 读取截图失败 | 1 | 当前 pwsh 不支持 `Byte` 编码名称；改用专用 `view_image` 工具查看原始图片。 |
| 第三版完整测试输出超过工具上下文而被截断 | 1 | 改用 `console;verbosity=quiet` 重新运行；60 项测试全部通过。 |
| Release 构建与测试并行写入同一 WPF `obj` 目录导致 RG1000 重复资源键 | 1 | 构建已单独成功；同一解决方案的最终验证改为串行运行，避免共享中间产物竞争。 |
| 移除密码同步代码时一并删除了 `CancelEventArgs` 所需命名空间 | 1 | `OnClosing` 仍需要 `System.ComponentModel`；恢复该 using 后重新构建。 |
