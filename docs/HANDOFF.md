# 摸鱼封存与接管

封存基线：**0.5.1 / v0.5.1 / 2026-11-12**。源码与发行包位于公开仓库 [LynViii/moyu](https://github.com/LynViii/moyu)，后续可直接下载或继续开发。此次仅做本地封存，不将远程仓库设为只读，不删除当前工作区，不停用应用。

## 当前目录

- 原工作副本：`C:\Users\liyua\Documents\Codex\2026-06-15\windows-esc\outputs`。
- 原运行程序：该目录的 `摸鱼.exe`；已有开始菜单快捷方式继续指向原位置。
- 封存目录：上级的 `archives\moyu-0.5.1-2026-11-12`，先阅读其 `START_HERE.md`。
- 个人偏好仍在 `%LOCALAPPDATA%\Moyu\settings.xml`，不打进源码/封存发行包。应用无方案导入导出、持久历史或 CSV 功能。

## 封存包内容

| 路径 | 用途 |
| --- | --- |
| `release/` | 最终 EXE、便携 ZIP、发布 SHA-256 |
| `source/moyu-0.5.1-source.zip` | 干净的已提交源码、图标、脚本和文档 |
| `history/moyu-all.bundle` | 所有本地 Git 引用和完整可达历史，包含旧标签 |
| `history/legacy-work.zip` | 本地旧 work 资料的保留副本，仅本地，不上传个人配置 |
| `evidence/` | 资源原始 JSON、回归记录及离线恢复验证 |
| `manifest.json`、`SHA256SUMS.txt` | 文件清单、大小、哈希及提交标识 |
| `verify_archive.ps1` | 封存完整性检查，不修改内容 |

封存目录应作为固定快照保留，恢复到另外的目录，不在快照内继续编辑。SHA-256 用于发现变动/损坏，不是发布者数字签名。

## 从 GitHub 接管

只使用：到 [v0.5.1 发行页](https://github.com/LynViii/moyu/releases/tag/v0.5.1) 下载 `moyu.exe`，或解压便携 ZIP。无需安装开发工具。

继续开发：安装 Git，克隆后从封存标签创建新分支；今后若已有更新版，可检查 main 再决定基线。

```powershell
git clone https://github.com/LynViii/moyu.git moyu-resumed
cd moyu-resumed
git switch -c resume-work v0.5.1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

不要重写旧标签，也不要强推覆盖历史。

## 从本地离线接管

在封存目录运行校验，再克隆到旁边的新目录：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\verify_archive.ps1
git clone .\history\moyu-all.bundle ..\moyu-resumed
git -C ..\moyu-resumed remote set-url origin https://github.com/LynViii/moyu.git
cd ..\moyu-resumed
git switch -c resume-work
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

bundle 恢复不需要联网。重新设置 origin 避免以后误把本地 bundle 当推送目标。没有 Git 时可解压 source ZIP 构建，但 Git 历史要从 bundle 恢复。原 outputs 也是保留的工作副本，接管前先看 `git status`，不要把用户的新修改还原掉。

## 构建与验证

环境：Windows x64、系统 .NET Framework（建议 4.8/4.8.1），不需要 .NET SDK、Node 或 Python。脚本使用系统自带 Framework64 C# 编译器，构建输出 `摸鱼.exe`。进行历史接管时另需 Git。

测试前从托盘完整退出应用，或运行 `摸鱼.exe --exit`。真实桌面测试会短暂显示窗口，验证快捷键和当前用户开机项并恢复原值，需要普通交互桌面权限。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\run_all.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\verify_updates.ps1 -LiveOnly
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\measure_resources.ps1 -Label resumed
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\package.ps1
```

查看 `docs/VALIDATION_0.5.1.md` 与 `docs/PERFORMANCE_0.5.1.md`。封存已实际验证 bundle 克隆、提交匹配、重新构建和独立 EXE 启动，不承诺旧编译器输出二进制逐字节可重现。

## 后续修改约束

- 版本唯一来源 `src/VersionInfo.cs`，修改行为后同步版本、CHANGELOG 与逐版发行说明。
- 保留原图标与简洁 UI；Win11 圆弧、Win10 圆点，均为自绘近似，不宣称系统原生资源。
- 不新增方案管理、设置导入导出、持久会话历史、自动退出；默认 Esc 及可自定义退出均保留，不能屏蔽系统安全组合键。
- 动画只在 UI 线程绘制，隐藏/最小化暂停；Win11 流畅模式有专用小缓冲和消息唤醒，其他模式保留兼容路径。
- 更新助手须继续验证来源、哈希、产品/版本与架构，原位更新失败保留旧程序，新版启动失败回滚。不可把哈希校验说成数字签名。
- 保留源码、测试与历史。源码提交 Git，EXE/ZIP 放 Releases，本地 `.build` 不上传。

## 仍需人工确认

开始菜单真实关闭闭环、物理显示器热插拔、混合 DPI 与真实登录后启动等，不因自动回归通过而视作已经覆盖。应用未签名，不能承诺所有 Windows/安全软件环境兼容。先确认这些边界，再决定下一轮维护范围。
