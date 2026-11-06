import { Injectable, signal } from '@angular/core';

export type ToastKind = 'error' | 'success' | 'info';

export interface Toast {
  id: number;
  kind: ToastKind;
  message: string;
}

/** Minimal signal-based toasts (design system §Toast): auto-dismiss, stacked bottom-right. */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 0;

  readonly toasts = signal<Toast[]>([]);

  error(message: string, durationMs = 4500): void {
    this.push('error', message, durationMs);
  }

  success(message: string, durationMs = 3500): void {
    this.push('success', message, durationMs);
  }

  info(message: string, durationMs = 3500): void {
    this.push('info', message, durationMs);
  }

  dismiss(id: number): void {
    this.toasts.update((toasts) => toasts.filter((t) => t.id !== id));
  }

  private push(kind: ToastKind, message: string, durationMs: number): void {
    const toast: Toast = { id: this.nextId++, kind, message };
    this.toasts.update((toasts) => [...toasts.slice(-3), toast]);
    setTimeout(() => this.dismiss(toast.id), durationMs);
  }
}
