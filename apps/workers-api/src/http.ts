import type { ProblemDetails, ValidationProblemDetails } from "@board-enthusiasts/migration-contract";

export class ApiError extends Error {
  status: number;
  payload: ProblemDetails | ValidationProblemDetails;

  constructor(status: number, payload: ProblemDetails | ValidationProblemDetails) {
    super(payload.title);
    this.status = status;
    this.payload = payload;
  }
}

export function corsHeaders(origin?: string | null): HeadersInit {
  const allowOrigin = origin && origin.trim().length > 0 ? origin : "*";
  return {
    "access-control-allow-origin": allowOrigin,
    "access-control-allow-methods": "GET,POST,PUT,DELETE,OPTIONS",
    "access-control-allow-headers": "authorization,content-type,accept",
    "access-control-max-age": "86400",
    vary: "Origin"
  };
}

export function json(data: unknown, init?: ResponseInit): Response {
  return new Response(JSON.stringify(data, null, 2), {
    ...init,
    headers: {
      "content-type": "application/json; charset=utf-8",
      ...(init?.headers ?? {})
    }
  });
}

export function empty(status = 204): Response {
  return new Response(null, { status });
}

export function problem(
  status: number,
  code: string,
  title: string,
  detail: string,
  type = `https://boardtpl.dev/problems/${code}`
): ApiError {
  return new ApiError(status, {
    type,
    title,
    status,
    detail,
    code
  });
}

export function validationProblem(errors: Record<string, string[]>, title = "One or more validation errors occurred."): ApiError {
  return new ApiError(422, {
    title,
    status: 422,
    errors
  });
}
