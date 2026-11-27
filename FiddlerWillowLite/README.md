# FiddlerWillowLite

[![CI](https://github.com/yaowenqiang/FiddlerWillowLite/actions/workflows/ci.yml/badge.svg)](https://github.com/yaowenqiang/FiddlerWillowLite/actions/workflows/ci.yml)
[![Release](https://github.com/yaowenqiang/FiddlerWillowLite/actions/workflows/release.yml/badge.svg)](https://github.com/yaowenqiang/FiddlerWillowLite/actions/workflows/release.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

一个参考 Fiddler Willow 插件实现的 **Fiddler Classic** 扩展：以项目为单位管理 Host 映射规则，一键切换整套环境，并在独立标签页中查看抓包与过滤结果。

> A lightweight Willow-like extension for Fiddler Classic: project-based host mapping with one-click environment switching, plus a built-in traffic capture and filter tab.

## 功能特性

- **项目 / 环境一键切换**：用树形结构管理项目，勾选项目即启用整套规则；多个项目启用时按自上而下第一个命中生效。
- **Host 映射**：域名 → 指定 `IP` 或 `IP:端口`，支持 `*.example.com` 通配。通过 Fiddler 的 `x-overrideHost` 改写连接目标，不改 Host 请求头，并设置 `bypassGateway`。
- **批量添加 Host**：粘贴多行文本一次导入大量规则，实时预览解析结果，自动跳过注释、空行和重复域名。兼容系统 hosts 文件格式与 `域名 -> 目标` 写法。
- **批量删除**：`Ctrl/Shift` 多选项目/规则后一次删除；或点 **批量删除** 粘贴域名文本，预览命中规则后按「仅当前项目 / 全部项目」范围批量删除，支持 `*.example.com` 通配。
- **抓包与过滤**：独立列表记录请求方法、域名、路径、状态码、耗时和命中项目；支持关键字过滤、仅看命中规则、暂停、清空，双击复制 URL。
- **配置持久化**：自动保存到 `%APPDATA%\FiddlerWillowLite\config.json`，改动即时写入。
- **无需 Visual Studio**：自带脚本化构建流程，一键编译、校验、安装。

## 环境要求

- Windows
- **Fiddler Classic**（已测试 6.0.20261.7291 / 6.0.20262.10021）
- 构建方式二选一：
  - `bootstrap.py` + `build.py`（只需 Python 3，无需 VS）
  - Visual Studio + .NET Framework 4.7.2 开发工具包

## 安装插件

1. 获取 `FiddlerWillowLite.dll`：
   - **推荐**：从 [Releases](https://github.com/yaowenqiang/FiddlerWillowLite/releases) 下载最新版本的 zip 压缩包（含 DLL、README 和 LICENSE），解压取用。
   - 或按下方步骤自行编译。
2. 复制到 Fiddler 的扩展目录：

   ```
   %USERPROFILE%\Documents\Fiddler2\Scripts\FiddlerWillowLite.dll
   ```

3. **完全退出并重启 Fiddler Classic**。
4. Fiddler 6 会弹出 `Caution: Unverified Extension Detected`，请点击 **Always allow**（插件未做代码签名，这是 Fiddler 的正常安全提示）。
5. 标签栏出现 **Willow Lite** 即安装成功。

> 注意：每次重新编译 DLL 后文件哈希变化，Fiddler 会再次弹出上述对话框，需要重新点一次 `Always allow`。

## 使用说明

1. 打开 **Willow Lite** 标签。
2. 左侧点击 **新增项目**，填写项目名称。
3. 选中项目后点击 **新增 Host**，填写域名与目标地址，例如：
   - 域名：`api.example.com`，目标：`127.0.0.1:8080`
   - 域名：`*.test.local`，目标：`10.0.0.5`
4. 勾选项目节点即可启用该项目下的全部规则；取消勾选即关闭。
5. 需要一次导入多条规则时，选中项目后点 **批量添加**，粘贴文本并预览确认。支持以下写法（可混用）：

   ```
   # 系统 hosts 格式：目标在前，一行可跟多个域名
   127.0.0.1  api.example.com  web.example.com
   # 域名在前（可带端口）
   dev.example.com 10.0.0.5:8080
   # 箭头或等号分隔
   stage.example.com -> 10.0.0.6
   prod.example.com=10.0.0.7
   # # 和 // 开头的行、空行会被忽略；与目标项目中重复的域名会跳过
   ```

   对话框下方实时显示「有效 / 重复跳过 / 错误 / 注释空行」统计，格式错误的行会列出原因且不会导入。
6. 删除规则有两种方式：
   - **多选删除**：在左侧树中按住 `Ctrl` 或 `Shift` 选中多个项目或规则，点 **删除** 一次删掉（选中的项目会连同其规则一起删除，不会重复计数）。
   - **批量删除**：选中项目后点 **批量删除**，对话框会**自动填入当前项目的所有域名**（若当前项目为空则填入全部项目的域名），删掉不想删除的行即可；也可清空后粘贴自定义文本（同样支持 hosts 格式 / 箭头 / 等号 / 注释 / `*.` 通配）。实时预览命中的规则，选择「仅当前项目」或「全部项目」后确认。手动编辑过文本后切换范围不会覆盖你的输入。
7. 「启用 Host 映射」总开关可一键停用所有映射。
8. 右侧抓包列表可按关键字过滤、只看命中规则，双击行复制完整 URL。
9. 配置保存在 `%APPDATA%\FiddlerWillowLite\config.json`，可直接查看或备份。

示例配置：

```json
{
  "MappingEnabled": true,
  "Projects": [
    {
      "Name": "本地开发",
      "Enabled": true,
      "Rules": [
        { "Domain": "api.example.com", "Target": "127.0.0.1:8080", "Enabled": true }
      ]
    }
  ]
}
```

## 从源码构建

### 方式一：脚本构建（推荐，无需 Visual Studio）

```bash
python bootstrap.py        # 仅首次：从 NuGet 下载 Roslyn，复制 .NET Framework 引用
python build.py --test     # 编译 → 反射校验 → 16 项行为测试 → 安装到 Fiddler Scripts
```

其他常用命令：

```bash
python build.py                       # 编译 + 校验 + 安装
python build.py --no-install          # 只编译，不安装
python build.py -c Release            # 发布版本
python build.py --fiddler-ref "D:\path\Fiddler.exe"   # 指定 Fiddler 引用
python build.py --install-dir "D:\custom\Scripts"     # 指定安装目录
```

Windows 也可直接运行 `build.bat`，Git Bash 下用 `./build.sh`。

`bootstrap.py` 只准备 `.toolchain/` 中的第三方二进制（Roslyn 编译器、.NET Framework 引用程序集），这些文件不纳入版本控制。`build.py` 会自动优先使用本机已安装的 Fiddler Classic 作为引用。

### 方式二：Visual Studio

用 Visual Studio 2017+ 打开 `FiddlerWillowLite.csproj`（目标框架 .NET Framework 4.7.2）。工程会自动探测常见安装目录下的 `Fiddler.exe`；若安装在自定义位置，可传：

```
msbuild FiddlerWillowLite.csproj /p:FiddlerExe="D:\path\Fiddler.exe"
```

编译后 `PostBuildEvent` 会把 DLL 自动复制到 `%USERPROFILE%\Documents\Fiddler2\Scripts\`。

## 测试

`python build.py --test` 会在 shim 环境中运行 27 项行为测试，覆盖：

- 精确域名映射写入 `x-overrideHost`、命中后 `bypassGateway = true`、会话列表自定义列写入项目名
- `*.test.local` 通配符映射并自动补端口、禁用规则不生效、未命中域名不改写、映射总开关关闭时不应用
- 批量解析：hosts 格式、`一行多域名`、域名在前带端口、箭头/等号分隔、注释与空行跳过、重复域名跳过、无效行报错
- 批量删除：域名提取（纯域名/hosts/箭头/等号/通配）、重复与注释跳过、精确与 `*.` 通配匹配、作用范围（当前项目/全部项目）、未命中与多项目删除

测试脚本会自动备份并还原 `%APPDATA%\FiddlerWillowLite\config.json`，不会污染真实配置。

## CI 与自动发布

仓库内置 GitHub Actions：

- **CI**（`.github/workflows/ci.yml`）：push 到 `main`、PR 或手动触发时，在 `windows-latest` 上准备工具链、下载 Fiddler 引用、编译、`--test` 全套行为测试，并上传 DLL 构建产物。
- **Release**（`.github/workflows/release.yml`）：推送 `v*` 标签时触发，从标签名写入程序集版本（`v0.1.0` → `0.1.0.0`），编译并测试通过后打包 `FiddlerWillowLite-<tag>.zip`，自动创建 GitHub Release 并生成更新说明。

CI 环境与本机不同，没有预装 Fiddler Classic。`fetch_fiddler_ref.py` 会从 Telerik 官方地址下载安装包，提取内部的 `Fiddler.exe` 作为编译引用（不随仓库分发，避免授权问题）。本地如果已安装 Fiddler，`build.py` 会优先使用本机版本，无需运行该脚本。

维护者发布新版本：

```bash
git tag v0.1.0
git push origin v0.1.0
```

推送后 Release 工作流会自动完成编译、测试与发布。

## 诊断

插件会在 `%TEMP%\FiddlerWillowLite-debug.log` 记录构造与 `OnLoad` 阶段的关键步骤。如果标签没有出现，先看这个日志，再看 Fiddler 的 **Log** 标签页。

## 已知限制

- 第一版**不包含** Mock 响应、请求重定向和限速模拟。
- HTTPS 场景下 `x-overrideHost` 会影响 SNI，映射到纯 IP 时个别站点可能报证书错误（Fiddler 本身限制）。
- 插件未做代码签名，Fiddler 6 每次更新 DLL 后都会提示未验证扩展。
- Fiddler 启动时可能单独弹出 `AppContainer Configuration` 提示，与插件无关，点 `No` 关闭即可。
- 仅支持 Windows / Fiddler Classic，不支持 Fiddler Everywhere。

## 目录结构

```
FiddlerWillowLite/
├── bootstrap.py             # 工具链引导（首次构建前运行）
├── build.py                 # 一键编译 / 校验 / 安装
├── build.bat / build.sh     # 构建脚本包装
├── fetch_fiddler_ref.py     # CI 用：下载并提取 Fiddler.exe 编译引用
├── FiddlerWillowLite.csproj # Visual Studio 工程
├── WillowPlugin.cs          # Fiddler 钩子与规则应用
├── WillowView.cs            # 选项卡 UI
├── BulkHostDialog.cs        # 批量添加 Host 对话框与解析预览
├── HostRuleParser.cs        # 批量添加的多行文本解析（hosts / 箭头 / 等号格式）
├── BulkDelete.cs            # 批量删除的域名解析、匹配与删除逻辑
├── BulkDeleteDialog.cs      # 批量删除对话框与命中预览
├── MultiSelectTree.cs       # 支持 Ctrl/Shift 多选的项目树
├── Models.cs                # 数据模型
├── RuleStore.cs             # JSON 配置读写
├── EditDialog.cs            # 项目/规则编辑对话框
├── Properties/AssemblyInfo.cs
├── tests/                   # shim 与行为测试
├── .github/workflows/       # CI 与 Release 工作流
└── .toolchain/              # 本地工具链（gitignore，由 bootstrap.py 生成）
```

## 许可证

MIT License，详见 [LICENSE](LICENSE)。

本项目为独立实现，与 Telerik / Progress Software 无关联。Fiddler Classic 是 Telerik 的商标产品，其本体不随本仓库分发。
