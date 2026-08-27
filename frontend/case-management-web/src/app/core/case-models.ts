export const CASE_STATUSES = ['New', 'InProgress', 'Waiting', 'Completed'] as const;

export const CASE_PRIORITIES = ['Low', 'Medium', 'High'] as const;

export type CaseStatus = (typeof CASE_STATUSES)[number];

export type CasePriority = (typeof CASE_PRIORITIES)[number];

export const CASE_SORT_FIELDS = [
  'CreatedAt',
  'UpdatedAt',
  'Title',
  'OrganizationName',
  'Status',
  'Priority'
] as const;

export type CaseSortField = (typeof CASE_SORT_FIELDS)[number];

export type SortDirection = 'Ascending' | 'Descending';

export const CASE_STATUS_LABELS: Record<CaseStatus, string> = {
  New: 'New',
  InProgress: 'In progress',
  Waiting: 'Waiting',
  Completed: 'Completed'
};

export interface Case {
  readonly id: number;
  readonly title: string;
  readonly organizationName: string;
  readonly status: CaseStatus;
  readonly priority: CasePriority;
  readonly createdAt: string;
  readonly updatedAt: string;
  readonly rowVersion: string;
}

export interface PagedResult<T> {
  readonly items: readonly T[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly totalPages: number;
  readonly hasPreviousPage: boolean;
  readonly hasNextPage: boolean;
}

export interface CaseSummary {
  readonly totalCount: number;
  readonly newCount: number;
  readonly inProgressCount: number;
  readonly waitingCount: number;
  readonly completedCount: number;
  readonly lowPriorityCount: number;
  readonly mediumPriorityCount: number;
  readonly highPriorityCount: number;
  readonly averageOpenAgeInDays: number | null;
  readonly updatedInLastSevenDays: number;
}

export interface CaseQuery {
  readonly search?: string;
  readonly status?: readonly CaseStatus[];
  readonly priority?: readonly CasePriority[];
  readonly organization?: string;
  readonly createdFrom?: string;
  readonly createdTo?: string;
  readonly sortBy?: CaseSortField;
  readonly sortDirection?: SortDirection;
  readonly page?: number;
  readonly pageSize?: number;
}
