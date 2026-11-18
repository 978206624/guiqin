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
        static readonly Vector3 Origin = new Vector3(1000f, 0f, 0f);          // 战场搭在远离地图的位置，不用销毁 / 重建地图
        static readonly Vector2 EnemyPerson = new Vector2(235, 520), EnemyPet = new Vector2(312, 612), AllyPet = new Vector2(408, 768), AllyPerson = new Vector2(485, 860), RowStep = new Vector2(84, -42);
        static readonly Dictionary<string, string[]> Badge = new Dictionary<string, string[]> {
            { "bleed", new[] { "血", "#e0364a" } }, { "vuln", new[] { "易", "#f08a4a" } }, { "bless", new[] { "战", "#f0c040" } }, { "rage", new[] { "狂", "#e04a3a" } },
            { "fear", new[] { "惧", "#9a6bf0" } }, { "mark", new[] { "魔", "#b86bff" } }, { "slow", new[] { "缓", "#5aa0ff" } }, { "shield", new[] { "盾", "#f0c040" } },
            { "hot", new[] { "愈", "#44c77f" } }, { "stun", new[] { "晕", "#aaaaaa" } }, { "curse", new[] { "罚", "#d85050" } }, { "dd", new[] { "破", "#d8883a" } },
            { "mfear", new[] { "惑", "#aa5bf0" } }, { "absorb", new[] { "吸", "#f0c040" } } };

        class BU
        {
            public UnitView V; public GameObject Go; public Transform BodyT; public SpriteRenderer Body, Shadow;
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
        Text roundText, clockText, bannerText, tipText, statText;
        RectTransform cmdPanel, listRoot; Button autoBtn; Text autoLabel;
        GameObject resultPanel;
        bool auto, waitingCmd, finished; float shakeT, shakePower, bannerT; int round;
        BattleCmd chosen; Action<BU> pickCb; BU me;

        public bool Active { get; private set; }

        // ---------------- 开战 ----------------
        public static BattleController Begin(Camera cam, Transform uiParent, BattleView view, Func<BattleCmd, Task<Result<BattleRoundResp>>> send, Action<BattleEnd> onDone, Action<string> toast)
        {
            var go = new GameObject("Battle");
            var b = go.AddComponent<BattleController>();
            b.cam = cam; b.view = view; b.send = send; b.onDone = onDone; b.toast = toast; b.Active = true;
            b.Build(uiParent);
            b.StartCoroutine(b.Loop());
            return b;
        }

        public void Dispose()
        {
            Active = false;
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
            foreach (var u in units.Values) if (u.V.me) me = u;
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
        }

        static void Shadow(Text t) { var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.9f); o.effectDistance = new Vector2(1.5f, -1.5f); }

        // ---------------- 指令界面 ----------------
        void BuildUi(Transform parent)
        {
            ui = Ui.Rect("BattleUi", parent); Ui.Anchor(ui, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            roundText = Ui.Label("round", ui, "", 34, new Color(1f, 0.92f, 0.65f), TextAnchor.UpperCenter);
            Ui.Anchor(roundText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -60), new Vector2(0, -16)); Shadow(roundText);
            clockText = Ui.Label("clock", ui, "", 40, Color.white, TextAnchor.UpperRight);
            Ui.Anchor(clockText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-120, -70), new Vector2(-20, -16)); Shadow(clockText);
            bannerText = Ui.Label("banner", ui, "", 36, Color.white, TextAnchor.MiddleCenter);
            Ui.Anchor(bannerText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -150), new Vector2(0, -90)); Shadow(bannerText);
            var panel = Ui.Box("cmd", ui, new Color(0.04f, 0.04f, 0.08f, 0.82f)); cmdPanel = panel.rectTransform;
            Ui.Anchor(cmdPanel, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 330));
            statText = Ui.Label("stat", panel.transform, "", 24, new Color(0.85f, 0.95f, 1f), TextAnchor.MiddleLeft);
            Ui.Anchor(statText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -44), new Vector2(-20, -6));
            tipText = Ui.Label("tip", panel.transform, "", 24, new Color(1f, 0.92f, 0.65f), TextAnchor.MiddleLeft);
            Ui.Anchor(tipText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -84), new Vector2(-20, -48));
            listRoot = Ui.Rect("list", panel.transform); Ui.Anchor(listRoot, Vector2.zero, Vector2.one, new Vector2(12, 96), new Vector2(-12, -90));

            string[][] bs = { new[] { "attack", "攻击" }, new[] { "skill", "技能" }, new[] { "item", "物品" }, new[] { "defend", "防御" }, new[] { "flee", "逃跑" }, new[] { "auto", "自动" } };
            for (int i = 0; i < bs.Length; i++)
            {
                string id = bs[i][0];
                var btn = Ui.Btn("b_" + id, panel.transform, bs[i][1], 26, new Color(0.25f, 0.3f, 0.5f, 1f), () => OnCmd(id));
                Ui.Anchor(btn.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), new Vector2(12 + i * 114, 14), new Vector2(12 + i * 114 + 108, 84));
                if (id == "auto") { autoBtn = btn; autoLabel = btn.GetComponentInChildren<Text>(); }
            }
            UpdateStat();
        }

        void UpdateStat()
        {
            if (me != null) statText.text = "气血 " + me.Hp + "/" + me.MaxHp + "　法力 " + me.Mp + "/" + me.V.maxMp;
        }

        void ClearList() { for (int i = listRoot.childCount - 1; i >= 0; i--) Destroy(listRoot.GetChild(i).gameObject); }

        void OnCmd(string c)
        {
            if (c == "auto") { auto = !auto; autoBtn.GetComponent<Image>().color = auto ? new Color(0.7f, 0.25f, 0.2f) : new Color(0.25f, 0.3f, 0.5f); autoLabel.text = auto ? "取消自动" : "自动"; if (auto && waitingCmd) Choose(new BattleCmd { type = "auto" }); return; }
            if (!waitingCmd) return;
            pickCb = null; ClearList();
            if (c == "attack") PickEnemy(t => Choose(new BattleCmd { type = "skill", skill = "attack", target = t.V.id }));
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
                    var b = Ui.Btn("it_" + id, listRoot, (it != null ? (string)it["name"] : id) + " ×" + left + "\n<size=18>" + desc + "</size>", 24, left > 0 ? new Color(0.2f, 0.45f, 0.32f) : new Color(0.25f, 0.25f, 0.3f), () => OnItem(id));
                    b.GetComponentInChildren<Text>().supportRichText = true; b.interactable = left > 0;
                    int col = i % 3, row = i / 3; i++;
                    Ui.Anchor(b.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(col * 226, -row * 70 - 66), new Vector2(col * 226 + 216, -row * 70));
                }
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
            foreach (var sk in view.skills)
            {
                var s = sk; bool ok = s.usable && me != null && me.Mp >= s.mp;
                string label = s.name + "\n<size=18>" + (s.usable ? "法力 " + s.mp : s.note) + "</size>";
                var b = Ui.Btn("sk_" + s.id, listRoot, label, 24, ok ? new Color(0.3f, 0.34f, 0.56f) : new Color(0.25f, 0.25f, 0.3f), () => OnSkill(s));
                b.GetComponentInChildren<Text>().supportRichText = true; b.interactable = ok;
                int col = i % 3, row = i / 3; i++;
                Ui.Anchor(b.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(col * 226, -row * 70 - 66), new Vector2(col * 226 + 216, -row * 70));
            }
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

        void Choose(BattleCmd c) { pickCb = null; ClearList(); chosen = c; waitingCmd = false; tipText.text = ""; clockText.text = ""; }

        // ---------------- 主流程 ----------------
        IEnumerator Loop()
        {
            Banner(view.title, 1.2f); yield return new WaitForSeconds(0.8f);
            BattleEnd end = null;
            while (end == null && Active)
            {
                round++; roundText.text = "第 " + round + " 回合";
                if (auto) chosen = new BattleCmd { type = "auto" };
                else
                {
                    chosen = null; waitingCmd = true; tipText.text = "请选择行动";
                    float left = view.turnSeconds;
                    while (chosen == null && Active)
                    {
                        left -= Time.deltaTime; clockText.text = Mathf.Max(0, Mathf.CeilToInt(left)).ToString();
                        if (left <= 0f) Choose(new BattleCmd { type = "auto" });
                        yield return null;
                    }
                    if (!Active) yield break;
                }
                var task = send(chosen);
                while (!task.IsCompleted) yield return null;
                var r = task.Result;
                if (!r.Ok) { toast(r.msg); Banner("网络不稳：" + r.msg, 1.5f); round--; yield return new WaitForSeconds(1f); continue; }    // 请求没处理成功：同一回合重新选
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
                            if (e.skill != "attack" && e.skill != "bite" && u != null && s != null) Banner(u.V.name + " · " + s.name, 0.9f);
                            var t0 = e.ts != null && e.ts.Length > 0 ? U(e.ts[0]) : null;
                            bool offensive = t0 != null && u != null && t0.V.side != u.V.side;
                            string pseq = u != null && t0 != null ? PetSeqFor(u, e.skill) : null;
                            if (pseq != null)
                            {
                                // 神兽有自己的逐帧招式：原地出招，到释放帧放弹道，弹道到了再结算后面的伤害事件
                                yield return new WaitForSeconds(0.06f);
                                bool rel = false; StartCoroutine(PetPlay(u, pseq, () => rel = true));
                                while (!rel) yield return null;
                                float dly = PetFx(u, e.skill, t0); if (dly > 0f) yield return new WaitForSeconds(dly * 0.85f);
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
        IEnumerator PetPlay(BU u, string name, Action onRel)
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
                yield return new WaitForSeconds(s[i][1] / 1000f);
            }
            if (onRel != null && !fired) onRel();
            if (p.Gen == g) p.Playing = false;
        }

        // 弹道特效：原图朝右，按 施法者→目标 的方向旋转，飞向目标。返回飞行时间（秒），没有特效返回 0
        float PetFx(BU u, string skill, BU t)
        {
            var m = u.Pa != null ? u.Pa.M.pfx : null; int[] fr;
            if (m == null || m.map == null || !m.map.TryGetValue(skill, out fr) || fr.Length == 0) return 0f;
            var frames = PetAnim.Pfx(m); if (frames == null) return 0f;
            float sc = m.sc != null && m.sc.ContainsKey(skill) ? m.sc[skill] : 1f, k = u.Pa.K * sc;
            Vector2 sp = u.Pos + u.Off;
            Vector2 s0 = new Vector2(sp.x - u.H * 0.12f, sp.y - u.H * 0.55f), e0 = new Vector2(t.Pos.x, t.Pos.y - t.H * 0.5f);
            float dist = (e0 - s0).magnitude, dur = Mathf.Clamp(dist * 1.1f, 160f, 320f) / 1000f;
            StartCoroutine(PfxRoutine(frames, fr, s0, e0, k, m.cw * k, dur));
            return dur;
        }

        IEnumerator PfxRoutine(Sprite[] frames, int[] fr, Vector2 s0, Vector2 e0, float k, float w, float dur)
        {
            var go = new GameObject("pfx"); go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sortingOrder = 30000;
            Vector2 d = e0 - s0; float dist = d.magnitude; Vector2 dir = dist > 0.01f ? d / dist : Vector2.right;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg);      // 舞台 y 向下，世界 y 向上
            go.transform.localScale = new Vector3(k, k, 1f);
            float travel = Mathf.Max(0f, dist - w * 0.6f);
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                sr.sprite = frames[Mathf.Clamp(fr[Mathf.FloorToInt(t / 0.07f) % fr.Length], 0, frames.Length - 1)];
                go.transform.localPosition = ToLocal(s0 + dir * travel * Mathf.Pow(t / dur, 1.5f));
                yield return null;
            }
            for (float t = 0f; t < 0.16f; t += Time.deltaTime) { sr.color = new Color(1, 1, 1, 1f - t / 0.16f); yield return null; }
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

        void Shake(float power, float dur) { shakePower = power; shakeT = dur; }
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
            var bg = Ui.Box("result", ui, new Color(0.04f, 0.04f, 0.08f, 0.94f)); resultPanel = bg.gameObject;
            Ui.Anchor(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, -250), new Vector2(300, 250));
            string title = end.result == "win" ? "战斗胜利" : end.result == "lose" ? "战败" : "成功逃脱";
            var tl = Ui.Label("title", bg.transform, title, 40, new Color(1f, 0.92f, 0.65f), TextAnchor.UpperCenter);
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
