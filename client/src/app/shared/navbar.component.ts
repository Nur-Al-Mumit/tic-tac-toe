import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'ttt-navbar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <header class="sticky top-0 z-40 border-b border-white/5 bg-bg/70 backdrop-blur-md">
      <div class="mx-auto flex h-16 max-w-5xl items-center justify-between px-4">
        <a routerLink="/lobby" class="group flex items-center gap-2.5">
          <span class="flex h-9 w-9 items-center justify-center rounded-xl bg-gradient-to-br from-primary to-sky-500 font-mono text-sm font-black text-white shadow-lg shadow-primary/30 transition-transform group-hover:scale-105">
            X·O
          </span>
          <span class="hidden flex-col leading-none sm:flex">
            <span class="text-sm font-extrabold tracking-tight">TIC·TAC·TOE</span>
            <span class="text-[10px] font-semibold uppercase tracking-[0.2em] text-slate-500">online arena</span>
          </span>
        </a>
        @if (auth.user(); as user) {
          <div class="flex items-center gap-3">
            <span class="flex h-9 w-9 items-center justify-center rounded-full border border-primary/40 bg-primary/15 font-mono text-sm font-bold text-primary">
              {{ user.username[0].toUpperCase() }}
            </span>
            <span class="hidden text-sm font-semibold text-slate-200 sm:inline">{{ user.username }}</span>
            <button (click)="logout()" class="btn-ghost danger !py-1.5">Log out</button>
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
