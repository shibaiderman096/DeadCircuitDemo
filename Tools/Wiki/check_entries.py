"""Targeted encyclopedia link/data checks. Run: python -B Tools/Wiki/check_entries.py"""
from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from generate_wiki import ArtBank, HtmlCheck, SOURCE, WikiRenderer


class EntryChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.renderer = WikiRenderer(SOURCE.read_text(encoding='utf-8-sig'),ArtBank())
        cls.catalog = cls.renderer.catalog

    def test_longest_match(self):
        result = self.renderer.inline('高级电路与电路、铁矿与铁锭', 'recipes')
        self.assertEqual(4,result.count('data-entry='))
        self.assertIn('href="#/item-13"',result)
        self.assertIn('href="#/item-11"',result)
        self.assertIn('href="#/item-1"',result)
        self.assertIn('href="#/item-2"',result)

    def test_markdown_links_and_code_protected(self):
        source = '[铁锭](https://example.invalid/铁锭) ' + chr(96) + '铁锭' + chr(96) + ' 和铁锭'
        result = self.renderer.inline(source,'recipes')
        self.assertEqual(2,result.count('<a '))
        self.assertIn('<code>铁锭</code>',result)
        HtmlCheck().feed(result)

    def test_illustrated_inline_no_nested_anchors(self):
        parsed = HtmlCheck()
        parsed.feed(self.renderer.illustrated('**铁锭**、[铁锭](WIKI.md)、铁矿2','recipes'))
        self.assertEqual(3,len(parsed.links))

    def test_homonym_research_context(self):
        self.assertIn('href="#/research-weapon-10"',self.renderer.inline('精密枪管','technology'))
        self.assertIn('href="#/part-6"',self.renderer.inline('精密枪管','armor'))
        self.assertIn('href="#/research-precision-2"',self.renderer.inline('半导体器件','technology'))
        self.assertIn('href="#/item-58"',self.renderer.inline('半导体器件','recipes'))

    def test_real_catalog_costs(self):
        self.assertEqual([('item-0',6)],self.catalog.entries['building-10']['cost'])
        self.assertEqual([('item-10',4),('item-8',2),('item-3',5)],self.catalog.entries['gear-6']['cost'])
        self.assertEqual('research-weapon-1',self.catalog.entries['gear-6']['research'])
        self.assertEqual('research-nuclear-10',self.catalog.entries['gear-19']['research'])

    def test_recipe_station_and_quantity(self):
        recipe = next(value for value in self.catalog.recipes if value['id']==52)
        self.assertEqual('building-5',recipe['station'])
        self.assertEqual([('item-2',2),('item-8',1)],recipe['inputs'])
        self.assertEqual([('item-21',6)],recipe['outputs'])

    def test_integrated_catalogs_and_ground_pickups(self):
        self.assertEqual(66,len(self.renderer.items))
        self.assertEqual(67,len(self.renderer.buildings))
        self.assertEqual(66,len(self.catalog.item_ids))
        self.assertEqual(67,len(self.catalog.build_ids))
        for name,index,title in (('Branch',63,'树枝'),('LooseStone',64,'石头'),('DryGrass',65,'干草')):
            with self.subTest(item=name):
                self.assertEqual(index,self.catalog.item_ids[name])
                self.assertEqual(title,self.catalog.entries[f'item-{index}']['name'])
                self.assertIn('拾取',self.renderer.entity_article(self.catalog.entries[f'item-{index}']))
                self.assertTrue(any(entry['id']=='gear-0' for entry in self.catalog.cost_uses(f'item-{index}')))

    def test_backpack_specs_and_material_links(self):
        cases = (
            (20, 'GrassBackpack', '草绳背包', '原始', None, ('50', '100', '0.9'), {0:3,65:1}),
            (21, 'ReinforcedBackpack', '加固背包', '水力', 'building-10', ('70', '120', '1.7'), {0:4,3:4,9:2}),
            (22, 'EngineerBackpack', '工程背包', 'LV', 'building-10', ('110', '160', '2.8'), {38:2,42:2,3:4}),
        )
        dry_grass = self.renderer.entity_article(self.catalog.entries['item-65'])
        self.assertIn('首堆有3份', dry_grass)
        self.assertIn('href="#/gear-20"', dry_grass)
        self.assertNotIn('href="#/gear-21"', dry_grass)
        for index, identifier, title, era, station, weights, materials in cases:
            slug = f'gear-{index}'
            with self.subTest(backpack=identifier):
                self.assertEqual(index, self.catalog.gear_ids[identifier])
                entry = self.catalog.entries[slug]
                self.assertEqual((title, era, '背包', 'armor', station), tuple(entry[key] for key in ('name', 'era', 'category', 'parent', 'station')))
                facts = dict(entry['facts'])
                self.assertEqual('独立背包位', facts['穿戴位置'])
                self.assertEqual(tuple(weight + '千克' for weight in weights), tuple(facts[key] for key in ('包内容量', '穿戴后总容量', '备用时单重')))
                self.assertNotIn('research', entry)
                self.assertEqual({f'item-{i}': n for i, n in materials.items()}, dict(entry['cost']))
                article = self.renderer.entity_article(entry)
                self.assertIn('包内物品随背包落地与拾回', article)
                for material, _ in entry['cost']:
                    self.assertIn(f'data-entry="{material}"', article)
                    self.assertIn(entry, self.catalog.cost_uses(material))
                if station:
                    self.assertIn(f'data-entry="{station}"', article)
                else:
                    self.assertIn('随身制作', article)

    def test_backpacks_have_a_separate_filterable_gallery(self):
        weapon_gallery = self.renderer.illustration('weapons')
        gear_galleries = self.renderer.illustration('armor')
        backpack_gallery, rest = gear_galleries.split('</section>', 1)
        self.assertIn('背包图鉴', backpack_gallery)
        self.assertEqual(3, backpack_gallery.count('class="item-card"'))
        for index, era in ((20, 'primitive'), (21, 'water'), (22, 'lv')):
            with self.subTest(gear=index):
                slug = f'gear-{index}'
                self.assertIn(f'data-entry="{slug}"', backpack_gallery)
                self.assertIn(f'data-filter="{era}"', backpack_gallery)
                self.assertNotIn(slug, weapon_gallery)
                self.assertNotIn(slug, rest)
        self.assertIn('href="#/armor/背包与负重"', self.renderer.inline('[背包与负重](#背包与负重)', 'controls'))

    def test_modular_recipe_data(self):
        expected = {
            200: ('building-63','4',[('item-37',6),('item-33',2)],[('item-10',6)]),
            201: ('building-63','2',[('item-18',2)],[('item-2',4)]),
            202: ('building-63','2',[('item-7',2)],[('item-8',2)]),
            203: ('building-63','2',[('item-19',2)],[('item-20',2)]),
            210: ('building-64','3',[('item-40',8),('item-41',4)],[('item-42',8)]),
            211: ('building-64','3',[('item-43',2),('item-10',2),('item-30',2),('item-31',2)],[('item-44',2)]),
            212: ('building-64','3',[('item-43',2),('item-29',2),('item-41',2)],[('item-45',2)]),
            220: ('building-65','2',[('item-1',2)],[('item-18',4),('item-25',1)]),
        }
        actual = {recipe['id']:recipe for recipe in self.catalog.recipes if recipe['station'] in ('building-63','building-64','building-65')}
        self.assertEqual(set(expected),set(actual))
        for identifier,values in expected.items():
            with self.subTest(recipe=identifier):
                recipe = actual[identifier]
                self.assertEqual(values,(recipe['station'],recipe['seconds'],recipe['inputs'],recipe['outputs']))
                self.assertIn(f'recipe-{identifier}',self.catalog.entries)

    def test_modular_construction_quotes_and_per_cell_costs(self):
        steam = {9:2,28:2,29:5,30:3,31:1,36:8,38:5,39:5}
        electric = {11:2,28:2,29:5,30:4,31:1,36:4,38:7,39:1,42:4,44:2}
        for index,expected in ((63,steam),(64,electric),(65,electric)):
            with self.subTest(building=index):
                entry = self.catalog.entries[f'building-{index}']
                self.assertEqual({f'item-{i}':quantity for i,quantity in expected.items()},dict(entry['cost']))
                self.assertEqual(8,len(entry['module_costs']))
                self.assertEqual(9,sum(module['minimum'] for module in entry['module_costs']))
                casing = next(module for module in entry['module_costs'] if module['role']=='Casing')
                self.assertEqual([('item-38',2),('item-36',2)],casing['cost'])
                article = self.renderer.entity_article(entry)
                for text in ('最小职责集合报价','逐格施工材料','不是固定3×3模板','退出绘制后','增加机壳另付'):
                    self.assertIn(text,article)

    def test_modular_research_links_and_reverse_uses(self):
        for index,research in ((63,'research-steam-7'),(64,'research-lowvoltage-5'),(65,'research-lowvoltage-3')):
            slug = f'building-{index}'
            with self.subTest(building=slug):
                self.assertEqual(research,self.catalog.entries[slug]['research'])
                for recipe in self.catalog.uses(slug):
                    self.assertEqual(research,self.catalog.entries[f"recipe-{recipe['id']}"]['research'])
                    for material,_ in recipe['inputs']:
                        self.assertIn(recipe,self.catalog.uses(material))
                    for material,_ in recipe['outputs']:
                        self.assertIn(recipe,self.catalog.production(material))
                self.assertTrue(any(entry['id']==slug for entry in self.catalog.cost_uses('item-38')))
        self.assertIn('href="#/building-63"',self.renderer.inline('大型蒸汽高炉','building'))
        self.assertIn('href="#/recipe-220"',self.renderer.inline('大型铁矿破碎','recipes'))
        self.assertIn('href="#/building-64"',self.renderer.entity_article(self.catalog.entries['research-lowvoltage-5']))

    def test_modular_art_stays_a_truthful_placeholder(self):
        for index in (63,64,65):
            with self.subTest(building=index):
                rendered = self.renderer.art.img(f'build_{index}',self.renderer.buildings[index])
                self.assertTrue(rendered.startswith('<span '))
                self.assertNotIn('<img ',rendered)
                self.assertIn('无整机缩略图',rendered)

    def test_known_relationships(self):
        for entry in self.catalog.entries.values():
            for parent in entry.get('parents',[]):
                self.assertIn(parent,self.catalog.entries)
            if entry.get('research'):
                self.assertIn(entry['research'],self.catalog.entries)
            for slug,_ in entry['cost']:
                self.assertIn(slug,self.catalog.entries)
        for recipe in self.catalog.recipes:
            self.assertIn(recipe['station'],self.catalog.entries)
            for slug,_ in recipe['inputs'] + recipe['outputs']:
                self.assertIn(slug,self.catalog.entries)

    def test_item_sources_and_uses(self):
        self.assertTrue(any(recipe['id']==10 for recipe in self.catalog.production('item-2')))
        self.assertTrue(any(recipe['id']==113 for recipe in self.catalog.uses('item-2')))
        self.assertTrue(any(entry['id']=='gear-6' for entry in self.catalog.cost_uses('item-10')))

    def test_every_entry_route_exists_no_nested_links(self):
        page,metadata = self.renderer.build()
        parsed = HtmlCheck()
        parsed.feed(page)
        self.assertGreater(metadata['entity_entries'],300)
        for link in parsed.links:
            if link.startswith('#/'):
                route, _, anchor = link[2:].partition('/')
                self.assertIn(route, parsed.routes)
                if anchor:
                    self.assertIn(f'{route}--{anchor}', parsed.ids)


if __name__=='__main__':
    unittest.main(verbosity=2)
