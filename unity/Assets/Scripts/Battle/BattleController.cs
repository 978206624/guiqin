using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using GuiQin.Net;

namespace GuiQin.Battle
{
    // 战斗表现层：只负责“摆人 → 收指令 → 把指令交给服务端 → 播放服务端回传的事件 → 结算面板”。
    // 不做任何伤害 / 命中 / 暴击计算——这些全在服务端（BattleEngine）。
    public class BattleController : MonoBehaviour
    {
        static Sprite[] groundFireFrames, soulWindupFrames, soulHitFrames;
        static readonly int[] GroundFireSequence = { 0, 1, 2, 3, 2, 3, 2, 3, 2, 3, 4, 5 };
        static readonly Vector3 Origin = new Vector3(1000f, 0f, 0f);          // 战场搭在远离地图的位置，不用销毁 / 重建地图
        static readonly Vector2 EnemyPerson = new Vector2(235, 520), EnemyPet = new Vector2(312, 612), AllyPet = new Vector2(408, 768), AllyPerson = new Vector2(485, 860), RowStep = new Vector2(84, -42);
        static readonly Dictionary<string, string[]> Badge = new Dictionary<string, string[]> {
            { "bleed", new[] { "血", "#e0364a" } }, { "vuln", new[] { "易", "#f08a4a" } }, { "bless", new[] { "战", "#f0c040" } }, { "rage", new[] { "狂", "#e04a3a" } },
            { "fear", new[] { "惧", "#9a6bf0" } }, { "mark", new[] { "魔", "#b86bff" } }, { "slow", new[] { "缓", "#5aa0ff" } }, { "shield", new[] { "盾", "#f0c040" } },
            { "hot", new[] { "愈", "#44c77f" } }, { "stun", new[] { "晕", "#aaaaaa" } }, { "curse", new[] { "罚", "#d85050" } }, { "dd", new[] { "破", "#d8883a" } },
            { "mfear", new[] { "惑", "#aa5bf0" } }, { "absorb", new[] { "吸", "#f0c040" } } };

        class BU
        {
            public UnitView V; public GameObject Go; public Transform BodyT; public SpriteRenderer Body, Shadow, Ward;
            public Vector2 Pos, Home, Off; public int Hp, MaxHp, Mp; public bool Alive = true; public float W, H, Phase;
            public RectTransform Label; public RectTransform Fill; public Text St; public Dictionary<string, int> Sts = new Dictionary<string, int>();
            public float Tint;
            public PetAnim Pa;                       // 神兽逐帧动画（有图集的单位才有）
        }
        class Fl { public Text T; public Vector3 World; public float Age, Life; }

        Camera cam; Transform root; Canvas labels; RectTransform ui;
        BattleView view; Func<BattleCmd, Task<Result<BattleRoundResp>>> send; Action<BattleEnd> onDone; Action<string> toast;
        readonly Dictionary<int, BU> units = new Dictionary<int, BU>();
        readonly List<Fl> floats = new List<Fl>();
        Text roundText, clockText, bannerText, tipText;
        Action<int, int, int, int> onVitals, onPetVitals;
        RectTransform cmdPanel, listRoot; ScrollRect commandScroll; Button autoBtn; Text autoLabel;
        GameObject resultPanel;
        Button[] commandButtons;
        bool auto, waitingCmd, finished; float shakeT, shakePower, bannerT; int round;
        BattleCmd chosen, heroCommand; Action<BU> pickCb; BU me, pet;
        bool petTurn; float seconds;
        bool HasPetTurn => pet != null && pet.Alive && view.petSkills != null && view.petSkills.Count > 0;

        public bool Active { get; private set; }

        // ---------------- 开战 ----------------
        public static BattleController Begin(Camera cam, Transform uiParent, BattleView view, Func<BattleCmd, Task<Result<BattleRoundResp>>> send, Action<BattleEnd> onDone, Action<string> toast, Action<int, int, int, int> onVitals, Action<int, int, int, int> onPetVitals)
        {
            var go = new GameObject("Battle");
            var b = go.AddComponent<BattleController>();
            b.cam = cam; b.view = view; b.send = send; b.onDone = onDone; b.toast = toast; b.onVitals = onVitals; b.onPetVitals = onPetVitals; b.Active = true;
            b.Build(uiParent);
            Time.timeScale = Settings.Speed;                 // 战斗速度：整个战斗按倍速跑（回合倒计时用的是真实时间，不受影响）
            b.StartCoroutine(b.Loop());
            return b;
        }

        void OnDestroy() { Time.timeScale = 1f; }

        public void Dispose()
        {
            Active = false; Time.timeScale = 1f;
            if (labels != null) Destroy(labels.gameObject);
            if (ui != null) Destroy(ui.gameObject);
            Destroy(gameObject);
        }

        static Vector3 ToLocal(Vector2 p) { return new Vector3(p.x / Art.PX, -p.y / Art.PX, 0f); }
        static Vector2 Slot(string side, string row, int i, int n)
        {
            Vector2 c = side == "enemy" ? (row == "person" ? EnemyPerson : EnemyPet) : (row == "person" ? AllyPerson : AllyPet);
            float k = i - (n - 1) / 2f;
            return new Vector2(c.x + RowStep.x * k, c.y + RowStep.y * k);
        }

        void Build(Transform uiParent)
        {
            root = new GameObject("battleRoot").transform; root.SetParent(transform, false); root.position = Origin;
            var bgSprite = Art.Background(!string.IsNullOrEmpty(view.bg) ? view.bg : "bg_battle", 720f);
            if (bgSprite != null)
            {
                var bg = new GameObject("bg"); bg.transform.SetParent(root, false);
                var sr = bg.AddComponent<SpriteRenderer>(); sr.sprite = bgSprite; sr.sortingOrder = -10000;
                bg.transform.localScale = new Vector3(1f, 12.8f / bgSprite.bounds.size.y, 1f);
            }
            labels = Ui.MakeCanvas("BattleLabels", 5, transform);
            labels.GetComponent<GraphicRaycaster>().enabled = false;

            // 摆人：每一侧、每一排按出场顺序沿斜线排开
            foreach (var side in new[] { "enemy", "ally" })
                foreach (var row in new[] { "person", "pet" })
                {
                    var list = view.units.FindAll(u => u.side == side && u.row == row);
                    for (int i = 0; i < list.Count; i++) AddUnit(list[i], Slot(side, row, i, list.Count));
                }
            foreach (var u in units.Values)
            {
                if (u.V.me) me = u;
                else if (u.V.side == "ally" && u.V.row == "pet" && pet == null) pet = u;
            }
            BuildUi(uiParent);
        }

        void AddUnit(UnitView v, Vector2 pos)
        {
            var u = new BU { V = v, Pos = pos, Home = pos, Hp = v.hp, MaxHp = v.maxHp, Mp = v.mp, Phase = UnityEngine.Random.value * 6f };
            var sprite = Art.One(v.sprite); var sz = Art.Size(v.sprite);
            u.Go = new GameObject("unit_" + v.id); u.Go.transform.SetParent(root, false);
            var sh = new GameObject("shadow"); sh.transform.SetParent(u.Go.transform, false);
            u.Shadow = sh.AddComponent<SpriteRenderer>(); u.Shadow.sprite = Art.Dot(); u.Shadow.color = new Color(0, 0, 0, 0.35f);
            float scale = sz != null ? v.h / sz[1] : 1f; u.W = sz != null ? sz[0] * scale : v.h * 0.6f; u.H = v.h;
            sh.transform.localScale = new Vector3(u.W * 0.8f / Art.PX, u.W * 0.24f / Art.PX, 1f);
            var b = new GameObject("body"); b.transform.SetParent(u.Go.transform, false); u.BodyT = b.transform;
            u.Body = b.AddComponent<SpriteRenderer>(); u.Body.sprite = sprite;
            u.BodyT.localScale = new Vector3((v.flip ? -1f : 1f) * scale, scale, 1f);
            PetMeta pm;
            if (v.row == "pet" && GameData.Assets.pet != null && GameData.Assets.pet.TryGetValue(v.sprite, out pm))
            {
                var pa = PetAnim.Create(pm, v.h);
                if (pa != null) { u.Pa = pa; u.Body.sprite = pa.Frame((int)pm.seq["idle"][0][0]); u.BodyT.localScale = new Vector3((v.flip ? -1f : 1f) * pa.K, pa.K, 1f); }
            }
            // 名牌 + 血条 + 状态
            var lab = Ui.Rect("lab_" + v.id, labels.transform); lab.sizeDelta = new Vector2(120, 50); lab.pivot = new Vector2(0.5f, 0f); u.Label = lab;
            if (v.row == "person")
            {
                var nm = Ui.Label("nm", lab, v.name, 20, v.me ? new Color(0.55f, 1f, 0.7f) : Color.white, TextAnchor.LowerCenter);
                Ui.Anchor(nm.rectTransform, Vector2.zero, Vector2.one, new Vector2(-40, 20), new Vector2(40, 40)); Shadow(nm);
            }
            var back = Ui.Box("hpb", lab, new Color(0, 0, 0, 0.6f)); back.raycastTarget = false;
            Ui.Anchor(back.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-30, 8), new Vector2(30, 16));
            var fill = Ui.Box("fill", back.transform, v.side == "enemy" ? new Color(0.9f, 0.25f, 0.25f) : new Color(0.3f, 0.85f, 0.45f)); fill.raycastTarget = false;
            Ui.Anchor(fill.rectTransform, Vector2.zero, Vector2.one, new Vector2(1, 1), new Vector2(-1, -1)); u.Fill = fill.rectTransform;
            u.St = Ui.Label("st", lab, "", 16, Color.white, TextAnchor.LowerCenter); u.St.supportRichText = true;
            Ui.Anchor(u.St.rectTransform, Vector2.zero, Vector2.one, new Vector2(-40, -10), new Vector2(40, 8));
            units[v.id] = u; SetHp(u, u.Hp);
            // 重连 / 重进游戏时的快照：已倒下的直接隐藏，身上的状态原样挂回去
            if (v.sts != null) foreach (var kv in v.sts) u.Sts[kv.Key] = kv.Value;
            if (v.dead) { u.Alive = false; u.Sts.Clear(); u.Body.color = new Color(1, 1, 1, 0); u.Shadow.enabled = false; lab.gameObject.SetActive(false); }
            else RefreshSt(u);
        }

        static void Shadow(Text t) { var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.9f); o.effectDistance = new Vector2(1.5f, -1.5f); }

        // ---------------- 指令界面 ----------------
        void BuildUi(Transform parent)
        {
            ui = Ui.Rect("BattleUi", parent); Ui.Anchor(ui, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var turn = Ui.Box("turnBadge", ui, WebUi.Gold);
            turn.sprite = WebUi.Circle(); turn.raycastTarget = false;
            WebUi.At(turn.rectTransform, 596, 20, 104, 104);
            var center = Ui.Box("turnCenter", turn.transform, WebUi.Purple);
            center.sprite = WebUi.Circle(); center.raycastTarget = false;
            WebUi.At(center.rectTransform, 3, 3, 98, 98);
            clockText = Ui.Label("clock", turn.transform, "", 40, WebUi.Amber, TextAnchor.MiddleCenter);
            WebUi.At(clockText.rectTransform, 4, 13, 96, 48);
            roundText = Ui.Label("round", turn.transform, "", 14, WebUi.Text, TextAnchor.MiddleCenter);
            WebUi.At(roundText.rectTransform, 4, 59, 96, 27);
            bannerText = WebUi.Title(Ui.Label("banner", ui, "", 30, WebUi.Amber, TextAnchor.MiddleCenter));
            WebUi.At(bannerText.rectTransform, 110, 196, 500, 45);
            Shadow(bannerText);
            var panel = WebUi.Gradient("cmd", ui, WebUi.C("#120c208c"), WebUi.C("#120c20f2"));
            panel.raycastTarget = true;
            cmdPanel = panel.rectTransform;
            WebUi.At(cmdPanel, 0, 1030, 720, 242);
            tipText = Ui.Label("tip", panel.transform, "", 17, WebUi.Text, TextAnchor.MiddleCenter);
            WebUi.At(tipText.rectTransform, 14, 8, 692, 31);
            var viewport = Ui.Box("listViewport", panel.transform, Color.clear);
            WebUi.At(viewport.rectTransform, 14, 44, 692, 104);
            viewport.gameObject.AddComponent<RectMask2D>();
            listRoot = Ui.Rect("list", viewport.transform);
            listRoot.pivot = Vector2.up;
            Ui.Anchor(listRoot, Vector2.up, Vector2.up, Vector2.zero, new Vector2(692, 0));
            commandScroll = viewport.gameObject.AddComponent<ScrollRect>();
            commandScroll.viewport = viewport.rectTransform;
            commandScroll.content = listRoot;
            commandScroll.horizontal = false;
            commandScroll.movementType = ScrollRect.MovementType.Clamped;

            string[][] bs = { new[] { "attack", "攻击" }, new[] { "skill", "技能" }, new[] { "item", "道具" }, new[] { "defend", "防御" }, new[] { "flee", "逃跑" }, new[] { "auto", "自动" } };
            commandButtons = new Button[bs.Length];
            for (int i = 0; i < bs.Length; i++)
            {
                string id = bs[i][0];
                var btn = Ui.Btn("b_" + id, panel.transform, bs[i][1], 20, WebUi.Purple, () => OnCmd(id));
                WebUi.At((RectTransform)btn.transform, 14 + i * 117, 150, 108, 54);
                commandButtons[i] = btn;
                if (id == "auto") { autoBtn = btn; autoLabel = btn.GetComponentInChildren<Text>(); }
            }
            UpdateStat();
        }

        void UpdateStat()
        {
            if (me != null) onVitals?.Invoke(me.Hp, me.MaxHp, me.Mp, me.V.maxMp);
            if (pet != null) onPetVitals?.Invoke(pet.Hp, pet.MaxHp, pet.Mp, pet.V.maxMp);
        }

        void ClearList()
        {
            for (int i = listRoot.childCount - 1; i >= 0; i--)
            {
                var child = listRoot.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            ResizeCommands(0);
        }

        void ResizeCommands(int count)
        {
            listRoot.sizeDelta = new Vector2(692, Mathf.Max(104, ((count + 2) / 3) * 52));
            listRoot.anchoredPosition = Vector2.zero;
            commandScroll.StopMovement();
        }

        void OnCmd(string c)
        {
            if (c == "auto") { auto = !auto; WebUi.Button(autoBtn, auto ? WebUi.Red : WebUi.Purple); autoLabel.text = auto ? "取消自动" : "自动"; if (auto && waitingCmd) Choose(new BattleCmd { type = "auto" }); return; }
            if (!waitingCmd || (!petTurn && !me.Alive) || (petTurn && (c == "item" || c == "flee"))) return;
            pickCb = null; ClearList();
            if (c == "attack")
            {
                string skill = petTurn ? view.petSkills[0].id : "attack";
                PickEnemy(t => Choose(new BattleCmd { type = "skill", skill = skill, target = t.V.id }));
            }
            else if (c == "defend") Choose(new BattleCmd { type = "defend" });
            else if (c == "flee") Choose(new BattleCmd { type = "flee" });
            else if (c == "skill") ShowSkills();
            else if (c == "item") ShowItems();
        }

        // 物品：开战时服务端给的药品快照；点一下就用，用完这回合就过了。数量以服务端为准，这里只做显示
        void ShowItems()
        {
            tipText.text = "选择物品（用掉这回合）";
            int i = 0;
            if (view.items != null)
                foreach (var kv in view.items)
                {
                    string id = kv.Key; int left = kv.Value - (used.ContainsKey(id) ? used[id] : 0);
                    var it = GameData.Raw["items"][id]; string desc = it != null ? (it["hp"] != null ? "气血 +" + (int)it["hp"] : it["mp"] != null ? "法力 +" + (int)it["mp"] : "") : "";
                    var b = Ui.Btn("it_" + id, listRoot, (it != null ? (string)it["name"] : id) + " ×" + left + "\n<size=13>" + desc + "</size>", 17, left > 0 ? new Color(0.2f, 0.45f, 0.32f) : new Color(0.25f, 0.25f, 0.3f), () => OnItem(id));
                    b.GetComponentInChildren<Text>().supportRichText = true; b.interactable = left > 0;
                    int col = i % 3, row = i / 3; i++;
                    WebUi.At((RectTransform)b.transform, col * 232, row * 52, 224, 48);
                }
            ResizeCommands(i);
            if (i == 0) tipText.text = "背包里没有药品";
        }

        readonly Dictionary<string, int> used = new Dictionary<string, int>();
        void OnItem(string id)
        {
            if (!waitingCmd) return;
            ClearList(); int n; used.TryGetValue(id, out n); used[id] = n + 1;
            Choose(new BattleCmd { type = "item", skill = id });
        }

        void ShowSkills()
        {
            tipText.text = "选择技能";
            int i = 0;
            foreach (var sk in petTurn ? view.petSkills : view.skills)
            {
                var s = sk;
                if (petTurn && s.id == "bite") continue;        // 普通攻击走「攻击」按钮，不列入技能
                int cd = 0;
                if (petTurn && view.petCooldowns != null) view.petCooldowns.TryGetValue(s.id, out cd);
                bool enoughMana = (petTurn ? pet.Mp : me.Mp) >= s.mp;
                bool ok = s.usable && enoughMana && cd <= 1;
                string detail = cd > 1 ? "冷却 " + (cd - 1) + " 回合" : s.usable ? "法力 " + s.mp : s.note;
                if (petTurn && s.usable && !enoughMana && cd <= 1) detail = "法力不足 · 需 " + s.mp;
                string label = s.name + "\n<size=13>" + detail + "</size>";
                var b = Ui.Btn("sk_" + s.id, listRoot, label, 17, ok ? new Color(0.3f, 0.34f, 0.56f) : new Color(0.25f, 0.25f, 0.3f), () => OnSkill(s));
                b.GetComponentInChildren<Text>().supportRichText = true; b.interactable = ok;
                int col = i % 3, row = i / 3; i++;
                WebUi.At((RectTransform)b.transform, col * 232, row * 52, 224, 48);
            }
            ResizeCommands(i);
        }

        void OnSkill(SkillView s)
        {
            if (!waitingCmd) return;
            ClearList();
            if (s.target == "one") PickEnemy(t => Choose(new BattleCmd { type = "skill", skill = s.id, target = t.V.id }));
            else Choose(new BattleCmd { type = "skill", skill = s.id });
        }

        List<BU> LivingEnemies() { var l = new List<BU>(); foreach (var u in units.Values) if (u.Alive && u.V.side == "enemy") l.Add(u); return l; }

        void PickEnemy(Action<BU> cb)
        {
            var es = LivingEnemies();
            if (es.Count == 1) { cb(es[0]); return; }
            tipText.text = "点击要攻击的敌人"; pickCb = cb;
        }

        void Prompt(bool choosePet)
        {
            petTurn = choosePet; waitingCmd = true; seconds = view.turnSeconds;
            for (int i = 0; i < commandButtons.Length; i++)
                commandButtons[i].interactable = !choosePet || (i != 2 && i != 4);
            tipText.text = choosePet ? "请选择宠物行动 · " + pet.V.name : "请选择主角行动";
        }

        void Choose(BattleCmd c)
        {
            pickCb = null; ClearList();
            if (!petTurn && HasPetTurn && !auto)
            {
                heroCommand = c;
                Prompt(true);
                return;
            }
            if (petTurn && c.type != "auto")
            {
                chosen = heroCommand ?? new BattleCmd { type = "auto" };
                chosen.petSkill = c.type == "defend" ? "defend" : c.skill;
                chosen.petTarget = c.target;
            }
            else chosen = c;
            waitingCmd = false; tipText.text = ""; clockText.text = "";
        }

        // ---------------- 主流程 ----------------
        IEnumerator Loop()
        {
            Banner(view.title, 1.2f); yield return new WaitForSeconds(0.8f);
            BattleEnd end = null;
            while (end == null && Active)
            {
                round++; roundText.text = "第 " + round + " 回合";
                heroCommand = null;
                if (auto || (!me.Alive && !HasPetTurn))
                {
                    commandButtons[5].interactable = true;
                    chosen = new BattleCmd { type = "auto" };
                }
                else
                {
                    chosen = null;
                    Prompt(!me.Alive);
                    while (chosen == null && Active)
                    {
                        seconds -= Time.unscaledDeltaTime;
                        clockText.text = Mathf.Max(0, Mathf.CeilToInt(seconds)).ToString();
                        if (seconds <= 0f) Choose(new BattleCmd { type = petTurn ? "defend" : "auto" });
                        yield return null;
                    }
                    if (!Active) yield break;
                }
                var task = send(chosen);
                while (!task.IsCompleted) yield return null;
                var r = task.Result;
                if (!r.Ok) { toast(r.msg); Banner("网络不稳：" + r.msg, 1.5f); round--; yield return new WaitForSeconds(1f); continue; }    // 请求没处理成功：同一回合重新选
                view.petCooldowns = r.data.petCooldowns;
                yield return Play(r.data.events);
                if (r.data.over) end = r.data.end;
            }
            if (!Active) yield break;
            yield return new WaitForSeconds(0.5f);
            ShowResult(end);
        }

        // ---------------- 事件回放 ----------------
        BU U(int id) { BU u; return units.TryGetValue(id, out u) ? u : null; }

        IEnumerator Play(List<BattleEvent> evs)
        {
            BU actor = null; bool away = false;
            foreach (var e in evs)
            {
                if (away && (e.t == "round" || e.t == "act" || e.t == "stun" || e.t == "defend" || e.t == "flee"))
                { yield return Move(actor, actor.Home, 0.18f); away = false; yield return new WaitForSeconds(0.08f); }
                var u = U(e.u);
                switch (e.t)
                {
                    case "round": break;
                    case "act":
                        {
                            actor = u; var s = SkillOf(e.skill);
                            if (!e.followUp && e.skill != "attack" && e.skill != "bite" && u != null && s != null) Banner(u.V.name + " · " + s.name, 0.9f);
                            var t0 = e.ts != null && e.ts.Length > 0 ? U(e.ts[0]) : null;
                            bool offensive = t0 != null && u != null && t0.V.side != u.V.side;
                            string pseq = u != null && t0 != null ? PetSeqFor(u, e.skill) : null;
                            if (pseq != null)
                            {
                                // 神兽原地出招，到释放帧播放对应特效，随后播放伤害事件。
                                yield return new WaitForSeconds(0.06f);
                                bool soul = e.skill == "ss_ts";
                                petCues.TryGetValue(e.skill ?? "", out var cue);
                                // 天蛇噬血先结算抵挡，再播放进攻；自身事件不重复播放出手。
                                if (t0 == u && e.skill == "ss_tsy") break;
                                if (!soul || !e.followUp)
                                {
                                    bool rel = false;
                                    if (soul) StartCoroutine(SoulWindup(u, t0, () => rel));
                                    var windup = soul ? 1.65f : PetWindupScale(u, pseq, cue);
                                    if (cue != null) StartCoroutine(PetCharge(u, t0, cue, () => rel));
                                    StartCoroutine(PetPlay(u, pseq, () => rel = true, windup));
                                    while (!rel) yield return null;
                                }
                                float dly;
                                if (soul)
                                {
                                    var hit = new List<BU>();
                                    foreach (var id in e.ts) { var target = U(id); if (target != null) hit.Add(target); }
                                    dly = SoulStrike(u, hit);
                                }
                                else dly = PetFx(u, e.skill, t0);
                                if (!soul && s != null && s.maxTargets > 1)
                                    for (int i = 1; i < e.ts.Length; i++)
                                    {
                                        var target = U(e.ts[i]);
                                        if (target != null) dly = Mathf.Max(dly, PetFx(u, e.skill, target, i));
                                    }
                                if (dly > 0f) yield return new WaitForSeconds(dly * (cue == null ? 0.85f : 1f));
                                break;
                            }
                            if (offensive)
                            {
                                float k = e.ts.Length > 1 ? 0.4f : 0.7f; yield return Move(u, Vector2.Lerp(u.Home, t0.Home, k), 0.16f); away = true;
                                Burst(t0, CastColor(s), 0.4f, 1.2f, 0.3f, true);
                            }
                            else if (u != null) { Burst(u, CastColor(s), 0.3f, 1.0f, 0.4f, true); yield return new WaitForSeconds(0.25f); if (e.ts != null) foreach (var id in e.ts) { var t = U(id); if (t != null && t != u) Burst(t, CastColor(s), 0.3f, 0.9f, 0.4f, true); } }
                            break;
                        }
                    case "defend": if (u != null) Float(u, "防御", "info"); yield return new WaitForSeconds(0.35f); break;
                    case "hit":
                        if (u != null)
                        {
                            SetHp(u, e.hp);
                            if (!PlayPetImpact(actor, u, e.skill, e.crit))
                                Burst(u, e.crit ? new Color(1f, 0.8f, 0.3f) : Color.white, 0.2f, e.crit ? 1.3f : 0.8f, 0.25f, false);
                            Float(u, (e.crit ? "暴击 " : "") + "-" + e.v, e.crit ? "crit" : e.v >= u.MaxHp * 0.25f ? "big" : "");
                            Shake(e.crit ? 0.09f : 0.04f, e.crit ? 0.34f : 0.22f);
                            StartCoroutine(Flash(u, new Color(1f, 0.35f, 0.35f)));
                            if (u.Pa != null) StartCoroutine(PetPlay(u, "hit", null));
                            if (actor != null) StartCoroutine(Knock(u, actor, e.crit ? 30f : 15f));
                            yield return new WaitForSeconds(0.28f);
                        }
                        break;
                    case "dot": if (u != null) { SetHp(u, e.hp); Float(u, string.IsNullOrEmpty(e.text) ? "-" + e.v : e.text + " -" + e.v, ""); yield return new WaitForSeconds(0.25f); } break;
                    case "heal": if (u != null) { SetHp(u, e.hp); Float(u, "+" + e.v, "heal"); Burst(u, new Color(0.4f, 1f, 0.6f), 0.3f, 1f, 0.4f, true); yield return new WaitForSeconds(0.2f); } break;
                    case "miss": if (u != null) { Float(u, e.text, "miss"); StartCoroutine(Dodge(u, actor)); if (u.Pa != null) StartCoroutine(PetPlay(u, "dodge", null)); yield return new WaitForSeconds(0.2f); } break;
                    case "float": if (u != null) Float(u, e.text, e.kind); break;
                    case "mp": if (u != null) { u.Mp = e.mp; UpdateStat(); } break;
                    case "status": if (u != null) { u.Sts[e.st] = e.turns; RefreshSt(u); } break;
                    case "unstatus": if (u != null) { u.Sts.Remove(e.st); RefreshSt(u); } break;
                    case "maxhp": if (u != null) { u.MaxHp = e.maxHp; SetHp(u, e.hp); } break;
                    case "stun": if (u != null) { Float(u, "眩晕", "info"); yield return new WaitForSeconds(0.4f); } break;
                    case "revive": if (u != null) { u.Alive = true; u.Body.color = Color.white; u.Shadow.enabled = true; SetHp(u, e.hp); Float(u, "复活", "heal"); yield return new WaitForSeconds(0.4f); } break;
                    case "flee": Banner(e.text, 1.2f); yield return new WaitForSeconds(0.7f); break;
                    case "die": if (u != null) { u.Alive = false; u.Sts.Clear(); RefreshSt(u); yield return Fade(u); } break;
                }
            }
            if (away) { yield return Move(actor, actor.Home, 0.2f); }
            yield return new WaitForSeconds(0.1f);
        }

        // ---------------- 神兽逐帧动画 ----------------
        // 这一招该播哪段序列：data.petSeq 里按精灵名 + 技能名配；没配的话，普攻和“神兽技”用 atk；都没有就不用逐帧（走通用冲刺）
        string PetSeqFor(BU u, string skill)
        {
            var pa = u.Pa; if (pa == null) return null;
            var tbl = GameData.Raw["petSeq"]; var t = tbl != null ? tbl[u.V.sprite] : null;
            string s = t != null && t[skill] != null ? (string)t[skill] : null;
            if (s != null && pa.M.seq.ContainsKey(s)) return s;
            var sd = SkillOf(skill);
            return pa.M.seq.ContainsKey("atk") && (skill == "attack" || (sd != null && sd.ss)) ? "atk" : null;
        }

        // 播一段帧序列；到释放帧调 onRel；被新的动作顶掉时（Gen 变了）也会补调一次，免得外面一直等
        IEnumerator PetPlay(BU u, string name, Action onRel, float windupScale = 1f)
        {
            var p = u.Pa; List<float[]> s;
            if (p == null || !p.M.seq.TryGetValue(name, out s)) { if (onRel != null) onRel(); yield break; }
            int g = ++p.Gen; p.Playing = true;
            int rel = p.M.rel != null && p.M.rel.ContainsKey(name) ? p.M.rel[name] : -1;
            bool fired = false;
            for (int i = 0; i < s.Count; i++)
            {
                if (p.Gen != g || u.Body == null) { if (onRel != null && !fired) onRel(); yield break; }
                u.Body.sprite = p.Frame((int)s[i][0]);
                if (i == rel && onRel != null) { onRel(); fired = true; }
                yield return new WaitForSeconds(s[i][1] / 1000f * (i < rel ? windupScale : 1f));
            }
            if (onRel != null && !fired) onRel();
            if (p.Gen == g) p.Playing = false;
        }

        enum PetStaging { Basic, DragonWave, Orbit, Swords, Fireball, Spear }

        sealed class PetCue
        {
            public readonly Color Color;
            public readonly float Windup, Flight, Width, Lift;
            public readonly bool Ring;
            public readonly string Frames;
            public readonly PetStaging Stage;

            public PetCue(string color, float windup, float flight, float width, float lift, bool ring, string frames = null, PetStaging stage = PetStaging.Basic)
            {
                ColorUtility.TryParseHtmlString(color, out var tint);
                Color = tint;
                Windup = windup; Flight = flight; Width = width; Lift = lift; Ring = ring; Frames = frames; Stage = stage;
            }
        }

        // 六只神兽沿用背视本体；独立调校聚光、弹道速度、冲击和护盾。
        static readonly Dictionary<string, PetCue> petCues = new Dictionary<string, PetCue>
        {
            { "ss_sl_bw", new PetCue("#60DDF2", 0.53f, 0.76f, 0.92f, 15f, true) },
            { "ss_kl", new PetCue("#83C4FF", 0.67f, 1.15f, 1.18f, 33f, true, stage: PetStaging.DragonWave) },
            { "ss_qk", new PetCue("#BE70FF", 0.93f, 1.02f, 0.76f, 0f, true, stage: PetStaging.Orbit) },
            { "ss_sy", new PetCue("#C9E7FF", 1.07f, 0.82f, 1.02f, 0f, false, stage: PetStaging.Swords) },
            { "ss_sx", new PetCue("#F2DB78", 0.53f, 0.80f, 1.06f, 0f, false, "ss_hit") },
            { "ss_tsy", new PetCue("#ED547D", 0.53f, 1.30f, 1.07f, 24f, true) },
            { "ss_sg", new PetCue("#FFD189", 0.50f, 0.68f, 1.13f, 0f, false, stage: PetStaging.Spear) },
            { "ss_zw", new PetCue("#E66166", 0.39f, 1f, 1f, 0f, true) },
            { "ss_dy", new PetCue("#FF7A3C", 0.67f, 0.82f, 0.91f, 0f, false) },
            { "ss_th", new PetCue("#FFB846", 0.97f, 1.24f, 1.18f, 25f, true, stage: PetStaging.Fireball) },
        };

        static float PetWindupScale(BU caster, string sequence, PetCue cue)
        {
            if (cue == null || caster.Pa.M.rel == null || !caster.Pa.M.rel.TryGetValue(sequence, out int end)) return 1f;
            float seconds = 0f;
            var frames = caster.Pa.M.seq[sequence];
            for (int i = 0; i < end && i < frames.Count; i++) seconds += frames[i][1] / 1000f;
            return seconds > 0f ? cue.Windup / seconds : 1f;
        }

        // 舞台坐标的 y 向下，只有腾身招式移动角色图层，脚下阴影留在原地。
        static Vector2 PetCastOffset(BU caster, BU target, float side, PetCue cue, float progress)
        {
            if (cue.Stage == PetStaging.Spear)
            {
                Vector2 delta = target.Pos - caster.Pos;
                float stop = Mathf.Max(42f, caster.W * 0.4f + target.W * 0.32f);
                float advance = Mathf.Max(0f, delta.magnitude - stop);
                float load = Mathf.Sin(Mathf.Clamp01(progress / 0.45f) * Mathf.PI) * -caster.H * 0.07f;
                float thrust = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.46f, 1f, progress));
                return delta.normalized * (load + advance * thrust);
            }
            if (cue.Stage != PetStaging.DragonWave) return Vector2.zero;
            float jump = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.32f, 1f, progress));
            return new Vector2(side * 0.23f * caster.H, -0.32f * caster.H) * jump;
        }

        static Vector2 PetMuzzle(BU caster, PetCue cue, float side)
        {
            if (cue?.Stage == PetStaging.Spear) return new Vector2(side * caster.W * 0.47f, -caster.H * 0.49f);
            float forward = cue?.Stage == PetStaging.Fireball ? 0.43f : cue?.Stage == PetStaging.Swords ? 0.39f : 0.22f;
            float height = cue?.Stage == PetStaging.Fireball || cue?.Stage == PetStaging.Swords ? 0.64f : 0.55f;
            return new Vector2(side * caster.W * forward, -caster.H * height);
        }

        IEnumerator PetCharge(BU caster, BU target, PetCue cue, Func<bool> released)
        {
            if (cue.Stage != PetStaging.Basic)
            {
                yield return PetStagedCharge(caster, target, cue, released);
                yield break;
            }
            var effect = SoulSprite(cue.Ring ? Art.RingSprite() : Art.Dot());
            float side = target.Pos.x >= caster.Pos.x ? 1f : -1f;
            for (float t = 0f; !released() && t < 1.4f; t += Time.deltaTime)
            {
                float progress = Mathf.Min(1f, t / 0.36f);
                float scale = caster.H / 150f * (cue.Ring ? Mathf.Lerp(0.82f, 0.24f, progress) : Mathf.Lerp(0.19f, 0.52f, progress));
                effect.transform.localScale = new Vector3(scale, scale * 0.72f, 1f);
                float forward = caster == target ? 0f : side * caster.W * 0.31f;
                effect.transform.localPosition = ToLocal(caster.Pos + caster.Off + new Vector2(forward, -caster.H * 0.66f));
                effect.color = new Color(cue.Color.r, cue.Color.g, cue.Color.b, Mathf.Lerp(0.18f, 0.85f, progress));
                yield return null;
            }
            Destroy(effect.gameObject);
        }

        IEnumerator PetStagedCharge(BU caster, BU target, PetCue cue, Func<bool> released)
        {
            float side = target.Pos.x >= caster.Pos.x ? 1f : -1f;
            int count = cue.Stage == PetStaging.Fireball ? 5 : cue.Stage == PetStaging.DragonWave ? 2 : cue.Stage == PetStaging.Spear ? 1 : 3;
            var layers = new SpriteRenderer[count];
            Sprite blade = null;
            if (cue.Stage == PetStaging.Swords)
            {
                var meta = caster.Pa.M.pfx;
                var sprites = PetAnim.Pfx(meta);
                if (sprites != null && meta.map != null && meta.map.TryGetValue("ss_sy", out var indices) && indices.Length > 0)
                    blade = sprites[indices[0]];
            }
            for (int i = 0; i < count; i++)
                layers[i] = SoulSprite(blade ?? (cue.Stage == PetStaging.DragonWave && i == 0 ? Art.RingSprite() : Art.Dot()));

            float releaseAt = -1f;
            bool moves = cue.Stage == PetStaging.DragonWave || cue.Stage == PetStaging.Spear;
            const float trail = 0.12f, landing = 0.27f;
            float hold = cue.Stage == PetStaging.Spear ? 0.10f : 0f;
            for (float t = 0f; caster.BodyT != null; t += Time.deltaTime)
            {
                if (releaseAt < 0f && (released() || t > cue.Windup + 0.4f)) releaseAt = t;
                float after = releaseAt < 0f ? 0f : t - releaseAt;
                if (after >= (moves ? landing + hold : trail)) break;
                float p = Mathf.Clamp01((releaseAt < 0f ? t : releaseAt) / cue.Windup), h = caster.H;
                float tail = 1f - Mathf.Clamp01(after / trail);
                float fall = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((after - hold) / landing));
                Vector2 offset = PetCastOffset(caster, target, side, cue, p) * fall;
                if (moves) caster.BodyT.localPosition = ToLocal(offset);
                if (hold > 0f && caster.Shadow != null) caster.Shadow.transform.localPosition = ToLocal(offset);
                Vector2 origin = caster.Pos + caster.Off + offset;
                Vector2 muzzle = PetMuzzle(caster, cue, side);
                for (int i = 0; i < count; i++)
                {
                    Vector2 position = muzzle;
                    Color color = cue.Color;
                    float size = h * 0.18f;
                    float opacity = Mathf.Clamp01((p - 0.09f * i) * 3f);
                    float angle = 0f;
                    switch (cue.Stage)
                    {
                        case PetStaging.DragonWave:
                            position = i == 0 ? new Vector2(-offset.x, -offset.y - 9f) : muzzle;
                            size = h * (i == 0 ? Mathf.Lerp(0.72f, 0.93f, p) : Mathf.Lerp(0.13f, 0.44f, p));
                            if (i == 0) opacity *= 1f - p * 0.65f;
                            break;
                        case PetStaging.Orbit:
                            float theta = i * Mathf.PI * 2f / count + p * Mathf.PI * 3f;
                            float radius = Mathf.Lerp(0.50f, 0.14f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.64f, 1f, p)));
                            position = new Vector2(Mathf.Cos(theta) * h * radius, -h * 0.62f + Mathf.Sin(theta) * h * radius * 0.66f);
                            position += muzzle * Mathf.InverseLerp(0.77f, 1f, p) * 0.58f;
                            color = Color.Lerp(cue.Color, new Color(0.33f, 0.91f, 1f), Mathf.InverseLerp(0.37f, 0.68f, p));
                            size = h * Mathf.Lerp(0.06f, 0.105f, p);
                            opacity *= Mathf.Lerp(0.38f, 0.78f, Mathf.InverseLerp(0.65f, 1f, p));
                            break;
                        case PetStaging.Swords:
                            float spread = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.13f, 0.54f, p));
                            spread *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.68f, 1f, p));
                            position += new Vector2(side * h * (0.20f + (i == 1 ? 0.28f : 0.05f)) * spread, (i - 1) * h * 0.34f * spread);
                            angle = side > 0f ? 0f : 180f;
                            size = h * Mathf.Lerp(0.44f, 0.67f, p);
                            opacity *= Mathf.InverseLerp(i * 0.13f + 0.13f, i * 0.13f + 0.28f, p);
                            break;
                        case PetStaging.Spear:
                            size = h * Mathf.Lerp(0.07f, 0.23f, p);
                            opacity = Mathf.InverseLerp(0.49f, 0.88f, p);
                            break;
                        case PetStaging.Fireball:
                            float gather = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.26f, 0.76f, p));
                            if (i < 2) size = h * Mathf.Lerp(0.07f, i == 0 ? 0.60f : 0.34f, gather);
                            else
                            {
                                float spark = p * Mathf.PI * 4f + (i - 2) * Mathf.PI * 2f / 3f;
                                position += new Vector2(Mathf.Cos(spark), Mathf.Sin(spark) * 0.82f) * (h * 0.32f * gather);
                                size = h * (0.05f + Mathf.Abs(Mathf.Sin(spark)) * 0.05f);
                            }
                            opacity *= gather * (i == 0 ? 0.82f : 1f);
                            color = i == 0 ? new Color(1f, 0.29f, 0.04f) : new Color(1f, 0.86f, 0.34f);
                            break;
                    }

                    var layer = layers[i];
                    if (cue.Stage != PetStaging.DragonWave || i != 0)
                        position.x += side * h * 0.17f * (1f - tail);
                    layer.transform.localPosition = ToLocal(origin + position);
                    layer.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                    float k = size / Art.PX / layer.sprite.bounds.size.x;
                    layer.transform.localScale = new Vector3(k, k * (cue.Stage == PetStaging.DragonWave && i == 0 ? 0.38f : 1f), 1f);
                    layer.color = new Color(color.r, color.g, color.b, opacity * tail);
                    layer.sortingOrder = cue.Stage == PetStaging.Orbit && position.y < -h * 0.62f
                        ? caster.Body.sortingOrder - 1 : 30000;
                }
                yield return null;
            }
            foreach (var layer in layers) Destroy(layer.gameObject);

            if (moves && caster.BodyT != null) caster.BodyT.localPosition = Vector3.zero;
            if (hold > 0f && caster.Shadow != null) caster.Shadow.transform.localPosition = Vector3.zero;
        }

        // 返回结算伤害前的演出延时（秒）。狐焰从目标脚下腾起。
        float PetFx(BU u, string skill, BU t, int targetIndex = 0)
        {
            if (skill == "foxfire")
            {
                if (groundFireFrames == null)
                {
                    var tex = Art.Tex("fx_foxfire_ground");
                    if (tex == null) return 0f;
                    groundFireFrames = new Sprite[6];
                    int width = tex.width / groundFireFrames.Length;
                    for (int i = 0; i < groundFireFrames.Length; i++)
                        groundFireFrames[i] = Sprite.Create(tex, new Rect(i * width, 0, width, tex.height),
                            new Vector2(0.5f, 0f), tex.height, 0, SpriteMeshType.FullRect);
                }
                StartCoroutine(GroundFire(t, targetIndex));
                return 0.18f;
            }
            petCues.TryGetValue(skill ?? "", out var cue);
            if (u == t)
            {
                if (skill != "ss_zw") return 0f;
                PetShield(u, cue.Color);
                return 0.23f;
            }
            if (cue?.Stage == PetStaging.Spear)
            {
                float side = t.Pos.x >= u.Pos.x ? 1f : -1f;
                Vector2 start = u.Pos + u.Off + PetCastOffset(u, t, side, cue, 1f);
                Vector2 contact = t.Pos + t.Off - Vector2.up * (t.H * 0.48f);
                Vector2 direction = t.Pos - start;
                if (direction.sqrMagnitude < 1f) direction = Vector2.right * side;
                StartCoroutine(PetSpearContact(contact, direction, cue.Color, u.H));
                return 0.075f;
            }
            var m = u.Pa != null ? u.Pa.M.pfx : null; int[] fr;
            if (m == null || m.map == null || !m.map.TryGetValue(cue?.Frames ?? skill, out fr) || fr.Length == 0) return 0f;
            float crop = skill == "ss_th" || skill == "ss_dy" ? PetAnim.EmperorFireCrop : skill == "ss_sx" ? PetAnim.SnakeFireCrop : 0f;
            var frames = PetAnim.Pfx(m, crop); if (frames == null) return 0f;
            float sc = m.sc != null && m.sc.ContainsKey(skill) ? m.sc[skill] : 1f, k = u.Pa.K * sc;
            Vector2 sp = u.Pos + u.Off;
            Vector2 s0 = new Vector2(sp.x - u.H * 0.12f, sp.y - u.H * 0.55f), e0 = new Vector2(t.Pos.x, t.Pos.y - t.H * 0.5f);
            bool perspective = skill != "ss_hit" && skill.StartsWith("ss_", StringComparison.Ordinal);
            if (perspective)
            {
                float side = t.Pos.x >= sp.x ? 1f : -1f;
                s0 = sp + PetMuzzle(u, cue, side);
                if (cue != null) s0 += PetCastOffset(u, t, side, cue, 1f);
                e0 = t.Pos + t.Off + new Vector2(0f, -t.H * 0.48f);
            }
            float dist = (e0 - s0).magnitude, dur = Mathf.Clamp(dist * 1.1f, 160f, 320f) / 1000f;
            if (cue != null) { k *= cue.Width; dur *= cue.Flight; }
            float width = m.cw * k * (1f - crop);
            bool staged = cue != null && cue.Stage != PetStaging.Basic;
            StartCoroutine(PfxRoutine(frames, fr, s0, e0, k, width, dur, perspective, cue?.Lift ?? 0f, staged, cue != null));
            return dur;
        }

        IEnumerator PetSpearContact(Vector2 contact, Vector2 direction, Color tint, float height)
        {
            float angle = Mathf.Atan2(-direction.y, direction.x) * Mathf.Rad2Deg;
            Vector2 tip = direction.normalized * height;
            var rim = SoulSprite(Art.Dot());
            var core = SoulSprite(Art.Dot());
            rim.transform.localRotation = core.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            float length = height * 0.53f;
            for (float t = 0f; t < 0.17f; t += Time.deltaTime)
            {
                float p = Mathf.Clamp01(t / 0.075f), fade = 1f - Mathf.InverseLerp(0.075f, 0.17f, t);
                Vector3 pos = ToLocal(contact + tip * Mathf.Lerp(-0.14f, 0.035f, p));
                rim.transform.localPosition = core.transform.localPosition = pos;
                float grow = Mathf.Lerp(0.38f, 1f, p) * fade;
                rim.transform.localScale = new Vector3(length * grow / Art.PX, height * 0.11f / Art.PX, 1f);
                core.transform.localScale = new Vector3(length * grow * 0.78f / Art.PX, height * 0.032f / Art.PX, 1f);
                rim.color = new Color(tint.r, tint.g * 0.65f, tint.b * 0.57f, fade);
                core.color = new Color(1f, 0.94f, 0.72f, fade);
                yield return null;
            }
            Destroy(rim.gameObject);
            Destroy(core.gameObject);
        }

        // 等服务器确认目标实际受到伤害再播命中，闪避、抵挡和吸收走各自的反馈。
        bool PlayPetImpact(BU caster, BU target, string skill, bool critical)
        {
            if (caster?.Pa == null || !petCues.TryGetValue(skill ?? "", out var cue)) return false;
            var stage = skill == "ss_dy" ? PetStaging.Fireball : skill == "ss_sl_bw" ? PetStaging.DragonWave : cue.Stage;
            StartCoroutine(PetHitSparks(caster, target, stage, cue.Color, critical));
            if (skill == "ss_tsy") StartCoroutine(PetBloodReturn(target, caster, cue.Color));
            return true;
        }

        IEnumerator PetHitSparks(BU caster, BU target, PetStaging stage, Color tint, bool critical)
        {
            Vector2 center = target.Pos + target.Off - Vector2.up * (target.H * 0.48f);
            Vector2 aim = center - (caster.Pos + caster.Off - Vector2.up * (caster.H * 0.55f));
            float bearing = Mathf.Atan2(aim.y, aim.x);
            float radius = Mathf.Clamp(target.H * (critical ? 0.73f : 0.57f), 48f, 105f);
            bool radial = stage == PetStaging.DragonWave || stage == PetStaging.Orbit;
            bool embers = stage == PetStaging.Fireball, shards = stage == PetStaging.Swords || stage == PetStaging.Spear;
            int count = radial ? 8 : embers ? 7 : shards ? 6 : 4;
            var pieces = new SpriteRenderer[count];
            var flash = SoulSprite(radial ? Art.RingSprite() : Art.Dot());
            for (int i = 0; i < count; i++) pieces[i] = SoulSprite(Art.Dot());
            const float duration = 0.34f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float p = t / duration, ease = 1f - (1f - p) * (1f - p);
                float collapse = stage == PetStaging.Orbit ? 1f - ease : ease;
                float scale = Mathf.Lerp(0.24f, 0.95f, collapse) * radius * 1.3f / Art.PX;
                flash.transform.localPosition = ToLocal(center);
                flash.transform.localScale = new Vector3(scale, scale * 0.64f, 1f);
                flash.color = new Color(tint.r, tint.g, tint.b, (1f - p) * (radial ? 0.84f : 0.48f));

                for (int i = 0; i < count; i++)
                {
                    float k = (float)i / (count - 1);
                    float angle = radial ? i * Mathf.PI * 2f / count
                        : embers ? Mathf.Lerp(-2.8f, -0.34f, k)
                        : bearing + Mathf.Lerp(shards ? -0.68f : -0.45f, shards ? 0.68f : 0.45f, k);
                    Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    float speed = i % 2 == 0 ? 0.90f : 0.60f;
                    var pos = center + dir * (radius * (0.12f + speed * collapse));
                    if (embers) pos.y -= radius * 0.36f * p;
                    float size = radius * (shards ? 0.29f : 0.18f) * (1f - 0.55f * p) / Art.PX;
                    var piece = pieces[i];
                    piece.transform.localPosition = ToLocal(pos);
                    piece.transform.localScale = new Vector3(size, size * (shards ? 0.24f : 0.55f), 1f);
                    piece.transform.localRotation = Quaternion.Euler(0, 0, -angle * Mathf.Rad2Deg);
                    Color core = critical || embers ? new Color(1f, 0.82f, 0.48f) : Color.white;
                    Color color = Color.Lerp(core, tint, 0.35f + p * 0.65f);
                    color.a = (1f - p) * (1f - p);
                    piece.color = color;
                }
                yield return null;
            }
            foreach (var piece in pieces) Destroy(piece.gameObject);
            Destroy(flash.gameObject);
        }

        void PetShield(BU caster, Color tint)
        {
            Burst(caster, tint, 0.44f, 1.12f, 0.54f, true);
            Burst(caster, new Color(tint.r * 0.5f, tint.g * 0.5f, tint.b * 0.5f), 0.12f, 0.61f, 0.23f, false);
        }

        // 只播放吸回的血光；护盾次数与生命值由服务器事件决定。
        IEnumerator PetBloodReturn(BU target, BU caster, Color tint)
        {
            var effect = SoulSprite(Art.Dot());
            Vector2 from = target.Pos + target.Off - Vector2.up * (target.H * 0.53f);
            effect.transform.localScale = Vector3.one * 0.24f;
            for (float t = 0f; t < 0.20f; t += Time.deltaTime)
            {
                float p = t / 0.20f;
                Vector2 to = caster.Pos + caster.Off - Vector2.up * (caster.H * 0.62f);
                effect.transform.localPosition = ToLocal(Vector2.Lerp(from, to, p) - Vector2.up * (24f * Mathf.Sin(p * Mathf.PI)));
                effect.color = new Color(tint.r, tint.g, tint.b, Mathf.Lerp(1f, 0.64f, p));
                yield return null;
            }
            Destroy(effect.gameObject);
            PetShield(caster, tint);
        }

        IEnumerator GroundFire(BU target, int targetIndex)
        {
            var effect = new GameObject("ground-foxfire");
            effect.transform.SetParent(root, false);
            effect.transform.localPosition = ToLocal(target.Pos + target.Off + new Vector2(0f, 4f));
            // 在同一批目标间轻微变化火焰的轮廓，地面锚点保持不动。
            float scale = Mathf.Clamp(target.H * 1.15f, 96f, 150f) * (1f - targetIndex % 3 * 0.06f) / Art.PX;
            effect.transform.localScale = new Vector3(scale, scale * 1.25f, scale);
            var sr = effect.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 30000;
            sr.flipX = targetIndex % 2 != 0;
            var quickFrame = new WaitForSeconds(0.075f);
            var burnFrame = new WaitForSeconds(0.092f + targetIndex % 3 * 0.008f);
            foreach (int frame in GroundFireSequence)
            {
                sr.sprite = groundFireFrames[frame];
                // 涌起后交替播放两帧狐焰，再切到火星和余烬。
                yield return frame == 2 || frame == 3 ? burnFrame : quickFrame;
            }
            Destroy(effect);
        }

        SpriteRenderer SoulSprite(Sprite sprite)
        {
            var effect = new GameObject("tianmo-bolt");
            effect.transform.SetParent(root, false);
            var renderer = effect.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 30000;
            return renderer;
        }

        IEnumerator SoulWindup(BU caster, BU target, Func<bool> released)
        {
            if (soulWindupFrames == null)
            {
                var atlas = Art.Tex("fx_tianmo_windup");
                if (atlas == null) yield break;
                soulWindupFrames = new Sprite[5];
                int cell = atlas.width / soulWindupFrames.Length;
                for (int i = 0; i < soulWindupFrames.Length; i++)
                    soulWindupFrames[i] = Sprite.Create(atlas, new Rect(i * cell, 0, cell, atlas.height),
                        new Vector2(0.5f, 0f), Art.PX, 0, SpriteMeshType.FullRect);
            }
            var windup = SoulSprite(soulWindupFrames[0]);
            windup.flipX = target.Pos.x < caster.Pos.x;
            windup.transform.localScale = Vector3.one * (caster.H / 216f);
            // 起手图集已经包含九天玄女身体和紫光，播放时隐藏通用身体帧。
            var body = caster.Body;
            bool wasVisible = body.enabled;
            body.enabled = false;
            try
            {
                for (float time = 0f; !released() && time < 0.8f; time += Time.deltaTime)
                {
                    windup.transform.localPosition = ToLocal(caster.Pos + caster.Off);
                    windup.sprite = soulWindupFrames[Mathf.Min(soulWindupFrames.Length - 1, (int)(time / 0.078f))];
                    windup.sortingOrder = body.sortingOrder;
                    yield return null;
                }
            }
            finally
            {
                if (body != null) body.enabled = wasVisible;
                if (windup != null) Destroy(windup.gameObject);
            }
        }

        IEnumerator SoulRelease(BU caster, BU target, Sprite sprite)
        {
            var flash = SoulSprite(sprite);
            float side = target.Pos.x >= caster.Pos.x ? 1f : -1f;
            flash.flipX = side < 0f;
            flash.transform.localPosition = ToLocal(caster.Pos + caster.Off + new Vector2(side * caster.W * 0.52f, -caster.H * 0.61f));
            flash.transform.localScale = Vector3.one * 0.5f;
            for (float t = 0f; t < 0.11f; t += Time.deltaTime)
            {
                flash.color = new Color(1f, 1f, 1f, 1f - t / 0.11f);
                yield return null;
            }
            Destroy(flash.gameObject);
        }

        // 天魔噬魂：不飞弹道，手前闪光后在每个命中目标身上浮现红色骷髅魂魄缠绕（fx_tianmo_soul，4×2 共 8 帧）。
        // 与网页版 soulStrike 一致：延迟 60ms，每帧 70ms，目标间错开 45ms，第 4 帧（骷髅缠绕最紧）时结算伤害。
        const float SoulHitDelay = 0.06f, SoulHitFrame = 0.07f, SoulHitStagger = 0.045f;
        const int SoulHitPeak = 3;

        float SoulStrike(BU caster, IList<BU> targets)
        {
            if (targets.Count == 0) return 0f;
            var flash = Art.One("fx_tianmo_release", true);
            if (flash != null) StartCoroutine(SoulRelease(caster, targets[0], flash));
            if (soulHitFrames == null)
            {
                var atlas = Art.Tex("fx_tianmo_soul");
                if (atlas == null) return 0f;
                soulHitFrames = new Sprite[8];
                int cw = atlas.width / 4, ch = atlas.height / 2;
                for (int i = 0; i < 8; i++)
                    // 贴图坐标原点在左下：前 4 帧在上半行。
                    soulHitFrames[i] = Sprite.Create(atlas, new Rect(i % 4 * cw, (i < 4 ? 1 : 0) * ch, cw, ch),
                        new Vector2(0.5f, 0.5f), Art.PX, 0, SpriteMeshType.FullRect);
            }
            for (int i = 0; i < targets.Count; i++) StartCoroutine(SoulHit(targets[i], i));
            return SoulHitDelay + SoulHitPeak * SoulHitFrame + (targets.Count - 1) * SoulHitStagger;
        }

        IEnumerator SoulHit(BU target, int index)
        {
            yield return new WaitForSeconds(SoulHitDelay + index * SoulHitStagger);
            var effect = SoulSprite(soulHitFrames[0]);
            float size = Mathf.Clamp(target.H * 1.45f, 150f, 230f);
            effect.transform.localScale = Vector3.one * (size / soulHitFrames[0].rect.width);
            for (float time = 0f; time < SoulHitFrame * 8f && effect != null; time += Time.deltaTime)
            {
                effect.transform.localPosition = ToLocal(target.Pos + target.Off - Vector2.up * (target.H * 0.5f));
                effect.sprite = soulHitFrames[Mathf.Min(7, (int)(time / SoulHitFrame))];
                yield return null;
            }
            if (effect != null) Destroy(effect.gameObject);
        }

        IEnumerator PfxRoutine(Sprite[] frames, int[] fr, Vector2 s0, Vector2 e0, float k, float w, float dur, bool perspective, float lift, bool staged, bool petSkill)
        {
            var go = new GameObject("pfx"); go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sortingOrder = 30000;
            Vector2 d = e0 - s0; float dist = d.magnitude; Vector2 dir = dist > 0.01f ? d / dist : Vector2.right;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg);      // 舞台 y 向下，世界 y 向上
            go.transform.localScale = new Vector3(k, k, 1f);
            float endScale = perspective ? (e0.y < s0.y ? 0.62f : 1.18f) : 1f;
            float travel = Mathf.Max(0f, dist - w * 0.6f * endScale);
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                sr.sprite = frames[Mathf.Clamp(fr[Mathf.FloorToInt(t / 0.07f) % fr.Length], 0, frames.Length - 1)];
                float progress = Mathf.Pow(t / dur, staged ? 1.12f : 1.5f);
                if (staged) sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t / 0.055f));
                if (perspective)
                {
                    float depth = Mathf.Lerp(1f, endScale, progress);
                    go.transform.localScale = new Vector3(k * depth, k * depth * 0.78f, 1f);
                }
                go.transform.localPosition = ToLocal(s0 + dir * travel * progress - Vector2.up * (lift * Mathf.Sin(progress * Mathf.PI)));
                yield return null;
            }
            float tail = petSkill ? 0.075f : 0.16f;
            for (float t = 0f; t < tail; t += Time.deltaTime) { sr.color = new Color(1f, 1f, 1f, 1f - t / tail); yield return null; }
            Destroy(go);
        }

        SkillDef SkillOf(string id) { SkillDef s; return id != null && BattleFactory.Skills.TryGetValue(id, out s) ? s : null; }
        static Color CastColor(SkillDef s) { return s == null ? Color.white : s.fire ? new Color(1f, 0.55f, 0.2f) : s.heal > 0 || s.healPow > 0 || s.revive > 0 ? new Color(0.4f, 1f, 0.6f) : s.magic ? new Color(0.7f, 0.5f, 1f) : Color.white; }

        IEnumerator Move(BU u, Vector2 to, float dur)
        {
            if (u == null) yield break;
            Vector2 from = u.Pos; float t = 0f;
            while (t < dur) { t += Time.deltaTime; float k = Mathf.SmoothStep(0f, 1f, t / dur); u.Pos = Vector2.Lerp(from, to, k); yield return null; }
            u.Pos = to;
        }

        IEnumerator Knock(BU t, BU from, float px)
        {
            Vector2 d = (t.Home - from.Home); if (d.sqrMagnitude < 1f) d = Vector2.right; d.Normalize();
            for (float k = 0f; k < 0.1f; k += Time.deltaTime) { t.Off = d * px * (k / 0.1f); yield return null; }
            for (float k = 0f; k < 0.15f; k += Time.deltaTime) { t.Off = d * px * (1f - k / 0.15f); yield return null; }
            t.Off = Vector2.zero;
        }

        IEnumerator Dodge(BU t, BU from)
        {
            Vector2 d = from != null ? (t.Home - from.Home) : Vector2.right; d.Normalize(); d = new Vector2(d.y, -d.x) * 40f + new Vector2(0, 0);
            for (float k = 0f; k < 0.12f; k += Time.deltaTime) { t.Off = d * (k / 0.12f); yield return null; }
            for (float k = 0f; k < 0.18f; k += Time.deltaTime) { t.Off = d * (1f - k / 0.18f); yield return null; }
            t.Off = Vector2.zero;
        }

        IEnumerator Flash(BU u, Color c)
        {
            if (u.Body == null || !u.Alive && u.Body.color.a < 0.9f) yield break;
            u.Body.color = c; yield return new WaitForSeconds(0.08f);
            if (u.Alive) u.Body.color = Color.white;
        }

        IEnumerator Fade(BU u)
        {
            for (float k = 0f; k < 0.4f; k += Time.deltaTime) { u.Body.color = new Color(1, 1, 1, 1f - k / 0.4f); u.Shadow.color = new Color(0, 0, 0, 0.35f * (1f - k / 0.4f)); yield return null; }
            u.Body.color = new Color(1, 1, 1, 0); u.Shadow.enabled = false; u.Label.gameObject.SetActive(false);
        }

        // 扩散圈：技能释放 / 受击
        void Burst(BU u, Color c, float s0, float s1, float dur, bool ring)
        {
            if (u == null) return;
            StartCoroutine(BurstRoutine(u.Home + u.Off + new Vector2(0, -u.H * 0.5f), c, s0, s1, dur, ring));
        }

        IEnumerator BurstRoutine(Vector2 pos, Color c, float s0, float s1, float dur, bool ring)
        {
            var go = new GameObject("fx"); go.transform.SetParent(root, false); go.transform.localPosition = ToLocal(pos);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = ring ? Art.RingSprite() : Art.Dot(); sr.sortingOrder = 3000;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float k = t / dur; go.transform.localScale = Vector3.one * Mathf.Lerp(s0, s1, k) * (ring ? 1.2f : 1f);
                sr.color = new Color(c.r, c.g, c.b, (1f - k) * 0.9f); yield return null;
            }
            Destroy(go);
        }

        void Shake(float power, float dur) { if (!Settings.Shake) return; shakePower = power; shakeT = dur; }
        void Banner(string text, float dur) { bannerText.text = text; bannerT = dur; }

        void SetHp(BU u, int hp)
        {
            u.Hp = Mathf.Clamp(hp, 0, u.MaxHp);
            u.Fill.anchorMax = new Vector2(u.MaxHp > 0 ? (float)u.Hp / u.MaxHp : 0f, 1f);
            if (u == me) UpdateStat();
        }

        void RefreshSt(BU u)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var k in u.Sts.Keys) { string[] b; if (Badge.TryGetValue(k, out b)) sb.Append("<color=" + b[1] + ">" + b[0] + "</color>"); }
            u.St.text = sb.ToString();
            bool guarded = u.Alive && u.Sts.ContainsKey("absorb");
            if (guarded && u.Ward == null)
            {
                u.Ward = SoulSprite(Art.RingSprite());
                u.Ward.transform.SetParent(u.Go.transform, false);
                u.Ward.name = "blood-ward";
            }
            else if (!guarded && u.Ward != null)
            {
                var ward = u.Ward;
                u.Ward = null;
                StartCoroutine(FadePetWard(ward));
            }
        }

        IEnumerator FadePetWard(SpriteRenderer ward)
        {
            for (float t = 0f; t < 0.17f && ward != null; t += Time.deltaTime)
            {
                var color = ward.color;
                color.a = 0.56f * (1f - t / 0.17f);
                ward.color = color;
                yield return null;
            }
            if (ward != null) Destroy(ward.gameObject);
        }

        void Float(BU u, string text, string kind)
        {
            var t = Ui.Label("fl", labels.transform, text, kind == "crit" ? 36 : kind == "big" ? 32 : 28, Color.white, TextAnchor.MiddleCenter);
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.rectTransform.sizeDelta = new Vector2(260, 50);
            switch (kind)
            {
                case "heal": t.color = new Color(0.45f, 1f, 0.6f); break;
                case "info": t.color = new Color(1f, 0.9f, 0.5f); break;
                case "miss": t.color = new Color(0.8f, 0.85f, 0.95f); break;
                case "crit": t.color = new Color(1f, 0.65f, 0.2f); break;
                case "big": t.color = new Color(1f, 0.5f, 0.45f); break;
                default: t.color = new Color(1f, 0.93f, 0.9f); break;
            }
            Shadow(t);
            floats.Add(new Fl { T = t, World = root.position + ToLocal(u.Home + u.Off + new Vector2(UnityEngine.Random.Range(-18f, 18f), -u.H - 10f)), Life = 1.0f });
        }

        // ---------------- 每帧 ----------------
        void Update()
        {
            if (!Active) return;
            float dt = Time.deltaTime;
            // 相机固定在战场中央
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            cam.orthographicSize = Mathf.Max(6.4f, 3.6f / aspect);
            Vector3 sh = Vector3.zero;
            if (shakeT > 0f) { shakeT -= dt; sh = new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), 0f) * shakePower; }
            cam.transform.position = Origin + new Vector3(3.6f, -6.4f, -10f) + sh;
            if (bannerT > 0f) { bannerT -= dt; if (bannerT <= 0f) bannerText.text = ""; }

            // 单位位置 / 呼吸 / 排序 / 名牌
            foreach (var u in units.Values)
            {
                Vector2 p = u.Pos + u.Off; u.Go.transform.localPosition = ToLocal(p);
                int order = Mathf.RoundToInt(p.y) + (u.Pos != u.Home ? 1500 : 0);
                u.Body.sortingOrder = order; u.Shadow.sortingOrder = order - 1;
                if (u.Ward != null)
                {
                    u.Ward.sortingOrder = order + 1;
                    u.Ward.transform.localPosition = ToLocal(new Vector2(0f, -u.H * 0.48f));
                    u.Ward.transform.localScale = new Vector3(u.H * 0.93f / Art.PX, u.H * 1.05f / Art.PX, 1f);
                    u.Ward.color = new Color(0.94f, 0.24f, 0.32f, 0.46f + Mathf.Sin(Time.time * 3f) * 0.09f);
                }
                if (u.Pa != null && !u.Pa.Playing) u.Body.sprite = u.Pa.IdleSprite(dt);
                float bob = u.Alive ? Mathf.Sin(Time.time * 2.2f + u.Phase) * 0.012f : 0f;
                var ls = u.BodyT.localScale; u.BodyT.localScale = new Vector3(ls.x, Mathf.Sign(ls.y) * Mathf.Abs(ls.x) * (1f + bob), 1f);
                if (u.Label.gameObject.activeSelf)
                {
                    Vector3 sp = cam.WorldToScreenPoint(root.position + ToLocal(p + new Vector2(0, -u.H - 6f)));
                    u.Label.position = new Vector3(sp.x, sp.y, 0f);
                }
                // 选目标时敌人轻轻放大提示
                if (pickCb != null && u.Alive && u.V.side == "enemy") u.Body.color = Color.Lerp(Color.white, new Color(1f, 0.9f, 0.5f), 0.5f + 0.5f * Mathf.Sin(Time.time * 8f));
                else if (pickCb == null && u.Alive && u.Body.color != Color.white && u.Body.color.r > 0.99f && u.Body.color.b < 0.99f && u.Body.color.g > 0.8f) u.Body.color = Color.white;
            }
            for (int i = floats.Count - 1; i >= 0; i--)
            {
                var f = floats[i]; f.Age += dt; float k = f.Age / f.Life;
                if (k >= 1f) { Destroy(f.T.gameObject); floats.RemoveAt(i); continue; }
                Vector3 sp = cam.WorldToScreenPoint(f.World + new Vector3(0f, k * 0.9f, 0f));
                f.T.rectTransform.position = new Vector3(sp.x, sp.y, 0f);
                var c = f.T.color; c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f; f.T.color = c;
            }

            // 选敌人：点到哪个就是哪个
            if (pickCb != null)
            {
                PointerInput.Poll();
                if (PointerInput.Up && !PointerInput.OverUi())
                {
                    Vector3 w = cam.ScreenToWorldPoint(new Vector3(PointerInput.Pos.x, PointerInput.Pos.y, -cam.transform.position.z)) - root.position;
                    Vector2 sp2 = new Vector2(w.x * Art.PX, -w.y * Art.PX);
                    BU hit = null;
                    foreach (var u in LivingEnemies())
                        if (Mathf.Abs(sp2.x - u.Pos.x) < Mathf.Max(50f, u.W * 0.6f) && sp2.y < u.Pos.y + 20f && sp2.y > u.Pos.y - u.H - 30f) hit = u;
                    if (hit != null) { var cb = pickCb; pickCb = null; foreach (var u in units.Values) if (u.Alive) u.Body.color = Color.white; cb(hit); }
                }
            }
        }

        // ---------------- 结算 ----------------
        void ShowResult(BattleEnd end)
        {
            cmdPanel.gameObject.SetActive(false);
            var bg = WebUi.Panel("result", ui); resultPanel = bg.gameObject;
            Ui.Anchor(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-310, -250), new Vector2(310, 250));
            string title = end.result == "win" ? "战斗胜利" : end.result == "lose" ? "战败" : "成功逃脱";
            var tl = Ui.Label("title", bg.transform, title, 40, new Color(1f, 0.92f, 0.65f), TextAnchor.UpperCenter);
            WebUi.Title(tl);
            Ui.Anchor(tl.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -76), new Vector2(0, -16));
            var body = new System.Text.StringBuilder();
            if (end.result == "win")
            {
                body.Append("经验 +" + end.exp + "　银两 +" + end.coin);
                foreach (var kv in end.items) { var it = GameData.Raw["items"][kv.Key]; body.Append("\n获得 " + (it != null ? (string)it["name"] : kv.Key) + " ×" + kv.Value); }
                if (end.levelUp) body.Append("\n升级！现在 " + end.lv + " 级，气血法力回满");
            }
            else if (end.result == "lose") body.Append("你被抬回了城里。\n找人歇一歇可以回满气血，等级不够就先打前面的敌人。");
            else body.Append("你带着玄晶天狐退开了几步。");
            if (end.questDone) body.Append("\n任务完成：" + end.questTitle);
            if (!string.IsNullOrEmpty(end.note)) body.Append("\n" + end.note);
            body.Append("\n\n共 " + end.round + " 回合");
            var tx = Ui.Label("body", bg.transform, body.ToString(), 28, new Color(0.9f, 0.93f, 1f), TextAnchor.UpperCenter);
            Ui.Anchor(tx.rectTransform, Vector2.zero, Vector2.one, new Vector2(30, 100), new Vector2(-30, -90));
            var ok = Ui.Btn("ok", bg.transform, end.result == "lose" ? "回去休整" : "继续", 30, new Color(0.7f, 0.25f, 0.2f), () => { finished = true; onDone(end); });
            Ui.Anchor(ok.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-110, 24), new Vector2(110, 90));
        }
    }
}
