import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  linkedSignal,
  signal
} from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Params, Router } from '@angular/router';
import { catchError, distinctUntilChanged, map, of, startWith, switchMap, timer } from 'rxjs';
import { CaseApi } from '../../../core/case-api';
import {
  CASE_PRIORITIES,
  CASE_SORT_FIELDS,
  CASE_STATUSES,
  Case,
  CaseQuery,
  CaseSortField,
  PagedResult
} from '../../../core/case-models';
import { ApiError } from '../../../core/http-error-interceptor';
import { StateMessage } from '../../../shared/state-message/state-message';
import { CaseFilterValues, CaseFilters, EMPTY_FILTERS } from '../case-filters/case-filters';
import { CaseStatusEditor } from '../case-status-editor/case-status-editor';
import { CaseSummaryPanel } from '../case-summary/case-summary';

type ListState =
  | { readonly status: 'loading' }
  | { readonly status: 'ready'; readonly page: PagedResult<Case> }
  | { readonly status: 'error'; readonly message: string };

const DEFAULT_PAGE_SIZE = 25;

const PROGRESS_DELAY_MS = 250;

const PAGE_SIZE_OPTIONS = [10, 25, 50, 100];

const DEFAULT_SORT: Sort = { active: 'CreatedAt' satisfies CaseSortField, direction: 'desc' };

@Component({
  selector: 'app-case-list',
  imports: [
    DatePipe,
    CaseFilters,
    CaseStatusEditor,
    CaseSummaryPanel,
    StateMessage,
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
    MatProgressBarModule
  ],
  templateUrl: './case-list.html',
  styleUrl: './case-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CaseList {
  private readonly caseApi = inject(CaseApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  private readonly params = toSignal(this.route.queryParamMap, {
    initialValue: this.route.snapshot.queryParamMap
  });

  protected readonly filters = computed<CaseFilterValues>(() => {
    const params = this.params();

    return {
      search: params.get('q') ?? '',
      organization: params.get('org') ?? '',
      status: allowed(params.getAll('status'), CASE_STATUSES),
      priority: allowed(params.getAll('priority'), CASE_PRIORITIES),
      createdFrom: params.get('from'),
      createdTo: params.get('to')
    };
  });

  protected readonly pageIndex = computed(() => positiveNumber(this.params().get('page'), 1) - 1);

  protected readonly pageSize = computed(() =>
    positiveNumber(this.params().get('size'), DEFAULT_PAGE_SIZE)
  );

  protected readonly sort = computed<Sort>(() => ({
    active: allowed([this.params().get('sort')], CASE_SORT_FIELDS)[0] ?? DEFAULT_SORT.active,
    direction: this.params().get('dir') === 'asc' ? 'asc' : 'desc'
  }));

  private readonly filterQuery = computed<CaseQuery>(() => {
    const filters = this.filters();

    return {
      search: filters.search || undefined,
      organization: filters.organization || undefined,
      status: filters.status.length ? filters.status : undefined,
      priority: filters.priority.length ? filters.priority : undefined,
      createdFrom: startOfLocalDay(filters.createdFrom),
      createdTo: endOfLocalDay(filters.createdTo)
    };
  });

  private readonly query = computed<CaseQuery>(() => {
    return {
      ...this.filterQuery(),
      page: this.pageIndex() + 1,
      pageSize: this.pageSize(),
      // Column ids are the API's sort field names, so the active column needs no translation.
      sortBy: this.sort().active as CaseSortField,
      sortDirection: this.sort().direction === 'asc' ? 'Ascending' : 'Descending'
    };
  });

  private readonly listReload = signal(0);

  private readonly listSource = computed(() => ({
    query: this.query(),
    reload: this.listReload()
  }));

  private readonly state = toSignal(
    toObservable(this.listSource).pipe(
      distinctUntilChanged(unchanged),
      switchMap(({ query }) =>
        this.caseApi.getCases(query).pipe(
          map((page): ListState => ({ status: 'ready', page })),
          catchError((error: ApiError) => of<ListState>({ status: 'error', message: error.message })),
          startWith<ListState>({ status: 'loading' })
        )
      )
    ),
    { initialValue: { status: 'loading' } satisfies ListState as ListState }
  );

  // The last page survives the next load, so a reload does not empty the table under the user.
  private readonly page = linkedSignal<ListState, PagedResult<Case> | undefined>({
    source: this.state,
    computation: (state, previous) => (state.status === 'ready' ? state.page : previous?.value)
  });

  // A status change alters the counts without altering the filters, so it needs its own trigger.
  private readonly summaryReload = signal(0);

  private readonly summarySource = computed(() => ({
    query: this.filterQuery(),
    reload: this.summaryReload()
  }));

  protected readonly summary = toSignal(
    toObservable(this.summarySource).pipe(
      distinctUntilChanged(unchanged),
      switchMap(({ query }) => this.caseApi.getSummary(query).pipe(catchError(() => of(undefined))))
    )
  );

  protected readonly rows = linkedSignal<readonly Case[]>(() => this.page()?.items ?? []);

  protected readonly totalCount = computed(() => this.page()?.totalCount ?? 0);

  protected readonly errorMessage = computed(() => {
    const state = this.state();

    return state.status === 'error' ? state.message : null;
  });

  protected readonly isEmpty = computed(
    () => this.state().status === 'ready' && this.rows().length === 0
  );

  // A request that answers in 40ms would otherwise flash a progress bar on every keystroke.
  protected readonly showProgress = toSignal(
    toObservable(computed(() => this.state().status === 'loading')).pipe(
      switchMap(loading => (loading ? timer(PROGRESS_DELAY_MS).pipe(map(() => true)) : of(false)))
    ),
    { initialValue: false }
  );

  protected readonly pageSizeOptions = PAGE_SIZE_OPTIONS;

  protected readonly displayedColumns = [
    'Title',
    'OrganizationName',
    'Status',
    'Priority',
    'CreatedAt',
    'UpdatedAt'
  ];

  protected applyRow(updated: Case): void {
    this.rows.update(rows => rows.map(row => (row.id === updated.id ? updated : row)));
    this.summaryReload.update(reload => reload + 1);
  }

  protected retry(): void {
    this.listReload.update(reload => reload + 1);
    this.summaryReload.update(reload => reload + 1);
  }

  protected clearFilters(): void {
    this.changeFilters(EMPTY_FILTERS);
  }

  protected changePage(event: PageEvent): void {
    this.updateUrl({ page: event.pageIndex + 1, size: event.pageSize });
  }

  protected changeSort(sort: Sort): void {
    this.updateUrl({ sort: sort.active, dir: sort.direction, page: null });
  }

  protected changeFilters(filters: CaseFilterValues): void {
    this.updateUrl({
      q: filters.search || null,
      org: filters.organization || null,
      status: filters.status.length ? [...filters.status] : null,
      priority: filters.priority.length ? [...filters.priority] : null,
      from: filters.createdFrom,
      to: filters.createdTo,
      page: null
    });
  }

  private updateUrl(queryParams: Params): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams,
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
  }
}

function unchanged<T>(a: T, b: T): boolean {
  return JSON.stringify(a) === JSON.stringify(b);
}

function allowed<T extends string>(values: readonly (string | null)[], options: readonly T[]): T[] {
  return values.filter((value): value is T => options.includes(value as T));
}

// The table renders CreatedAt in local time, so a picked day means the local day. A date-time
// string without a zone parses as local, while a date-only string would parse as UTC.
function startOfLocalDay(date: string | null): string | undefined {
  return date ? new Date(`${date}T00:00:00`).toISOString() : undefined;
}

function endOfLocalDay(date: string | null): string | undefined {
  return date ? new Date(`${date}T23:59:59.999`).toISOString() : undefined;
}

function positiveNumber(value: string | null, fallback: number): number {
  const parsed = Number(value);

  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
}
