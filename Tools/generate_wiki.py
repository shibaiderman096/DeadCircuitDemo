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
    ("armor", "防具与配件", "装备与战斗", "防具部位、枪械配件与核作业防护服。", "gear_17"),
    ("combat", "战斗与复活", "装备与战斗", "攻击、敌人、尸潮和死亡后的救援。", "enemy_1"),
    ("growth", "角色成长", "装备与战斗", "人物属性、技能点和三个专精方向。", "gear_5"),
    ("multiplayer", "合作与存档", "资料与帮助", "四人合作、身份、保存与读取。", "build_7"),
    ("faq", "常见问题", "资料与帮助", "卡在开局、研究或产线时，先从这里找原因。", "item_45"),
    ("roadmap", "规划与当前边界", "资料与帮助", "分清已经可以游玩的内容与后续设计。", "item_60"),
    ("maintenance", "Wiki维护", "资料与帮助", "规则来源、更新方式和核对记录。", "item_11"),
]
PAGE_IDS = {title: slug for slug, title, *_ in PAGES}
ERA_LABELS = {"primitive": "原始", "water": "水力", "steam": "蒸汽", "lv": "LV", "mv": "MV", "hv": "HV", "nuclear": "核工业"}


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
        for name in ("world", "icons", "modules", "landmarks", "floors", "biomes", "weapons", "equipment-icons"):
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
            candidates = [f"weapon_icon_{number}", f"equipment_icon_gear_{number}", key]
            if number == 19:
                candidates += ["equipment_icon_gear_17", "gear_17"]
        elif key.startswith("part_"):
            candidates = ["equipment_icon_" + key, key]
        elif key.startswith("build_"):
            number = int(key[6:])
            candidates = [f"machine_{number}", f"build_{number}", f"module_{number}"]
            aliases = {0: "build_wall_0", 1: "build_generator_0", 2: "build_miner_0", 3: "build_belt_0", 4: "build_smelter_0", 5: "build_assembler_0", 6: "build_turret_0", 7: "build_chest_0"}
            if number in aliases:
                candidates.append(aliases[number])
        elif key.startswith("enemy_"):
            candidates = [key, "zombie_" + key[6:], "zombie_0_0"]
        elif key.startswith("creature_"):
            candidates = [key, "animal_" + key[9:]]
        for candidate in tuple(candidates):
            if candidate in self.game_fallbacks:
                candidates.append(self.game_fallbacks[candidate])
        for candidate in candidates:
            if candidate in self.sprites:
                self.resolved[key] = candidate
                if candidate in self.game_fallbacks.values() and candidate != key:
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
        if key in ("build_61", "build_62"):
            return f'<span class="{cls} missing-art" aria-hidden="true" title="当前游戏没有专用图标">◇</span>'
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
        equipment_source = (CORE / "EquipmentCatalog.cs").read_text(encoding="utf-8-sig")
        state_source = (CORE / "DemoState.cs").read_text(encoding="utf-8-sig")
        gear_enum = re.search(r"enum GearKind\s*\{([^}]+)\}", state_source).group(1)
        self.gear_metadata = []
        era_keys = {"Primitive": "primitive", "Water": "water", "Steam": "steam", "LowVoltage": "lv", "MediumVoltage": "mv", "HighVoltage": "hv", "Nuclear": "nuclear"}
        for enum_name in [part.strip().split("=")[0].strip() for part in gear_enum.split(",")]:
            block = re.search(r"case GearKind\." + enum_name + r":(.*?)(?=case GearKind\.|\n\s*\}\n\s*return d;)", equipment_source, re.S)
            if not block:
                raise ValueError(f"Missing equipment definition: {enum_name}")
            era = re.search(r"d.era\s*=\s*TechEra\.(\w+)", block.group(1))
            self.gear_metadata.append({"era": era_keys[era.group(1)] if era else "primitive", "armor": "SetArmor(" in block.group(1)})
        self.entities = {name: f"item_{i}" for i, name in enumerate(self.items)}
        self.entities.update({name: f"build_{i}" for i, name in enumerate(self.buildings)})
        self.entities.update({name: f"gear_{i}" for i, name in enumerate(self.gear)})
        self.entities.update({name: f"part_{i}" for i, name in enumerate(self.parts)})
        self.foods = re.findall(r'new SurvivalFoodDefinition\(FoodKind\.\w+, "([^"\n]+)"', (CORE / "SurvivalCatalog.cs").read_text(encoding="utf-8-sig"))
        self.entities.update({name: f"food_{i}" for i, name in enumerate(self.foods)})
        self.entity_pattern = re.compile("|".join(re.escape(name) for name in sorted(self.entities, key=len, reverse=True)))
        self.sections = dict(re.findall(r"^## ([^\n]+)\n(.*?)(?=^## |\Z)", source, re.M | re.S))
        self.date = re.search(r"最后核对：(\d{4}-\d{2}-\d{2})", source).group(1)
        self.baseline = re.search(r"规则基线：" + re.escape(TICK) + r"([^" + TICK + "]+)", source).group(1)

    def inline(self, text: str, page: str) -> str:
        tokens: list[str] = []
        def save(fragment: str) -> str:
            tokens.append(fragment)
            return f"\x00{len(tokens)-1}\x00"
        text = re.sub(re.escape(TICK) + "([^" + TICK + "]+)" + re.escape(TICK), lambda m: save(f"<code>{esc(m.group(1))}</code>"), text)
        def link(match: re.Match) -> str:
            label, target = match.group(1), match.group(2)
            if target.startswith("#"):
                raw = target[1:]
                target = "#/" + PAGE_IDS.get(raw, raw if raw != "wiki维护" else "maintenance")
            if re.match(r"(?i)(?:javascript|data):", target):
                raise ValueError("Unsafe document link")
            return save(f'<a href="{esc(target)}">{esc(label)}</a>')
        text = re.sub(r"\[([^\]]+)\]\(([^)]+)\)", link, text)
        text = esc(text)
        text = re.sub(r"\*\*([^*]+)\*\*", r"<strong>\1</strong>", text)
        for index, token in enumerate(tokens):
            text = text.replace(f"\x00{index}\x00", token)
        return text

    def illustrated(self, text: str, page: str) -> str:
        result, previous = [], 0
        for match in self.entity_pattern.finditer(text):
            result.append(self.inline(text[previous:match.start()], page))
            name = match.group(0)
            result.append(f'<span class="entity-pill">{self.art.img(self.entities[name])}<span>{esc(name)}</span></span>')
            previous = match.end()
        result.append(self.inline(text[previous:], page))
        return "".join(result)

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
                output.append(f'<h{level} id="{page}--{esc(anchor)}" data-anchor="{esc(anchor)}">{self.inline(title,page)}</h{level}>')
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
            era = self.gear_metadata[number]["era"] if mode == "gear" else ("lv" if number in (0, 3, 4) else "hv" if number == 6 else "mv")
            if era not in selected_eras:
                selected_eras.append(era)
            kind = "工具" if mode == "gear" and number < 5 else "武器" if mode == "gear" and number < 10 else "防具" if mode == "gear" else "枪械配件"
            note = '<span class="art-note">沿用游戏中的防具示意图</span>' if mode == "gear" and number == 19 else ''
            cards.append(f'<article class="item-card" data-era="{era}" data-name="{esc(labels[number])}"><div class="item-art">{self.art.img(f"{mode}_{number}",labels[number],"catalog-art")}</div><span class="item-type">{kind} · {ERA_LABELS[era]}</span><h3>{esc(labels[number])}</h3>{note}</article>')
        filters = '<button type="button" data-filter="all" aria-pressed="true">全部</button>' + ''.join(f'<button type="button" data-filter="{era}" aria-pressed="false">{ERA_LABELS[era]}</button>' for era in selected_eras)
        return f'<section class="gallery" data-gallery="{mode}-{indices[0]}"><div class="section-heading"><h2>{title}</h2><span>{len(indices):02d} 条目</span></div><div class="gallery-tools"><div class="filter-list" role="group" aria-label="按时代筛选">{filters}</div><label class="gallery-search"><span class="sr-only">搜索{title}</span><input type="search" data-gallery-search placeholder="搜索名称…"></label></div><div class="gallery-grid">{"".join(cards)}</div><p class="gallery-empty" hidden>没有符合条件的条目，试试其他名称或时代。</p></section>'

    def illustration(self, page: str) -> str:
        if page == "weapons":
            return self.gallery("武器与工具图鉴", [index for index, value in enumerate(self.gear_metadata) if not value["armor"]])
        if page == "armor":
            return self.gallery("防具图鉴", [index for index, value in enumerate(self.gear_metadata) if value["armor"]]) + self.gallery("配件图鉴", list(range(len(self.parts))), "part")
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
            cards = ''.join(f'<figure class="catalog-preview">{self.art.img(key,label,"preview-art")}<figcaption>{label}</figcaption></figure>' for key, label in collections[page])
            return f'<div class="chapter-illustration"><span class="eyebrow">{caption}</span><div class="catalog-strip">{cards}</div><p class="figure-caption">图示取自游戏资源，部分沿用游戏当前的复用图；具体机制见下文。</p></div>'
        return ''

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
        metadata = {"sections": len(entries), "research": len(research_names), "sprite_images": len(self.art.encoded), "reused_art_keys": len(self.art.fallbacks), "source_sha256": replacements["{{SOURCE_HASH}}"]}
        return template, metadata


class HtmlCheck(HTMLParser):
    def __init__(self) -> None:
        super().__init__()
        self.ids: set[str] = set()
        self.images = 0
        self.links: list[str] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str,str]]) -> None:
        values = dict(attrs)
        if values.get("id"):
            if values["id"] in self.ids:
                raise ValueError(f"Duplicate HTML id: {values['id']}")
            self.ids.add(values["id"])
        if tag == "img":
            self.images += 1
            if not values.get("src", "").startswith("data:image/") or "alt" not in values:
                raise ValueError("Image must be embedded with an alt attribute")
        if tag in ("script", "link") and (values.get("src") or values.get("href")):
            raise ValueError("External runtime dependency in offline HTML")
        if tag == "a":
            self.links.append(values.get("href", ""))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Fail if published HTML differs from source/assets/templates")
    args = parser.parse_args()
    source = SOURCE.read_text(encoding="utf-8-sig").replace("\r\n", "\n")
    page, metadata = WikiRenderer(source, ArtBank()).build()
    check = HtmlCheck()
    check.feed(page)
    valid_routes = {"home"} | {entry[0] for entry in PAGES}
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
