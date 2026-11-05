import { routes } from './app.routes';

describe('app routes (V1 shell)', () => {
  it('defines the documented route set', () => {
    const paths = routes.map((r) => r.path);
    expect(paths).toContain('');
    expect(paths).toContain('login');
    expect(paths).toContain('register');
    expect(paths).toContain('lobby');
    expect(paths).toContain('game/:id');
    expect(paths).toContain('**');
  });

  it('redirects the empty path to the lobby', () => {
    const root = routes.find((r) => r.path === '');
    expect(root?.redirectTo).toBe('lobby');
  });

  it('lazy-loads every page', () => {
    for (const route of routes.filter((r) => r.path && r.path !== '')) {
      expect(route.loadComponent).toBeDefined();
    }
  });
});
