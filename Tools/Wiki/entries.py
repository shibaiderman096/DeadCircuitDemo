"""Catalog-backed encyclopedia entries and relationships for the offline Wiki.

The small parser reads declarative catalog calls, not arbitrary C# expressions.
Unsupported declarations fail the build instead of publishing guessed values.
"""
from __future__ import annotations

import json
import re
from pathlib import Path


ERAS = {"Primitive": "原始", "Water": "水力", "Steam": "蒸汽", "LowVoltage": "LV", "MediumVoltage": "MV", "HighVoltage": "HV", "Nuclear": "核工业"}
INDUSTRY = [("Primitive", "Primitive"), ("Water", "Water"), ("Steam", "Steam"), ("LowVoltage", "LowVoltage"), ("Petrochemical", "MediumVoltage"), ("Precision", "HighVoltage"), ("Nuclear", "Nuclear")]


def split_arguments(text: str) -> list[str]:
    parts, start, depth, quoted, escaped = [], 0, 0, False, False
    for index, char in enumerate(text):
        if quoted:
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == '"':
                quoted = False
        elif char == '"':
            quoted = True
        elif char in "([{":
            depth += 1
        elif char in ")]}":
            depth -= 1
        elif char == "," and depth == 0:
            parts.append(text[start:index].strip())
            start = index + 1
    if depth or quoted:
        raise ValueError("Unbalanced catalog declaration")
    parts.append(text[start:].strip())
    return parts


def calls(text: str, name: str):
    for match in re.finditer(r"\b" + re.escape(name) + r"\(", text):
        start, depth, quoted, escaped = match.end(), 1, False, False
        for index in range(start, len(text)):
            char = text[index]
            if quoted:
                if escaped:
                    escaped = False
                elif char == "\\":
                    escaped = True
                elif char == '"':
                    quoted = False
            elif char == '"':
                quoted = True
            elif char == "(":
                depth += 1
            elif char == ")":
                depth -= 1
                if depth == 0:
                    yield split_arguments(text[start:index])
                    break
        else:
            raise ValueError(f"Unclosed call to {name}")


def string(text: str) -> str:
    return json.loads(text)


def number(text: str) -> str:
    return str(float(text.removesuffix("f"))).removesuffix(".0")


def enum_names(text: str, name: str) -> list[str]:
    match = re.search(r"enum " + name + r"\s*\{([^}]+)\}", text)
    if not match:
        raise ValueError(f"Missing enum {name}")
    return [part.strip().split("=")[0].strip() for part in match.group(1).split(",")]


class EntryCatalog:
    def __init__(self, root: Path, arrays: dict[str, list[str]]) -> None:
        self.root, self.core = root, root / "Assets/DeadCircuit/Scripts/Core"
        self.arrays, self.entries, self.names, self.icons = arrays, {}, {}, {}
        state = self.read("DemoState.cs")
        self.item_ids = {name: i for i, name in enumerate(enum_names(state, "ItemKind"))}
        self.build_ids = {name: i for i, name in enumerate(enum_names(state, "BuildKind"))}
        self.gear_ids = {name: i for i, name in enumerate(enum_names(state, "GearKind"))}
        self.part_ids = {name: i for i, name in enumerate(enum_names(state, "PartKind"))}
        self.recipes = []
        for args in calls(self.read("TechCatalog.cs"), "R"):
            if not args[0].isdigit():
                continue
            self.recipes.append({"id": int(args[0]), "name": string(args[1]), "era": ERAS[args[2].split(".")[-1]], "station": f"building-{self.build_ids[args[3].split('.')[-1]]}", "seconds": number(args[4]), "inputs": self.materials(args[5]), "outputs": self.materials(args[6])})
        for prefix, category, parent in (("item", "工业物品", "recipes"), ("building", "设备与建筑", "building"), ("gear", "装备", "weapons"), ("part", "枪械配件", "armor"), ("food", "食物与原料", "survival")):
            for i, name in enumerate(arrays[prefix]):
                icon = f"build_{i}" if prefix == "building" else f"{prefix}_{i}"
                self.add(f"{prefix}-{i}", name, category, parent, icon)
        self.add('water-raw','原水','生存饮水','survival',None,source='WorldSimulator.Survival.cs',summary='原水可在附近可见水域反复采集。饮用恢复30水分并增加25感染；可经石炉煮沸成为净水。')
        self.add('water-clean','净水','生存饮水','survival',None,source='WorldSimulator.Survival.cs',summary='净水用于饮用与烹饪，饮用恢复40水分。研究烹饪净水后，在合法石炉用原水1与普通燃料1煮得净水1；不等于工业工艺水。')
        descriptions = self.array(state, "BuildDescriptions")
        build_source = state.split("public static int[] BuildCost", 1)[1].split("public static int Width", 1)[0]
        costs = {}
        for cases, expression in re.findall(r"((?:case BuildKind\.\w+:\s*)+)return\s+(DemoCatalog\.Materials\([^;]+|Materials\([^;]+);", build_source):
            for name in re.findall(r"BuildKind\.(\w+)", cases):
                costs[self.build_ids[name]] = self.materials(expression)
        era_map = {}
        required_era = self.read("TechCatalog.cs").split("public static TechEra RequiredEra", 1)[1].split("public static bool IsUnlocked", 1)[0]
        for cases, era in re.findall(r"((?:case BuildKind\.\w+:\s*)+)return TechEra\.(\w+);", required_era):
            for name in re.findall(r"BuildKind\.(\w+)", cases):
                era_map[self.build_ids[name]] = ERAS[era]
        if len(descriptions) != len(arrays["building"]) or len(era_map) != len(arrays["building"]):
            raise ValueError("Building catalog shape changed")
        for i, name in enumerate(arrays["building"]):
            entry = self.entries[f"building-{i}"]
            entry.update(summary=descriptions[i], cost=costs.get(i, []), era=era_map[i], source="DemoState.cs")
        self.read_equipment()
        self.read_research()
        self.read_modular_buildings()
        self.read_survival()
        self.read_environment()
        self.link_equipment_research()
        self.entries['item-2']['summary'] = '铁锭是早期冶金材料，可由铁矿或碎铁矿冶炼取得，用于铁板、机械部件、设备和部分装备制作。'
        self.entries['item-10']['summary'] = '钢是蒸汽时代开始生产的金属材料，用于钢板、工具、枪械、电机与后续工业制造。'
        self.entries['item-0']['summary'] = '木材用于工作台、炉窑、木炭与多种设施，主要由持斧砍伐有限的树木资源取得。'
        self.entries['item-3']['summary'] = '废料可用扳手从废料堆取得，也可在工作台用铁锭加工。'
        for recipe in self.recipes:
            slug = "recipe-" + str(recipe["id"])
            name = recipe["name"]
            entry = self.add(slug, name, "配方", "recipes", None, era=recipe["era"], recipe=recipe, source="SurvivalCatalog.cs" if isinstance(recipe["id"], str) else "TechCatalog.cs", summary=f"{name}的输入、产出和工作设备。配方须满足相应时代与研究条件，基础周期还受实际供能影响。")
            station = self.entries[recipe["station"]]
            if station.get("module_costs"):
                entry["research"] = station["research"]
            if self.names[name] != slug:
                entry["title"] = name + "（配方）"
                self.names[entry["title"]] = slug
        self.aliases()

    def read(self, filename: str) -> str:
        return (self.core / filename).read_text(encoding="utf-8-sig")

    @staticmethod
    def array(text: str, name: str) -> list[str]:
        body = re.search(r"\b" + name + r"\s*=\s*\{([^}]+)\}", text).group(1)
        return re.findall(r'"([^"\n]+)"', body)

    def add(self, slug: str, name: str, category: str, parent: str, icon: str | None, **kwargs) -> dict:
        entry = {"id": slug, "name": name, "title": name, "category": category, "parent": parent, "icon": icon, "summary": f"{name}是《废土回路》中的{category}条目。", "facts": [], "cost": [], "source": "DemoState.cs", **kwargs}
        self.entries[slug] = entry
        if name not in self.names:
            self.names[name] = slug
        if icon:
            self.icons[icon] = slug
        return entry

    def materials(self, expression: str) -> list[tuple[str, int]]:
        matches = list(calls(expression, "Materials"))
        if not matches:
            return []
        values = matches[0]
        if len(values) % 2:
            raise ValueError(f"Invalid materials: {expression}")
        return [(f"item-{self.item_ids[values[i].split('.')[-1]]}", int(values[i+1])) for i in range(0, len(values), 2)]

    def read_modular_buildings(self) -> None:
        source = self.read("ModularMachineCatalog.cs")
        roles = enum_names(source, "MachineModuleRole")
        names = self.array(source, "Names")
        kinds = re.findall(r"BuildKind\.(\w+)", re.search(r"bool IsModular\([^)]*\)\s*=>\s*([^;]+);", source).group(1))
        selectors = dict(re.findall(r"bool\s+(\w+)\s*=\s*kind\s*==\s*BuildKind\.(\w+)\s*;", source))
        block = source.split("public static int[] ModuleCost(BuildKind kind,MachineModuleRole role)", 1)[1].split("public static int[] TotalCost", 1)[0]
        expressions, default = {}, None
        for cases, expression in re.findall(r"((?:case MachineModuleRole\.\w+:\s*)+|default:\s*)return\s+([^;]+);", block):
            if cases.startswith("default:"):
                default = expression
            for role in re.findall(r"MachineModuleRole\.(\w+)", cases):
                expressions[role] = expression
        missing = set(roles) - expressions.keys()
        if len(missing) != 1 or default is None or len(names) != len(roles):
            raise ValueError("Modular construction catalog shape changed")
        expressions[missing.pop()] = default
        casing_min = int(re.search(r"counts\[0\]<(\d+)", source).group(1))
        max_modules = re.search(r"MaxModules\s*=\s*(\d+)", source).group(1)
        max_span = re.search(r"MaxSpan\s*=\s*(\d+)", source).group(1)
        research = {}
        for prefix in ("Steam", "LowVoltage"):
            build_research = self.read(prefix + "Catalog.cs").split("int BuildResearch(BuildKind kind)", 1)[1].split("public static string BuildRequirement", 1)[0]
            for cases, ref in re.findall(r"((?:case BuildKind\.\w+:\s*)+)return \(int\)(\w+Tech\.\w+);", build_research):
                for kind in re.findall(r"BuildKind\.(\w+)", cases):
                    research[kind] = self.research_refs[ref]
        for kind in kinds:
            entry = self.entries[f"building-{self.build_ids[kind]}"]
            modules, totals = [], {}
            for role, name in zip(roles, names):
                expression = expressions[role]
                if "?" in expression:
                    selector, branches = expression.split("?", 1)
                    yes, no = branches.split(":", 1)
                    expression = yes if kind == selectors[selector.strip()] else no
                if not re.fullmatch(r"\s*(?:DemoCatalog\.)?Materials\(.+\)\s*", expression):
                    raise ValueError(f"Unsupported module cost: {expression}")
                cost = self.materials(expression)
                minimum = casing_min if role == "Casing" else 1
                if role == "Exhaust" and kind != selectors["steam"]:
                    name = "通风口"
                modules.append({"role": role, "name": name, "minimum": minimum, "cost": cost})
                for slug, quantity in cost:
                    totals[slug] = totals.get(slug, 0) + minimum * quantity
            entry.update(module_costs=modules, cost=sorted(totals.items(), key=lambda pair: int(pair[0].split("-")[1])), research=research[kind], source="ModularMachineCatalog.cs")
            entry["facts"] += [("布局", "逐格自由绘制，空洞不占地"), ("模块预算", f"最多{max_modules}格，跨度最多{max_span}×{max_span}")]
        if any(not self.entries[f"building-{i}"]["cost"] for i in self.build_ids.values()):
            raise ValueError("Missing building construction costs")

    def read_equipment(self) -> None:
        source = self.read("EquipmentCatalog.cs")
        gear_switch = source.split("switch (kind)", 1)[1].split("return d;", 1)[0]
        for name, i in self.gear_ids.items():
            block = re.search(r"case GearKind\." + name + r":(.*?)(?=case GearKind\.|\Z)", gear_switch, re.S).group(1)
            entry = self.entries[f"gear-{i}"]
            era = re.search(r"d.era\s*=\s*TechEra\.(\w+)", block)
            description = re.search(r'd.description\s*=\s*("[^"\n]*")', block)
            entry.update(era=ERAS[era.group(1)] if era else "原始", cost=self.materials(block), source="EquipmentCatalog.cs")
            if description:
                entry["summary"] = string(description.group(1))
            armor = list(calls(block, "SetArmor"))
            gun = list(calls(block, "SetGun"))
            if armor:
                entry.update(category="防具", parent="armor")
                entry["facts"] += [("基础护甲", number(armor[0][1])), ("移速代价", f"{float(number(armor[0][2]))*100:g}%")]
            elif gun:
                entry["facts"] += [("基础伤害", number(gun[0][3])), ("基础射程", number(gun[0][4]) + "格"), ("弹匣容量", gun[0][2]), ("装填时间", number(gun[0][6]) + "秒")]
                entry["ammo"] = f"item-{self.item_ids[gun[0][1].split('.')[-1]]}"
            elif i < 5:
                entry["category"] = "工具"
                if name == "StoneAxe":
                    entry["summary"] = "在T的原始页随身制作并装备，无需工作台或研究。可用于砍树和拆取补给箱。"
                elif name == "StonePickaxe":
                    entry["summary"] = "在T的原始页随身制作并装备，无需工作台或研究。可用于开采石材、黏土和矿床。"
            max_durability = re.search(r"d.maxDurability\s*=\s*(\d+)", block)
            entry["facts"].append(("最大耐久", max_durability.group(1) if max_durability else "100"))
        part_source = source.split("public static PartDefinition Part", 1)[1].split("return d;", 1)[0]
        for name, i in self.part_ids.items():
            marker = "default:" if i == len(self.part_ids)-1 else "case PartKind." + name + ":"
            block = part_source.split(marker, 1)[1].split("break;", 1)[0]
            era = re.search(r"d.era\s*=\s*TechEra\.(\w+)", block)
            description = re.search(r'd.description\s*=\s*("[^"\n]*")', block)
            self.entries[f"part-{i}"].update(era=ERAS[era.group(1)] if era else "LV", summary=string(description.group(1)), cost=self.materials(block), source="EquipmentCatalog.cs")

    def read_research(self) -> None:
        self.research_refs = {}
        pending = []
        for prefix, era in INDUSTRY:
            filename = prefix + "Catalog.cs"
            source = self.read(filename)
            identifiers = enum_names(source, prefix + "Tech")
            for args in calls(source, "T"):
                if not args[0].startswith(prefix + "Tech."):
                    continue
                enum_name = args[0].split(".")[-1]
                index = identifiers.index(enum_name)
                slug = f"research-{prefix.lower()}-{index}"
                name = string(args[1])
                entry = self.add(slug, name, "工业研究", "technology", None, era=ERAS[era], summary=string(args[2]), cost=self.materials(args[4]), source=filename)
                self.research_refs[prefix + "Tech." + enum_name] = slug
                pending.append((entry, args[3], prefix))
        for args in calls(self.read("WeaponTechCatalog.cs"), "T"):
            if not args[0].startswith("WeaponTech."):
                continue
            ref = args[0]
            index = len([e for e in self.entries if e.startswith("research-weapon-")])
            entry = self.add(f"research-weapon-{index}", string(args[2]), "武器研究", "technology", None, era=ERAS[args[1].split('.')[-1]], summary=string(args[3]), cost=self.materials(args[5]), production=self.materials(args[6]), source="WeaponTechCatalog.cs")
            self.research_refs[ref] = entry["id"]
            pending.append((entry, args[4], "Weapon"))
        tool_source = self.read("ToolTechCatalog.cs")
        for block in re.split(r"new EquipmentTechnologyDefinition\s*\{", tool_source)[1:]:
            block = block.split("};", 1)[0]
            ref = re.search(r"id=\(int\)(ToolTech\.\w+)", block).group(1)
            name, description = re.search(r'name="([^"]+)",description="([^"]+)"', block).groups()
            index = len([e for e in self.entries if e.startswith("research-tool-")])
            cost_expression = block.split("cost=", 1)[1]
            entry = self.add(f"research-tool-{index}", name, "工具研究", "technology", None, era="蒸汽", summary=description, cost=self.materials(cost_expression), production=self.materials(block.split("production=", 1)[1]), source="ToolTechCatalog.cs")
            self.research_refs[ref] = entry["id"]
            pending.append((entry, block.split("prerequisites=", 1)[1].split("cost=", 1)[0], "Tool"))
        for entry, expression, prefix in pending:
            refs = re.findall(r"(?:Primitive|Water|Steam|LowVoltage|Petrochemical|Precision|Nuclear|Weapon|Tool)Tech\.\w+", expression)
            entry["parents"] = list(dict.fromkeys(self.research_refs[ref] for ref in refs))
            existing = self.names.get(entry["name"])
            if existing and existing != entry["id"]:
                entry["title"] = entry["name"] + "（研究）"
                self.names[entry["title"]] = entry["id"]
        expected = sum(len(enum_names(self.read(prefix + 'Catalog.cs'),prefix + 'Tech')) for prefix,_ in INDUSTRY)
        expected += len(enum_names(self.read('WeaponTechCatalog.cs'),'WeaponTech')) + len(enum_names(self.read('ToolTechCatalog.cs'),'ToolTech'))
        if len(pending) != expected:
            raise ValueError(f"Research catalog extraction incomplete: {len(pending)}/{expected}")

    def read_survival(self) -> None:
        source = self.read("SurvivalCatalog.cs")
        food_ids = {}
        for args in calls(source, "new SurvivalFoodDefinition"):
            name = args[0].split(".")[-1]
            index = len(food_ids)
            food_ids[name] = index
            entry = self.entries[f"food-{index}"]
            entry.update(facts=[("饱食变化", number(args[2])), ("水分变化", number(args[3])), ("营养变化", number(args[4]))], source="SurvivalCatalog.cs")
            entry["summary"] = f"{entry['name']}是生存系统中的食材或食品，与普通工业库存中的食物分别管理。"
        for args in calls(source, "new SurvivalRecipeDefinition"):
            recipe = {"id": f"food-{args[0]}", "name": string(args[1]), "era": "烹饪净水研究", "station": f"building-{self.build_ids[args[2].split('.')[-1]]}", "seconds": None, "inputs": [], "outputs": [(f"food-{food_ids[args[3].split('.')[-1]]}", int(args[4]))]}
            for ingredient in args[7:]:
                key = f"food-{food_ids[ingredient.split('.')[-1]]}"
                existing = next((i for i, value in enumerate(recipe["inputs"]) if value[0] == key), None)
                if existing is None:
                    recipe["inputs"].append((key, 1))
                else:
                    recipe["inputs"][existing] = (key, recipe["inputs"][existing][1]+1)
            if int(args[5]):
                recipe["inputs"].append(("water-clean", int(args[5])))
            if int(args[6]):
                recipe["inputs"].append(("item-4", int(args[6])))
            self.recipes.append(recipe)
        for i, args in enumerate(calls(source, "new SurvivalCreatureDefinition")):
            name = string(args[1])
            self.add(f"creature-{i}", name, "野生动物", "survival", f"creature_{i}", source="SurvivalCatalog.cs", summary=f"{name}是初始区域按环境尝试生成的野生动物，当前没有死亡后补刷或繁殖路径。", facts=[("基础生命", number(args[3]))])

    def read_environment(self) -> None:
        biomes = ("草甸", "阔叶林", "针叶林", "荒漠", "湿地", "冻原", "沼泽", "雪山", "樱花林", "河谷")
        for i, name in enumerate(biomes):
            self.add(f"biome-{i}", name, "自然群系", "world", f"biome_{i}", source="ChunkTerrain.Biomes.cs", summary=f"{name}是当前世界的十种自然群系之一；资源为分布倾向，不保证每个区块包含所有材料。")
        source = self.read("EnemyCatalog.cs")
        names = self.array(source, "Names")
        for args in calls(source, "new EnemyStats"):
            i = int(args[0])
            self.add(f"enemy-{i}", names[i], "丧尸", "combat", f"enemy_{i}", source="EnemyCatalog.cs", summary=string(args[2]), facts=[("基础生命", number(args[4])), ("目录解锁波次", args[3]), ("基础伤害", number(args[6]))])

    def link_equipment_research(self) -> None:
        text = self.read("EquipmentResearchCatalog.cs")
        for type_name, ids, prefix in (("Gear", self.gear_ids, "gear"), ("Part", self.part_ids, "part"), ("Item", self.item_ids, "item")):
            method = "Ammo" if type_name == "Item" else type_name
            block = text.split(f"public static ResearchRef {method}Research", 1)[1].split("default:", 1)[0]
            for cases, expression in re.findall(r"((?:case \w+Kind\.\w+:\s*)+)return\s+([^;]+);", block):
                refs = re.findall(r"(?:Primitive|Water|Steam|Weapon|Tool)Tech\.\w+", expression)
                if not refs:
                    continue
                research = self.research_refs[refs[0]]
                for name in re.findall(type_name + r"Kind\.(\w+)", cases):
                    self.entries[f"{prefix}-{ids[name]}"]["research"] = research
        self.entries["gear-19"]["research"] = self.research_refs["NuclearTech.RadiationProtection"]

    def aliases(self) -> None:
        for alias, slug in {"手枪": "gear-6", "霰弹枪": "gear-7", "卡宾枪": "gear-8", "狙击步枪": "gear-9", "军刀": "gear-5", "普通弹": "item-5", "基础电路": "item-11", "电缆": "item-42", "防护服": "gear-19", "四倍镜": "part-1"}.items():
            self.names.setdefault(alias, slug)

    def production(self, slug: str) -> list[dict]:
        return [recipe for recipe in self.recipes if any(key == slug for key, _ in recipe["outputs"])]

    def uses(self, slug: str) -> list[dict]:
        return [recipe for recipe in self.recipes if any(key == slug for key, _ in recipe["inputs"]) or recipe["station"] == slug]

    def cost_uses(self, slug: str) -> list[dict]:
        return [entry for entry in self.entries.values() if any(key == slug for key, _ in entry["cost"])]
