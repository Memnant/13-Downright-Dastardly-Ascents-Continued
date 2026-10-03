# 从源码构建

本仓库运行时 `src/**/*.cs` 对应 1.5.20。开发环境以 **Windows x64、PowerShell、PEAK 2.6.a / Build 25667990、BepInEx 5** 为基准，输出目标为 **.NET Standard 2.1**。

## 准备

你需要本机已安装游戏与 BepInEx。编译只引用本地程序集；Publicizer 只处理引用副本，不覆盖游戏目录的 `Assembly-CSharp.dll`。

项目固定使用 **.NET SDK 10.0.401** 和 **BepInEx.AssemblyPublicizer.MSBuild 0.4.2**。引导脚本把 SDK 下载到 `.tools`，验证记录的 SHA-512；依赖包首次恢复需要网络。

```powershell
.\tools\bootstrap.ps1
```

仓库不提交从游戏提取的原版雪暴 bundle。先准备 Python 和固定版本 UnityPy，然后从自己的游戏安装生成它：

```powershell
python -m pip install -r .\requirements-dev.txt --target .\.tools\unity-inspect
python .\tools\extract-native-snow.py --game 'D:\SteamLibrary\steamapps\common\PEAK'
```

提取器核查版本及主程序集哈希，输出 `src/Resources/NativeAlpineSnow.bundle` 和本地来源报告；不会改写游戏文件。该 bundle 被 Git 忽略，构建时嵌入 DLL。玩家直接使用 Release DLL 时无需 Python。

2.5 将资源合并进 `data.unity3d`；提取器只解压所需资源对应的块，不把整个游戏包展开到内存或磁盘。此步骤只在构建时执行。

## 构建和检查

以下每次指定自己的游戏路径，不依赖脚本中的本机默认路径：

```powershell
.\tools\build.ps1 -GamePath 'D:\SteamLibrary\steamapps\common\PEAK'
.\tools\test.ps1 -GamePath 'D:\SteamLibrary\steamapps\common\PEAK'
```

构建产物为 `artifacts/BepInEx/plugins/PeakAscentsContinued/13dda.dll`。`test.ps1` 执行构建、静态补丁目标/成员检查和纯逻辑测试，不进入实际地图。

可选的隔离检查须先退出 PEAK：

```powershell
.\tools\smoke.ps1 -GamePath 'D:\SteamLibrary\steamapps\common\PEAK'
```

它启动隐藏的 `-batchmode -nographics` 游戏测试子进程，使用项目内独立 BepInEx 配置，仅加载本模组和测试夹具。测试结束退出子进程并核对原生存档/游戏程序集哈希。它不做真实画面、操控或多人房间验证。

`tests/Test-Installer.ps1` 用模拟目录检查备份、安装和回滚，默认从本机已安装的 Continued DLL 读取旧插件样本，也可用 `-BaselineDll '旧版本的13dda.dll完整路径'` 指定。它不改写真实插件或存档。首次来源确认见 [UPSTREAM.md](UPSTREAM.md)。

## 维护入口

| 文件 | 作用 |
|---|---|
| `src/DifficultySnapshot.cs` | 等级、强制开关与配置编码 |
| `src/RunCoordinator.cs` | 房主规则、开局与房间状态 |
| `src/RunSaveStore.cs`、`src/SavedRuleRecord.cs` | RunId 侧存档和版本兼容 |
| `src/AirportSelector.cs` | 机场难度/免宝石面板 |
| `src/EffectState.cs` | 参数保存和跨局还原 |
| `src/Effects/` | 各项原效果适配及新增地区效果 |
| `src/Effects/ChasingFogTint.cs` | 火山、雾沼追赶雾颜色与 150 米渐隐 |
| `tests/PolicyTests/` | 不启动 Unity 的规则、保存和同步状态模拟 |
| `tests/SmokeHarness/` | 在实际 Unity/游戏程序集内执行合成组件检查 |

## 发布内容

玩家包包含唯一的 `13dda.dll`、安装/恢复脚本及公开说明。原游戏程序集、个人存档、配置、日志和工作区备份都不进入仓库或发布包。独立原版 bundle 不作为源码资源提交；现有 DLL 内的嵌入视觉资源及代码授权边界见 [LICENSING.md](../LICENSING.md)。
