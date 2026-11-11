const TOKEN_KEY = "SideBox-admin-token";
const USER_KEY = "SideBox-admin-user";

export function getStoredToken() {
  return window.localStorage.getItem(TOKEN_KEY);
}

export function setStoredToken(token: string) {
  window.localStorage.setItem(TOKEN_KEY, token);
}

export function clearStoredToken() {
  window.localStorage.removeItem(TOKEN_KEY);
}

export function getStoredUsername() {
  return window.localStorage.getItem(USER_KEY);
}

export function setStoredUsername(username: string) {
  window.localStorage.setItem(USER_KEY, username);
}

export function clearStoredUsername() {
  window.localStorage.removeItem(USER_KEY);
}
