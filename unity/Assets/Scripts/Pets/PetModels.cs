using System.Collections.Generic;
using GuiQin.Inventory;

namespace GuiQin.Pets
{
    public static class PetCmd
    {
        public const string Use = "pet.use";            // 切换出战宠物
        public const string Exchange = "pet.exch";      // 神兽使者：神兽之魂兑换神兽
        public const string Rite = "pet.rite";          // 神兽祭：花银两求神兽之魂
        public const string Gift = "pet.gift";          // 神兽体验福利：一次性送七只神兽（演示用）
    }

    public class PetReq { public string id; }

    // 宠物相关的最新状态，整包覆盖客户端（和 BagResp 一样的做法）
    public class PetResp
    {
        public bool ok; public string msg;
        public string petAct; public List<string> petOwn = new List<string>(); public int bond; public bool ssGift;
        public int coin; public BagState bag;
    }
}
