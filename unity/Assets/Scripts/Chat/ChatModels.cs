using System.Collections.Generic;

namespace GuiQin.Chat
{
    public static class ChatCmd
    {
        public const string Send = "chat.send";         // 发言：world / guild / party / pm
        public const string Hist = "chat.hist";         // 进游戏后取最近的聊天记录 + 帮派状态
        public const string Guild = "chat.guild";       // 演示：加入 / 退出帮派（真服务器走帮派系统）
        public const string Push = "push.chat";         // 服务端推送：别人的发言、公告、系统消息
    }

    // ch: world 世界 / guild 帮派 / party 队伍 / pm 私聊 / system 系统 / notice 公告
    public class ChatMsg
    {
        public int id; public string ch, from, cls, text, peer; public int lv; public long t; public bool self;
    }

    public class ChatSendReq { public string ch, text, peer; }
    public class ChatGuildReq { public bool join; }
    public class ChatResp { public bool ok; public string msg; public ChatMsg sent; public string guild; public int members; }
    public class ChatHistResp { public List<ChatMsg> msgs = new List<ChatMsg>(); public string guild; public int members; }
}
