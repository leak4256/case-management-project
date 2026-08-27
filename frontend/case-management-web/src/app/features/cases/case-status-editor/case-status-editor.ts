import { ChangeDetectionStrategy, Component, inject, input, linkedSignal, output, signal } from '@angular/core';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarConfig } from '@angular/material/snack-bar';
import { CaseApi } from '../../../core/case-api';
import { CASE_STATUS_LABELS, CASE_STATUSES, Case, CaseStatus } from '../../../core/case-models';
import { ApiError } from '../../../core/http-error-interceptor';

// No duration: a conflict discards what the user just picked, so the notice waits to be read.
const NOTICE_CONFIG: MatSnackBarConfig = {
  panelClass: 'notice-snack-bar',
  horizontalPosition: 'center',
  verticalPosition: 'top'
};

@Component({
  selector: 'app-case-status-editor',
  imports: [MatSelectModule],
  templateUrl: './case-status-editor.html',
  styleUrl: './case-status-editor.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CaseStatusEditor {
  readonly case = input.required<Case>();

  readonly applied = output<Case>();

  private readonly caseApi = inject(CaseApi);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly statuses = CASE_STATUSES;

  protected readonly statusLabels = CASE_STATUS_LABELS;

  protected readonly value = linkedSignal(() => this.case().status);

  protected readonly pending = signal(false);

  // The row's own version, not a freshly read one: the change is asserted against the state the
  // user was looking at when they chose, which is what makes a competing write a conflict.
  protected pick(status: CaseStatus): void {
    const previous = this.case().status;

    if (status === previous) {
      return;
    }

    this.value.set(status);
    this.pending.set(true);

    this.caseApi.updateStatus(this.case().id, status, this.case().rowVersion).subscribe({
      next: updated => {
        this.pending.set(false);
        this.applied.emit(updated);
      },
      error: (error: ApiError) => this.fail(error, previous, status)
    });
  }

  private fail(error: ApiError, previous: CaseStatus, attempted: CaseStatus): void {
    this.pending.set(false);

    const current = error.status === 409 ? conflictState(error) : null;

    if (!current) {
      this.value.set(previous);
      this.snackBar.open(error.message, 'Dismiss', NOTICE_CONFIG);

      return;
    }

    this.value.set(current.status);

    // The new version travels with the refreshed row, so the next attempt is not stale again.
    this.applied.emit({
      ...this.case(),
      status: current.status,
      updatedAt: current.updatedAt,
      rowVersion: current.rowVersion
    });

    this.snackBar.open(
      `Someone else already changed this case to "${this.statusLabels[current.status]}", ` +
        `so your change to "${this.statusLabels[attempted]}" was not saved. ` +
        'The row now shows the current status — pick again if you still want the change.',
      'Got it',
      NOTICE_CONFIG
    );
  }
}

function conflictState(
  error: ApiError
): { status: CaseStatus; updatedAt: string; rowVersion: string } | null {
  const status = error.problem?.['currentStatus'];
  const updatedAt = error.problem?.['currentUpdatedAt'];
  const rowVersion = error.problem?.['currentRowVersion'];

  return CASE_STATUSES.includes(status as CaseStatus) &&
    typeof updatedAt === 'string' &&
    typeof rowVersion === 'string'
    ? { status: status as CaseStatus, updatedAt, rowVersion }
    : null;
}
