(() => {
  "use strict";
  const data = window.WIKI_DATA;
  if (!data || !Array.isArray(data.entries)) return;
  const byId = new Map(data.entries.map(entry => [entry.id, entry]));
  const main = document.getElementById("main-content");
  const sidebar = document.getElementById("sidebar");
  const menuButton = document.getElementById("mobile-menu");
  const shade = document.getElementById("sidebar-shade");
  const input = document.getElementById("search-input");
  const results = document.getElementById("search-results");
  const clear = document.getElementById("search-clear");
  const toc = document.getElementById("toc-list");
  const progress = document.getElementById("reading-progress");
  const backTop = document.getElementById("back-to-top");
  const pages = [...document.querySelectorAll("[data-page-id]")];
  let currentPage = "home";
  let resultIndex = -1;
  let previousRoute = "";
  let scrollScheduled = false;
  const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
  const top = () => window.scrollTo({top: 0, behavior: reducedMotion.matches ? "instant" : "smooth"});

  function setMenu(open) {
    sidebar.classList.toggle("is-open", open);
    shade.hidden = !open;
    document.body.classList.toggle("menu-open", open);
    menuButton.setAttribute("aria-expanded", String(open));
    menuButton.setAttribute("aria-label", open ? "关闭百科目录" : "打开百科目录");
    if (open) requestAnimationFrame(() => sidebar.querySelector("a")?.focus());
  }
  menuButton.addEventListener("click", () => setMenu(!sidebar.classList.contains("is-open")));
  shade.addEventListener("click", () => setMenu(false));
  sidebar.addEventListener("click", event => {
    if (event.target.closest("a")) setMenu(false);
  });
  window.matchMedia("(max-width: 680px)").addEventListener("change", event => {
    if (!event.matches) setMenu(false);
  });

  function closeSearch() {
    results.hidden = true;
    input.setAttribute("aria-expanded", "false");
    input.removeAttribute("aria-activedescendant");
    resultIndex = -1;
  }
  function selectResult(index) {
    const links = [...results.querySelectorAll(".search-result")];
    if (!links.length) return;
    resultIndex = (index + links.length) % links.length;
    links.forEach((link, i) => link.classList.toggle("is-selected", i === resultIndex));
    input.setAttribute("aria-activedescendant", links[resultIndex].id);
    links[resultIndex].scrollIntoView({block: "nearest"});
  }
  function search() {
    const query = input.value.trim().toLocaleLowerCase();
    clear.hidden = query.length === 0;
    results.replaceChildren();
    resultIndex = -1;
    if (!query) { closeSearch(); return; }
    const words = query.split(/\s+/u).filter(Boolean);
    const matches = data.entries.map(entry => {
      const title = entry.title.toLocaleLowerCase();
      const body = (entry.searchText || "").toLocaleLowerCase();
      const haystack = title + " " + entry.category + " " + body;
      if (!words.every(word => haystack.includes(word))) return null;
      return {entry, score: (title === query ? 100 : 0) + words.reduce((sum, word) => sum + (title.includes(word) ? 20 : 0) + (entry.summary.includes(word) ? 5 : 1), 0)};
    }).filter(Boolean).sort((a, b) => b.score - a.score).slice(0, 8);
    results.hidden = false;
    input.setAttribute("aria-expanded", "true");
    if (!matches.length) {
      const empty = document.createElement("p");
      empty.className = "search-empty";
      empty.textContent = "没有找到相关条目，试试“核燃料”“石镐”或“联机”。";
      results.append(empty);
      return;
    }
    matches.forEach(({entry}, index) => {
      const link = document.createElement("a");
      link.className = "search-result";
      link.id = "search-result-" + index;
      link.href = "#/" + entry.id;
      const title = document.createElement("strong");
      title.textContent = entry.title;
      const category = document.createElement("small");
      category.textContent = entry.category;
      const snippet = document.createElement("p");
      const raw = entry.searchText.replace(/[#*|\u0060]/g, " ").replace(/\s+/g, " ");
      const at = Math.max(0, raw.toLocaleLowerCase().indexOf(words[0]));
      snippet.textContent = (at > 20 ? "…" : "") + raw.slice(Math.max(0, at - 20), Math.max(0, at - 20) + 85) + "…";
      link.append(category, title, snippet);
      link.addEventListener("click", () => { closeSearch(); input.blur(); });
      results.append(link);
    });
  }
  input.addEventListener("input", search);
  input.addEventListener("focus", () => { if (input.value.trim()) search(); });
  input.addEventListener("keydown", event => {
    if (event.key === "ArrowDown" && !results.hidden) { event.preventDefault(); selectResult(resultIndex + 1); }
    if (event.key === "ArrowUp" && !results.hidden) { event.preventDefault(); selectResult(resultIndex - 1); }
    if (event.key === "Enter" && !results.hidden) {
      const links = results.querySelectorAll(".search-result");
      if (links.length) { event.preventDefault(); links[Math.max(0, resultIndex)].click(); }
    }
  });
  clear.addEventListener("click", () => { input.value = ""; clear.hidden = true; closeSearch(); input.focus(); });
  document.addEventListener("click", event => { if (!event.target.closest(".search-box")) closeSearch(); });
  document.addEventListener("keydown", event => {
    const editing = event.target.matches("input, textarea, select, [contenteditable='true']");
    if (((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") || (event.key === "/" && !editing)) {
      event.preventDefault(); input.focus();
    }
    if (event.key === "Escape") {
      closeSearch();
      if (sidebar.classList.contains("is-open")) { setMenu(false); menuButton.focus(); }
      else if (document.activeElement === input) input.blur();
    }
    if (event.key === "Tab" && sidebar.classList.contains("is-open")) {
      const links = [...sidebar.querySelectorAll("a")];
      const first = links[0], last = links[links.length - 1];
      if (event.shiftKey && (document.activeElement === first || document.activeElement === menuButton)) { event.preventDefault(); last.focus(); }
      else if (!event.shiftKey && (document.activeElement === last || document.activeElement === menuButton)) { event.preventDefault(); first.focus(); }
    }
  });

  document.querySelectorAll(".gallery").forEach(gallery => {
    const searchInput = gallery.querySelector("[data-gallery-search]");
    const buttons = [...gallery.querySelectorAll("[data-filter]")];
    const cards = [...gallery.querySelectorAll(".item-card")];
    let era = "all";
    function filter() {
      const query = searchInput.value.trim().toLocaleLowerCase();
      let count = 0;
      cards.forEach(card => {
        const visible = (era === "all" || card.dataset.era === era) && card.dataset.name.toLocaleLowerCase().includes(query);
        card.hidden = !visible;
        if (visible) count++;
      });
      gallery.querySelector(".gallery-empty").hidden = count > 0;
    }
    buttons.forEach(button => button.addEventListener("click", () => {
      era = button.dataset.filter;
      buttons.forEach(other => other.setAttribute("aria-pressed", String(other === button)));
      filter();
    }));
    searchInput.addEventListener("input", filter);
  });

  function updateToc(page) {
    toc.replaceChildren();
    const entry = byId.get(page);
    document.getElementById("toc-caption").textContent = entry ? "本页目录" : "阅读导航";
    if (!entry) {
      [["start","快速开局"],["recipes","查找生产配方"],["weapons","武器与工具图鉴"],["multiplayer","合作与存档"]].forEach(([id,title]) => {
        const link = document.createElement("a"); link.href = "#/" + id; link.textContent = title; toc.append(link);
      });
      return;
    }
    document.querySelectorAll('[data-page-id="' + page + '"] [data-anchor]').forEach(heading => {
      const link = document.createElement("a");
      link.href = "#/" + page + "/" + encodeURIComponent(heading.dataset.anchor);
      link.textContent = heading.textContent;
      link.dataset.target = heading.id;
      if (heading.tagName === "H3" || heading.tagName === "H4") link.className = "toc-sub";
      toc.append(link);
    });
    if (!toc.children.length) {
      const link = document.createElement("a");link.href = "#/" + page;link.textContent = entry.title;toc.append(link);
    }
  }
  function route() {
    let segments;
    try { segments = location.hash.replace(/^#\/?/, "").split("/").map(decodeURIComponent); }
    catch { segments = ["home"]; }
    let page = segments[0] || "home";
    if (page !== "home" && !byId.has(page)) {
      page = "home";
      history.replaceState(null, "", location.pathname + location.search + "#/home");
    }
    const changed = currentPage !== page || previousRoute === "";
    currentPage = page;
    pages.forEach(node => { node.hidden = node.dataset.pageId !== page; });
    document.querySelectorAll(".nav-link[data-page]").forEach(link => {
      if (link.dataset.page === page || link.dataset.page === byId.get(page)?.parent) link.setAttribute("aria-current", "page");
      else link.removeAttribute("aria-current");
    });
    document.title = (page === "home" ? "废土回路 · 游戏百科" : byId.get(page).title + " · 废土回路百科");
    updateToc(page);
    closeSearch();setMenu(false);
    requestAnimationFrame(() => {
      const section = segments.length > 1 ? document.getElementById(page + "--" + segments.slice(1).join("/")) : null;
      if (section) section.scrollIntoView({block:"start", behavior:reducedMotion.matches ? "instant" : "smooth"});
      else if (changed || previousRoute !== location.hash) window.scrollTo({top:0,behavior:"instant"});
      if (changed && previousRoute) main.focus({preventScroll:true});
      previousRoute = location.hash;
      updateReading();
    });
  }
  function updateReading() {
    const height = document.documentElement.scrollHeight - window.innerHeight;
    progress.style.width = (height > 0 ? Math.min(100, window.scrollY / height * 100) : 0) + "%";
    backTop.hidden = window.scrollY < 450;
    const links = [...toc.querySelectorAll("[data-target]")];
    let active = null;
    links.forEach(link => { const heading = document.getElementById(link.dataset.target); if (heading && heading.getBoundingClientRect().top < 160) active = link; });
    links.forEach(link => link.classList.toggle("active", link === active));
  }
  window.addEventListener("scroll", () => {
    if (scrollScheduled) return;
    scrollScheduled = true;
    requestAnimationFrame(() => { updateReading();scrollScheduled=false; });
  }, {passive:true});
  window.addEventListener("resize", updateReading);
  backTop.addEventListener("click", top);
  window.addEventListener("hashchange", route);
  document.documentElement.classList.replace("no-js", "js-enabled");
  route();
})();
