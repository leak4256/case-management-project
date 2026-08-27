import { ChangeDetectionStrategy, Component, effect, input, output } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { debounceTime, distinctUntilChanged, merge } from 'rxjs';
import {
  CASE_PRIORITIES,
  CASE_STATUS_LABELS,
  CASE_STATUSES,
  CasePriority,
  CaseStatus
} from '../../../core/case-models';

const SEARCH_DEBOUNCE_MS = 300;

export interface CaseFilterValues {
  readonly search: string;
  readonly organization: string;
  readonly status: readonly CaseStatus[];
  readonly priority: readonly CasePriority[];
  readonly createdFrom: string | null;
  readonly createdTo: string | null;
}

export const EMPTY_FILTERS: CaseFilterValues = {
  search: '',
  organization: '',
  status: [],
  priority: [],
  createdFrom: null,
  createdTo: null
};

@Component({
  selector: 'app-case-filters',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule
  ],
  templateUrl: './case-filters.html',
  styleUrl: './case-filters.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CaseFilters {
  readonly values = input.required<CaseFilterValues>();

  readonly valuesChange = output<CaseFilterValues>();

  protected readonly statuses = CASE_STATUSES;

  protected readonly priorities = CASE_PRIORITIES;

  protected readonly statusLabels = CASE_STATUS_LABELS;

  protected readonly form = new FormGroup({
    search: new FormControl('', { nonNullable: true }),
    organization: new FormControl('', { nonNullable: true }),
    status: new FormControl<CaseStatus[]>([], { nonNullable: true }),
    priority: new FormControl<CasePriority[]>([], { nonNullable: true }),
    createdFrom: new FormControl<Date | null>(null),
    createdTo: new FormControl<Date | null>(null)
  });

  constructor() {
    effect(() => this.applyIncoming(this.values()));

    // distinctUntilChanged per control, before the merge: across two text controls a shared one
    // would swallow the second field's value whenever it equals the first field's last value.
    merge(
      this.form.controls.search.valueChanges.pipe(distinctUntilChanged()),
      this.form.controls.organization.valueChanges.pipe(distinctUntilChanged())
    )
      .pipe(debounceTime(SEARCH_DEBOUNCE_MS), takeUntilDestroyed())
      .subscribe(() => this.emit());

    merge(
      this.form.controls.status.valueChanges,
      this.form.controls.priority.valueChanges,
      this.form.controls.createdFrom.valueChanges,
      this.form.controls.createdTo.valueChanges
    )
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.emit());
  }

  protected clear(): void {
    this.form.reset(toFormValue(EMPTY_FILTERS), { emitEvent: false });
    this.valuesChange.emit(EMPTY_FILTERS);
  }

  private applyIncoming(values: CaseFilterValues): void {
    if (!sameFilters(values, this.currentValues())) {
      this.form.setValue(toFormValue(values), { emitEvent: false });
    }
  }

  private emit(): void {
    this.valuesChange.emit(this.currentValues());
  }

  private currentValues(): CaseFilterValues {
    const value = this.form.getRawValue();

    return {
      search: value.search.trim(),
      organization: value.organization.trim(),
      status: value.status,
      priority: value.priority,
      createdFrom: toIsoDate(value.createdFrom),
      createdTo: toIsoDate(value.createdTo)
    };
  }
}

function toFormValue(values: CaseFilterValues) {
  return {
    search: values.search,
    organization: values.organization,
    status: [...values.status],
    priority: [...values.priority],
    createdFrom: fromIsoDate(values.createdFrom),
    createdTo: fromIsoDate(values.createdTo)
  };
}

function sameFilters(a: CaseFilterValues, b: CaseFilterValues): boolean {
  return (
    a.search === b.search &&
    a.organization === b.organization &&
    a.createdFrom === b.createdFrom &&
    a.createdTo === b.createdTo &&
    a.status.join() === b.status.join() &&
    a.priority.join() === b.priority.join()
  );
}

function toIsoDate(value: Date | null): string | null {
  if (!value) {
    return null;
  }

  const month = `${value.getMonth() + 1}`.padStart(2, '0');
  const day = `${value.getDate()}`.padStart(2, '0');

  return `${value.getFullYear()}-${month}-${day}`;
}

function fromIsoDate(value: string | null): Date | null {
  return value ? new Date(`${value}T00:00:00`) : null;
}
