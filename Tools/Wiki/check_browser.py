"""Exercise the generated offline Wiki in an isolated headless Chromium session.

Start a task-only Chromium profile with --headless=new --remote-debugging-port=9237.
Run: python -B Tools/Wiki/check_browser.py --screenshots <temporary-directory>
Uses the installed websocket-client package; never connects to a user profile.
"""
from __future__ import annotations

import argparse
import base64
import json
import time
import urllib.request
from pathlib import Path

import websocket

ROOT = Path(__file__).resolve().parents[2]


class Browser:
    def __init__(self, port: int) -> None:
        request = urllib.request.Request(f"http://127.0.0.1:{port}/json/new?about:blank", method="PUT")
        with urllib.request.urlopen(request) as response:
            target = json.load(response)
        self.socket = websocket.create_connection(target["webSocketDebuggerUrl"], timeout=15, suppress_origin=True)
        self.next_id = 0
        self.exceptions: list[dict] = []
        self.command("Page.enable")
        self.command("Runtime.enable")

    def command(self, method: str, params: dict | None = None) -> dict:
        self.next_id += 1
        expected = self.next_id
        self.socket.send(json.dumps({"id": expected, "method": method, "params": params or {}}))
        while True:
            message = json.loads(self.socket.recv())
            if message.get("method") == "Runtime.exceptionThrown":
                self.exceptions.append(message["params"])
            if message.get("id") == expected:
                if "error" in message:
                    raise RuntimeError(message["error"])
                return message.get("result", {})

    def evaluate(self, expression: str):
        result = self.command("Runtime.evaluate", {"expression": expression, "returnByValue": True, "awaitPromise": True})
        if "exceptionDetails" in result:
            raise AssertionError(result["exceptionDetails"])
        return result["result"].get("value")

    def wait(self, expression: str, seconds: float = 6) -> None:
        deadline = time.monotonic() + seconds
        while time.monotonic() < deadline:
            if self.evaluate(expression):
                return
            time.sleep(.05)
        raise AssertionError(f"Browser condition timed out: {expression}")

    def viewport(self, width: int, height: int = 1000) -> None:
        self.command("Emulation.setDeviceMetricsOverride", {"width": width, "height": height, "deviceScaleFactor": 1, "mobile": width < 681})

    def navigate(self, url: str, page: str) -> None:
        self.command("Page.navigate", {"url": url})
        self.wait("document.readyState === 'complete' && document.documentElement.classList.contains('js-enabled')")
        self.wait(f"!document.querySelector('[data-page-id={page}]').hidden")

    def route(self, page: str) -> None:
        self.evaluate("location.hash=" + json.dumps("#/" + page))
        self.wait("!document.querySelector('[data-page-id=" + page.split('/')[0] + "]').hidden")

    def screenshot(self, path: Path) -> None:
        self.evaluate("Promise.all([...document.images].filter(i=>i.getClientRects().length).map(i=>{i.loading='eager';return i.decode().catch(()=>{});} ))")
        time.sleep(.15)
        result = self.command("Page.captureScreenshot", {"format": "png", "captureBeyondViewport": False})
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(base64.b64decode(result["data"]))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=9237)
    parser.add_argument("--screenshots", type=Path, required=True)
    parser.add_argument("--url", default="http://127.0.0.1:8768/Docs/WIKI.html")
    args = parser.parse_args()
    browser = Browser(args.port)
    checks: list[str] = []

    def check(name: str, expression: str) -> None:
        if not browser.evaluate(expression):
            raise AssertionError(name + ": " + expression)
        checks.append(name)

    browser.viewport(1440)
    browser.navigate(args.url + "#/home", "home")
    check("desktop homepage", "document.title === '废土回路 · 游戏百科' && document.querySelectorAll('.entry-card').length===6")
    check("all chapters retained", "document.querySelectorAll('.article-page').length === 17")
    check("desktop no horizontal overflow", "document.documentElement.scrollWidth <= innerWidth + 1")
    browser.screenshot(args.screenshots / "desktop-home.png")
    check("visible images loaded", "[...document.images].filter(i=>i.getClientRects().length).every(i=>i.complete && i.naturalWidth > 0)")

    browser.evaluate("document.querySelector('.nav-link[data-page=weapons]').click()")
    browser.wait("!document.querySelector('[data-page-id=weapons]').hidden")
    check("weapon navigation", "document.querySelector('.nav-link[data-page=weapons]').getAttribute('aria-current')==='page'")
    browser.evaluate("document.querySelector('[data-gallery=gear-0] [data-filter=steam]').click()")
    check("era filter", "[...document.querySelectorAll('[data-gallery=gear-0] .item-card')].filter(c=>!c.hidden).length===3")
    browser.evaluate("(()=>{const i=document.querySelector('[data-gallery=gear-0] input');i.value='不存在zzz';i.dispatchEvent(new Event('input',{bubbles:true}));})()")
    check("gallery empty result", "!document.querySelector('[data-gallery=gear-0] .gallery-empty').hidden")
    browser.evaluate("(()=>{const i=document.querySelector('[data-gallery=gear-0] input');i.value='';i.dispatchEvent(new Event('input',{bubbles:true}));document.querySelector('[data-gallery=gear-0] [data-filter=all]').click();})()")
    browser.screenshot(args.screenshots / "desktop-weapons.png")
    browser.evaluate("(()=>{const i=document.getElementById('search-input');i.value='防护服';i.dispatchEvent(new Event('input',{bubbles:true}));})()")
    check("full text search", "!document.getElementById('search-results').hidden && document.querySelectorAll('.search-result').length>0")
    browser.evaluate("document.querySelector('.search-result').click()")
    browser.wait("document.getElementById('search-results').hidden && location.hash !== '#/weapons'")
    check("search result routing", "location.hash.startsWith('#/') && document.querySelectorAll('.article-page:not([hidden])').length===1")
    browser.evaluate("document.getElementById('search-clear').click()")
    check("search clear", "document.getElementById('search-input').value==='' && document.getElementById('search-results').hidden")
    browser.evaluate("(()=>{const i=document.getElementById('search-input');i.value='不存在zzz';i.dispatchEvent(new Event('input',{bubbles:true}));})()")
    check("search empty result", "!!document.querySelector('.search-empty')")
    browser.command("Input.dispatchKeyEvent", {"type":"keyDown", "key":"Escape", "code":"Escape", "windowsVirtualKeyCode":27})
    browser.command("Input.dispatchKeyEvent", {"type":"keyUp", "key":"Escape", "code":"Escape", "windowsVirtualKeyCode":27})
    check("escape closes search", "document.getElementById('search-results').hidden")

    browser.navigate(args.url + "#/power", "power")
    check("deep link load", "document.title.includes('能源与物流')")
    browser.evaluate("document.querySelector('#toc-list a[data-target]').click()")
    browser.wait("location.hash.split('/').length>2")
    check("chapter anchors", "location.hash.startsWith('#/power/')")
    browser.evaluate("location.hash='#/unknown-entry'")
    browser.wait("location.hash==='#/home' && !document.getElementById('home-page').hidden")
    checks.append("unknown route fallback")
    browser.evaluate("document.getElementById('search-clear').click();document.getElementById('search-input').blur()")

    for width in (390, 320):
        browser.viewport(width, 844)
        browser.route("home")
        browser.wait("innerWidth === " + str(width))
        check(f"{width}px no horizontal overflow", "document.documentElement.scrollWidth <= innerWidth + 1")
        browser.screenshot(args.screenshots / f"mobile-home-{width}.png")
        browser.evaluate("document.getElementById('mobile-menu').click()")
        check(f"{width}px menu opens", "document.getElementById('mobile-menu').getAttribute('aria-expanded')==='true' && !document.getElementById('sidebar-shade').hidden")
        browser.evaluate("document.querySelector('.nav-link[data-page=recipes]').click()")
        browser.wait("!document.querySelector('[data-page-id=recipes]').hidden")
        check(f"{width}px menu closes on navigation", "document.getElementById('mobile-menu').getAttribute('aria-expanded')==='false'")
        check(f"{width}px table scroll isolation", "document.documentElement.scrollWidth<=innerWidth+1 && [...document.querySelectorAll('[data-page-id=recipes] .table-wrap')].every(t=>t.clientWidth<=innerWidth)")
        browser.screenshot(args.screenshots / f"mobile-recipes-{width}.png")

    browser.viewport(1280)
    file_url = (ROOT / "Docs/WIKI.html").as_uri()
    browser.navigate(file_url + "#/weapons", "weapons")
    check("file URL offline route", "location.protocol==='file:' && document.querySelectorAll('[data-gallery=gear-0] .item-card').length===10")
    browser.evaluate("Promise.all([...document.images].filter(i=>i.getClientRects().length).map(i=>{i.loading='eager';return i.decode().catch(()=>{});} ))")
    check("file URL embedded images", "[...document.images].filter(i=>i.getClientRects().length).every(i=>i.complete && i.naturalWidth>0)")
    check("no external runtime dependencies", "![...document.querySelectorAll('script[src],link[rel=stylesheet]')].length && [...document.images].every(i=>i.src.startsWith('data:image/'))")
    if browser.exceptions:
        raise AssertionError(browser.exceptions)
    checks.append("no runtime exceptions")
    browser.command("Page.close")
    browser.socket.close()
    print(json.dumps({"passed": len(checks), "checks": checks, "screenshots": str(args.screenshots)}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
