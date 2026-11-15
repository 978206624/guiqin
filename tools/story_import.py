#!/usr/bin/env python3
# 把 story-src/main_story_existing.json（来自 kunkunxunqin 的主线剧本）转成本游戏的任务节拍数据 js/story.js。
# 用法：python3 tools/story_import.py            （默认接入 1000–1006，即第一章：恒山村 → 村郊 → 清溪谷，对应 1–10 级）
#       CHAPTER_LAST=1053 python3 tools/story_import.py   接入更多任务（同时要补 NPC / 关卡 / 地图摆位）
# 原任务目标类型：0 找人交谈 / 1 采集 / 2 首领战 / 4 清怪计数；原 NPC、首领、怪物编号在下面映射到本游戏的 id。
import json, re, os
R = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
src = json.load(open(R + '/story-src/main_story_existing.json'))['tasks']
# 目标信息（类型/NPC/目标键）取自服务端任务表 data_task_defs；已抽成下面这张精简表，避免依赖 SQL
T = json.load(open(R + '/story-src/task_targets.json'))
FIRST, LAST = 1000, int(os.environ.get('CHAPTER_LAST', 1006))

NPC = {'10000000': 'elder', '10000003': 'zhanhw', '10000002': 'bianque', '10000005': 'qingxizi', '10000007': 'jy_dao', '10000008': 'jy_yin',
       '10000009': 'jy_mo', '10000010': 'zc_elder', '10000012': 'xiaohs', '10000013': 'hanxin', '10000014': 'equipman', '10000018': 'baisu',
       '10000026': 'xiaohs2', '10000011': 'zc_soldier'}
# 首领战 / 清怪 / 采集 → 关卡 id、所在地图、地图上出现的位置
FIGHT = {'boss_0': ('s_zhw', 'outskirts', [480, 1040]), 'boss_1': ('s_xinmo', 'outskirts', [700, 930]), 'boss_2': ('s_mystery', 'wild', [700, 380]),
         'boss_3': ('s_xieyue', 'wild', [560, 330]), 'boss_4': ('s_hanxin', 'mist', [1100, 1180]), 'boss_5': ('s_shadow', 'town', [1500, 620]),
         'boss_6': ('s_mengyuan', 'town', [1000, 760]), 'boss_7': ('s_tianxing', 'town', [1040, 700])}
KILL = {'1004': ('s_worm', 'wild', [520, 300]), '1003': ('s_tiger', 'wild', [800, 460]), '1005': ('s_snake', 'town', [640, 900])}
SPOT = {'cj1000': ('creek', [1060, 1230], '神韵')}
NPC_MAP = {'elder': 'village', 'zhanhw': 'outskirts', 'bianque': 'outskirts', 'qingxizi': 'creek', 'jy_dao': 'creek', 'jy_yin': 'creek', 'jy_mo': 'creek',
           'zc_elder': 'wild', 'xiaohs': 'wild', 'hanxin': 'mist', 'equipman': 'town', 'baisu': 'town', 'xiaohs2': 'town'}
# 每个任务想让玩家拿到的总经验（战斗经验已含在内，脚本会扣掉战斗那部分）。1–10 级共需约 5400 经验。
TOTAL = dict(zip(range(1000, 1024), [120, 220, 280, 320, 220, 220, 280, 220, 320, 260, 320, 150, 300, 150, 150, 200, 300, 260, 200, 300, 260, 320, 320, 340]))
BATTLE_EXP = {'s_zhw': 60, 's_xinmo': 90, 's_mystery': 120, 's_xieyue': 160, 's_hanxin': 180, 's_shadow': 200, 's_mengyuan': 220, 's_tianxing': 250,
              's_worm': 25, 's_tiger': 30, 's_snake': 35}
ITEMS = {1006: {'mp_potion': 2}, 1012: {'ore': 2}, 1015: {'iron_sword': 1}, 1023: {'sachet': 1, 'hp_potion': 3}}
KNOWN = ['你', '村长', '展护卫', '扁老鹊', '清溪子', '陨神残念', '墨家接引师', '道家接引师', '阴阳接引师', '赵村村长', '除魔军士卒', '邪月', '萧恨水', '韩信',
         '装备商人', '白素', '暗影魔将', '梦渊尸魔', '天星子的身影', '心魔']

def page(l):
    l = l.strip()
    m = re.match(r'^([^：:，。]{1,12})[：:](.*)$', l)
    if m:
        w, t = m.group(1), m.group(2).strip()
        if w in KNOWN: return [w, t]
        for k in sorted(KNOWN, key=len, reverse=True):            # “展护卫收起兵刃：……”→ 说话人 展护卫
            if w.startswith(k) and len(w) > len(k): return [k, '（' + w[len(k):] + '）' + t]
    return ['', l]
pages = lambda ls: [page(l) for l in ls]
strip = lambda s: re.sub(r'<[^>]+>', '', s)

quests = []
for k in range(FIRST, LAST + 1):
    q = src[str(k)]; tg = T[str(k)]; beats = []; battle = 0
    for i, t in enumerate(tg):
        key, ptype, npc, desc, n = t['key'], t['type'], t['npc'], strip(t['desc']), t['sum']
        st = q['stages'][i] if i < len(q['stages']) else []
        b = {'goal': desc, 'pages': pages(st)}
        if ptype == 2:
            sid, mp, at = FIGHT[key]; b.update(type='fight', stage=sid, map=mp, at=at, pre=b.pop('pages')); battle += BATTLE_EXP[sid]
        elif ptype == 4:
            sid, mp, at = KILL[key]; b.update(type='kill', stage=sid, map=mp, at=at, n=n, pre=b.pop('pages')); battle += BATTLE_EXP[sid] * n
        elif ptype == 1:
            mp, at, nm = SPOT[key]; b.update(type='spot', npc='spot_' + key, map=mp, at=at, spot=nm)
        else:
            b.update(type='talk', npc=NPC[npc], map=NPC_MAP[NPC[npc]])
        beats.append(b)
    mg = []                                                   # 同一任务里连续找同一个人说话的阶段合并成一次对白，少点几下
    for b in beats:
        if mg and b['type'] == 'talk' and mg[-1]['type'] == 'talk' and mg[-1]['npc'] == b['npc']:
            mg[-1]['pages'] += b['pages']; mg[-1]['goal'] = b['goal']
        else: mg.append(b)
    beats = mg
    reward = {'exp': max(20, TOTAL[k] - battle), 'coin': 60 + (40 if any(b['type'] == 'fight' for b in beats) else 0)}
    if k % 2 == 0 and k not in ITEMS: reward['items'] = {'hp_potion': 2}
    if k in ITEMS: reward['items'] = ITEMS[k]
    quests.append({'id': str(k), 'title': q['title'], 'chapter': q['chapter'], 'summary': q['summary'], 'accept': pages(q['accept']),
                   'beats': beats, 'complete': pages(q['complete']), 'reward': reward})
out = '// 由 tools/story_import.py 生成（原剧本：story-src/main_story_existing.json）。不要手改，改映射表后重新运行脚本。\n'
out += 'window.STORY = ' + json.dumps({'quests': quests}, ensure_ascii=False, separators=(',', ':')) + ';\n'
open(R + '/js/story.js', 'w').write(out)
print(len(quests), 'quests,', sum(len(q['beats']) for q in quests), 'beats; total exp ~', sum(q['reward']['exp'] + sum(BATTLE_EXP[b['stage']] * b.get('n', 1) for b in q['beats'] if 'stage' in b) for q in quests))
