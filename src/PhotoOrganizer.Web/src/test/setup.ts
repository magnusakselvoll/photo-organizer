import '@testing-library/jest-dom/vitest';

// ResizeObserver is not implemented in jsdom; provide a no-op polyfill
// so components that use it (e.g. PhotoGrid) don't throw in unit tests.
// The virtualizer's DOM-measurement logic is not exercisable in jsdom and
// is tested indirectly through the pure helper functions instead.
if (typeof ResizeObserver === 'undefined') {
  (globalThis as unknown as Record<string, unknown>).ResizeObserver = class ResizeObserver {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
}
