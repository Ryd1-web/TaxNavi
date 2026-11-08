/**
 * Tiny global store behind the top progress bar. Two sources feed it:
 * route navigations (started on link click, finished when the route commits)
 * and in-flight data requests (started/finished by the API client).
 */
type Listener = () => void;

const listeners = new Set<Listener>();
let navigating = false;
let pendingRequests = 0;
let navigationTimeout: ReturnType<typeof setTimeout> | undefined;

// Safety net: never leave the bar running if a navigation is abandoned.
const NAVIGATION_TIMEOUT_MS = 10_000;

function emit() {
  listeners.forEach((listener) => listener());
}

export const progress = {
  get navigating() {
    return navigating;
  },
  get loading() {
    return pendingRequests > 0;
  },
  startNavigation() {
    navigating = true;
    clearTimeout(navigationTimeout);
    navigationTimeout = setTimeout(() => progress.finishNavigation(), NAVIGATION_TIMEOUT_MS);
    emit();
  },
  finishNavigation() {
    clearTimeout(navigationTimeout);
    if (!navigating) return;
    navigating = false;
    emit();
  },
  requestStarted() {
    pendingRequests += 1;
    emit();
  },
  requestFinished() {
    pendingRequests = Math.max(0, pendingRequests - 1);
    emit();
  },
  subscribe(listener: Listener) {
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  },
};
