# SideBox

Windows 10 / 11 上贴在屏幕边缘的悬浮岛：钉程序，并查看时钟、便笺等模块。手动换情景默认打开，放在设置的「高级」里。系统专注、显示器和时间的自动切换默认关闭。

SideBox 不是 macOS Dock 的逐像素复刻，也不是 Explorer / 任务栏替换。它只切换启动项、信息和轻量动作，默认不接管窗口管理。

手动换情景**默认打开**（`ProductFeatures.ProfilesEnabled`）。系统专注 / 显示器 / 时间**自动切换尚未开放**。出厂日常为时钟 + 便笺，默认不自动隐藏。`general`（日常）不能删除。

## 最低环境

- Windows 10 2004（build 19041）或更新（全面兼容 Windows 10 与 Windows 11）
- .NET 10 SDK（开发）。发布用的安装包是自包含的，不要求用户另装桌面运行时
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

配置目录：`%AppData%\SideBox\`  
日志目录：`%LocalAppData%\SideBox\logs\`

## 发布

```powershell
powershell -NoProfile -File pack/publish.ps1
```

用 [Inno Setup](https://jrsoftware.org/isinfo.php) 编译 `pack/WinDock.iss`。安装器会拷贝 publish 输出、开始菜单快捷方式，可选任务「开机启动」。卸载删除 Run 键，**保留** `%AppData%\SideBox` 与日志。安装包文件为 `pack/output/SideBox-setup.exe`，图标与程序相同。

有 Authenticode 证书时，再按脚本注释打开 `SignTool`。仓库不内置证书。

## 官网

项目展示官网位于 `website/`。开发地址 `http://127.0.0.1:5175`。

```powershell
cd website
npm install
npm run dev
```
