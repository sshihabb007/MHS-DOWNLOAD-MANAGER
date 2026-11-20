/* docs/app.js — Interactive controls for MHS Download Manager official page
   Uses shihab_/mehedi_/sshihabb007_ naming conventions matching shihabCV DESIGN.md */

(() => {
  'use strict';

  const shihab_toastEl = document.getElementById('shihab_toast');
  let shihab_toastTimer = null;

  function shihab_showToast(message) {
    if (!shihab_toastEl) return;
    shihab_toastEl.textContent = message;
    shihab_toastEl.classList.add('show');
    if (shihab_toastTimer) window.clearTimeout(shihab_toastTimer);
    shihab_toastTimer = window.setTimeout(() => {
      shihab_toastEl.classList.remove('show');
    }, 2400);
  }

  /* 1. Dark / Light Theme + 7-Accent Palette (aligned with shihabCV) */
  function mehedi_initThemeControls() {
    const themeToggle = document.getElementById('sshihabb007-theme-toggle');
    const moonIcon = document.getElementById('shihab_icon_moon');
    const sunIcon = document.getElementById('shihab_icon_sun');
    const paletteToggle = document.getElementById('palette-toggle');
    const colorPalette = document.getElementById('color-palette');
    const colorBtns = document.querySelectorAll('.color-btn');

    const syncThemeIcons = (isLight) => {
      if (moonIcon) moonIcon.style.display = isLight ? 'none' : 'block';
      if (sunIcon) sunIcon.style.display = isLight ? 'block' : 'none';
      if (themeToggle) {
        themeToggle.setAttribute('aria-pressed', String(isLight));
        themeToggle.setAttribute('aria-label', isLight ? 'Switch to dark mode' : 'Switch to light mode');
      }
    };

    const storedTheme = localStorage.getItem('mehedi_theme');
    if (storedTheme === 'light') {
      document.body.classList.add('light-mode');
    }
    syncThemeIcons(document.body.classList.contains('light-mode'));

    if (themeToggle) {
      themeToggle.addEventListener('click', () => {
        const isLight = document.body.classList.toggle('light-mode');
        localStorage.setItem('mehedi_theme', isLight ? 'light' : 'dark');
        syncThemeIcons(isLight);
      });
    }

    const applyAccent = (hex) => {
      if (!hex || hex === 'default') {
        document.documentElement.style.removeProperty('--primary-color');
      } else {
        document.documentElement.style.setProperty('--primary-color', hex);
      }
      localStorage.setItem('mehedi_color', hex || 'default');
      colorBtns.forEach((btn) => {
        const isActive = btn.getAttribute('data-color') === (hex || 'default');
        btn.classList.toggle('active', isActive);
        btn.setAttribute('aria-pressed', String(isActive));
      });
    };

    applyAccent(localStorage.getItem('mehedi_color') || 'default');

    if (paletteToggle && colorPalette) {
      paletteToggle.addEventListener('click', (e) => {
        e.stopPropagation();
        const isHidden = colorPalette.hasAttribute('hidden');
        if (isHidden) {
          colorPalette.removeAttribute('hidden');
          paletteToggle.setAttribute('aria-expanded', 'true');
        } else {
          colorPalette.setAttribute('hidden', '');
          paletteToggle.setAttribute('aria-expanded', 'false');
        }
      });

      document.addEventListener('click', (e) => {
        if (!e.target.closest('.color-switcher-container')) {
          colorPalette.setAttribute('hidden', '');
          paletteToggle.setAttribute('aria-expanded', 'false');
        }
      });
    }

    colorBtns.forEach((btn) => {
      btn.addEventListener('click', () => {
        applyAccent(btn.getAttribute('data-color'));
      });
    });
  }

  /* 2. Interactive MDM Desktop App Theme Previewer */
  function shihab_initAppThemePreview() {
    const appWin = document.getElementById('shihab_mdm_preview_window');
    const themeBtns = document.querySelectorAll('.shihab_mdm_theme_btn');
    if (!appWin || themeBtns.length === 0) return;

    themeBtns.forEach((btn) => {
      btn.addEventListener('click', () => {
        const theme = btn.getAttribute('data-mdm-theme') || 'obsidian';
        appWin.setAttribute('data-mdm-theme', theme);
        themeBtns.forEach((b) => b.setAttribute('aria-pressed', String(b === btn)));
      });
    });
  }

  /* 3. Filterable & Searchable Feature Matrix */
  function shihab_initFeatureFilter() {
    const searchInput = document.getElementById('shihab_feature_search');
    const filterBtns = document.querySelectorAll('.shihab_filter_btn');
    const cards = Array.from(document.querySelectorAll('.shihab_feature_card'));
    const statusEl = document.getElementById('shihab_feature_status');
    const emptyEl = document.getElementById('shihab_feature_empty');
    const resetBtn = document.getElementById('shihab_feature_reset');

    if (cards.length === 0) return;

    let activeCategory = 'all';

    const updateList = () => {
      const query = (searchInput ? searchInput.value : '').trim().toLowerCase();
      let visibleCount = 0;

      cards.forEach((card) => {
        const cat = card.getAttribute('data-category') || '';
        const text = (card.textContent || '').toLowerCase();
        const matchesCat = activeCategory === 'all' || cat.split(' ').includes(activeCategory);
        const matchesQuery = query === '' || text.includes(query);
        const show = matchesCat && matchesQuery;
        card.hidden = !show;
        if (show) visibleCount += 1;
      });

      if (statusEl) {
        statusEl.textContent = `Showing ${visibleCount} of ${cards.length} core subsystems`;
      }
      if (emptyEl) {
        emptyEl.hidden = visibleCount > 0;
      }
    };

    filterBtns.forEach((btn) => {
      btn.addEventListener('click', () => {
        activeCategory = btn.getAttribute('data-filter') || 'all';
        filterBtns.forEach((b) => b.setAttribute('aria-pressed', String(b === btn)));
        updateList();
      });
    });

    if (searchInput) {
      searchInput.addEventListener('input', updateList);
      document.addEventListener('keydown', (e) => {
        if (
          e.key === '/' &&
          document.activeElement !== searchInput &&
          !/INPUT|TEXTAREA|SELECT/.test(document.activeElement?.tagName || '')
        ) {
          e.preventDefault();
          searchInput.focus();
        }
      });
    }

    if (resetBtn) {
      resetBtn.addEventListener('click', () => {
        activeCategory = 'all';
        if (searchInput) searchInput.value = '';
        filterBtns.forEach((b) =>
          b.setAttribute('aria-pressed', String(b.getAttribute('data-filter') === 'all'))
        );
        updateList();
      });
    }
  }

  /* 4. Accessible WAI-ARIA Tablists (Extension Guide + CLI/MCP Workbench) */
  function mehedi_initTablists() {
    document.querySelectorAll('[role="tablist"]').forEach((tablist) => {
      const tabs = Array.from(tablist.querySelectorAll('[role="tab"]'));

      const activateTab = (tab) => {
        const targetId = tab.getAttribute('aria-controls');
        tabs.forEach((t) => {
          const isSelected = t === tab;
          t.setAttribute('aria-selected', String(isSelected));
          t.setAttribute('tabindex', isSelected ? '0' : '-1');
          const panelId = t.getAttribute('aria-controls');
          const panel = panelId ? document.getElementById(panelId) : null;
          if (panel) panel.hidden = !isSelected;
        });
        if (targetId) {
          const activePanel = document.getElementById(targetId);
          if (activePanel) activePanel.hidden = false;
        }
      };

      tabs.forEach((tab, index) => {
        tab.addEventListener('click', () => activateTab(tab));
        tab.addEventListener('keydown', (e) => {
          let nextIdx = index;
          if (e.key === 'ArrowRight') nextIdx = (index + 1) % tabs.length;
          else if (e.key === 'ArrowLeft') nextIdx = (index - 1 + tabs.length) % tabs.length;
          else if (e.key === 'Home') nextIdx = 0;
          else if (e.key === 'End') nextIdx = tabs.length - 1;
          else return;
          e.preventDefault();
          tabs[nextIdx].focus();
          activateTab(tabs[nextIdx]);
        });
      });
    });
  }

  /* 5. Copy-to-Clipboard Controls */
  function shihab_initCopyButtons() {
    document.querySelectorAll('[data-copy-text], [data-copy-target]').forEach((btn) => {
      btn.addEventListener('click', async () => {
        let text = btn.getAttribute('data-copy-text') || '';
        const targetSel = btn.getAttribute('data-copy-target');
        if (!text && targetSel) {
          const target = document.querySelector(targetSel);
          text = target ? target.textContent.trim() : '';
        }
        if (!text) return;

        try {
          if (navigator.clipboard && navigator.clipboard.writeText) {
            await navigator.clipboard.writeText(text);
          } else {
            const ta = document.createElement('textarea');
            ta.value = text;
            ta.setAttribute('readonly', '');
            ta.style.position = 'fixed';
            ta.style.opacity = '0';
            document.body.appendChild(ta);
            ta.select();
            document.execCommand('copy');
            ta.remove();
          }
          shihab_showToast(btn.getAttribute('data-copy-label') || 'Copied to clipboard');
        } catch {
          shihab_showToast('Press Ctrl+C to copy');
        }
      });
    });
  }

  /* 6. Back-to-Top Floating Control */
  function mehedi_initBackToTop() {
    const backBtn = document.getElementById('back-to-top');
    if (!backBtn) return;

    const toggle = () => {
      backBtn.classList.toggle('show', window.scrollY > 360);
    };

    toggle();
    window.addEventListener('scroll', toggle, { passive: true });
    backBtn.addEventListener('click', () => {
      window.scrollTo({ top: 0, behavior: 'smooth' });
    });
  }

  document.addEventListener('DOMContentLoaded', () => {
    mehedi_initThemeControls();
    shihab_initAppThemePreview();
    shihab_initFeatureFilter();
    mehedi_initTablists();
    shihab_initCopyButtons();
    mehedi_initBackToTop();
  });
})();
