# 摸鱼

Windows 桌面状态模拟工具，当前版本 **0.2.0**。

## 使用

下载 `摸鱼.exe` 后直接运行。选择正在更新、正在重启或准备 Windows，再点击“开始摸鱼”。Esc 返回首页。首页可正常关闭，全屏期间拦截普通关闭操作；系统级切换快捷键保留。

- 窗口内动态预览，不进入全屏。
- 自动返回：不限时、15/30/60 分钟，或自定义 1–240 分钟。
- 三档动画速度，窗口预览与全屏同步。
- 可取消的 5 秒延迟开始；点击倒计时按钮或 Esc 取消。
- 返回首页显示本次用时；统计仅存在当前进程内。
- 主屏显示状态，可选择是否将扩展屏覆盖为黑色。
- 自动记住状态、时长和扩展屏选项。
- 单实例运行，重复启动唤起已有窗口。
- 响应显示器变化并重建覆盖窗口，不重置会话计时。

## 环境与数据

Windows x64，需 .NET Framework 4.x；构建与测试使用系统自带 Framework 编译器。
仅运行 exe 即可，图片和图标已嵌入。偏好设置位于 `%LOCALAPPDATA%\Moyu\settings.xml`，不随程序上传。
不执行真实更新或重启。圆点动画为自绘复现，并非 Windows 系统资源。程序尚未做代码签名。

## 构建

快捷方式的目标后可以附加启动参数，例如：

```powershell
.\摸鱼.exe --mode update --minutes 25 --speed normal --primary-only --start
```

状态取值 `update/restart/prepare`，时长 `0..240`（0 不限时），速度 `slow/normal/fast`。
省略 `--start` 只填入选项；`--start` 遵循已保存的 5 秒延迟设置。
已有全屏会话时重复启动只唤起会话，不重置参数或计时。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build_exe.ps1
```

`VersionInfo.cs` 是应用版本唯一来源：首页、产品版本和文件版本均引用它。修改功能时同步递增版本并追加 `CHANGELOG.md`；普通重试构建不计为一次新发布。
manifest 的 `1.0.0.0` 是固定部署标识，不是产品版本。

回归检查（会短暂显示测试窗口并自动关闭）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\verify_modes.ps1
```

## 验证与限制

已验证 200% DPI 首页布局、设置恢复、单实例交接、预览关闭、仅主屏覆盖、显示器事件重建、Esc 和自动返回清理。物理显示器插拔与混合 DPI 多屏尚未完整实测。定时截止使用模拟经过时间验证。

参见 [更新日志](CHANGELOG.md)、[参考项目](REFERENCES.md)。本仓库未指定开源许可证，上传源码不等于授予任意再分发许可。
