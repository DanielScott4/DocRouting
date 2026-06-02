window.pdfViewer = {
  render: async (url) => {
    const pdfjsLib = window['pdfjs-dist/build/pdf'];
    pdfjsLib.GlobalWorkerOptions.workerSrc =
      'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/4.2.67/pdf.worker.min.js';
    const doc    = await pdfjsLib.getDocument(url).promise;
    const page   = await doc.getPage(1);
    const scale  = 1.5;
    const vp     = page.getViewport({ scale });
    const canvas = document.getElementById('pdf-canvas');
    canvas.width  = vp.width;
    canvas.height = vp.height;
    await page.render({ canvasContext: canvas.getContext('2d'), viewport: vp }).promise;
  }
};