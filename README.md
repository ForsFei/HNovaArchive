# HNova Archive（原 HDecompressHelper）

一个基于 `.NET 8 + WPF` 的桌面端多任务解压管理器。程序不自行实现解压算法，而是为每个任务启动独立的 7-Zip 或 WinRAR 命令行进程，集中展示进度、当前文件、任务状态、历史记录和目录直达操作。

## 第四版功能

- 多选、拖放或启动参数添加压缩文件，默认最多并行解压 2 个任务。
- 每个任务独立显示百分比、当前文件、耗时、状态、解压引擎和错误信息。
- 一键在 Explorer 中定位源压缩包或打开解压目标目录。
- 历史记录持久化，最多保留 100 条；异常退出时未结束任务会恢复为“已中断”。
- 列表/平铺显示切换，默认列表；选择会随设置保存。
- 深色/浅色背景即时切换，默认深色；无需重启窗口。
- 7-Zip/WinRAR 引擎切换，默认 7-Zip；任务创建后会锁定当时选择的引擎。
- 密码输入始终启用并默认明文显示；解压成功的非空密码最多保存 50 条，可从下拉列表切换或删除。
- 当前密码失败后，会按最近成功顺序尝试密码列表；密码大小写敏感，相同值不会重复尝试。
- 支持直接加载 `.7z.001`、`.zip.001`、`.rar.001` 分卷首卷。
- `.7z.11111` 这类附加后缀会剥离为 `.7z`；未知扩展名（如 `.mp4`）会改为 `.zip` 后加载。
- 取消等待或运行中的任务；运行任务会终止其对应的解压进程树。

> [!WARNING]
> 密码完全不加密。它会以明文出现在 `%AppData%\HDecompressHelper\settings.json`、`history.json`、任务详情日志以及解压程序命令行中。任何能读取这些文件或查看本机进程命令行的程序/用户都可能看到密码。请勿在不可信电脑上保存敏感密码。

支持添加 ZIP、7Z、RAR、TAR、GZ、XZ、BZ2、CAB、ISO、WIM 等常见扩展名；实际能否解压取决于当前选择的解压引擎。自动重命名只改变文件名，不会转换文件内容；如果文件本身不是压缩数据，解压仍会失败。改名发生在原目录，目标文件已存在时不会覆盖，并会跳过该文件。

## 下载安装

Windows 10/11 x64 用户可从 [GitHub Releases](https://github.com/ForsFei/HNovaArchive/releases/latest) 下载最新的 `HNovaArchive-*-win-x64-Setup.exe`。

安装包是自包含版本，目标电脑无需另装 .NET 8 Desktop Runtime；安装后会创建开始菜单入口，并可在向导中选择是否创建桌面快捷方式。应用仍需使用本机已有的 7-Zip 或 WinRAR 作为解压引擎。

当前安装包尚未进行代码签名，因此 Windows 可能显示“未知发布者”或 SmartScreen 提示。可使用 Release 中同时提供的 `SHA256SUMS.txt` 核对文件完整性。

## 运行 Release 构建

执行 `dotnet build .\HDecompressHelper.sln -c Release` 后，主程序位于：

```text
src\HDecompressHelper\bin\Release\net8.0-windows\HDecompressHelper.exe
```

请保留该目录中的 `.dll`、`.json` 和 `.exe` 文件在一起，不要只复制可执行文件。运行要求 Windows 10/11 和 .NET 8 Desktop Runtime，并且至少安装 7-Zip 或 WinRAR 中的一种。

## 构建安装包

安装 Inno Setup 6 或 7 后，在仓库根目录执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build-Installer.ps1
```

脚本会发布 `win-x64` 自包含单文件程序，并在以下目录生成带版本号的安装包：

```text
artifacts\installer\HNovaArchive-1.0.0-win-x64-Setup.exe
```

安装包包含开始菜单快捷方式、可选桌面快捷方式和标准卸载入口。目标电脑无需另装 .NET 8 Desktop Runtime，但仍需安装 7-Zip 或 WinRAR 中至少一种解压引擎。要指定新版本号，可传入 `-Version 1.1.0`；版本格式为三段数字。

## 持久化文件

应用使用当前 Windows 用户的漫游配置目录：

```text
%AppData%\HDecompressHelper\settings.json
%AppData%\HDecompressHelper\history.json
```

`settings.json` 保存主题、视图模式、默认引擎、7-Zip/WinRAR 路径、最后输入的明文密码和最近成功密码列表。`history.json` 保存任务路径、引擎、状态、进度、时间、错误、完整详情日志和任务明文密码。

写入采用同目录临时文件后覆盖目标文件。JSON 损坏或无法读取时，程序会回退到默认设置或空历史，不阻止启动。

## 解压引擎

### 7-Zip（默认）

程序依次查找用户选择路径、应用目录 `Tools\7zip\7z.exe`、Program Files 和 `PATH`。核心命令为：

```text
7z.exe x <archive> -o<destination> -y -bsp1 -bso1 -bse2 -bb1 -sccUTF-8 [-p<password>]
```

- `-bsp1` 将进度发送到 stdout。
- `-bso1` 保留普通输出，`-bse2` 保留错误输出。
- `-bb1` 输出已处理文件名，作为当前文件的尽力而为来源。
- `-sccUTF-8` 让重定向输出中的中文文件名按 UTF-8 读取。

7-Zip 使用 `\r` 原地刷新进度，因此项目直接读取字符流，把 `\r` 和 `\n` 都作为记录边界，而不是只使用 `ReadLineAsync()`。成功与否始终以退出码判断，不以进度达到 100% 代替。

### WinRAR

程序可定位或手动选择 `Rar.exe`、`UnRAR.exe`、`WinRAR.exe`，使用 `x -o+ -y [-p<password>]` 解压。WinRAR 是商业试用软件，本项目不会捆绑、下载或安装它；需要用户自行拥有可用安装。命令语法和版本说明可参考 [RARLAB 命令行文档](https://www.rarlab.com/rar_archiver.htm) 与 [WinRAR 下载页](https://www.rarlab.com/download.htm)。

WinRAR 的文本输出不是稳定的机器事件协议。程序会从 `\r`/`\n` 字符流尽力解析总百分比和 `Extracting`/`Testing` 文件名；不同版本或使用 GUI `WinRAR.exe` 回退时，当前文件或实时百分比可能为空。退出码仍是最终成功/失败依据。

## 任务隔离

```text
一个任务 = 一个解压进程
         = 一个流式进度解析器
         = 一个 CancellationTokenSource
         = 一个 ExtractionTaskViewModel
```

不同任务不会混用进度或密码。切换顶部默认引擎、密码或主题，不会改变已经创建的任务。

## 密码重试与效率

每个任务先尝试创建任务时的当前密码；发生可重试的致命/未知解压错误后，再按“最近成功优先”尝试保存列表。空密码、当前密码和完全相同的重复值会跳过；大小写不同的密码视为不同值。写入错误、命令行错误、内存不足和用户取消等明确非密码错误不会继续遍历。

密码功能始终启用不等于每个任务都会产生额外开销：文本框为空时，程序会关闭隐藏命令行的标准输入，WinRAR 还会使用 `-p-` 禁止交互询问；加密包会立即进入保存密码重试，或明确提示需要输入密码，不会停在 0%。有密码但归档未加密时，解压工具通常直接忽略它。正常成功路径不会扫描成功密码列表。

重试复用原有解压队列，同一任务任一时刻只运行一个解压进程，不会把几十个密码同时启动。最坏耗时近似为单次失败耗时乘以候选密码数量，即 `O(N)`；对需要读完整归档才能确认密码错误的格式，大文件和较长密码列表会明显变慢。列表上限 50 条用于控制最坏重试次数。每次尝试的密码、命令、退出码、stdout 和 stderr 都按需求明文写入任务详情与历史。

每个进程的 stdout 和 stderr 分别最多保留 512 KiB，避免异常工具输出无限占用内存；达到上限后的后续内容会被截断，并在日志末尾标注。

## 从源码构建

首次启动默认使用 7-Zip。开发环境需要 .NET 8 SDK 或能够构建 `net8.0-windows` 的更新版 SDK。

```powershell
dotnet build .\HDecompressHelper.sln
dotnet run --project .\src\HDecompressHelper\HDecompressHelper.csproj
```

也可以把压缩文件作为启动参数：

```powershell
dotnet run --project .\src\HDecompressHelper\HDecompressHelper.csproj -- "D:\Downloads\demo.7z"
```

构建和测试会重新生成 `.vs`、`obj`、Debug 输出及测试项目的 `bin` 等临时目录；这些目录均不属于源码，可以随时删除。主程序 Release 输出位于前述 `src\HDecompressHelper\bin\Release\net8.0-windows`。

## 测试

```powershell
dotnet test .\HDecompressHelper.sln -c Release
```

当前 65 项测试覆盖：7-Zip/WinRAR 流式进度解析、退出码、非交互密码处理、命令参数和明文密码日志；成功密码持久化、顺序重试与停止条件；异常扩展名改名和冲突保护；JSON 设置/历史往返与损坏降级；任务历史中断恢复；默认目标路径；并发上限和等待取消。本机安装 7-Zip 时还会运行真实加密归档缺失密码提示、密码重试、`.7z.001` 分卷和包含中文及空格文件名的 ZIP 解压集成测试。

## 已知限制

- 两种 CLI 都没有在所有格式和版本上稳定一致的结构化逐文件事件，`CurrentFile` 属于 best-effort 信息。
- 当前不展示可能误导的精确已解压字节数或 MB/s；压缩包文件大小不能直接代表解压后的总字节数。
- 平铺模式使用 `WrapPanel`，因此不启用列表模式的纵向 UI 虚拟化；历史上限 100 条用于约束界面和 JSON 体积。
- 7-Zip/WinRAR 的退出码通常不能精确区分“密码错误”和“归档损坏”，因此致命/未知错误可能触发一次无效的密码列表遍历；可取消任务或缩短保存列表来控制耗时。
- 多开应用实例会同时读写同一组用户设置和历史文件，最后关闭/写入的实例会覆盖更早实例的内容；建议正常使用时只开一个实例。
