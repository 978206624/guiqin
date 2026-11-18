using System.Collections.Generic;

namespace GuiQin.Chat
{
    public static class ChatCmd
    {
        public const string Send = "chat.send", Hist = "chat.hist", Guild = "chat.guild", Push = "push.chat";
    }

    // 链接范围以 UTF-16 字符索引表示，只有服务端验证过的物品才会带链接。
    public class ChatItemLink { public int start, length; public string itemId; }

    // ch：world 世界、guild 帮派、party 队伍、pm 私聊、system 系统、notice 公告。
    public class ChatMsg
    {
        public int id; public string roleId, ch, from, cls, text, peer; public int lv; public long t; public bool self;
        public List<ChatItemLink> links = new List<ChatItemLink>();
    }

    public class ChatPerson { public string name, cls; public int lv; }
    public class ChatPeerReq { public string peer; }
    public class PartyState
    {
        public string roleId;
        public int revision;
        public List<ChatPerson> members = new List<ChatPerson>();
        public List<string> pending = new List<string>();
    }
    public class FriendResp { public bool ok; public string msg; public List<ChatPerson> friends = new List<ChatPerson>(); }
    public class PartyResp { public bool ok; public string msg; public PartyState party; }
    public class ChatSendReq { public string ch, text, peer; }
    public class ChatGuildReq { public bool join; }
    public class ChatResp { public bool ok; public string msg; public ChatMsg sent; public string guild; public int members; }
    public class ChatHistResp
    {
        public List<ChatMsg> msgs = new List<ChatMsg>();
        public string guild; public int members;
        public List<ChatPerson> friends = new List<ChatPerson>();
        public PartyState party;
    }
}
