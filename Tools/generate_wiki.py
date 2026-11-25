"""Build the offline illustrated Wiki from Docs/WIKI.md and shipped game artwork.

    python Tools/generate_wiki.py
    python Tools/generate_wiki.py --check

Requires the project's existing Pillow dependency. No web services or npm build.
"""
from __future__ import annotations

import argparse
import base64
import hashlib
import html
import io
import json
import re
from pathlib import Path
from html.parser import HTMLParser

from PIL import Image
from Wiki.entries import EntryCatalog

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Docs/WIKI.md"
OUTPUT = ROOT / "Docs/WIKI.html"
PARTS = ROOT / "Tools/Wiki"
ART = ROOT / "Assets/DeadCircuit/Resources/Art"
CORE = ROOT / "Assets/DeadCircuit/Scripts/Core"
TICK = chr(96)

PAGES = [
    ("overview", "游戏概览", "开始阅读", "认识世界、合作方式与主要玩法。", "build_10"),
    ("start", "快速开局", "开始阅读", "从自然出生到第一座工坊，走通生存的第一步。", "gear_0"),
    ("controls", "操作与界面", "开始阅读", "按键、背包、科技树与设备操作速查。", "part_0"),
    ("survival", "生存与补给", "生存与探索", "饮水、食物、种植和野生动物。", "food_0"),
    ("world", "探索与资源", "生存与探索", "十种群系、有限矿藏、聚落与地图。", "biome_1"),
    ("building", "建造与基地", "生存与探索", "围墙、门、工坊和全部设备目录。", "build_10"),
    ("technology", "科技与工业", "工业与科技", "从原始工艺到核工业，理解真正的解锁条件。", "item_30"),
    ("recipes", "生产配方速查", "工业与科技", "材料目录与关键生产链，按图寻找下一道工序。", "item_38"),
    ("power", "能源与物流", "工业与科技", "动力、电网、运输，以及核设施安全作业。", "build_29"),
    ("weapons", "武器与工具", "装备与战斗", "武器图鉴、工具用途、弹药与维护。", "gear_8"),
    ("armor", "防具与配件", "装备与战斗", "三档背包、穿戴防具、枪械配件与核作业防护服。", "gear_17"),
    ("combat", "战斗与复活", "装备与战斗", "攻击、敌人、尸潮和死亡后的救援。", "enemy_1"),
    ("growth", "角色成长", "装备与战斗", "人物属性、技能点和三个专精方向。", "gear_5"),
    ("multiplayer", "合作与存档", "资料与帮助", "四人合作、身份、保存与读取。", "build_7"),
    ("faq", "常见问题", "资料与帮助", "卡在开局、研究或产线时，先从这里找原因。", "item_45"),
    ("roadmap", "规划与当前边界", "资料与帮助", "分清已经可以游玩的内容与后续设计。", "item_60"),
    ("maintenance", "Wiki维护", "资料与帮助", "规则来源、更新方式和核对记录。", "item_11"),
]
PAGE_IDS = {title: slug for slug, title, *_ in PAGES}
SECTION_ROUTES = {"背包与负重": "armor/背包与负重"}
ERA_LABELS = {"primitive": "原始", "water": "水力", "steam": "蒸汽", "lv": "LV", "mv": "MV", "hv": "HV", "nuclear": "核工业"}
ERA_KEYS = {label: key for key, label in ERA_LABELS.items()}


def esc(text: object) -> str:
    return html.escape(str(text), quote=True)


def read_array(filename: str, name: str) -> list[str]:
    text = (CORE / filename).read_text(encoding="utf-8-sig")
    match = re.search(r"\b" + name + r"\s*=\s*\{([^}]+)\}", text)
    if not match:
        raise ValueError(f"Missing source catalog: {filename}/{name}")
    return re.findall(r'"([^"\n]+)"', match.group(1))


class ArtBank:
    def __init__(self) -> None:
        self.sprites: dict[str, tuple[Path, dict]] = {}
        self.images: dict[Path, Image.Image] = {}
        self.encoded: dict[str, str] = {}
        self.resolved: dict[str, str] = {}
        self.fallbacks: set[str] = set()
        art_source = (ROOT / "Assets/DeadCircuit/Scripts/Presentation/DemoArt.cs").read_text(encoding="utf-8-sig")
        self.game_fallbacks = {}
        for cases, fallback in re.findall(r'((?:case\s+"[^"]+":\s*)+)fallback\s*=\s*"([^"]+)"', art_source):
            for name in re.findall(r'case\s+"([^"]+)"', cases):
                self.game_fallbacks[name] = fallback
        sources = [(ART / "atlas.json", ART / "sprites.png")]
        for name in ("world", "icons", "modules", "landmarks", "floors", "biomes", "weapons", "equipment-icons", "completion"):
            sources.append((ART / "Expansion" / f"{name}.json", ART / "Expansion" / f"{name}.png"))
        for atlas_path, image_path in sources:
            data = json.loads(atlas_path.read_text(encoding="utf-8-sig"))
            for sprite in data["sprites"]:
                self.sprites[sprite["name"]] = (image_path, sprite)

    def resolve(self, key: str) -> str:
        if key in self.resolved:
            return self.resolved[key]
        candidates = [key]
        if key.startswith("gear_"):
            number = int(key[5:])
            # Match DemoArt.GetIcon/Get: specialized equipment art, weapon arms, then direct gear.
            candidates = [f"equipment_icon_gear_{number}", f"weapon_icon_{number}", key]
        elif key.startswith("part_"):
            candidates = ["equipment_icon_" + key, key]
        elif key.startswith("build_"):
            number = int(key[6:])
            candidates = [f"machine_{number}", f"build_{number}", f"module_{number}"]
            aliases = {0: "build_wall_0", 1: "build_generator_0", 2: "build_miner_0", 3: "build_belt_0", 4: "build_smelter_0", 5: "build_assembler_0", 6: "build_turret_0", 7: "build_chest_0", 66: "machine_10"}
            if number in aliases:
                candidates.append(aliases[number])
        elif key.startswith("enemy_"):
            candidates = [key, "zombie_" + key[6:], "zombie_0_0"]
        elif key.startswith("creature_"):
            candidates = [key, "animal_" + key[9:]]
        direct_candidates = set(candidates)
        for candidate in tuple(candidates):
            if candidate in self.game_fallbacks:
                candidates.append(self.game_fallbacks[candidate])
        for candidate in candidates:
            if candidate in self.sprites:
                self.resolved[key] = candidate
                if candidate not in direct_candidates:
                    self.fallbacks.add(key)
                return candidate
        raise ValueError(f"No game artwork for {key}: {candidates}")

    def uri(self, key: str) -> str:
        resolved = self.resolve(key)
        if resolved not in self.encoded:
            path, sprite = self.sprites[resolved]
            if path not in self.images:
                self.images[path] = Image.open(path).convert("RGBA")
            width = sprite.get("w", sprite.get("width"))
            height = sprite.get("h", sprite.get("height"))
            x, y = sprite["x"], sprite["y"]
            crop = self.images[path].crop((x, y, x + width, y + height))
            if crop.getbbox() is None:
                raise ValueError(f"Empty sprite {resolved}")
            buffer = io.BytesIO()
            crop.save(buffer, "WEBP", lossless=True, method=6)
            self.encoded[resolved] = "data:image/webp;base64," + base64.b64encode(buffer.getvalue()).decode("ascii")
        return self.encoded[resolved]

    def img(self, key: str, label: str = "", cls: str = "entity-icon") -> str:
        return f'<img class="{cls}" src="{self.uri(key)}" alt="{esc(label)}" loading="lazy" decoding="async">'

    def hero(self) -> str:
        image = Image.open(ART / "TitlePanorama.png").convert("RGB")
        image.thumbnail((1600, 900), Image.Resampling.LANCZOS)
        buffer = io.BytesIO()
        image.save(buffer, "WEBP", quality=86, method=6)
        return "data:image/webp;base64," + base64.b64encode(buffer.getvalue()).decode("ascii")


class WikiRenderer:
    def __init__(self, source: str, art: ArtBank) -> None:
        self.source, self.art = source, art
        self.items = read_array("DemoState.cs", "ItemNames")
        self.buildings = read_array("DemoState.cs", "BuildNames")
        self.gear = read_array("EquipmentCatalog.cs", "GearNames")
        self.parts = read_array("EquipmentCatalog.cs", "PartNames")
        self.foods = re.findall(r'new SurvivalFoodDefinition\(FoodKind\.\w+, "([^"\n]+)"', (CORE / "SurvivalCatalog.cs").read_text(encoding="utf-8-sig"))
        self.sections = dict(re.findall(r"^## ([^\n]+)\n(.*?)(?=^## |\Z)", source, re.M | re.S))
        self.date = re.search(r"最后核对：(\d{4}-\d{2}-\d{2})", source).group(1)
        self.baseline = re.search(r"规则基线：" + re.escape(TICK) + r"([^" + TICK + "]+)", source).group(1)
        self.catalog = EntryCatalog(ROOT, {"item": self.items, "building": self.buildings, "gear": self.gear, "part": self.parts, "food": self.foods})
        self.gear_categories = [self.catalog.entries[f"gear-{index}"]["category"] for index in range(len(self.gear))]
        self.term_names = self.catalog.names
        self.term_pattern = re.compile("|".join(re.escape(name) for name in sorted(self.term_names, key=len, reverse=True)))
        self.research_names = {entry["name"]: entry["id"] for entry in self.catalog.entries.values() if entry["id"].startswith("research-")}

    def entry_link(self, slug: str, label: str | None = None, illustrated: bool = False, current: str = "") -> str:
        entry = self.catalog.entries[slug]
        text = label or entry["title"]
        icon = self.art.img(entry["icon"]) if illustrated and entry["icon"] else ""
        if slug == current:
            return icon + esc(text)
        cls = "wiki-term entity-pill" if icon else "wiki-term"
        return f'<a class="{cls}" href="#/{slug}" data-entry="{slug}" title="{esc(entry["summary"][:130])}">{icon}<span>{esc(text)}</span></a>'

    def inline(self, text: str, page: str, autolink: bool = True, illustrated: bool = False) -> str:
        tokens: list[str] = []
        def save(fragment: str) -> str:
            tokens.append(fragment)
            return f"\x00{len(tokens)-1}\x00"
        text = re.sub(re.escape(TICK) + "([^" + TICK + "]+)" + re.escape(TICK), lambda m: save(f"<code>{esc(m.group(1))}</code>"), text)
        def link(match: re.Match) -> str:
            label, target = match.group(1), match.group(2)
            if target.startswith("#"):
                raw = target[1:]
                target = "#/" + SECTION_ROUTES.get(raw, PAGE_IDS.get(raw, raw if raw != "wiki维护" else "maintenance"))
            if re.match(r"(?i)(?:javascript|data):", target):
                raise ValueError("Unsafe document link")
            return save(f'<a href="{esc(target)}">{esc(label)}</a>')
        text = re.sub(r"\[([^\]]+)\]\(([^)]+)\)", link, text)
        if autolink:
            def term(match: re.Match) -> str:
                name = match.group(0)
                slug = self.research_names.get(name, self.term_names[name]) if page == "technology" else self.term_names[name]
                return save(self.entry_link(slug, name, illustrated, current=page))
            text = self.term_pattern.sub(term, text)
        text = esc(text)
        text = re.sub(r"\*\*([^*]+)\*\*", r"<strong>\1</strong>", text)
        for index, token in enumerate(tokens):
            text = text.replace(f"\x00{index}\x00", token)
        return text

    def illustrated(self, text: str, page: str) -> str:
        return self.inline(text, page, illustrated=True)

    def markdown(self, text: str, page: str) -> str:
        lines, output, index = text.strip().splitlines(), [], 0
        while index < len(lines):
            line = lines[index].strip()
            if not line:
                index += 1
                continue
            heading = re.match(r"^(#{3,6}) (.+)", line)
            if heading:
                level = min(4, len(heading.group(1)) - 1)
                title = heading.group(2)
                anchor = re.sub(r"\s+", "-", title.lower())
                output.append(f'<h{level} id="{page}--{esc(anchor)}" data-anchor="{esc(anchor)}">{self.inline(title,page,autolink=False)}</h{level}>')
                index += 1
                continue
            if line.startswith("|"):
                rows = []
                while index < len(lines) and lines[index].strip().startswith("|"):
                    cells = [cell.strip() for cell in lines[index].strip().strip("|").split("|")]
                    if not all(re.fullmatch(r":?-+:?", cell) for cell in cells):
                        rows.append(cells)
                    index += 1
                output.append('<div class="table-wrap" tabindex="0" role="region" aria-label="可横向滚动的资料表"><table><thead><tr>')
                output.extend(f'<th scope="col">{self.inline(cell,page)}</th>' for cell in rows[0])
                output.append('</tr></thead><tbody>')
                for row in rows[1:]:
                    output.append('<tr>')
                    for cell in row:
                        visual = page in ("recipes", "building", "weapons", "armor", "survival", "power") and "[" not in cell and TICK not in cell
                        if visual and page == "recipes" and "→" in cell:
                            inputs, outputs = cell.split("→", 1)
                            content = '<div class="recipe-flow"><span class="recipe-side">' + self.illustrated(inputs.strip(),page) + '</span><span class="recipe-arrow" aria-label="产出">→</span><span class="recipe-side">' + self.illustrated(outputs.strip(),page) + '</span></div>'
                        else:
                            content = self.illustrated(cell,page) if visual else self.inline(cell,page)
                        output.append('<td>' + content + '</td>')
                    output.append('</tr>')
                output.append('</tbody></table></div>')
                continue
            unordered = line.startswith("- ")
            ordered = bool(re.match(r"\d+\. ", line))
            if unordered or ordered:
                tag = "ul" if unordered else "ol"
                output.append(f'<{tag} class="guide-list">')
                while index < len(lines):
                    candidate = lines[index].strip()
                    match = re.match(r"- (.+)" if unordered else r"\d+\. (.+)", candidate)
                    if not match:
                        break
                    output.append(f'<li>{self.inline(match.group(1),page)}</li>')
                    index += 1
                output.append(f'</{tag}>')
                continue
            paragraph = [line]
            index += 1
            while index < len(lines) and lines[index].strip() and not re.match(r"(?:#{3,6} |\||- |\d+\. )", lines[index].strip()):
                paragraph.append(lines[index].strip())
                index += 1
            content = self.inline(" ".join(paragraph), page)
            if line.startswith("来源："):
                output.append(f'<details class="sources"><summary>规则来源与相关资料</summary><p>{content}</p></details>')
            elif page == "faq" and line.startswith("**"):
                question = re.match(r"\*\*(.+?)\*\*(.*)", " ".join(paragraph))
                output.append(f'<details class="faq-item"><summary>{esc(question.group(1))}</summary><p>{self.inline(question.group(2).strip(),page)}</p></details>')
            else:
                output.append(f'<p>{content}</p>')
        return "\n".join(output)

    def gallery(self, title: str, indices: list[int], mode: str = "gear") -> str:
        labels = self.gear if mode == "gear" else self.parts
        selected_eras = []
        cards = []
        for number in indices:
            slug = f"{mode}-{number}"
            entry = self.catalog.entries[slug]
            era = ERA_KEYS[entry["era"]]
            if era not in selected_eras:
                selected_eras.append(era)
            kind = entry["category"]
            cards.append(f'<article class="item-card" data-era="{era}" data-name="{esc(labels[number])}"><a class="item-entry" href="#/{slug}" data-entry="{slug}"><div class="item-art">{self.art.img(f"{mode}_{number}",labels[number],"catalog-art")}</div><span class="item-type">{kind} · {ERA_LABELS[era]}</span><h3>{esc(labels[number])}</h3></a></article>')
        filters = '<button type="button" data-filter="all" aria-pressed="true">全部</button>' + ''.join(f'<button type="button" data-filter="{era}" aria-pressed="false">{ERA_LABELS[era]}</button>' for era in selected_eras)
        return f'<section class="gallery" data-gallery="{mode}-{indices[0]}"><div class="section-heading"><h2>{title}</h2><span>{len(indices):02d} 条目</span></div><div class="gallery-tools"><div class="filter-list" role="group" aria-label="按时代筛选">{filters}</div><label class="gallery-search"><span class="sr-only">搜索{title}</span><input type="search" data-gallery-search placeholder="搜索名称…"></label></div><div class="gallery-grid">{"".join(cards)}</div><p class="gallery-empty" hidden>没有符合条件的条目，试试其他名称或时代。</p></section>'

    def illustration(self, page: str) -> str:
        if page == "weapons":
            return self.gallery("武器与工具图鉴", [index for index, category in enumerate(self.gear_categories) if category in ("武器", "工具")])
        if page == "armor":
            packs = [index for index, category in enumerate(self.gear_categories) if category == "背包"]
            armor = [index for index, category in enumerate(self.gear_categories) if category == "防具"]
            return self.gallery("背包图鉴", packs) + self.gallery("防具图鉴", armor) + self.gallery("配件图鉴", list(range(len(self.parts))), "part")
        if page in ("survival", "world", "combat", "building", "power", "technology", "recipes"):
            collections = {
                "survival": [("food_0", "浆果"), ("food_14", "烤肉"), ("food_21", "蔬菜炖汤"), ("creature_0", "野兔"), ("creature_4", "狼"), ("creature_5", "熊")],
                "world": [(f"biome_{i}", name) for i, name in enumerate(("草甸", "阔叶林", "针叶林", "荒漠", "湿地", "冻原", "沼泽", "雪山", "樱花林", "河谷"))],
                "combat": [(f"enemy_{i}", name) for i, name in enumerate(("普通", "快跑", "暴徒", "爬行", "喷吐", "装甲", "燃烧", "尖啸", "防化", "冻伤", "自爆", "夜行精英"))],
                "building": [(f"build_{i}", self.buildings[i]) for i in (10, 12, 15, 48, 56, 29)],
                "power": [(f"build_{i}", self.buildings[i]) for i in (12, 35, 19, 1, 21, 29)],
                "technology": [(f"item_{i}", self.items[i]) for i in (27, 29, 10, 44, 50, 60, 15)],
                "recipes": [(f"item_{i}", self.items[i]) for i in (0, 27, 28, 4, 10, 60)],
            }
            caption = {"world": "群系图鉴", "combat": "敌人图鉴", "survival": "补给与野生动物", "building": "基地设备", "power": "动力设备", "technology": "工业进程", "recipes": "材料样本"}[page]
            cards = ''.join(f'<figure class="catalog-preview"><a class="item-entry" href="#/{self.catalog.icons[key]}" data-entry="{self.catalog.icons[key]}">{self.art.img(key,label,"preview-art")}<figcaption>{label}</figcaption></a></figure>' for key, label in collections[page])
            return f'<div class="chapter-illustration"><span class="eyebrow">{caption}</span><div class="catalog-strip">{cards}</div><p class="figure-caption">图示取自游戏资源，部分沿用游戏当前的复用图；具体机制见下文。</p></div>'
        return ''

    def quantity_links(self, values: list[tuple[str, int]], current: str) -> str:
        return ' <span class="quantity-separator">+</span> '.join((self.entry_link(slug, illustrated=True, current=current) if slug in self.catalog.entries else esc(slug)) + f' <b>×{quantity}</b>' for slug, quantity in values) or "无"

    def recipe_table(self, recipes: list[dict], current: str) -> str:
        if not recipes:
            return ''
        rows = []
        for recipe in recipes:
            ref = "recipe-" + str(recipe["id"])
            seconds = f'{recipe["seconds"]}秒基础周期' if recipe["seconds"] else '生存烹饪操作'
            rows.append('<tr><td>' + self.entry_link(ref, current=current) + '</td><td>' + self.quantity_links(recipe["inputs"],current) + '</td><td>' + self.quantity_links(recipe["outputs"],current) + '</td><td>' + self.entry_link(recipe["station"],current=current) + '</td><td>' + esc(recipe["era"] + ' · ' + seconds) + '</td></tr>')
        return '<div class="table-wrap" role="region" tabindex="0" aria-label="相关配方"><table><thead><tr><th>配方</th><th>输入</th><th>产出</th><th>工位</th><th>时代 / 周期</th></tr></thead><tbody>' + ''.join(rows) + '</tbody></table></div>'

    def entity_article(self, entry: dict) -> str:
        slug = entry["id"]
        def heading(title: str) -> str:
            return f'<h2 id="{slug}--{title}" data-anchor="{title}">{title}</h2>'
        facts = [('分类', entry['category'])]
        if entry.get("era"):
            facts.append(('所属时代', entry['era']))
        facts += entry['facts']
        visual = self.art.img(entry['icon'], entry['name'], 'infobox-art') if entry['icon'] else '<span class="infobox-symbol" aria-hidden="true">◇</span>'
        info = f'<aside class="entity-infobox" aria-label="{esc(entry["title"])}基本资料"><p class="infobox-caption">{esc(entry["title"])}</p>{visual}<dl class="infobox-facts">' + ''.join(f'<dt>{esc(label)}</dt><dd>{esc(value)}</dd>' for label,value in facts) + '</dl></aside>'
        body = [info, '<p class="entity-overview">' + self.inline(entry['summary'],slug) + '</p>']
        if entry.get('parents'):
            body += [heading('前置研究'), '<p>' + '、'.join(self.entry_link(ref,current=slug) for ref in entry['parents']) + '</p>']
        if entry.get('research'):
            if entry.get('module_costs') or entry.get('recipe', {}).get('station') in {value['id'] for value in self.catalog.entries.values() if value.get('module_costs')}:
                body += [heading('建造与运行研究' if entry.get('module_costs') else '配方研究'), '<p>大型机械及其工艺需要完成 ' + self.entry_link(entry['research'],current=slug) + '，并满足时代、结构成形、供能、维护、输入与输出空间条件。</p>']
            else:
                body += [heading('制作研究'), '<p>自主制作需要完成 ' + self.entry_link(entry['research'],current=slug) + '，并满足时代、工位、材料及容量条件。成品使用与制造研究分别判断。</p>']
        if entry.get('cost'):
            body += [heading('最小职责集合报价' if entry.get('module_costs') else '研究样品' if '研究' in entry['category'] else '制造材料'), '<p class="entry-materials">' + self.quantity_links(entry['cost'],slug) + '</p>']
        if entry['category'] == '背包':
            station = self.entry_link(entry['station'], current=slug) if entry['station'] else None
            craft = '靠近无遮挡的' + station + '制作。' if station else '在背包面板随身制作。'
            body += [heading('制作与穿戴'), '<p>' + craft + ' 拾起地上的包会立即穿戴，原包连同全部内容落地。具体操作见<a href="#/armor/背包与负重">背包与负重</a>。</p>']
        if entry.get('module_costs'):
            rows = ''.join('<tr><td>' + esc(module['name']) + '</td><td>' + str(module['minimum']) + '</td><td>' + self.quantity_links(module['cost'],slug) + '</td></tr>' for module in entry['module_costs'])
            body += [heading('逐格施工材料'), '<p>先手持有效扳手，在T中选机器、从八类模块菜单选控制器起画，再逐格自由放置；R独立旋转当前模块，擦图删除单格。上方合计仅是最小职责集合报价，不是固定3×3模板，也不在画图时预扣。退出绘制后对具体格按E分批投料，增加机壳另付该格材料；包围盒内空洞不占地。</p>', '<div class="table-wrap" role="region" tabindex="0" aria-label="逐格施工材料"><table><thead><tr><th>模块</th><th>最少格数</th><th>每格材料</th></tr></thead><tbody>' + rows + '</tbody></table></div>']
        if entry.get('production'):
            body += [heading('累计实产要求'), '<p>全队实际生产累计值达到以下数量；不是背包中的现有库存，也不会因研究而扣除这份累计记录。</p>', '<p>' + self.quantity_links(entry['production'],slug) + '</p>']
        if '研究' in entry['category'] and not entry.get('production'):
            body.append('<p class="callout">上述样品和前置不是全部研究条件。工业研究还可能要求专门的有效运行、真实加工或到货记录，具体缺项请在游戏科技树中查看。</p>')
        if entry.get('recipe'):
            body += [heading('配方'), self.recipe_table([entry['recipe']],slug)]
        produced = self.catalog.production(slug)
        if produced:
            body += [heading('获取'), self.recipe_table(produced,slug), '<p>表中数量为单批配方。还需满足相应研究、供能、燃料和整批输出空间条件；副产物满仓也会导致停产。</p>']
        elif slug.startswith('item-'):
            raw = {
                0: '用斧砍树取得。手工采尽时还可能结算野食。',
                1: '用镐开采铁矿节点取得。',
                6: '普通补给来源之一；与生存系统中的具体食材分别管理。',
                7: '用镐开采铜矿节点取得，后续通过冶炼加工。',
                14: '用镐开采铀矿节点取得，核工业燃料生产前还需铀矿选冶。',
                17: '反应堆消耗燃料时生成，须由专用仓接收，不能按普通背包物品收发。',
                19: '用镐开采锡矿节点取得，后续通过冶炼加工。',
                25: '用镐开采石材节点取得，用于基础工具研究、石器维修和建造。',
                26: '用镐开采黏土节点取得，供陶砖与绝缘件生产。',
                32: '用镐开采煤矿节点取得，用于焦化和热工。',
                33: '用镐开采石灰石节点取得，供生铁和钢铁工序使用。',
                34: '用镐开采耐火黏土节点取得，供耐火砖烧制。',
                46: '用邻近油源的通电抽油机从有限油藏按周期取出。',
                52: '工艺取水泵从有效水源取得，不能代替生活饮水。',
                54: '用镐开采石英节点取得，用于工业硅制造。',
                63: '在出生附近或后续区块的地面用左键、E或按住Space拾取。',
                64: '在地面直接拾取，用于随身制作石斧和石镐。采矿产出的石材另有用途。',
                65: '在出生附近或后续区块直接拾取。首堆有3份，可各用1份随身制作石斧、石镐和草绳背包。',
            }
            description = raw.get(int(slug.split('-')[1]), '获取方式请结合下方相关说明和游戏当前工艺界面查看。')
            body += [heading('获取'), '<p>' + self.inline(description,slug) + '</p>']
        uses = self.catalog.uses(slug)
        if uses:
            body += [heading('用途'), self.recipe_table(uses,slug)]
        manufactured = self.catalog.cost_uses(slug)
        if manufactured:
            body += [heading('作为制造或研究材料'), '<ul class="entry-index-list">' + ''.join('<li>' + self.entry_link(value['id'],current=slug) + '</li>' for value in manufactured) + '</ul>']
        unlocked = [value for value in self.catalog.entries.values() if value.get('research') == slug]
        if unlocked:
            body += [heading('解锁内容'), '<ul class="entry-index-list">' + ''.join('<li>' + self.entry_link(value['id'],current=slug) + '</li>' for value in unlocked) + '</ul>']
        if entry.get('ammo'):
            body += [heading('弹药'), '<p>基础兼容弹种：' + self.entry_link(entry['ammo'],current=slug) + '。步枪还可能使用穿甲弹，具体装填规则见' + '<a href="#/weapons">武器与工具</a>。</p>']
        mentions = []
        for page,title,*_ in PAGES:
            if page in ('maintenance','roadmap') or entry['name'] not in self.sections[title]:
                continue
            paragraphs = [value for value in self.sections[title].split('\n\n') if entry['name'] in value and not value.startswith(('|','#','来源：')) and len(value) <= 650 and value.strip()]
            if paragraphs:
                quote = paragraphs[0].strip()
                mentions.append('<p><a href="#/' + page + '">' + esc(title) + '</a> · ' + self.inline(quote,slug) + '</p>')
            if len(mentions) == 2:
                break
        if mentions:
            body += [heading('相关说明'), *mentions]
        body += [heading('参见'), '<div class="entry-related"><a href="#/' + entry['parent'] + '">返回主题指南</a><a href="#/entries">全部词条</a></div>', '<details class="sources"><summary>条目数据来源</summary><p><a href="../Assets/DeadCircuit/Scripts/Core/' + entry['source'] + '">' + entry['source'] + '</a> · 按当前游戏目录生成；不等同于现实工艺规范。</p></details>']
        return '<section class="article-page entity-page" id="entry-' + slug + '" data-page-id="' + slug + '" hidden><header class="article-header"><div class="breadcrumbs"><a href="#/home">百科首页</a><span>/</span><a href="#/entries">词条索引</a><span>/</span>' + esc(entry['category']) + '</div><p class="eyebrow">DEAD CIRCUIT / 词条</p><h1>' + esc(entry['title']) + '</h1></header><div class="article-body">' + ''.join(body) + '</div><footer class="article-footer">规则核对：' + self.date + ' · <a href="#/entries">全部词条</a></footer></section>'

    def entry_index(self) -> str:
        groups: dict[str,list[dict]] = {}
        for entry in self.catalog.entries.values():
            groups.setdefault(entry['category'], []).append(entry)
        body = []
        for category, values in groups.items():
            body.append('<h2 id="entries--' + category + '" data-anchor="' + category + '">' + category + f' <small>{len(values)}</small></h2><ul class="entry-index-list">')
            body += ['<li>' + self.entry_link(value['id'],illustrated=True) + '</li>' for value in values]
            body.append('</ul>')
        return '<section class="article-page entry-index" id="entry-entries" data-page-id="entries" hidden><header class="article-header"><div class="breadcrumbs"><a href="#/home">百科首页</a><span>/</span>词条索引</div><p class="eyebrow">DEAD CIRCUIT / INDEX</p><h1>词条索引</h1><p class="article-summary">按物品、设备、装备、食物、研究与配方分类，点击名称可连续查阅获取方式和用途。</p></header><div class="article-body">' + ''.join(body) + '</div></section>'

    def build(self) -> tuple[str, dict]:
        entries, nav, articles = [], [], []
        for name in self.items + self.buildings + self.gear + self.parts:
            if name not in self.source:
                raise ValueError(f"Wiki source needs a catalog update: {name}")
        research_names = []
        for filename in ("PrimitiveCatalog.cs", "WaterCatalog.cs", "SteamCatalog.cs", "LowVoltageCatalog.cs", "PetrochemicalCatalog.cs", "PrecisionCatalog.cs", "NuclearCatalog.cs"):
            research_names += re.findall(r'T\(\w+Tech\.\w+,\s*"([^"]+)"', (CORE / filename).read_text(encoding="utf-8-sig"))
        for name in research_names:
            if name not in self.source:
                raise ValueError(f"Wiki source needs a research update: {name}")
        previous_category = None
        for slug, title, category, summary, icon in PAGES:
            if title not in self.sections:
                raise ValueError(f"Missing Wiki section: {title}")
            if category != previous_category:
                if previous_category:
                    nav.append('</div>')
                nav.append(f'<div class="nav-group"><p class="nav-group-title">{category}</p>')
                previous_category = category
            nav.append(f'<a class="nav-link" data-page="{slug}" href="#/{slug}">{self.art.img(icon)}<span>{title}</span></a>')
            entries.append({"id": slug, "title": title, "category": category, "summary": summary, "searchText": self.sections[title]})
            articles.append(f'<section class="article-page" data-page-id="{slug}" id="entry-{slug}" hidden><header class="article-header"><div class="breadcrumbs"><a href="#/home">百科首页</a><span>/</span>{category}</div><p class="eyebrow">DEAD CIRCUIT / {category}</p><h1>{title}</h1><p class="article-summary">{summary}</p></header>{self.illustration(slug)}<div class="article-body">{self.markdown(self.sections[title],slug)}</div><footer class="article-footer">规则核对：{self.date} · <a href="WIKI.md">查看文字原文</a> · <a href="#/faq">遇到问题？</a></footer></section>')
        nav.append('</div>')
        nav.append('<div class="nav-group"><p class="nav-group-title">百科索引</p><a class="nav-link" data-page="entries" href="#/entries"><span aria-hidden="true">≡</span><span>全部词条</span></a></div>')
        entries.append({"id":"entries","title":"词条索引","category":"百科索引","summary":"全部物品、设备、装备、研究和配方。","searchText":""})
        articles.append(self.entry_index())
        for entry in self.catalog.entries.values():
            entries.append({"id":entry['id'], "title":entry['title'], "category":entry['category'], "summary":entry['summary'], "searchText":entry['name'] + ' ' + entry['summary'], "parent":entry['parent']})
            articles.append(self.entity_article(entry))
        entry_cards = []
        for slug in ("start", "survival", "world", "technology", "weapons", "building"):
            entry = next(value for value in PAGES if value[0] == slug)
            _, title, category, summary, icon = entry
            entry_cards.append(f'<a class="entry-card" href="#/{slug}"><div class="entry-card-art">{self.art.img(icon,title,"entry-art")}</div><div><span class="eyebrow">{category}</span><h3>{title}</h3><p>{summary}</p></div><span class="card-arrow" aria-hidden="true">↗</span></a>')
        process = ''.join(f'<a class="process-step" href="#/technology">{self.art.img(key,label,"process-art")}<span>{label}</span></a>' for key,label in (("item_0","采集资源"),("build_10","建立工坊"),("item_30","制造部件"),("build_29","核能体系")))
        data = {"entries": entries, "date": self.date}
        era_count = len(ERA_LABELS)
        biome_count = re.search(r"BiomeCount\s*=\s*(\d+)", (CORE / "WorldGeneration.cs").read_text(encoding="utf-8-sig")).group(1)
        player_count = re.search(r"peers.Count\s*>=\s*(\d+)", (ROOT / "Assets/DeadCircuit/Scripts/GameBridge.cs").read_text(encoding="utf-8-sig")).group(1)
        replacements = {
            "{{CSS}}": (PARTS / "site.css").read_text(encoding="utf-8"),
            "{{JS}}": (PARTS / "site.js").read_text(encoding="utf-8"),
            "{{NAV}}": ''.join(nav), "{{ARTICLES}}": ''.join(articles),
            "{{ENTRIES}}": ''.join(entry_cards), "{{PROCESS}}": process,
            "{{HERO}}": self.art.hero(), "{{DATE}}": self.date,
            "{{BASELINE}}": self.baseline,
            "{{ERAS}}": str(era_count), "{{BIOMES}}": biome_count, "{{PLAYERS}}": player_count,
            "{{DATA}}": json.dumps(data, ensure_ascii=False, separators=(",", ":")).replace("</", "<\\/"),
            "{{SOURCE_HASH}}": hashlib.sha256(self.source.encode("utf-8")).hexdigest(),
        }
        template = (PARTS / "template.html").read_text(encoding="utf-8")
        for key, value in replacements.items():
            template = template.replace(key, value)
        if re.search(r"\{\{[A-Z_]+\}\}", template):
            raise ValueError("Unresolved template slot")
        metadata = {"sections": len(PAGES), "entity_entries":len(self.catalog.entries), "items": len(self.items), "buildings": len(self.buildings), "recipes": len(self.catalog.recipes), "modular_recipes": sum(bool(self.catalog.entries[recipe['station']].get('module_costs')) for recipe in self.catalog.recipes), "research": len(self.research_names), "sprite_images": len(self.art.encoded), "reused_art_keys": len(self.art.fallbacks), "source_sha256": replacements["{{SOURCE_HASH}}"]}
        return template, metadata


class HtmlCheck(HTMLParser):
    def __init__(self) -> None:
        super().__init__()
        self.ids: set[str] = set()
        self.images = 0
        self.links: list[str] = []
        self.routes: set[str] = set()
        self.anchor_open = False

    def handle_starttag(self, tag: str, attrs: list[tuple[str,str]]) -> None:
        values = dict(attrs)
        if values.get("id"):
            if values["id"] in self.ids:
                raise ValueError(f"Duplicate HTML id: {values['id']}")
            self.ids.add(values["id"])
        if values.get("data-page-id"):
            self.routes.add(values['data-page-id'])
        if tag == "img":
            self.images += 1
            if not values.get("src", "").startswith("data:image/") or "alt" not in values:
                raise ValueError("Image must be embedded with an alt attribute")
        if tag in ("script", "link") and (values.get("src") or values.get("href")):
            raise ValueError("External runtime dependency in offline HTML")
        if tag == "a":
            if self.anchor_open:
                raise ValueError("Nested anchor in encyclopedia")
            self.anchor_open = True
            self.links.append(values.get("href", ""))

    def handle_endtag(self, tag: str) -> None:
        if tag == 'a':
            self.anchor_open = False


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Fail if published HTML differs from source/assets/templates")
    args = parser.parse_args()
    source = SOURCE.read_text(encoding="utf-8-sig").replace("\r\n", "\n")
    page, metadata = WikiRenderer(source, ArtBank()).build()
    check = HtmlCheck()
    check.feed(page)
    valid_routes = check.routes
    for link in check.links:
        if link.startswith("#/"):
            if link[2:].split("/")[0] not in valid_routes:
                raise ValueError(f"Unknown local route {link}")
        elif link and not link.startswith(("#", "http:", "https:", "mailto:")):
            if not (OUTPUT.parent / link.split("#")[0]).exists():
                raise ValueError(f"Broken reference link: {link}")
    metadata.update({"embedded_images": check.images, "bytes": len(page.encode("utf-8"))})
    if args.check:
        if not OUTPUT.exists() or OUTPUT.read_text(encoding="utf-8").replace("\r\n", "\n") != page:
            raise SystemExit("Wiki HTML is stale: run python Tools/generate_wiki.py")
        metadata["result"] = "up-to-date"
    else:
        OUTPUT.write_text(page, encoding="utf-8", newline="\n")
        metadata["result"] = "generated"
    print(json.dumps(metadata, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
