using GuiQin.Battle;
using GuiQin.Inventory;
using GuiQin.Net;
using MySqlConnector;
using Newtonsoft.Json;

namespace GuiQin.Server;

internal sealed record SavedAccount(string Id, string Name, string Hash, byte Status);
internal sealed record SavedRole(string Zone, ulong Version, RoleState State);
internal sealed record BattleRecord(string Id, string Stage, int Seed, string Rules, RoleState Initial, List<BattleCmd> Commands, BattleEnd End);

internal sealed class GameStore(string connectionString)
{
    MySqlConnection Open()
    {
        var connection = new MySqlConnection(connectionString);
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }

    static MySqlCommand Sql(MySqlConnection connection, string text, params (string Name, object Value)[] args)
    {
        var cmd = new MySqlCommand(text, connection);
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value);
        return cmd;
    }

    public SavedAccount Account(string name)
    {
        using var db = Open();
        using var cmd = Sql(db, "SELECT id,password_hash,status FROM account WHERE channel='password' AND external_id=@name",
            ("@name", name));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? new SavedAccount(reader.GetString(0), name, reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetByte(2)) : null;
    }

    public bool Register(SavedAccount account, string hash)
    {
        using var db = Open();
        using var cmd = Sql(db, "INSERT INTO account (id,channel,external_id,password_hash) VALUES (@id,'password',@name,@hash)",
            ("@id", account.Id), ("@name", account.Name), ("@hash", hash));
        try { return cmd.ExecuteNonQuery() == 1; }
        catch (MySqlException error) when (error.Number == 1062) { return false; }
    }

    public List<SavedRole> Roles(string account, string zone = null, string id = null)
    {
        using var db = Open();
        using var cmd = Sql(db, """
            SELECT zone_id,ver,state_json FROM role WHERE account_id=@account
              AND (@zone IS NULL OR zone_id=@zone) AND (@id IS NULL OR id=@id)
            ORDER BY created_at,id
            """, ("@account", account), ("@zone", zone), ("@id", id));
        using var reader = cmd.ExecuteReader();
        var roles = new List<SavedRole>();
        while (reader.Read()) roles.Add(new(reader.GetString(0), reader.GetUInt64(1), JsonConvert.DeserializeObject<RoleState>(reader.GetString(2))));
        return roles;
    }

    public bool Create(string account, string zone, RoleState role)
    {
        using var db = Open();
        using var cmd = Sql(db, "INSERT INTO role (id,account_id,zone_id,name,state_json) VALUES (@id,@account,@zone,@name,@state)",
            ("@id", role.id), ("@account", account), ("@zone", zone), ("@name", role.name), ("@state", JsonConvert.SerializeObject(role)));
        try { return cmd.ExecuteNonQuery() == 1; }
        catch (MySqlException error) when (error.Number == 1062) { return false; }
    }

    public bool Delete(string account, string id)
    {
        using var db = Open();
        using var cmd = Sql(db, "DELETE FROM role WHERE id=@id AND account_id=@account", ("@id", id), ("@account", account));
        return cmd.ExecuteNonQuery() == 1;
    }

    // 角色状态、审计记录和奖励流水必须一起提交。
    public bool Save(string account, SavedRole role, BattleRecord battle = null, MallPurchase purchase = null,
        (int Capacity, int Yuanbao)? expansion = null)
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        using var update = Sql(db, "UPDATE role SET state_json=@state,ver=ver+1 WHERE id=@id AND account_id=@account AND ver=@ver",
            ("@state", JsonConvert.SerializeObject(role.State)), ("@id", role.State.id), ("@account", account), ("@ver", role.Version));
        update.Transaction = tx;
        if (update.ExecuteNonQuery() != 1) return false;
        if (battle != null)
        {
            using var log = Sql(db, """
                INSERT INTO battle_log (battle_id,role_id,stage_id,seed,rules_version,initial_state_json,commands_json,result_json)
                VALUES (@id,@role,@stage,@seed,@rules,@initial,@commands,@result)
                """, ("@id", battle.Id), ("@role", role.State.id), ("@stage", battle.Stage), ("@seed", battle.Seed), ("@rules", battle.Rules),
                ("@initial", JsonConvert.SerializeObject(battle.Initial)), ("@commands", JsonConvert.SerializeObject(battle.Commands)),
                ("@result", JsonConvert.SerializeObject(battle.End)));
            log.Transaction = tx;
            log.ExecuteNonQuery();

            Flow("currency", "coin", battle.Initial.coin, role.State.coin, "battle", battle.Id);
            var before = battle.Initial.bag.items;
            var after = role.State.bag.items;
            foreach (var id in before.Keys.Union(after.Keys))
                Flow("item", id, before.GetValueOrDefault(id), after.GetValueOrDefault(id), "battle", battle.Id);
        }
        if (purchase != null)
        {
            string receipt = Guid.NewGuid().ToString("N");
            Flow("currency", purchase.Offer.currency, purchase.BalanceBefore, purchase.BalanceAfter, "mall.buy", receipt);
            Flow("item", purchase.Offer.item, purchase.CountBefore, purchase.CountAfter, "mall.buy", receipt);
        }
        if (expansion.HasValue)
        {
            string receipt = Guid.NewGuid().ToString("N");
            var before = expansion.Value;
            Flow("currency", "yuanbao", before.Yuanbao, role.State.yuanbao, "bag.expand", receipt);
            Flow("capacity", "bag", before.Capacity, role.State.bag.cap, "bag.expand", receipt);
        }
        tx.Commit();

        void Flow(string type, string id, int before, int after, string reason, string reference)
        {
            if (before == after) return;
            using var item = Sql(db, """
                INSERT INTO item_flow (role_id,asset_type,asset_id,before_value,after_value,reason,reference_id)
                VALUES (@role,@type,@asset,@before,@after,@reason,@reference)
                """, ("@role", role.State.id), ("@type", type), ("@asset", id), ("@before", before), ("@after", after),
                ("@reason", reason), ("@reference", reference));
            item.Transaction = tx;
            item.ExecuteNonQuery();
        }
        return true;
    }
}
