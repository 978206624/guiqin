using GuiQin.Chat;
using Newtonsoft.Json;

namespace GuiQin.Server;

internal sealed partial class GameStore
{
    public (string Id, ChatPerson Person) FindPerson(string zone, string name)
    {
        using var db = Open();
        using var cmd = Sql(db, """
            SELECT r.id,r.name,JSON_UNQUOTE(JSON_EXTRACT(r.state_json,'$.cls')),JSON_EXTRACT(r.state_json,'$.lv')
            FROM role r JOIN account a ON a.id=r.account_id
            WHERE r.zone_id=@zone AND r.name=@name AND a.status=0
            """, ("@zone", zone), ("@name", name));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? (reader.GetString(0), ReadPerson(reader, 1)) : default;
    }

    static ChatPerson ReadPerson(MySqlConnector.MySqlDataReader reader, int i) => new()
    {
        name = reader.GetString(i), cls = reader.GetString(i + 1), lv = Convert.ToInt32(reader.GetValue(i + 2))
    };

    public List<ChatPerson> Friends(string id)
    {
        using var db = Open();
        using var cmd = Sql(db, """
            SELECT r.name,JSON_UNQUOTE(JSON_EXTRACT(r.state_json,'$.cls')),JSON_EXTRACT(r.state_json,'$.lv')
            FROM friend f JOIN role r ON r.id=f.friend_role_id WHERE f.role_id=@id ORDER BY r.name
            """, ("@id", id));
        using var reader = cmd.ExecuteReader();
        var result = new List<ChatPerson>();
        while (reader.Read()) result.Add(ReadPerson(reader, 0));
        return result;
    }

    public void SetFriend(string id, string peer, bool add)
    {
        using var db = Open();
        using var cmd = Sql(db, add
            ? "INSERT IGNORE INTO friend (role_id,friend_role_id) VALUES (@id,@peer)"
            : "DELETE FROM friend WHERE role_id=@id AND friend_role_id=@peer", ("@id", id), ("@peer", peer));
        cmd.ExecuteNonQuery();
    }

    public int GuildMembers(string zone)
    {
        using var db = Open();
        using var cmd = Sql(db, """
            SELECT COUNT(*) FROM role WHERE zone_id=@zone AND JSON_UNQUOTE(JSON_EXTRACT(state_json,'$.guildMember'))='true'
            """, ("@zone", zone));
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void SaveChat(string zone, string from, string to, ChatMsg message, string scope)
    {
        using var db = Open();
        using var cmd = Sql(db, """
            INSERT INTO chat_msg (zone_id,channel,scope_id,sender_role_id,recipient_role_id,payload_json)
            VALUES (@zone,@ch,@scope,@from,@to,@data)
            """, ("@zone", zone), ("@ch", message.ch), ("@scope", scope), ("@from", from),
            ("@to", to), ("@data", JsonConvert.SerializeObject(message)));
        cmd.ExecuteNonQuery();
        message.id = checked((int)cmd.LastInsertedId);
    }

    public List<ChatMsg> ChatHistory(string zone, string id, bool guild, string party)
    {
        using var db = Open();
        using var cmd = Sql(db, """
            SELECT id,sender_role_id,payload_json FROM chat_msg WHERE zone_id=@zone AND
            (channel = 'notice' OR (channel = 'world' AND created_at > NOW(3) - INTERVAL 1 HOUR) OR (channel='guild' AND @guild)
                OR (channel='pm' AND (sender_role_id=@id OR recipient_role_id=@id))
                OR (channel='party' AND scope_id=@party)
                OR (channel='system' AND recipient_role_id=@id))
            ORDER BY id DESC LIMIT 200
            """, ("@zone", zone), ("@id", id), ("@guild", guild), ("@party", party));
        using var reader = cmd.ExecuteReader();
        var result = new List<ChatMsg>();
        while (reader.Read())
        {
            var msg = JsonConvert.DeserializeObject<ChatMsg>(reader.GetString(2));
            msg.id = reader.GetInt32(0); msg.roleId = id;
            msg.self = !reader.IsDBNull(1) && reader.GetString(1) == id;
            if (msg.ch == "pm" && !msg.self) msg.peer = msg.from;
            result.Add(msg);
        }
        result.Reverse();
        return result;
    }
}
