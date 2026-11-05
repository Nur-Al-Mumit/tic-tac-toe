import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'ttt-navbar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <header class="border-b border-surface-border bg-surface/80 backdrop-blur">
      <div class="mx-auto flex h-14 max-w-5xl items-center justify-between px-4">
        <a routerLink="/lobby" class="flex items-center gap-2 font-bold">
          <span class="font-mono text-mark-x">X</span>
          <span class="text-slate-500">×</span>
          <span class="font-mono text-mark-o">O</span>
          <span class="ml-1 hidden text-sm text-slate-400 sm:inline">Tic-Tac-Toe Online</span>
        </a>
        @if (auth.user(); as user) {
          <div class="flex items-center gap-3">
            <span class="flex h-8 w-8 items-center justify-center rounded-full bg-primary/20 text-sm font-bold text-primary">
              {{ user.username[0].toUpperCase() }}
            </span>
            <span class="hidden text-sm text-slate-300 sm:inline">{{ user.username }}</span>
            <button (click)="logout()"
                    class="rounded-lg border border-surface-border px-3 py-1.5 text-sm text-slate-300 transition-colors hover:border-danger hover:text-red-400">
              Log out
            </button>
          </div>
        }
      </div>
    </header>
  `,
})
export class NavbarComponent {
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  logout(): void {
    void this.auth.logout().then(() => this.router.navigateByUrl('/login'));
  }
}
