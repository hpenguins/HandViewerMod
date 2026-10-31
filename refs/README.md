# refs

编译期引用的程序集目录，内容来自 `tools\fetch-refs.ps1`，不入库。

```text
refs\
  CesiumLoader.SDK.dll   来自 CesiumLoader Release 的 cesium-sdk-tools.zip
  CesiumLoader.SDK.pdb   可选，SDK 符号
  game\
    AstralParty.Runtime.dll
    Google.Protobuf.Runtime.dll
    UniTask.dll
    UnityEngine.CoreModule.dll
```

`refs\game\` 是游戏 3.3.0 / Unity 2022 的引用程序集，哈希与 CesiumLoader 仓库的 `refs/game/sources.json` 一致。游戏更新后重新运行脚本即可；也可以把 `CESIUM_GAME_REFS` 指向自己提取的程序集目录。
