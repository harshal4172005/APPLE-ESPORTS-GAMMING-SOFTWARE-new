export const formatTimeDelta = (ms) => {
  if (ms <= 0) return '00:00:00';
  const totalSeconds = Math.floor(ms / 1000);
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  
  return [
    hours.toString().padStart(2, '0'),
    minutes.toString().padStart(2, '0'),
    seconds.toString().padStart(2, '0')
  ].join(':');
};

export const formatTime = (isoString) => {
  if (!isoString) return '';
  const d = new Date(isoString);
  return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: true });
};

// ── India Standard Time helpers ──────────────────────────────────────────
//
// Branches trade in IST, not UTC. `new Date().toISOString().split('T')[0]`
// silently returns YESTERDAY's date from midnight to 05:29 IST — exactly
// the hours a branch that trades till 2am is closing up and someone opens
// the EOD screen. Use these instead of hand-rolled UTC math anywhere a
// screen needs "today" or "this trading day" as the branch experiences it.
// Mirrors the backend's AppleEsportsErp.Application.Services.IndiaTime.
export const IST_TIME_ZONE = 'Asia/Kolkata';

// Hour (IST) at which one trading day ends and the next begins. Changed to
// plain midnight on 2026-08-23 by explicit business decision - see the
// comment on IndiaTime.BusinessDayStartHour on the backend, which this
// must always match.
const BUSINESS_DAY_START_HOUR = 0;

/** The current wall-clock date and hour in IST, read via the IANA zone. */
function nowIstParts() {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: IST_TIME_ZONE,
    hour12: false,
    year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit',
  }).formatToParts(new Date());
  const get = (type) => parts.find((p) => p.type === type).value;
  // 'en-CA' already formats as yyyy-mm-dd; hour can come back as "24" at midnight.
  return { date: `${get('year')}-${get('month')}-${get('day')}`, hour: Number(get('hour')) % 24 };
}

/** Any instant's calendar date in IST, as 'yyyy-MM-dd'. */
export const toIstDateString = (dateInput) => {
  const d = dateInput instanceof Date ? dateInput : new Date(dateInput);
  return new Intl.DateTimeFormat('en-CA', { timeZone: IST_TIME_ZONE }).format(d);
};

/** Today's calendar date in IST, as 'yyyy-MM-dd'. */
export const todayIst = () => nowIstParts().date;

/**
 * The trading day IST is in right now, as 'yyyy-MM-dd' - plain IST calendar date.
 */
export const currentTradingDayIst = () => {
  const { date, hour } = nowIstParts();
  if (hour >= BUSINESS_DAY_START_HOUR) return date;
  // Pure calendar-day subtraction, anchored in UTC so the local machine's
  // OS timezone can't shift the result - this is string arithmetic on a
  // date, not a timezone conversion.
  const [y, m, d] = date.split('-').map(Number);
  const prev = new Date(Date.UTC(y, m - 1, d));
  prev.setUTCDate(prev.getUTCDate() - 1);
  return prev.toISOString().split('T')[0];
};

/**
 * The [start, end) instant window for one IST trading day, ready to send
 * to the API. Midnight IST on `tradingDayDate` to midnight IST the next day -
 * the same window AppleEsportsErp.Application.Services.IndiaTime.BusinessDayRange
 * computes on the backend for /eod/preview.
 */
export const tradingDayRangeIst = (tradingDayDate) => {
  // +05:30 is IST's fixed, DST-free offset - explicit here so the instant
  // is unambiguous regardless of the browser/OS timezone reading this code.
  const start = new Date(`${tradingDayDate}T${String(BUSINESS_DAY_START_HOUR).padStart(2, '0')}:00:00+05:30`);
  const end = new Date(start.getTime() + 24 * 60 * 60 * 1000);
  return { startIso: start.toISOString(), endIso: end.toISOString() };
};
