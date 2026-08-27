import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { CaseSummary } from '../../../core/case-models';

interface SummaryTile {
  readonly label: string;
  readonly value: string;
  readonly accent?: string;
}

@Component({
  selector: 'app-case-summary',
  templateUrl: './case-summary.html',
  styleUrl: './case-summary.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CaseSummaryPanel {
  readonly summary = input<CaseSummary | undefined>();

  protected readonly tiles = computed<SummaryTile[]>(() => {
    const summary = this.summary();

    if (!summary) {
      return [];
    }

    return [
      { label: 'Total', value: `${summary.totalCount}` },
      { label: 'New', value: `${summary.newCount}`, accent: 'New' },
      { label: 'In progress', value: `${summary.inProgressCount}`, accent: 'InProgress' },
      { label: 'Waiting', value: `${summary.waitingCount}`, accent: 'Waiting' },
      { label: 'Completed', value: `${summary.completedCount}`, accent: 'Completed' },
      { label: 'High priority', value: `${summary.highPriorityCount}`, accent: 'High' },
      { label: 'Medium priority', value: `${summary.mediumPriorityCount}`, accent: 'Medium' },
      { label: 'Low priority', value: `${summary.lowPriorityCount}`, accent: 'Low' },
      {
        label: 'Avg. open age (days)',
        value: summary.averageOpenAgeInDays === null ? '—' : summary.averageOpenAgeInDays.toFixed(1)
      },
      { label: 'Updated in last 7 days', value: `${summary.updatedInLastSevenDays}` }
    ];
  });
}
