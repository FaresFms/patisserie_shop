(function () {
  window.applyDataTheme = function (isDark) {
    document.documentElement.setAttribute('data-theme', isDark ? 'dark' : 'light');
    try { localStorage.setItem('warm-theme', isDark ? 'dark' : 'light'); } catch (e) {}
  };

  window.setLegacyDarkModeClass = function (isDark) {
    document.body.classList.toggle('dark-mode', isDark);
    document.documentElement.classList.toggle('dark-mode', isDark);
  };

  // On page load, restore saved preference
  try {
    const saved = localStorage.getItem('warm-theme');
    if (saved === 'dark') document.documentElement.setAttribute('data-theme', 'dark');
  } catch (e) {}
})();
