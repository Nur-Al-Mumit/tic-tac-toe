import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ToastService } from '../core/toast.service';

@Component({
  selector: 'ttt-toasts',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pointer-events-none fixed bottom-4 right-4 z-[60] flex w-80 flex-col gap-2" role="status" aria-live="polite">
      @for (toast of service.toasts(); track toast.id) {
        <div class="animate-rise pointer-events-auto flex items-start justify-between gap-3 rounded-xl border px-4 py-3 text-sm font-medium shadow-lg backdrop-blur-md {{
            toast.kind === 'error' ? 'border-danger/40 bg-danger/15 text-red-300'
            : toast.kind === 'success' ? 'border-success/40 bg-success/15 text-emerald-300'
            : 'border-surface-border bg-surface/90 text-slate-200'
          }}">
          <span>{{ toast.message }}</span>
          <button (click)="service.dismiss(toast.id)" aria-label="Dismiss"
                  class="text-slate-400 transition-colors hover:text-slate-100">✕</button>
        </div>
      }
    </div>
  `,
})
export class ToastsComponent {
  readonly service = inject(ToastService);
}
