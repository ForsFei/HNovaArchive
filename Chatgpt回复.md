我想使用wpf制作一个桌面端解压管理工具，需求是管理监控各个文件的解压进度以及快速直达解压前后的文件目录。我想直接调用开源的解压软件像7zip，对于如何监控各个文件的解压进度有些疑问，界面做的科技简洁一点，帮我生成一段提示词我拿去给codex

可以。你这个项目比较适合让 Codex **先搭架构，再实现 7-Zip 进程封装 + 实时进度解析 + WPF 任务管理 UI**。关键是要明确告诉它：**不要把“解压”写成同步调用，而是把每个压缩包作为独立任务，启动独立 7z.exe 进程，并实时解析 stdout/stderr 中的进度信息。**

你可以直接把下面整段丢给 Codex：

我要使用 **C# + .NET 8 + WPF** 开发一个 Windows 桌面端的“解压任务管理工具”。

这个软件本身不实现压缩算法，而是调用开源的 **7-Zip / 7z.exe 命令行程序**完成解压。

请你作为一名资深 C# / WPF 桌面软件工程师，帮我从架构设计开始，逐步实现一个可以实际运行、方便后续扩展的项目。

---

# 一、项目目标

开发一个界面简洁、科技感、现代化的桌面端解压管理器。

核心用途：

1. 同时管理多个压缩文件的解压任务
2. 实时显示每个压缩包自己的解压进度
3. 显示当前正在解压的文件
4. 显示解压速度、已用时间等状态
5. 可以快速打开：

   * 压缩包所在目录
   * 解压后的目标目录
6. 支持多个任务排队或并行执行
7. 支持取消任务
8. 解压完成后保留历史记录
9. 单个任务失败不能影响其他任务
10. UI 必须保持流畅，任何解压操作都不能阻塞 WPF UI 线程

---

# 二、技术栈

请优先使用：

* C#
* .NET 8
* WPF
* MVVM
* CommunityToolkit.Mvvm
* Microsoft.Extensions.DependencyInjection
* async / await
* Process / ProcessStartInfo
* ObservableCollection
* CancellationToken

尽量不要为了简单功能引入大型第三方框架。

要求项目结构清晰，可以长期维护。

建议项目结构类似：

```text
ExtractorManager
│
├─ Models
│   ├─ ExtractionTask.cs
│   ├─ ExtractionStatus.cs
│   └─ ExtractionProgress.cs
│
├─ ViewModels
│   ├─ MainViewModel.cs
│   └─ ExtractionTaskViewModel.cs
│
├─ Views
│   ├─ MainWindow.xaml
│   └─ ...
│
├─ Services
│   ├─ IExtractionService.cs
│   ├─ SevenZipExtractionService.cs
│   ├─ ExtractionQueueService.cs
│   └─ FileExplorerService.cs
│
├─ Infrastructure
│   ├─ SevenZipProgressParser.cs
│   └─ ...
│
├─ Resources
│   ├─ Styles
│   └─ Icons
│
└─ App.xaml
```

如果你认为有更合理的目录结构，可以调整，但请说明原因。

---

# 三、最重要的部分：7-Zip 解压进度监控

这是整个项目最重要的技术点。

不要简单执行：

```text
7z.exe x xxx.zip
```

然后等待 Process.Exit。

我要能够 **实时得到每一个解压任务自己的进度百分比**。

请研究并使用 7-Zip CLI 提供的进度输出能力，例如：

```text
-bsp1
```

以及必要时：

```text
-bso1
-bse1
-bb1
```

具体参数请根据 7-Zip CLI 的实际行为选择。

目标命令大致类似：

```text
7z.exe x "D:\Downloads\Test.zip" -o"D:\Extract\Test" -y -bsp1
```

启动 Process 时要求：

```csharp
UseShellExecute = false;
RedirectStandardOutput = true;
RedirectStandardError = true;
CreateNoWindow = true;
```

然后异步读取 stdout / stderr。

---

# 四、进度解析

请专门设计：

```text
SevenZipProgressParser
```

不要把字符串解析逻辑全部塞进 Process 管理代码。

7-Zip 的输出中可能出现类似：

```text
  1%
 15%
 42%
 87%
100%
```

也可能包含：

```text
35% filename.xxx
```

需要从输出中可靠提取：

```csharp
int ProgressPercent
string? CurrentFile
```

例如：

```csharp
public sealed record ExtractionProgress(
    int Percent,
    string? CurrentFile
);
```

解析代码需要考虑：

* 前后空格
* `\r` 覆盖式控制台输出
* `\n`
* 输出被拆成多个 buffer
* 文件名中有空格
* 中文文件名
* 7-Zip 不同版本输出格式可能略有差异
* 不要因为一行无法识别就让任务报错
* Percent 必须限制在 0~100

特别注意：

**不要默认 ReadLineAsync 一定能够正确处理 7-Zip 所有进度输出。**

因为命令行程序可能使用 `\r` 原地刷新进度。

如果需要，请直接读取 StandardOutput 的字符流/Buffer，并自行处理：

```text
\r
\n
```

实现一个稳健的流式 parser。

---

# 五、每个压缩包必须是独立任务

设计：

```csharp
ExtractionTask
```

至少包含：

```csharp
Guid Id

string ArchivePath
string DestinationPath

string ArchiveName

ExtractionStatus Status

int Progress

string? CurrentFile

DateTime? StartTime
DateTime? EndTime

TimeSpan Elapsed

long? ArchiveSize

string? ErrorMessage

int? ProcessId
```

状态至少包含：

```csharp
Queued
Extracting
Completed
Failed
Cancelled
```

每启动一个压缩包：

```text
Task A
    ↓
7z.exe Process A

Task B
    ↓
7z.exe Process B

Task C
    ↓
7z.exe Process C
```

每个 Process 的 stdout 必须只更新属于自己的 ExtractionTask。

绝对不要使用一个全局 Progress。

---

# 六、任务队列

实现：

```text
ExtractionQueueService
```

支持：

```text
最大并发任务数量
```

例如默认：

```text
2
```

即：

```text
任务1 → Running
任务2 → Running
任务3 → Waiting
任务4 → Waiting
任务5 → Waiting
```

任务 1 完成：

```text
任务3 → Running
```

希望可以设计：

```csharp
MaxConcurrentExtractions
```

以后可以在设置页面修改。

可以考虑：

```csharp
SemaphoreSlim
```

来限制并发数量。

但需要保证：

* 单个任务异常不会导致整个 Queue 停止
* Cancel 某个任务不会影响其他任务
* UI 能实时知道 Waiting / Running / Completed 状态
* 不产生线程泄漏
* Process 对象正确 Dispose
* 应用退出时能够正确处理仍在运行的 7z.exe

---

# 七、取消任务

每个解压任务右侧提供：

```text
取消
```

要求真正停止这个任务对应的 7z.exe。

可以结合：

```csharp
CancellationTokenSource
```

以及：

```csharp
Process.Kill(entireProcessTree: true)
```

实现。

需要正确处理：

```text
用户主动取消
```

和：

```text
Process 因异常退出
```

这两种情况。

用户取消：

```text
Cancelled
```

程序错误：

```text
Failed
```

不要混淆。

---

# 八、判断解压成功

不能仅仅根据：

```text
Progress == 100
```

判断成功。

必须检查：

```csharp
process.ExitCode
```

并结合 7-Zip 的退出码规则判断。

请封装一个类似：

```csharp
SevenZipExitCodeResult ParseExitCode(int exitCode)
```

区分：

```text
成功
警告
错误
致命错误
命令行参数错误
内存不足
用户中止
```

对于 Warning 是否标记 CompletedWithWarning，请你根据整体架构设计。

---

# 九、主界面设计

我要的是：

```text
现代
科技感
极简
深色
专业工具软件
```

不要做成传统 Windows XP / WinForms 风格。

参考视觉方向：

* Windows 11
* Fluent Design
* VS Code
* GitHub Desktop
* Linear
* Arc
* Raycast
* 现代下载管理器

主色调：

```text
深灰 / 黑灰背景
浅灰文字
蓝色或青蓝色 Accent
```

不要大量渐变。
不要花哨动画。
不要玻璃效果泛滥。

整体强调：

```text
干净
信息层级清晰
高密度但不拥挤
```

---

# 十、主界面布局

窗口大概：

```text
┌─────────────────────────────────────────────────────────┐
│  Extractor                                  ─  □  ×     │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  解压任务                              + 添加压缩文件    │
│  3 Active · 12 Completed                               │
│                                                         │
│ ┌─────────────────────────────────────────────────────┐ │
│ │ test_data.7z                               68%      │ │
│ │ D:\Downloads\test_data.7z                           │ │
│ │                                                     │ │
│ │ ███████████████████████░░░░░░░░                    │ │
│ │                                                     │ │
│ │ Extracting: images\data_02341.png                   │ │
│ │ 1.2 GB / 1.8 GB                  00:01:28           │ │
│ │                                                     │ │
│ │ [源目录] [目标目录]                      [取消]      │ │
│ └─────────────────────────────────────────────────────┘ │
│                                                         │
│ ┌─────────────────────────────────────────────────────┐ │
│ │ firmware_backup.zip                       Waiting   │ │
│ │ E:\Backup\firmware_backup.zip                       │ │
│ │                                                     │ │
│ │ Waiting for available worker...                    │ │
│ └─────────────────────────────────────────────────────┘ │
│                                                         │
│ ┌─────────────────────────────────────────────────────┐ │
│ │ resources.rar                              ✓ Done   │ │
│ │ Completed in 00:00:32                              │ │
│ │                                  [打开目标目录]      │ │
│ └─────────────────────────────────────────────────────┘ │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

每一个任务使用一个现代化 Card。

Card 左边主要显示：

```text
文件名
源文件路径
当前文件
```

中间：

```text
ProgressBar
Percentage
状态
```

右侧：

```text
打开源目录
打开目标目录
取消
删除记录
```

---

# 十一、状态颜色

建议使用统一状态：

```text
Queued
灰色

Extracting
蓝色

Completed
绿色

Warning
黄色

Failed
红色

Cancelled
灰色
```

但不要在代码中到处硬编码 Color。

请通过：

```text
ResourceDictionary
Style
Converter
```

或者 ViewModel 属性统一管理。

---

# 十二、快速打开目录

实现：

```text
打开压缩包所在目录
```

希望使用 Windows Explorer，并最好能够：

```text
打开目录 + 自动选中压缩包
```

类似：

```text
explorer.exe /select,"D:\Downloads\test.zip"
```

另一个按钮：

```text
打开解压目录
```

直接打开：

```text
D:\Extract\Test
```

请封装成：

```csharp
IFileExplorerService
```

例如：

```csharp
OpenFolder(string path)

ShowFileInFolder(string filePath)
```

ViewModel 不要自己调用 explorer.exe。

---

# 十三、添加任务

支持：

## 方式1

点击：

```text
+ 添加文件
```

使用 OpenFileDialog。

允许：

```text
*.zip
*.7z
*.rar
*.tar
*.gz
*.xz
*.bz2
```

以及其他 7-Zip 支持的常见压缩格式。

支持 MultiSelect。

---

## 方式2

支持把压缩包直接：

```text
拖拽到窗口
```

自动创建解压任务。

---

# 十四、默认解压目录

例如：

```text
D:\Download\Test.zip
```

默认：

```text
D:\Download\Test\
```

即：

```text
源文件目录 + 去掉扩展名后的文件名
```

但请正确处理：

```text
.tar.gz
.tar.xz
```

这种双扩展名。

同时允许用户修改 DestinationPath。

---

# 十五、7-Zip 路径管理

不要在业务代码里写死：

```text
C:\Program Files\7-Zip\7z.exe
```

设计：

```csharp
ISevenZipLocator
```

启动时：

1. 查询用户设置
2. 查询常见 7-Zip 安装路径
3. 如果项目自己携带 7z.exe，则查询程序目录
4. 找不到时提示用户手动选择

例如：

```text
Tools\7zip\7z.exe
```

未来我可能考虑软件随程序携带一个 7-Zip CLI。

因此这部分必须方便替换。

---

# 十六、解压速度

如果 7-Zip 本身不能稳定输出实时解压字节数，不要伪造精确速度。

可以研究是否能够可靠获得：

```text
ProcessedBytes
TotalBytes
BytesPerSecond
```

如果 7-Zip stdout 无法可靠获得，就先只实现：

```text
百分比
当前文件
Elapsed Time
```

ArchiveSize 可以用于 UI 信息展示，但：

```text
ArchiveSize * Percent
```

并不一定等于真正的已解压数据量。

因此不要给用户展示误导性的“精确解压速度”。

如果只能估算，请明确命名：

```text
Estimated
```

并把速度功能设计为可选模块。

---

# 十七、UI 更新频率

7-Zip 有可能非常频繁地输出进度。

不要每收到一个字符就立即 Dispatcher 更新 UI。

设计合理的节流机制，例如：

```text
50~100ms
```

最多更新一次 UI。

避免：

```text
stdout 高频输出
        ↓
大量 Dispatcher.Invoke
        ↓
WPF UI 卡顿
```

推荐：

```text
后台解析最新状态
        ↓
Throttle
        ↓
更新 ViewModel
        ↓
WPF Binding
```

---

# 十八、线程安全

需要特别检查：

```text
ObservableCollection
INotifyPropertyChanged
Process stdout callback
CancellationToken
Dispatcher
```

之间的线程问题。

所有 ObservableCollection 修改必须安全地回到 UI Thread。

不要在后台 Process 回调中直接无脑修改绑定集合。

---

# 十九、异常处理

至少考虑：

```text
7z.exe 不存在

压缩包不存在

目标目录无法创建

目标盘空间不足

权限不足

压缩包损坏

压缩包有密码

错误密码

文件正在被占用

7-Zip 启动失败

7-Zip 非正常退出

用户取消

软件退出时仍有解压任务
```

单个错误应该显示在对应 Task Card：

```text
Failed

CRC Failed
```

并提供：

```text
查看详细错误
```

不要直接弹出大量 MessageBox。

---

# 二十、日志

每个任务保存：

```text
Command
Start Time
End Time
ExitCode
stdout
stderr
Error
```

但是 UI 默认只显示简短错误。

点击：

```text
查看详情
```

才显示完整日志。

注意日志中如果以后加入密码参数，需要避免把密码明文记录下来。

---

# 二十一、密码压缩包

架构上预留：

```csharp
string? Password
```

但注意密码不能：

```text
明文写到日志
明文保存到配置
```

如果以后支持密码输入：

```text
PasswordBox
```

或者其他安全方案。

当前第一版可以暂时不实现完整密码管理，但架构不能阻碍以后添加。

---

# 二十二、设置页面

第一版至少预留：

```text
7-Zip.exe Path

Max Concurrent Tasks

Default Output Behavior

Auto Open Folder After Completion

Keep History

Theme
```

设置存储可以使用：

```text
JSON
```

例如：

```text
%AppData%\ExtractorManager\settings.json
```

请设计：

```csharp
ISettingsService
```

不要直接在各 ViewModel 中读取 JSON。

---

# 二十三、任务历史记录

希望软件关闭再打开之后，还能够看到之前完成的任务。

可以保存：

```text
ArchivePath
DestinationPath
Status
Progress
StartTime
EndTime
ErrorMessage
```

到 JSON。

注意：

应用异常退出时：

```text
Extracting
```

状态的任务，下次启动不能继续显示成 Extracting。

应该转成类似：

```text
Interrupted
```

或者：

```text
Failed / Interrupted
```

请帮我设计合理状态。

---

# 二十四、架构要求

重点使用：

```text
MVVM
Dependency Injection
Service abstraction
Command
Observable properties
```

MainWindow.xaml.cs 尽量保持干净。

不允许出现：

```csharp
private void Button_Click(...)
{
    // 300 行业务代码
}
```

Code Behind 只能保留确实属于 View 的少量逻辑。

---

# 二十五、建议核心接口

可以设计类似：

```csharp
public interface IExtractionService
{
    Task<ExtractionResult> ExtractAsync(
        ExtractionRequest request,
        IProgress<ExtractionProgress>? progress,
        CancellationToken cancellationToken);
}
```

以及：

```csharp
public interface IFileExplorerService
{
    void OpenFolder(string folderPath);

    void ShowFileInFolder(string filePath);
}
```

以及：

```csharp
public interface ISevenZipLocator
{
    string? FindSevenZipExecutable();
}
```

以及：

```csharp
public interface IExtractionQueueService
{
    Task EnqueueAsync(...);

    Task CancelAsync(Guid taskId);
}
```

具体接口允许你优化。

---

# 二十六、第一阶段优先实现

不要一次写一大堆不可验证代码。

按照以下阶段开发。

## Phase 1：基础框架

先创建：

```text
.NET 8 WPF
MVVM
DI
MainWindow
Theme Resources
```

让项目能够运行。

---

## Phase 2：7-Zip Service

重点实现并测试：

```text
SevenZipExtractionService

SevenZipProgressParser
```

使用一个真实压缩包测试：

```text
0%
10%
20%
...
100%
```

能够实时回调：

```csharp
IProgress<ExtractionProgress>
```

这是第一优先级。

---

## Phase 3：单任务 UI

实现一个 Task Card。

能够：

```text
选择 zip
点击解压
实时显示 %
实时 ProgressBar
显示 CurrentFile
取消
打开源目录
打开目标目录
```

---

## Phase 4：多任务队列

增加：

```text
ObservableCollection

SemaphoreSlim

MaxConcurrentTasks
```

实现真正的多任务管理。

---

## Phase 5：拖拽 + 历史 + 设置

最后实现：

```text
Drag & Drop
History
Settings
Error Details
```

---

# 二十七、代码质量要求

所有代码要求：

* Nullable Reference Types 开启
* 不滥用 static
* 不使用 Thread.Sleep
* 不使用 `.Result`
* 不使用 `.Wait()`
* 不使用 async void，除非是必要的 WPF Event Handler
* 正确 Dispose Process
* 正确 Dispose CancellationTokenSource
* CancellationToken 全链路传递
* UI 不阻塞
* ViewModel 不直接依赖 Process
* ViewModel 不直接调用 Explorer
* 7-Zip 相关逻辑集中封装
* Progress Parser 可独立单元测试

---

# 二十八、测试

请为：

```text
SevenZipProgressParser
```

编写 Unit Test。

至少测试：

```text
"  0%"
" 10%"
"100%"

" 35% file.txt"

"\r 12%\r 13%\r 14%"

中文文件名

带空格文件名

无法识别的输出

被拆开的字符串数据

Percent > 100

Percent < 0
```

如果 parser 是流式的，请测试跨 buffer 情况：

第一次收到：

```text
" 3"
```

第二次收到：

```text
"5% abc.tx"
```

第三次收到：

```text
"t\r"
```

最终应该正确解析：

```text
35%
abc.txt
```

---

# 二十九、UI XAML 要求

请真正编写现代化 WPF XAML Style，而不是只告诉我“可以使用 Fluent Design”。

至少设计：

```text
Window Background
TaskCard
PrimaryButton
SecondaryButton
IconButton
ProgressBar
StatusBadge
TextBox
ScrollViewer
```

统一放到 ResourceDictionary。

Card 建议：

```text
CornerRadius = 8~12
```

使用适度边框。

不要大量 DropShadowEffect，因为大量任务 Card 使用 WPF 阴影可能影响性能。

通过：

```text
Border
Background
Opacity
```

体现层级。

---

# 三十、图标

尽量使用统一的矢量 Icon。

例如：

```text
Folder
FolderOpen
Archive
Play
Cancel
Check
Error
Settings
Delete
```

不要使用 emoji 当正式 UI 图标。

如果不引入 Icon Library，请使用 Geometry / Path。

---

# 三十一、最终希望达到的交互

用户把：

```text
A.zip
B.7z
C.rar
```

拖入窗口：

```text
A.zip            Waiting
B.7z             Waiting
C.rar            Waiting
```

开始任务后：

```text
A.zip             35%
████████░░░░░░░░░░

Extracting:
images/001.png


B.7z              81%
████████████████░░

Extracting:
models/model.bin


C.rar
Waiting for available worker...
```

A 完成：

```text
A.zip
✓ Completed

[打开源目录] [打开目标目录]
```

随后 C 自动开始：

```text
C.rar              1%
```

整个过程中软件 UI 始终能够正常拖动窗口、滚动列表和点击按钮。

---

# 三十二、请特别分析一个问题

在正式写代码前，请先重点回答：

**“使用 7z.exe CLI 时，怎样最可靠地获得单个解压任务的实时百分比进度？”**

请结合 7-Zip 实际 stdout / stderr / progress stream 的行为分析：

```text
-bsp
-bso
-bse
-bb
```

等参数。

重点分析：

1. 7-Zip 的进度到底输出到哪个 Stream
2. 是否使用 `\r` 原地刷新
3. `ReadLineAsync()` 是否可靠
4. 怎样解析实时 %
5. 怎样获得 CurrentFile
6. 怎样处理进度输出和普通日志输出混在一起
7. 多个 7z.exe 同时运行时怎样保证进度互不干扰
8. Process RedirectStandardOutput 是否存在缓冲问题
9. 是否需要直接读 BaseStream
10. Windows 下不同 7-Zip 版本是否有差异
11. 有没有比解析 stdout 更可靠的方式
12. 如果 CLI 无法获得某项数据，请明确说明，不要凭空编造

如果 7-Zip CLI 在某些情况下不能准确获得：

```text
CurrentFile
实时字节数
解压速度
```

请明确告诉我限制在哪里，并设计合理的降级方案。

---

# 三十三、开发方式

不要直接一次性生成几十个文件然后告诉我“完成了”。

请按照下面的方式工作：

```text
1. 分析架构
2. 分析 7-Zip 实时进度监控方案
3. 给出最终技术方案
4. 创建项目骨架
5. 实现 Extraction Model
6. 实现 SevenZipProgressParser
7. 为 Parser 写测试
8. 实现 SevenZipExtractionService
9. 验证单任务解压
10. 实现 WPF Task Card
11. 实现多任务 Queue
12. 最后完善 UI
```

每完成一个关键模块，都检查：

```text
是否能编译
是否存在线程问题
是否存在资源泄漏
是否正确响应 CancellationToken
```

如果发现设计需要调整，可以重构，不要为了保持最初代码而继续堆补丁。

---

最终目标不是 Demo，而是做一个可以长期使用的：

**Windows 现代化多任务解压管理器。**

现在先不要急着大量生成代码。

第一步请先：

**分析项目架构，并重点研究、验证 7-Zip CLI 实时解压进度的最佳监控方式，然后给出推荐实现方案。**

这个提示词里我特意把你最关心的**“各个文件解压进度怎么监控”**放到了核心位置。比较理想的架构其实就是：

`一个解压任务 = 一个 7z.exe Process = 一个 stdout/progress parser = 一个 CancellationTokenSource = 一个 TaskViewModel`

这样即使同时解压 5 个压缩包，5 个进度也天然是隔离的，不需要去猜“这一条 35% 到底属于哪个压缩包”。

另外我建议**第一版先别追求“MB/s 解压速度”**。7-Zip CLI 最容易稳定取得的是百分比，实时解压字节数/速度未必能作为可靠的结构化数据获得。先把 **百分比 + 当前文件 + 耗时 + 状态 + 队列 + 取消** 做扎实，整个软件的主体就已经成立了。
