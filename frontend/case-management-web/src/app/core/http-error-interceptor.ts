import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

export interface ProblemDetails {
  readonly title?: string;
  readonly detail?: string;
  readonly errors?: Record<string, string[]>;
  readonly [key: string]: unknown;
}

/** A failed API call: a message fit to show a user, plus the RFC 7807 body it came from. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
    readonly problem: ProblemDetails | null
  ) {
    super(message);
  }
}

export const httpErrorInterceptor: HttpInterceptorFn = (request, next) =>
  next(request).pipe(catchError((error: HttpErrorResponse) => throwError(() => toApiError(error))));

function toApiError(error: HttpErrorResponse): ApiError {
  if (error.status === 0) {
    return new ApiError(0, 'Cannot reach the server. Check that the API is running.', null);
  }

  const problem = isProblemDetails(error.error) ? error.error : null;

  return new ApiError(error.status, describe(problem, error.status), problem);
}

function describe(problem: ProblemDetails | null, status: number): string {
  const validationMessages = Object.values(problem?.errors ?? {}).flat();

  return (
    validationMessages.join(' ') ||
    problem?.detail ||
    problem?.title ||
    `The request failed with status ${status}.`
  );
}

function isProblemDetails(body: unknown): body is ProblemDetails {
  return typeof body === 'object' && body !== null;
}
