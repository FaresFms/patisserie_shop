(function () {
  window.applyDataTheme = function (isDark) {
    document.documentElement.setAttribute('data-theme', isDark ? 'dark' : 'light');
    try { localStorage.setItem('warm-theme', isDark ? 'dark' : 'light'); } catch (e) {}
  };

  window.setLegacyDarkModeClass = function (isDark) {
    document.body.classList.toggle('dark-mode', isDark);
    document.documentElement.classList.toggle('dark-mode', isDark);
  };

  // Restore the preference before stylesheets load. The legacy key keeps
  // existing users flash-free on their first refresh after this update.
  try {
    const saved = localStorage.getItem('warm-theme');
    const legacySaved = localStorage.getItem('ewallet-darkMode');
    const isDark = saved === 'dark' || (saved === null && legacySaved === 'true');
    document.documentElement.setAttribute('data-theme', isDark ? 'dark' : 'light');
  } catch (e) {}
})();
