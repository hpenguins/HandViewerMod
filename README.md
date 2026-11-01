# 手牌查看 HandViewerMod

CesiumLoader 的独立模组：在派对 PVE（MapType 4 / 12）对局中，从升星区右侧的三角按钮展开一条独立手牌列表，显示其余玩家的手牌。

模组只在手牌列表展开且可见时查询，全部收起后暂停；默认展开 5 秒后自动收回，可在配置里关闭或调整。同一张牌在右上角显示数量角标。

## 环境要求

- 已安装 [CesiumLoader](https://github.com/higashitaniyume/CesiumLoader)（`version.dll` + `AstralParty_ModLoader\`），SDK 版本不低于 **2.8.6**。
- 对局类型为派对 PVE（MapType 4 / 12），且服务器已下发观战码。
- 回放、PVP 与未开局状态不会发起查询。

## 安装

1. 下载本仓库 [Releases](https://github.com/hpenguins/HandViewerMod/releases) 里的 `HandViewerMod-<版本>.zip`。
2. 解压到游戏 exe 目录下的 `AstralParty_ModLoader\mods\`，得到：

   ```text
   AstralParty_ModLoader\
     mods\
       HandViewerMod\
         HandViewerMod.dll
         HandViewerMod.json
         HandViewerMod.pdb   (可选，用于把日志里的堆栈还原成源码行号)
   ```

3. 启动游戏。加载日志里出现 `HandViewer` 相关记录即为生效。

卸载时退出游戏，删除 `mods\HandViewerMod\` 目录即可，不影响其他模组。

## 使用

- 进入派对 PVE 对局后，升星区右侧出现红色小三角按钮，点击展开或收起手牌列表。
- 展开时查询整房手牌；再次展开会刷新。全部收起后停止查询，HUD 也会一起释放。
- 重新展开或手牌条恢复可见时立即刷新，状态提示与手牌条保持一致，展开时不会停留在“已收起”。
- 手牌列表按展开时的原顺序排列，默认双排显示，超出高度时整体收缩到可见区域。
- 服务器返回 `handKnown=false` 的玩家显示为“手牌不可用”，不会用占位牌冒充已知手牌。
- 查询失败时 HUD 顶部显示服务端错误信息；数据过期时保留上一次可见快照并提示。

## 配置

首次运行后配置位于 `AstralParty_ModLoader\mods\HandViewerMod\config.json`，也可以在 AstralParty.Toys 的「模组 → 手牌查看 → 配置」里修改（改动约 1 秒内热重载）。

```json
{
  "Enabled": true,
  "AutoRefresh": true,
  "ServiceUrl": "https://astralpartycards.hiynet.com/",
  "TimeoutSeconds": 20,
  "DoubleRow": true,
  "AutoCollapseSeconds": 5
}
```

| 配置项 | 默认值 | 说明 |
| --- | --- | --- |
| `Enabled` | `true` | 总开关：关闭后不查询也不显示手牌列表，总开关优先于其他项。 |
| `AutoRefresh` | `true` | 手牌展开可见时自动查询；关闭后只在手动刷新时查询。 |
| `ServiceUrl` | `https://astralpartycards.hiynet.com/` | 手牌查询服务地址。仅支持 HTTP/HTTPS，不接受用户信息、查询串或 fragment。 |
| `TimeoutSeconds` | `20` | 单次查询超时秒数，范围 5–60。 |
| `DoubleRow` | `true` | 手牌列表双排显示；关闭后按原尺寸单排显示。 |
| `AutoCollapseSeconds` | `5` | 手牌展开后自动收回的秒数；`0` 表示不自动收回。 |

## 数据来源与隐私

- 手牌由官方 PVE 观战流程取得：模组把当前对局的观战码发给 `ServiceUrl` 指向的查询服务，由该服务返回整房手牌快照。
- 观战码不写入日志，也不会出现在 HUD 上。若不需要该服务，把 `ServiceUrl` 指向自建实现即可；服务地址支持 HTTP，但局域网/明文地址仅适合在可信任网络中使用。
- 模组只读取游戏状态并绘制 UI，不修改牌序、牌堆或对局结果。

## 从源码构建

编译期引用的 `CesiumLoader.SDK.dll` 与游戏程序集不随仓库分发，需要先准备：

```powershell
pwsh -NoProfile -File tools\fetch-refs.ps1
```

脚本默认从 CesiumLoader 仓库与 Release 拉取，并按 `sources.json` 校验游戏程序集的 SHA256。已经在本机 clone 了 CesiumLoader 时，可以离线直接复制：

```powershell
pwsh -NoProfile -File tools\fetch-refs.ps1 -Source C:\src\Study\astralparty\modding\msvc
```

随后编译：

```powershell
dotnet build HandViewerMod.csproj -c Release
dotnet test tests\HandViewerMod.Tests\HandViewerMod.Tests.csproj -c Release
```

产物在 `bin\Release\netstandard2.0\`，取 `HandViewerMod.dll`、`HandViewerMod.json`（以及可选的 `.pdb`）放进 `mods\HandViewerMod\` 即可。自备游戏程序集时可用 `CESIUM_GAME_REFS` 环境变量或 `-p:GameRefsDir=` 指定目录。

## 发布

推 `v*` 标签会触发 GitHub Actions：准备引用程序集、编译、打包成含 `HandViewerMod\` 目录的 zip，并创建对应版本的 Release。标签版本必须与 `HandViewerMod.json` 的 `version` 一致。

## 许可

本仓库代码采用 [GNU Affero General Public License v3.0](LICENSE)。所依赖的 CesiumLoader.SDK 按 MIT 许可发布，详见 [CesiumLoader-NOTICE.txt](CesiumLoader-NOTICE.txt)。

CesiumLoader 与手牌查看均为非官方项目，与 Astral Party 的开发或发行方无关。修改客户端可能违反游戏服务条款，使用风险自负；请勿用于在线对局中获取不公平优势。
