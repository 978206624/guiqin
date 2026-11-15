# 素材来源与流程

- **全部原图在 `art-src/`**，不要只留 `assets/`（那是压缩后的构建产物，可由 `python3 build.py` 重新生成）。
- 生成方式：gpt-image-2（脚本 `tools/gen/`，提示词 `art-src/prompts/`）。角色/道具/图标先生成整张精灵表（纯色背景），`build.py` 做色键抠图、裁切、缩放成 webp（2 倍图）。
- 地图：恒山村郊 / 清溪谷 / 云雾谷 / 石松林 / 恶狼谷 为用户提供图（`outskirts/creek/mist/pine/wolfvalley.jpg`）；
  汉中郡 `town6.jpg`（5324×3042，由 `town3.jpg` 扩图后分块重绘）；门派三图 `tx_*.png`（原图 `taixu_*.jpg`）；世界图 `world2.png`。
- 体积：单文件版需 <16MB（发布限制），`build.py` 对大底图转 WebP（质量见循环表，如 bg_town 54、bg_village 78）。原图保留高质量。
- 碰撞图：`tools/collision.py` → `js/masks.js` / `data/collision`；水面：`tools/water.py`。
- 新增地图：放底图到 `art-src/`，在 `build.py` 加切图行，在 `js/data.js` 的 `maps` 加配置（含 `world:[x,y]`），再 `python3 build.py && node tools/export_data.js`。
