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
                self.assertIn(link[2:].split('/')[0],parsed.routes)


if __name__=='__main__':
    unittest.main(verbosity=2)
