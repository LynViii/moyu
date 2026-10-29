# 摸鱼

Windows 桌面状态模拟工具，当前版本 **0.5.0**。

## 使用

从 [Releases](https://github.com/LynViii/moyu/releases) 下载对应版本的 `moyu.exe` 后直接运行，或解压 ZIP 内的 `摸鱼.exe`，两者内容相同。先选 Windows 10 / Windows 11，再选择正在更新、正在重启或准备 Windows，选择倒计时后点击“开始摸鱼”。关闭首页默认驻留右下角托盘。双击托盘恢复窗口，右键菜单可完整退出。

- 窗口内动态预览，不进入全屏。
- 全局 Alt+L 快捷启动，提示框倒计时后进入伪装，支持取消和立即开始。
- 三档动画速度，窗口预览与全屏同步。
- 首页直接选择 1–60 秒后开始；点击倒计时按钮、Esc 或再次按全局快捷键取消。
- 返回首页显示本次用时；统计仅存在当前进程内。
- 默认主屏显示状态，也可指定其他屏幕；其余屏幕可选择是否覆盖为黑色。
- Win10 默认蓝底和经典圆点、Win11 默认黑底和连续圆弧，另有自定义浅色；配色可独立选择。
- 中英文状态文案、小/标准/大三档字型；默认流畅刷新，可选节能模式。
- 更多设置内可恢复默认、离线查看版本与更新日志。
- 自动记住状态、倒计时、显示器和扩展屏选项。
- 可选开机静默驻留，当前用户生效，默认关闭；在更多设置的常驻与快捷键页开启。
- 伪装期间可选择在 1–30 秒后收起前台开始菜单，默认 3 秒；Win 键仍可正常使用。
- 取消自动退出；默认 Esc 返回，可在设置中更换退出按键，托盘始终保留结束伪装。从托盘启动的伪装结束后返回托盘。
- 首页或托盘检查更新，查看说明后下载并安装；不会后台自动更新。
- 单实例运行，重复启动唤起已有窗口。
- 响应显示器变化并重建覆盖窗口，不重置会话计时。
- 首页场景缩略图随选项更新；小屏可滚动，开始按钮保持可见。
- 自定义配色不会因切换场景丢失；离线显示器选择保留，运行时临时回退。
- 长文案自动换行，文字与动画整体居中；保存设置失败会显示提示。

## 更新与退出

从 0.5.0 起，首页和托盘的“检查更新”会读取本仓库最新正式发行版。发现新版后，点击“下载并安装”完成校验和原位更新；保留 EXE 路径与已有设置，新版启动失败会恢复旧版。首次从 0.4.0 升级需要先手动下载一次 0.5.0。文件夹必须可写，不能更新时也可从发行页面手动下载。更新仅在主动打开窗口时检查，不收集使用记录。下载临时文件在本地 updates 目录暂存，过期已知文件会清理。

设置中的“常驻与快捷键 → 退出伪装”提供 Esc、连续两次 Esc、方向键序列、Ctrl+Alt+Q 和自定义录制。序列按顺序逐次按下，相邻按键间隔不超过 1.5 秒；按住不放不会计为多次。设置会拒绝系统保留组合键及与全局启动快捷键冲突的按键。退出方式会显示在开始提示框中，忘记时可从托盘结束伪装。不拦截 Ctrl+Alt+Del 等系统安全组合键。

## 环境与数据

Windows x64，需 .NET Framework 4.x；构建与测试使用系统自带 Framework 编译器。
仅运行 exe 即可，图片、图标和更新日志已嵌入，可复制到其他文件夹单独运行。偏好设置位于 `%LOCALAPPDATA%\Moyu\settings.xml`，不随程序上传。
不提供方案管理、设置导入导出、持久会话历史或 CSV 导出，只自动记住上次选项。
不执行真实的 Windows 系统更新或重启。圆弧与圆点动画为自绘近似，并非 Windows 系统资源。程序尚未做代码签名。
开机启动项为当前用户 Run 键的 Moyu 值。启用后请保持 EXE 路径有效；移动程序后在设置中重新应用开机启动选项。Windows 任务管理器也可能单独禁用启动项。不会创建系统服务。

## 构建

快捷方式的目标后可以附加启动参数，例如：

```powershell
.\摸鱼.exe --mode update --speed normal --primary-only --start
```

状态取值 `update/restart/prepare`，速度 `slow/normal/fast`。旧 `--minutes` 参数不再触发自动退出。
省略 `--start` 只填入选项；`--start` 遵循已保存的延迟设置。
已有全屏会话时重复启动只唤起会话，不重置参数或计时。
`--silent` 静默驻留托盘；重复以该参数启动不会唤起首页。`--exit` 完整退出已运行实例。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build_exe.ps1
```

`src/VersionInfo.cs` 是应用版本唯一来源：首页、产品版本和文件版本均引用它。修改功能时同步递增版本并追加 `CHANGELOG.md`；普通重试构建不计为一次新发布。
manifest 的 `1.0.0.0` 是固定部署标识，不是产品版本。

回归检查（会短暂显示测试窗口并自动关闭）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\verify_modes.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\verify_scenes.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\verify_scenes.ps1 -DpiUnaware
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\verify_executable.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\verify_fixes.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\verify_desktop.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\verify_exit.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\verify_updates.ps1
# Optional live GitHub download and UI check:
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\verify_updates.ps1 -Live
# Or run the complete suite:
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\run_all.ps1
```

## 验证与限制

0.5.0 的更新助手、可自定义退出按键及完整回归结果见 [本轮验证](docs/VALIDATION_0.5.0.md)。应用更新不等于 Windows 系统更新；HTTPS、SHA-256 与产品信息校验也不等于发布者数字签名。图标沿用原版，未采用被否定的生成图标提案。

当前验证覆盖 DPI 布局、设置恢复、单实例交接、预览、两屏覆盖、显示器事件重建、Esc 清理、托盘驻留、全局快捷键注册与冲突、倒计时弹窗、静默参数和真实开机项启停。测试会恢复原开机项，不代替用户开启开机启动。物理显示器插拔与混合 DPI 多屏尚未完整实测。

新版本额外覆盖场景与配色矩阵、动态圆点像素变化、字号、节能刷新、设置取消与默认值、目标屏幕回退、自定义倒计时和快捷键。DPI-unaware 测试用于逻辑 96 DPI 兼容检查，不等同于另一台物理 100% 缩放显示器。

0.3.1 的修复验收与动画测量见 [归档前检查](docs/ARCHIVE_CHECK.md)。帧间隔由实际 Paint 回调测量，不保证所有设备恒定 60 FPS，也不代表显示器呈现延迟。小于设计宽度的极窄窗口允许水平滚动，保证选项可达；主按钮独立于滚动区域。
0.4.0 的桌面集成结果见 [本轮验证](docs/VALIDATION_0.4.0.md)。开始菜单真实闭环在本次前台程序抢焦点的环境未验证，测试会明确打印 NOT VERIFIED；计时、重开重置和非目标窗口拒绝已验证。完整测试需要普通交互桌面权限，受限沙箱可能拒绝全局快捷键注册。

## 首页快捷键

| 快捷键 | 操作 |
| --- | --- |
| Alt+L（可更换） | 全局快捷启动，再按取消倒计时 |
| Ctrl+1 / 2 / 3 | 选择更新、重启、准备 |
| Ctrl+P | 预览 |
| Ctrl+Enter | 开始或取消倒计时 |
| Ctrl+, | 更多设置 |
| Esc | 默认退出全屏（可更换）；退出预览或取消倒计时 |

## 仓库结构

```text
src/         C# 源码、版本信息和 manifest
assets/      应用图标源文件
scripts/     构建、图标生成与启动脚本
tests/       GUI 与行为回归测试
docs/        参考说明、历史文档与逐版发布记录
build_exe.ps1 兼容保留的构建入口
README.md    使用与构建说明
CHANGELOG.md 完整交付记录
```

`.build/`、`dist/` 和 EXE 不提交到 Git。构建后仍在本机原位置生成 `摸鱼.exe`，已有快捷方式无需修改。安装包附件放到 GitHub Releases，不重写旧版本提交。运行 `scripts/package.ps1` 可生成版本 ZIP 和 SHA-256 校验文件。打包前应先完成构建与测试。

参见 [更新日志](CHANGELOG.md)、[参考项目](docs/REFERENCES.md)。本仓库未指定开源许可证，上传源码不等于授予任意再分发许可。
