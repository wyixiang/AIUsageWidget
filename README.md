# AI 用量 · AI Usage Widget

Windows 11 任务栏用量工具，在同一个显示条中查看 Codex 和 Cursor 的套餐、剩余额度与重置时间。

使用 WPF、.NET 8 和 Windows Acrylic 毛玻璃效果。以与任务栏对齐的独立窗口运行，支持多显示器，不需要向 Explorer 注入 DLL。

## 功能

| 平台 | 显示内容 |
| --- | --- |
| Codex | 套餐、5 小时和每周剩余额度、具体重置时间及剩余时长 |
| Cursor | 套餐、本周剩余额度和重置时间、独立 Credit 余额；旧套餐按接口返回的周期显示 |

- 每分钟自动刷新，支持手动刷新；读取失败时保留上次数据并提示。任务栏保留额度百分比，并用条形长度表示剩余用量；条形末端以 `35m`、`5d6h` 配合刷新图标标注重置倒计时。额度名称和百分比在上方，用量条居中，重置倒计时在下方；各额度保持一致的轨道宽度和数字对齐，任务栏简写使用 `5h`、`Wk`、`Credit`。倒计时每秒独立更新，省略零值单位，每周额度不足一天时改用小时和分钟。详情保留中文剩余时间。
- 任务栏显示条支持点击对应平台进入详情，悬停查看完整信息。
- 默认横向并排，使用 18 DIP 百分比和等宽额度轨道；横向所需宽度超过上限时自动恢复两行叠放。隐藏某个平台或 Credit 后重新计算宽度，可自动回到横向，不保留空白列。
- 右键“布局宽度”提供紧凑、标准、宽松三档，横向上限分别为 440、520、640 个逻辑像素，默认标准；叠放模式延续原来的紧凑字号和对齐方式。
- 右键“显示内容”分别开关 Codex、Cursor 和 Cursor Credit。默认显示两个平台、折叠 Credit；详情中的 Credit 也可以展开/折叠，并与任务栏设置同步。两个平台都关闭时隐藏信息卡片，仍可从托盘右键菜单重新开启。
- 详情贴在用量显示条上方，点击其他位置自动收起；切换平台或内容高度变化后重新贴齐，并限制在当前屏幕工作区内。
- 圆角与 Acrylic 效果；颜色支持跟随系统、浅色、深色。
- 显示区域菜单从 Windows 读取显示器编号、名称、分辨率和主屏标记。
- 保存物理显示器标识，避免休眠唤醒后编号重排导致位置漂移；目标屏暂时不可用时等待 15 秒，再临时回到主屏，重连后自动恢复。
- 支持固定位置和解锁拖动；任务栏隐藏或目标屏有全屏应用时隐藏显示条。
- 详情不显示普通应用任务栏按钮；最小化和关闭详情均收起到系统托盘，保留任务栏信息卡片；右键菜单中的“退出”结束程序。
- 重复启动会唤回已有窗口，不会创建多个实例。默认不添加开机自启。
- 启动时检查继承的包身份和宿主进程组，必要时先独立重启，避免随开发工具更新或关闭一起退出。额度查询使用本机登录状态，不要求 Codex 桌面窗口保持开启。

## 环境要求

- Windows 11 x64。
- 构建需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。
- 运行需要 .NET 8 Windows Desktop Runtime，只有普通 .NET Runtime 不够。
- Codex 额度需要本机已安装并登录 Codex 桌面版。
- Cursor 额度需要本机已安装并登录 Cursor。

## 构建与启动

```powershell
git clone https://github.com/wyixiang/AIUsageWidget.git
cd AIUsageWidget
.\build.ps1
.\dist\AIUsageWidget.exe
```

也可以双击 `dist` 中的 `启动.vbs`，或通过 `退出.vbs` 结束程序。首次启动只显示常驻卡片，点击卡片展开详情；需要启动时立即显示详情可运行 `AIUsageWidget.exe --details`。更新构建前，请先退出正在运行的工具，避免输出文件被占用。

从开发工具启动时，会在取得单实例锁前检查 Windows 包身份和 Job 的退出约束，使用 Windows 的 [Desktop App Policy](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-updateprocthreadattribute) 与 [进程创建标志](https://learn.microsoft.com/en-us/windows/win32/procthread/process-creation-flags) 请求独立运行，并检查子进程确实没有包身份和 Job 绑定。宿主禁止脱离时会提示从资源管理器双击启动，避免循环重启或假装启动成功。

构建为 framework-dependent 的 Windows x64 应用；仓库只包含源码，不提交个人运行目录。

## 使用

| 操作 | 结果 |
| --- | --- |
| 单击任务栏显示条的 Codex / Cursor 行 | 在显示条上方展开相应平台详情 |
| 点击详情以外的位置 | 收起详情，显示条和后台刷新继续运行 |
| 悬停显示条 | 查看套餐、重置时间和 Credit |
| 右键 → 颜色 | 选择跟随系统、浅色或深色 |
| 右键 → 显示区域 | 选择当前连接的显示器 |
| 右键 → 显示内容 | 开关 Codex、Cursor 和 Cursor Credit |
| 右键 → 布局宽度 | 选择自动切换横向/叠放布局的宽度档位 |
| 取消“固定位置”后拖动 | 调整横向位置并保存 |
| 详情中的最小化按钮 | 收起详情，保留托盘入口与任务栏信息卡片 |
| 详情中的关闭按钮 / Alt+F4 | 隐藏详情，保留托盘入口 |
| 双击系统托盘图标 | 重新打开详情 |
| 右键 → 退出 | 结束程序并移除显示条与托盘图标 |

Credit 是独立余额，不随每周套餐额度重置。没有返回的余额或重置时间会显示为暂不可用，不会用 0 或账单日期代替。

## 数据来源与本机文件

### Codex

自动查找 Codex 桌面版自带的 CLI，通过本机 `codex app-server` 的 `account/rateLimits/read` 查询。工具不读取 Codex 认证文件，不发送模型提示词。

### Cursor

只读当前用户的 `%APPDATA%\Cursor\User\globalStorage\state.vscdb`，读取当前会话；支持 Windows safeStorage 加密格式，通过当前用户的 DPAPI 解密，令牌仅在内存中使用。

查询只发往 Cursor 官方 `https://api2.cursor.sh`，使用 `GetCurrentPeriodUsage`、`GetPlanInfo`、`GetUserPrepaidBilling` 和 `GetComplatCreditBalance`。Credit 优先读取 Cursor 的预付余额，避免与另一个 Commerce 钱包混用。不会复制数据库、保存明文令牌、修改登录状态或自动发起充值。

这些 Cursor 个人用量接口并非稳定公开 API，服务更新后可能需要适配。不支持的账号仍可通过详情中的按钮进入官方用量页查看。

### 配置与诊断

下列文件在运行目录生成，已加入 `.gitignore`：

| 文件 | 用途 |
| --- | --- |
| `preferences.json` | 配色、位置、屏幕与物理设备绑定 |
| `运行状态.json` | 本机窗口状态、刷新时间和额度诊断 |

启动与退出诊断保存在 `%LOCALAPPDATA%\AIUsageWidget\logs`：

| 文件 | 用途 |
| --- | --- |
| `lifecycle.jsonl` | 启动、退出请求、异常与结束记录；保留两份轮换历史，每份最多 512 KiB |
| `session.json` | 当前进程、启动时间和最近心跳（约 30 秒更新） |
| `last-exit.json` | 上次结束状态、退出原因、异常类型及堆栈 |

`lifecycle.jsonl` 的 `launch-environment` 和 `运行状态.json` 的 `LaunchEnvironment` 记录实际包身份及 Job 状态。独立运行时 `PackageFullName` 为 `null`，`InJob` 为 `false`。启动隔离失败时，在程序旁的 `logs/launch-error.json` 保存错误类型和代码，不覆盖主实例会话。旧版本从打包的开发工具中启动时，Windows 可能把原日志重定向到该工具的包目录 `LocalCache/Local/AIUsageWidget/logs`；查找旧记录时应同时核对该位置。

日志不保存异常消息、账号令牌、接口响应或额度。被强制终止或断电时可能无法当场写入退出原因，下次启动会标记上次未正常结束，原因未知时不会推测。`运行状态.json` 是最后一次快照，是否仍在运行应结合进程与心跳判断。

仓库不包含上述文件、认证文件、浏览器会话、Cursor 数据库或构建产物。工具不向第三方服务上传使用数据。

## 验证

```powershell
dotnet run --project tests/ParserChecks.csproj -c Release
dotnet build tests/launch/LaunchIsolationChecks.csproj -c Release
.\tests\launch\bin\Release\net8.0-windows\LaunchIsolationChecks.exe
```

检查覆盖额度百分比转换、Credit 金额与钱包选择、周重置时间、缺失字段、平台隐藏与 Credit 折叠、横向/叠放的临界宽度，以及显示器编号重排、唤醒重连等待和断开后的恢复逻辑。屏幕休眠与唤醒需要在实际硬件上进一步观察。

启动检查使用临时宿主进程和两种带有退出约束的 Windows Job，验证宿主结束后独立进程仍然存活、参数原样传递且不会循环重启，不操作正在运行的小组件或 Codex。该检查需在构建后直接执行 EXE；`dotnet run` 自身附加的进程组可能禁止脱离，不能用于这项独立启动回归。

## 源码结构

```text
src/
  CodexClient.cs           Codex 本机额度查询
  CursorClient.cs          Cursor 套餐、额度和余额查询
  CursorSessionDecoder.cs Windows safeStorage / DPAPI 读取
  DockWindow.cs            任务栏显示条和定位
  DetailsWindow.cs         平台详情窗口
  ModernSurface.cs         圆角界面与分级菜单
  DisplayCatalog.cs        Windows 显示器信息、系统主题
  MonitorSelection.cs      物理屏幕选择和重连逻辑
  Program.cs               生命周期、单实例、刷新和托盘
  LaunchIsolation.cs       独立启动、包身份与宿主 Job 检查
scripts/                   启动和退出入口
tests/                     解析与显示器选择检查
build.ps1                  构建入口
```

## 限制

- 显示条叠放在任务栏上，不会自动为系统任务栏按钮腾出位置，请放在空白区域。
- 目前面向横向 Windows 11 任务栏和个人账号使用；镜像显示、远程桌面和部分团队套餐尚未全面验证。
- 更换显示接口或显卡驱动后，物理设备路径可能变化，可在“显示区域”中重新选择。
- 旧版 Windows 原生小组件属于独立工程，不包含在此仓库。

当前版本 **1.5.0** 支持自适应任务栏布局、平台显隐和 Credit 折叠。

## 图标许可

平台与应用标识采用本地嵌入的 Lucide `terminal`、`mouse-pointer-2` 与 `circle-gauge` 矢量图标，许可和来源见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
