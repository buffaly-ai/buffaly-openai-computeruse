/**
 * KCS Utility Module: LocalStorage2
 * ------------------------------------------------------------
 * Light-weight wrapper around `window.localStorage` that enforces
 * JSON serialisation and exposes a fluent interface. Use this whenever
 * you need to cache grid preferences, filters, or wizard state between
 * navigations so everything is consistently encoded/decoded.
 *
 * Key helpers
 *  - LocalStorage.set(key, value)   -> stores any serialisable object and
 *                                     returns the helper for chaining.
 *  - LocalStorage.get(key)          -> parses JSON back into an object or
 *                                     `null` if missing/invalid.
 *  - LocalStorage.remove(key)       -> clears the entry.
 *
 * Usage example – remember search criteria:
 * ```js
 * const STORAGE_KEY = "PatientSearch.Filters";
 * LocalStorage.set(STORAGE_KEY, filters);
 *
 * // Later when page loads
 * const saved = LocalStorage.get(STORAGE_KEY);
 * if (saved) {
 *     BlindBind(_$("searchPanel"), saved);
 * }
 * ```
 */
class LocalStorage2 {
        set(name, value) {
                window.localStorage.setItem(name, JSON.stringify(value));
                return this;
        }
        get(name) {
                const item = window.localStorage.getItem(name);
                if (!item)
                        return null;

                try {
                        return JSON.parse(item);
                }
                catch (e) {
                        return null;
                }
        }
        remove(name) {
                window.localStorage.removeItem(name);
        }
}

const LocalStorage = LocalStorage2;
