import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastsComponent } from './shared/toasts.component';

@Component({
  selector: 'ttt-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, ToastsComponent],
  template: `
    <router-outlet />
    <ttt-toasts />
  `,
})
export class App {}
