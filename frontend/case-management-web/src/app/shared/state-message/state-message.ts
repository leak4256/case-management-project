import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-state-message',
  imports: [MatButtonModule, MatIconModule],
  templateUrl: './state-message.html',
  styleUrl: './state-message.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StateMessage {
  readonly icon = input.required<string>();

  readonly message = input.required<string>();

  readonly actionLabel = input.required<string>();

  readonly action = output<void>();
}
