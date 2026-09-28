# 摸鱼

Windows 桌面状态模拟工具，当前版本 **0.3.1**。

## 使用

从 [Releases](https://github.com/LynViii/moyu/releases/latest) 下载 `moyu.exe` 后直接运行，或解压 ZIP 内的 `摸鱼.exe`，两者内容相同。先选 Windows 10 / Windows 11，再选择正在更新、正在重启或准备 Windows，点击“开始摸鱼”。Esc 返回首页。首页可正常关闭，全屏期间拦截普通关闭操作；系统级切换快捷键保留。

- 窗口内动态预览，不进入全屏。
- 自动返回：不限时、15/30/60 分钟，或自定义 1–240 分钟。
- 三档动画速度，窗口预览与全屏同步。
- 可取消的 1–60 秒延迟开始；点击倒计时按钮或 Esc 取消。
- 返回首页显示本次用时；统计仅存在当前进程内。
- 默认主屏显示状态，也可指定其他屏幕；其余屏幕可选择是否覆盖为黑色。
- Win10 默认蓝底、Win11 默认黑底，另有自定义浅色；配色可独立选择。
- 中英文状态文案、小/标准/大三档字型；默认流畅刷新，可选节能模式。
- 更多设置内可恢复默认、离线查看版本与更新日志。
- 自动记住状态、时长和扩展屏选项。
- 单实例运行，重复启动唤起已有窗口。
- 响应显示器变化并重建覆盖窗口，不重置会话计时。
- 首页场景缩略图随选项更新；小屏可滚动，开始按钮保持可见。
- 自定义配色不会因切换场景丢失；离线显示器选择保留，运行时临时回退。
- 长文案自动换行，文字与圆点整体居中；保存设置失败会显示提示。

## 环境与数据

Windows x64，需 .NET Framework 4.x；构建与测试使用系统自带 Framework 编译器。
仅运行 exe 即可，图片、图标和更新日志已嵌入，可复制到其他文件夹单独运行。偏好设置位于 `%LOCALAPPDATA%\Moyu\settings.xml`，不随程序上传。
不提供方案管理、设置导入导出、持久会话历史或 CSV 导出，只自动记住上次选项。
不执行真实更新或重启。圆点动画为自绘复现，并非 Windows 系统资源。程序尚未做代码签名。

## 构建

快捷方式的目标后可以附加启动参数，例如：

```powershell
.\摸鱼.exe --mode update --minutes 25 --speed normal --primary-only --start
```

状态取值 `update/restart/prepare`，时长 `0..240`（0 不限时），速度 `slow/normal/fast`。
省略 `--start` 只填入选项；`--start` 遵循已保存的延迟设置。
已有全屏会话时重复启动只唤起会话，不重置参数或计时。

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
# Or run the complete suite:
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\run_all.ps1
```

## 验证与限制

已验证 200% DPI 首页布局、设置恢复、单实例交接、预览关闭、仅主屏覆盖、显示器事件重建、Esc 和自动返回清理。物理显示器插拔与混合 DPI 多屏尚未完整实测。定时截止使用模拟经过时间验证。

新版本额外覆盖场景与配色矩阵、动态圆点像素变化、字号、节能刷新、设置取消与默认值、目标屏幕回退、自定义倒计时和快捷键。DPI-unaware 测试用于逻辑 96 DPI 兼容检查，不等同于另一台物理 100% 缩放显示器。

0.3.1 的修复验收与动画测量见 [归档前检查](docs/ARCHIVE_CHECK.md)。帧间隔由实际 Paint 回调测量，不保证所有设备恒定 60 FPS，也不代表显示器呈现延迟。小于设计宽度的极窄窗口允许水平滚动，保证选项可达；主按钮独立于滚动区域。

## 首页快捷键

| 快捷键 | 操作 |
| --- | --- |
| Ctrl+1 / 2 / 3 | 选择更新、重启、准备 |
| Ctrl+P | 预览 |
| Ctrl+Enter | 开始或取消倒计时 |
| Ctrl+, | 更多设置 |
| Esc | 退出全屏、退出预览或取消倒计时 |

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
