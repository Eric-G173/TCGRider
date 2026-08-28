// A lightweight anonymous ID, generated once and stored in localStorage —
// this is the correct use of localStorage here: it's just a "who is this
// browser" token, not app state. Actual collection data lives in the
// database, keyed by this ID.
const STORAGE_KEY = 'tcgrider_client_id';

export function getClientId() {
  let id = localStorage.getItem(STORAGE_KEY);
  if (!id) {
    id = crypto.randomUUID();
    localStorage.setItem(STORAGE_KEY, id);
  }
  return id;
}