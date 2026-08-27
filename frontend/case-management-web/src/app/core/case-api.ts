import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Case, CaseQuery, CaseStatus, CaseSummary, PagedResult } from './case-models';

@Injectable({ providedIn: 'root' })
export class CaseApi {
  private readonly http = inject(HttpClient);
  private readonly casesUrl = `${environment.apiBaseUrl}/cases`;

  getCases(query: CaseQuery): Observable<PagedResult<Case>> {
    return this.http.get<PagedResult<Case>>(this.casesUrl, { params: toParams(query) });
  }

  getSummary(query: CaseQuery): Observable<CaseSummary> {
    return this.http.get<CaseSummary>(`${this.casesUrl}/summary`, { params: toParams(query) });
  }

  updateStatus(id: number, status: CaseStatus, rowVersion: string): Observable<Case> {
    // Quotes are part of the entity-tag grammar; the API rejects an unquoted If-Match.
    return this.http.patch<Case>(
      `${this.casesUrl}/${id}/status`,
      { status },
      { headers: { 'If-Match': `"${rowVersion}"` } }
    );
  }
}

function toParams(query: CaseQuery): HttpParams {
  const supplied = Object.entries(query).filter(
    ([, value]) => value !== undefined && value !== null && value !== ''
  );

  // HttpParams repeats a key for every item of an array value, which is the shape the API binds.
  return new HttpParams({ fromObject: Object.fromEntries(supplied) });
}
