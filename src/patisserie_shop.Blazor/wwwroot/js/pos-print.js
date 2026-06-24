(function () {
  // Prints just the on-screen receipt panel. We add a body class that the
  // @media print rules (in Cashier.razor's scoped <style>) use to hide
  // everything except the element flagged with .pos-print-area, then call
  // the browser's native print dialog. No PDF, no server round-trip.
  window.posPrintReceipt = function () {
    try {
      document.body.classList.add('pos-printing');
      window.print();
    } finally {
      // Remove the flag after the dialog returns (covers both print + cancel).
      window.setTimeout(function () {
        document.body.classList.remove('pos-printing');
      }, 250);
    }
  };
})();
