# MidiForwarder

<p align="center">
  <b>一个简洁高效的 Windows MIDI 设备转发工具</b><br>
  <b>A lightweight and efficient Windows MIDI device forwarding tool</b>
</p>

<p align="center">
  支持实时转发 MIDI 消息、系统托盘运行、多语言切换等功能<br>
  Supports real-time MIDI message forwarding, system tray operation, multi-language switching, and more
</p>

---

## 功能特性 | Features

- **实时 MIDI 转发 | Real-time MIDI Forwarding** - 将 MIDI 输入设备的消息实时转发到输出设备 / Forward MIDI messages from input devices to output devices in real-time
- **自动连接 | Auto Connect** - 支持启动时自动连接上次使用的设备 / Automatically connect to last used devices on startup
- **系统托盘 | System Tray** - 最小化到系统托盘，后台静默运行 / Minimize to system tray for background operation
- **开机自启 | Auto Start** - 支持 Windows 开机自动启动 / Support Windows startup auto-launch
- **多语言支持 | Multi-language** - 支持简体中文、英文和系统默认语言 / Support Simplified Chinese, English, and system default language
- **消息日志 | Message Log** - 实时显示转发的 MIDI 消息详情 / Display forwarded MIDI message details in real-time

---

## 系统要求 | System Requirements

- Windows 10/11
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

---

## 安装使用 | Installation & Usage

### 从源码构建 | Build from Source

```bash
# 克隆仓库 | Clone repository
git clone <repository-url>
cd MidiForwarder

# 构建项目 | Build project
dotnet build -c Release

# 运行程序 | Run application
dotnet run
```

### 发布为独立应用 | Publish as Standalone

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

构建后的可执行文件位于 `bin/Release/net8.0-windows/win-x64/publish/` 目录。  
*The built executable is located in `bin/Release/net8.0-windows/win-x64/publish/`*

---

## 使用指南 | User Guide

### 基本操作 | Basic Operations

1. **选择设备 | Select Devices**
   - 从"输入设备"下拉框选择 MIDI 输入设备 / Select MIDI input device from the "Input Device" dropdown
   - 从"输出设备"下拉框选择 MIDI 输出设备 / Select MIDI output device from the "Output Device" dropdown

2. **连接设备 | Connect Devices**
   - 点击"连接"按钮建立 MIDI 转发通道 / Click "Connect" button to establish MIDI forwarding channel
   - 连接成功后状态标签显示为绿色 / Status label turns green when connected successfully

3. **查看消息 | View Messages**
   - 日志区域实时显示转发的 MIDI 消息 / Log area displays forwarded MIDI messages in real-time
   - 包含消息类型、通道、数据等信息 / Includes message type, channel, data, and other information

### 系统托盘功能 | System Tray Features

右键点击托盘图标可访问：  
*Right-click the tray icon to access:*

| 功能 | Feature | 说明 | Description |
|------|---------|------|-------------|
| 开机自启动 | Auto Start | 设置 Windows 启动时自动运行 | Set to auto-run on Windows startup |
| 最小化到托盘 | Minimize to Tray | 关闭窗口时最小化到系统托盘 | Minimize to system tray when closing window |
| 语言 | Language | 切换界面语言（中文/英文/系统默认） | Switch UI language (Chinese/English/System) |
| 关于 | About | 显示应用信息 | Display application information |
| 退出 | Exit | 完全退出应用程序 | Completely exit the application |

### 自动连接设置 | Auto Connect Settings

勾选"启动时自动连接"后，程序会在启动时自动连接上次使用的输入/输出设备。  
*When "Auto-connect on startup" is checked, the application will automatically connect to the last used input/output devices on startup.*

---

## 项目结构 | Project Structure

```
MidiForwarder/
├── Program.cs              # 应用程序入口，主窗体逻辑 / App entry, main form logic
├── MidiManager.cs          # MIDI 设备管理与消息转发 / MIDI device management & forwarding
├── ConfigManager.cs        # 配置文件管理 / Configuration file management
├── AppConfig.cs            # 配置数据模型 / Configuration data model
├── TrayManager.cs          # 系统托盘功能 / System tray functionality
├── MainFormLayout.cs       # 主窗体 UI 布局 / Main form UI layout
├── LocalizationManager.cs  # 多语言本地化管理 / Multi-language localization
├── AboutDialog.cs          # 关于对话框 / About dialog
└── Resources/              # 本地化资源文件 / Localization resources
    ├── Strings.resx        # 中文资源 / Chinese resources
    └── Strings.en.resx     # 英文资源 / English resources
```

---

## 技术架构 | Technical Architecture

### 核心组件 | Core Components

| 组件 | Component | 职责 | Responsibility |
|------|-----------|------|----------------|
| **MidiManager** | **MidiManager** | 使用 NAudio 库管理 MIDI 设备，处理消息接收与转发 | Manage MIDI devices using NAudio library, handle message receiving and forwarding |
| **ConfigManager** | **ConfigManager** | JSON 配置文件持久化，存储用户偏好设置 | JSON configuration persistence, store user preferences |
| **TrayManager** | **TrayManager** | 系统托盘图标管理，右键菜单与气泡提示 | System tray icon management, context menu and balloon tips |
| **LocalizationManager** | **LocalizationManager** | 基于 .NET 资源文件的本地化支持 | Localization support based on .NET resource files |

### 依赖库 | Dependencies

- [NAudio](https://github.com/naudio/NAudio) (2.2.1) - Windows 音频/MIDI 处理库 / Windows audio/MIDI processing library

---

## 配置文件 | Configuration File

配置文件存储在 `%APPDATA%\MidiForwarder\config.json`：  
*Configuration file is stored at `%APPDATA%\MidiForwarder\config.json`:*

```json
{
  "SelectedInputDeviceId": 0,
  "SelectedOutputDeviceId": 1,
  "AutoStart": false,
  "AutoConnectOnStartup": true,
  "MinimizeToTray": true,
  "Language": "zh-CN"
}
```

---

## 开发计划 | Roadmap

- [ ] 支持 MIDI 消息过滤 / Support MIDI message filtering
- [ ] 虚拟 MIDI 设备创建 / Virtual MIDI device creation
- [ ] MIDI 消息录制与回放 / MIDI message recording and playback
- [ ] 命令行模式支持 / Command-line mode support

---

## 许可证 | License

MIT License

---

## 致谢 | Acknowledgments

- [NAudio](https://github.com/naudio/NAudio) - 优秀的音频处理库 / Excellent audio processing library
