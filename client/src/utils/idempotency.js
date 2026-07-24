// crypto.randomUUID() only exists in a secure context (HTTPS or localhost) — it throws on
// plain HTTP LAN addresses (e.g. http://192.168.x.x:8081), which branches commonly use.
// This works everywhere, secure context or not.
export const generateIdempotencyKey = () =>
  Math.random().toString(36).substring(2) + Date.now().toString(36);
