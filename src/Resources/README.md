# 本地生成的原版雪暴资源

`NativeAlpineSnow.bundle` 来源于受支持的本机 PEAK 2.5.a 安装，归游戏资源权利人所有，不提交到 Git。

在仓库根目录安装 `requirements-dev.txt` 中的工具，然后运行：

```powershell
python .\tools\extract-native-snow.py --game 'D:\SteamLibrary\steamapps\common\PEAK'
```

脚本核对游戏版本及主程序集哈希，只读游戏目录，生成当前项目编译需要的 bundle。提取范围为原生雪暴粒子、材质、着色器与相关纹理，不包含地图。详细步骤见 [构建说明](../../docs/BUILD.md)。
