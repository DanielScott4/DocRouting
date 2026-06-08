// Communicates with the Chrome/Edge native messaging host (SmartcardHost).
// Requires the browser extension to be installed and the host registered.
window.smartcardBridge = {
  _send: (msg) => new Promise((resolve, reject) => {
    if (!chrome || !chrome.runtime)
      return reject('Chrome extension not available. Install the PadesSign browser extension.');
    chrome.runtime.sendMessage('YOUR_EXTENSION_ID_HERE', msg, (resp) => {
      if (chrome.runtime.lastError) return reject(chrome.runtime.lastError.message);
      if (resp.error)               return reject(resp.error);
      resolve(resp);
    });
  }),

  getCertificate: () =>
    window.smartcardBridge._send({ command: 'get-certificate' })
      .then(r => r.certificateBase64),

  sign: (digestBase64, algorithm) =>
    window.smartcardBridge._send({ command: 'sign', digestBase64, algorithm })
      .then(r => ({ Pkcs7Base64: r.pkcs7Base64, ChainBase64: r.chainBase64 }))
};