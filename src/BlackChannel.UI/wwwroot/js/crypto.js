// =============================================================================
// BlackChannel client-side crypto — the part the server must never see.
//
// Scheme: ECIES. ECDH on P-256 -> HKDF-SHA256 -> AES-256-GCM. Everything uses the
// browser's native Web Crypto API (window.crypto.subtle) — no external libraries.
//
// The device identity PRIVATE key is generated non-extractable and stored in
// IndexedDB. The browser will use it for key agreement but will never hand the raw
// bytes back to JavaScript, so app code (and anyone who tampers with it) cannot
// exfiltrate it.
//
// AUDIT NOTE (no backdoor): this whole file is the encryption. There is no network
// call here, no key upload of anything private, no escrow. Read it top to bottom.
// =============================================================================
(() => {
  const CURVE = "P-256";
  const DB_NAME = "blackchannel";
  const STORE = "identity";
  const HKDF_INFO = new TextEncoder().encode("blackchannel-ecies-v1");
  const HKDF_SALT = new Uint8Array(32); // fixed all-zero salt; both sides agree

  // ---- base64 helpers ----
  const toB64 = (buf) => {
    const bytes = new Uint8Array(buf);
    let s = "";
    for (let i = 0; i < bytes.length; i++) s += String.fromCharCode(bytes[i]);
    return btoa(s);
  };
  const fromB64 = (b64) => {
    const s = atob(b64);
    const bytes = new Uint8Array(s.length);
    for (let i = 0; i < s.length; i++) bytes[i] = s.charCodeAt(i);
    return bytes.buffer;
  };

  // ---- IndexedDB (store the non-extractable CryptoKey object directly) ----
  const openDb = () =>
    new Promise((resolve, reject) => {
      const req = indexedDB.open(DB_NAME, 1);
      req.onupgradeneeded = () => req.result.createObjectStore(STORE);
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
    });

  const idbGet = async (key) => {
    const db = await openDb();
    return new Promise((resolve, reject) => {
      const tx = db.transaction(STORE, "readonly").objectStore(STORE).get(key);
      tx.onsuccess = () => resolve(tx.result);
      tx.onerror = () => reject(tx.error);
    });
  };
  const idbPut = async (key, value) => {
    const db = await openDb();
    return new Promise((resolve, reject) => {
      const tx = db.transaction(STORE, "readwrite").objectStore(STORE).put(value, key);
      tx.onsuccess = () => resolve();
      tx.onerror = () => reject(tx.error);
    });
  };

  // ---- key agreement -> AES key ----
  const deriveAesKey = async (privateKey, publicKey, usages) => {
    const sharedBits = await crypto.subtle.deriveBits(
      { name: "ECDH", public: publicKey }, privateKey, 256
    );
    const hkdfKey = await crypto.subtle.importKey(
      "raw", sharedBits, "HKDF", false, ["deriveKey"]
    );
    return crypto.subtle.deriveKey(
      { name: "HKDF", hash: "SHA-256", salt: HKDF_SALT, info: HKDF_INFO },
      hkdfKey,
      { name: "AES-GCM", length: 256 },
      false,
      usages
    );
  };

  const importPublic = (b64) =>
    crypto.subtle.importKey("raw", fromB64(b64), { name: "ECDH", namedCurve: CURVE }, false, []);

  // ---- public API ----
  window.blackchannelCrypto = {
    // Ensure this device has an identity key pair; return its base64 raw public key.
    async ensureIdentity() {
      let entry = await idbGet("default");
      if (!entry) {
        const pair = await crypto.subtle.generateKey(
          { name: "ECDH", namedCurve: CURVE },
          /* extractable (private) */ false,
          ["deriveBits", "deriveKey"]
        );
        const pubRaw = toB64(await crypto.subtle.exportKey("raw", pair.publicKey));
        entry = { priv: pair.privateKey, pubB64: pubRaw };
        await idbPut("default", entry);
      }
      return entry.pubB64;
    },

    // Encrypt `plaintext` to `recipientPubB64`. Fresh ephemeral key per message.
    async seal(recipientPubB64, plaintext) {
      const recipientPub = await importPublic(recipientPubB64);
      const eph = await crypto.subtle.generateKey(
        { name: "ECDH", namedCurve: CURVE }, true, ["deriveBits", "deriveKey"]
      );
      const aesKey = await deriveAesKey(eph.privateKey, recipientPub, ["encrypt"]);
      const iv = crypto.getRandomValues(new Uint8Array(12));
      const ct = await crypto.subtle.encrypt(
        { name: "AES-GCM", iv }, aesKey, new TextEncoder().encode(plaintext)
      );
      const ephPubRaw = toB64(await crypto.subtle.exportKey("raw", eph.publicKey));
      // ephemeral private key is now dropped (goes out of scope) -> per-message forward secrecy
      return { eph: ephPubRaw, iv: toB64(iv), ct: toB64(ct) };
    },

    // Decrypt an envelope addressed to this device. Throws if not ours / tampered.
    async open(ephPubB64, ivB64, ctB64) {
      const entry = await idbGet("default");
      if (!entry) throw new Error("no identity key on this device");
      const ephPub = await importPublic(ephPubB64);
      const aesKey = await deriveAesKey(entry.priv, ephPub, ["decrypt"]);
      const pt = await crypto.subtle.decrypt(
        { name: "AES-GCM", iv: new Uint8Array(fromB64(ivB64)) }, aesKey, fromB64(ctB64)
      );
      return new TextDecoder().decode(pt);
    },
  };
})();
