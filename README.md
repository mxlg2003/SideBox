# WinDock

Windows 11 上的情境化桌面控制面：常驻边缘 Dock（模块直接嵌在条上）。手动换情景默认关闭。

WinDock 不是 macOS Dock 的逐像素复刻，也不是 Explorer / 任务栏替换。它只切换启动项、信息和轻量动作，默认不接管窗口管理。

手动换情景**默认打开**（`ProductFeatures.ProfilesEnabled`）。系统专注 / 显示器 / 时间**自动切换尚未开放**。出厂日常为时钟 + 便笺，默认不自动隐藏。`general`（日常）不能删除。

## 最低环境

- Windows 11 22H2（build 22621）或更新
- .NET 10 SDK（本机已有 Windows Desktop Runtime 即可）
- Visual Studio 2022 或 `dotnet` CLI

## 仓库入口

- 产品定义与边界：[docs/PRODUCT.md](docs/PRODUCT.md)
- 技术架构与工程规则：[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- 配置与领域模型：[docs/DATA-MODEL.md](docs/DATA-MODEL.md)
- 里程碑与验收：[docs/MVP-PLAN.md](docs/MVP-PLAN.md)

## 开发

```powershell
dotnet build src/WinDock.sln
dotnet test src/WinDock.sln
dotnet run --project src/WinDock.App
```

安全模式（停用全部模块与采样，只留 Overlay Dock；设置不可用）：

```powershell
dotnet run --project src/WinDock.App -- --safe
```

配置目录：`%AppData%\WinDock\`  
日志目录：`%LocalAppData%\WinDock\logs\`

## 发布

```powershell
powershell -NoProfile -File pack/publish.ps1
```

用 [Inno Setup](https://jrsoftware.org/isinfo.php) 编译 `pack/WinDock.iss`。安装器会拷贝 publish 输出、开始菜单快捷方式，可选任务「开机启动」。卸载删除 Run 键，**保留** `%AppData%\WinDock` 与日志。

有 Authenticode 证书时，再按脚本注释打开 `SignTool`。仓库不内置证书。
