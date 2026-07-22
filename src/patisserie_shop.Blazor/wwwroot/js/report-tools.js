(function () {
  window.reportTools = window.reportTools || {};

  window.reportTools.printStocktakeReconciliation = function () {
    try {
      document.body.classList.add('stocktake-report-printing');
      window.print();
    } finally {
      window.setTimeout(function () {
        document.body.classList.remove('stocktake-report-printing');
      }, 250);
    }
  };

  window.reportTools.downloadCsv = function (fileName, content) {
    var blob = new Blob(['\ufeff', content], { type: 'text/csv;charset=utf-8' });
    var url = URL.createObjectURL(blob);
    var link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.style.display = 'none';
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.setTimeout(function () { URL.revokeObjectURL(url); }, 500);
  };
})();
