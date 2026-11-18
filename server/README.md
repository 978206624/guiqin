# 归秦共享规则

`GuiQin.Rules` 是 .NET 8 类库。它编译 Unity 的 `Assets/Scripts/Rules` 源码，Unity 则通过同目录的 asmdef 编译这些文件。配置来自 Unity 工程的 `Assets/Resources/GuiQin/Data`。Unity 用 `UnityRulesDataSource` 从 Resources 读取，回放程序用 `FileDataSource` 从输出目录读取构建时复制的 JSON。两个宿主都在启动时调用 `GameData.Load`。

战斗重放需要相同版本的规则和配置、开战前的角色状态、关卡、种子和回合指令。战斗与掉落使用从同一个种子得到的独立随机序列。回放比较回合事件和结算，创建战斗时生成的会话 ID 不参与比较。

在仓库根目录用 .NET 8 SDK 执行：

```sh
dotnet build server/GuiQin.Rules.Smoke/GuiQin.Rules.Smoke.csproj -c Release
dotnet run --project server/GuiQin.Rules.Smoke -c Release --no-build
```

Smoke 连续执行两场固定战斗并比较输出，再与 `GuiQin.Rules.Smoke/Snapshots/battle-seed-12346.txt` 比对。有差异时返回非零退出码。若规则变更有意改变了回放结果，先用上面的命令加 `-- --print` 打印实际结果，确认差异后再更新快照。
