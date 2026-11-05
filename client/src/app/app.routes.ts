import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/guards';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'lobby' },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login.page').then((m) => m.LoginPage),
    canActivate: [guestGuard],
    title: 'Log in — Tic-Tac-Toe Online',
  },
  {
    path: 'register',
    loadComponent: () => import('./features/auth/register.page').then((m) => m.RegisterPage),
    canActivate: [guestGuard],
    title: 'Register — Tic-Tac-Toe Online',
  },
  {
    path: 'lobby',
    loadComponent: () => import('./features/lobby/lobby.page').then((m) => m.LobbyPage),
    canActivate: [authGuard],
    title: 'Lobby — Tic-Tac-Toe Online',
  },
  {
    path: 'game/:id',
    loadComponent: () => import('./features/game/game.page').then((m) => m.GamePage),
    canActivate: [authGuard],
    title: 'Game — Tic-Tac-Toe Online',
  },
  {
    path: '**',
    loadComponent: () => import('./shared/not-found.page').then((m) => m.NotFoundPage),
    title: 'Not found — Tic-Tac-Toe Online',
  },
];
