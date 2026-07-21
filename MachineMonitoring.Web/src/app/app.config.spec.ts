import { resolveBrowserApiBaseUrl } from './app.config';

describe('appConfig', () => {
  it('should resolve the browser API base URL from location.origin', () => {
    expect(resolveBrowserApiBaseUrl()).toBe(window.location.origin);
  });
});
