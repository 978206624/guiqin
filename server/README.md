# 归秦共享规则

`GuiQin.Rules` 是 .NET 8 类库。它编译 Unity 的 `Assets/Scripts/Rules` 源码，Unity 则通过同目录的 asmdef 编译这些文件。配置来自 Unity 工程的 `Assets/Resources/GuiQin/Data`。Unity 用 `UnityRulesDataSource` 从 Resources 读取，回放程序用 `FileDataSource` 从输出目录读取构建时复制的 JSON。两个宿主都在启动时调用 `GameData.Load`。

战斗重放需要相同版本的规则和配置、开战前的角色状态、关卡、种子和回合指令。战斗与掉落使用从同一个种子得到的独立随机序列。回放比较回合事件和结算，创建战斗时生成的会话 ID 不参与比较。

在仓库根目录用 .NET 8 SDK 执行：

```sh
dotnet build server/GuiQin.Rules.Smoke/GuiQin.Rules.Smoke.csproj -c Release
dotnet run --project server/GuiQin.Rules.Smoke -c Release --no-build
```

Smoke 连续执行两场固定战斗并比较输出，再与 `GuiQin.Rules.Smoke/Snapshots/battle-seed-12346.txt` 比对。有差异时返回非零退出码。若规则变更有意改变了回放结果，先用上面的命令加 `-- --print` 打印实际结果，确认差异后再更新快照。

## MySQL 开发库

建库脚本位于 `server/database/001_init.sql`。用有建库权限的账号从仓库根目录执行，`--password` 由客户端交互读取。

```powershell
mysql.exe --host=127.0.0.1 --port=3306 --user=root --password --default-character-set=utf8mb4 --execute="source server/database/001_init.sql"
```

本机开发库使用现有 MySQL 服务 `127.0.0.1:3306`。在现有数据库客户端中刷新该连接，即可看到 `guiqin`。临时验证用的独立实例使用端口 `3307`，其配置、数据和登录文件位于 `%LOCALAPPDATA%/GuiQin/mysql`。如需重新启动该独立实例：

```powershell
$guiqinMysql = Join-Path $env:LOCALAPPDATA 'GuiQin\mysql'
Start-Process -FilePath (Get-Command mysqld.exe).Source -ArgumentList "--defaults-file=`"$guiqinMysql\mysqld.ini`"" -WindowStyle Hidden
mysql.exe "--defaults-file=$guiqinMysql\app.ini" -e "SHOW TABLES"
```

`app.ini` 只用于本机连接，包含 `guiqin_app` 的登录信息。`admin.ini` 用于维护。Unity 当前继续使用进程内的 MockBackend。

## 玄兵塔本地服务

先对 MySQL 8.0 导入首版结构，并提供具备应用表权限的连接串。连接串放在 `ConnectionStrings__Game` 环境变量中，本机配置可从仓库之外读取：

```powershell
$env:ConnectionStrings__Game = Get-Content (Join-Path $env:LOCALAPPDATA 'GuiQin/server/connection.txt') -Raw
dotnet run --project server/GuiQin.Server -c Release
```

服务默认监听 `http://127.0.0.1:5090`。Unity 编辑器启动前设置 `GUIQIN_WS_PREVIEW=ws://127.0.0.1:5090/ws` 即可进入真实后端预览。未配置的 Unity 仍使用离线后端。

真实预览使用账号密码和函谷关区服。首次进入点「注册账号」。账号为 3 到 24 位字母、数字、点、下划线或连字符，密码为 8 到 32 个字符。角色满足 15 级后在汉中郡找守塔人。已结算的塔进度与奖励保存在 MySQL，进行中战斗保留在服务进程内。聊天、好友、组队及其他独立玩法按各自阶段接入。
