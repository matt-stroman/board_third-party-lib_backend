import { beforeEach, describe, expect, it, vi } from "vitest";
import { createClient } from "@supabase/supabase-js";
import { canViewerAccessTitleReportMessageAudience, WorkerAppService } from "./service-boundary";

type MarketingContactRow = {
  id: string;
  email: string;
  normalized_email: string;
  first_name: string | null;
  status: "subscribed";
  lifecycle_status: "waitlisted" | "invited" | "converted";
  consented_at: string;
  consent_text_version: string;
  source: string;
  utm_source: string | null;
  utm_medium: string | null;
  utm_campaign: string | null;
  utm_term: string | null;
  utm_content: string | null;
  brevo_contact_id: string | null;
  brevo_sync_state: "pending" | "synced" | "skipped" | "failed";
  brevo_synced_at: string | null;
  brevo_last_error: string | null;
  converted_app_user_id: string | null;
  created_at: string;
  updated_at: string;
};

type RoleInterestRow = {
  marketing_contact_id: string;
  role: "player" | "developer";
  created_at: string;
};

type AppUserRow = {
  id: string;
  auth_user_id: string;
  user_name: string;
  display_name: string | null;
  first_name: string | null;
  last_name: string | null;
  email: string | null;
  email_verified: boolean;
  identity_provider: string | null;
  avatar_url: string | null;
  avatar_storage_path?: string | null;
  updated_at: string;
};

type AppUserRoleRow = {
  user_id: string;
  role: "player" | "developer" | "verified_developer" | "moderator" | "admin" | "super_admin";
};

type TitleRow = {
  id: string;
  lifecycle_status: "draft" | "active" | "archived";
  visibility: "unlisted" | "listed";
  updated_at: string;
};

const tables: {
  marketing_contacts: MarketingContactRow[];
  marketing_contact_role_interests: RoleInterestRow[];
  app_users: AppUserRow[];
  app_user_roles: AppUserRoleRow[];
  titles: TitleRow[];
} = {
  marketing_contacts: [],
  marketing_contact_role_interests: [],
  app_users: [],
  app_user_roles: [],
  titles: [],
};

function resetTables() {
  tables.marketing_contacts = [];
  tables.marketing_contact_role_interests = [];
  tables.app_users = [];
  tables.app_user_roles = [];
  tables.titles = [];
}

const supabaseAuthMocks = vi.hoisted(() => ({
  getUser: vi.fn(),
  updateUserById: vi.fn(),
  listUsers: vi.fn(),
  signInWithPassword: vi.fn(),
}));

function createQueryBuilder(tableName: keyof typeof tables) {
  let filters: Array<{ column: string; value: unknown }> = [];
  let pendingUpdate: Record<string, unknown> | null = null;

  const applyFilters = <TRow extends Record<string, unknown>>(rows: TRow[]) =>
    rows.filter((row) => filters.every((filter) => row[filter.column] === filter.value));

  const builder = {
    select(_columns?: string) {
      return builder;
    },
    then(onFulfilled: (value: { data: Array<Record<string, unknown>>; error: null }) => unknown, onRejected?: (reason: unknown) => unknown) {
      return Promise.resolve({
        data: applyFilters(tables[tableName] as Array<Record<string, unknown>>),
        error: null,
      }).then(onFulfilled, onRejected);
    },
    limit(count: number) {
      return Promise.resolve({
        data: applyFilters(tables[tableName] as Array<Record<string, unknown>>).slice(0, count),
        error: null,
      });
    },
    upsert(payload: Record<string, unknown>, options?: { onConflict?: string }) {
      const rows = tables[tableName] as Array<Record<string, unknown>>;
      const conflictColumn = options?.onConflict ?? "id";
      const match = rows.find((row) => row[conflictColumn] === payload[conflictColumn]);
      if (match) {
        Object.assign(match, payload);
      } else {
        rows.push({ ...payload } as never);
      }
      return builder;
    },
    single() {
      const rows = applyFilters(tables[tableName] as Array<Record<string, unknown>>);
      return Promise.resolve({
        data: (rows[0] ?? (tables[tableName] as Array<Record<string, unknown>>).slice(-1)[0] ?? null),
        error: null,
      });
    },
    eq(column: string, value: unknown) {
      filters = [...filters, { column, value }];
      if (pendingUpdate) {
        for (const row of applyFilters(tables[tableName] as Array<Record<string, unknown>>)) {
          Object.assign(row, pendingUpdate);
        }

        return Promise.resolve({ error: null });
      }

      return builder;
    },
    insert(payload: Array<Record<string, unknown>> | Record<string, unknown>) {
      const rows = Array.isArray(payload) ? payload : [payload];
      (tables[tableName] as Array<Record<string, unknown>>).push(...rows.map((row) => ({ ...row })));
      return builder;
    },
    delete() {
      return {
        eq(column: string, value: unknown) {
          const rows = tables[tableName] as Array<Record<string, unknown>>;
          const kept = rows.filter((row) => row[column] !== value);
          rows.splice(0, rows.length, ...kept);
          return Promise.resolve({ error: null });
        },
      };
    },
    update(payload: Record<string, unknown>) {
      pendingUpdate = payload;
      return builder;
    },
  };

  return builder;
}

vi.mock("@supabase/supabase-js", () => ({
  createClient: vi.fn(() => ({
    from(tableName: keyof typeof tables) {
      return createQueryBuilder(tableName);
    },
    auth: {
      getUser: supabaseAuthMocks.getUser,
      signInWithPassword: supabaseAuthMocks.signInWithPassword,
      admin: {
        updateUserById: supabaseAuthMocks.updateUserById,
        listUsers: supabaseAuthMocks.listUsers,
      },
    },
  })),
}));

describe("WorkerAppService.createMarketingSignup", () => {
  beforeEach(() => {
    resetTables();
    vi.restoreAllMocks();
    supabaseAuthMocks.getUser.mockReset();
    supabaseAuthMocks.updateUserById.mockReset();
    supabaseAuthMocks.listUsers.mockReset();
    supabaseAuthMocks.signInWithPassword.mockReset();
  });

  it("stores waitlisted lifecycle state, role interests, and Brevo attributes for a new signup", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ success: true }),
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ id: 42 }),
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ messageId: "welcome-1" }),
      });
    vi.stubGlobal("fetch", fetchMock);

    const service = new WorkerAppService({
      APP_ENV: "staging",
      SUPABASE_URL: "https://example.supabase.co",
      SUPABASE_PUBLISHABLE_KEY: "publishable-key",
      SUPABASE_SECRET_KEY: "secret-key",
      TURNSTILE_SECRET_KEY: "turnstile-secret",
      BREVO_API_KEY: "brevo-api-key",
      BREVO_SIGNUPS_LIST_ID: "12",
    });

    const response = await service.createMarketingSignup({
      email: "Taylor@example.com",
      firstName: "Taylor",
      source: "landing_page",
      consentTextVersion: "landing-page-v1",
      turnstileToken: "turnstile-token",
      roleInterests: ["player", "developer", "player"],
    });

    expect(response.duplicate).toBe(false);
    expect(response.signup.lifecycleStatus).toBe("waitlisted");
    expect(response.signup.roleInterests).toEqual(["developer", "player"]);
    expect(tables.marketing_contacts[0]).toMatchObject({
      normalized_email: "taylor@example.com",
      lifecycle_status: "waitlisted",
      source: "landing_page",
    });
    expect(tables.marketing_contact_role_interests).toEqual([
      expect.objectContaining({ role: "developer" }),
      expect.objectContaining({ role: "player" }),
    ]);
    expect(fetchMock).toHaveBeenNthCalledWith(
      2,
      "https://api.brevo.com/v3/contacts",
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({
          email: "Taylor@example.com",
          attributes: {
            FIRSTNAME: "Taylor",
            SOURCE: "landing_page",
            BE_LIFECYCLE_STATUS: "waitlisted",
            BE_ROLE_INTEREST: "developer,player",
          },
          listIds: [12],
          updateEnabled: true,
          emailBlacklisted: false,
        }),
      }),
    );
    expect(fetchMock).toHaveBeenNthCalledWith(
      3,
      "https://api.brevo.com/v3/smtp/email",
      expect.objectContaining({
        method: "POST",
        body: expect.stringContaining("\"subject\":\"You're on the BE list!\""),
      }),
    );
    expect(fetchMock.mock.calls[2]?.[1]).toMatchObject({
      method: "POST",
    });
    expect(String(fetchMock.mock.calls[2]?.[1]?.body)).toContain("\"name\":\"Taylor\"");
    expect(String(fetchMock.mock.calls[2]?.[1]?.body)).toContain("\"htmlContent\":\"<!DOCTYPE html>");
    expect(String(fetchMock.mock.calls[2]?.[1]?.body)).toContain("creating third-party content for Board, following new Board games and apps");
    expect(String(fetchMock.mock.calls[2]?.[1]?.body)).toContain("For Board Players and Builders");
  });

  it("treats unchecked role interests as none for Brevo and an empty application record", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ success: true }),
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ id: 99 }),
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ messageId: "welcome-2" }),
      });
    vi.stubGlobal("fetch", fetchMock);

    const service = new WorkerAppService({
      APP_ENV: "staging",
      SUPABASE_URL: "https://example.supabase.co",
      SUPABASE_PUBLISHABLE_KEY: "publishable-key",
      SUPABASE_SECRET_KEY: "secret-key",
      TURNSTILE_SECRET_KEY: "turnstile-secret",
      BREVO_API_KEY: "brevo-api-key",
      BREVO_SIGNUPS_LIST_ID: "12",
    });

    const response = await service.createMarketingSignup({
      email: "hello@example.com",
      source: "landing_page",
      consentTextVersion: "landing-page-v1",
      turnstileToken: "turnstile-token",
      roleInterests: [],
    });

    expect(response.signup.roleInterests).toEqual([]);
    expect(tables.marketing_contact_role_interests).toEqual([]);
    expect(fetchMock).toHaveBeenNthCalledWith(
      2,
      "https://api.brevo.com/v3/contacts",
      expect.objectContaining({
        body: expect.stringContaining("\"BE_ROLE_INTEREST\":\"none\""),
      }),
    );
    expect(fetchMock).toHaveBeenNthCalledWith(
      3,
      "https://api.brevo.com/v3/smtp/email",
      expect.objectContaining({
        body: expect.stringContaining("\"subject\":\"You're on the BE list!\""),
      }),
    );
    expect(String(fetchMock.mock.calls[2]?.[1]?.body)).toContain("\"name\":\"Interested\"");
    expect(String(fetchMock.mock.calls[2]?.[1]?.body)).not.toContain("developer");
    expect(String(fetchMock.mock.calls[2]?.[1]?.body)).not.toContain("player");
  });

  it("waits for the welcome email delivery attempt before resolving a new signup", async () => {
    let resolveWelcome:
      | ((value: { ok: true; json: () => Promise<{ messageId: string }> }) => void)
      | undefined;
    const welcomeDelivery = new Promise<{ ok: true; json: () => Promise<{ messageId: string }> }>((resolve) => {
      resolveWelcome = resolve;
    });

    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ success: true }),
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ id: 123 }),
      })
      .mockImplementationOnce(() => welcomeDelivery);
    vi.stubGlobal("fetch", fetchMock);

    const service = new WorkerAppService({
      APP_ENV: "production",
      SUPABASE_URL: "https://example.supabase.co",
      SUPABASE_PUBLISHABLE_KEY: "publishable-key",
      SUPABASE_SECRET_KEY: "secret-key",
      TURNSTILE_SECRET_KEY: "turnstile-secret",
      BREVO_API_KEY: "brevo-api-key",
      BREVO_SIGNUPS_LIST_ID: "12",
    });

    let settled = false;
    const responsePromise = service.createMarketingSignup({
      email: "new-welcome@example.com",
      firstName: "Welcome",
      source: "landing_page",
      consentTextVersion: "landing-page-v1",
      turnstileToken: "turnstile-token",
      roleInterests: ["player"],
    }).then((result) => {
      settled = true;
      return result;
    });

    await Promise.resolve();
    expect(settled).toBe(false);

    resolveWelcome?.({
      ok: true,
      json: async () => ({ messageId: "welcome-awaited" }),
    });

    await expect(responsePromise).resolves.toMatchObject({
      accepted: true,
      duplicate: false,
    });
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });

  it("can bypass turnstile verification for deploy smoke signups while still syncing Brevo", async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce({
      ok: true,
      json: async () => ({ id: 314 }),
    });
    vi.stubGlobal("fetch", fetchMock);

    const service = new WorkerAppService({
      APP_ENV: "staging",
      SUPABASE_URL: "https://example.supabase.co",
      SUPABASE_PUBLISHABLE_KEY: "publishable-key",
      SUPABASE_SECRET_KEY: "secret-key",
      BREVO_API_KEY: "brevo-api-key",
      BREVO_SIGNUPS_LIST_ID: "12",
      DEPLOY_SMOKE_SECRET: "smoke-secret",
    });

    const response = await service.createMarketingSignup(
      {
        email: "smoke@example.com",
        firstName: "Smoke",
        source: "landing_page",
        consentTextVersion: "landing-page-v1",
        turnstileToken: null,
        roleInterests: ["player"],
      },
      { bypassTurnstile: true },
    );

    expect(response.accepted).toBe(true);
    expect(response.signup.roleInterests).toEqual(["player"]);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock).toHaveBeenCalledWith(
      "https://api.brevo.com/v3/contacts",
      expect.objectContaining({
        method: "POST",
      }),
    );
  });

  it("does not send the welcome email again for duplicate signups", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ success: true }),
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ id: 77 }),
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ messageId: "welcome-repeat" }),
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ success: true }),
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ id: 77 }),
      });
    vi.stubGlobal("fetch", fetchMock);

    const service = new WorkerAppService({
      APP_ENV: "staging",
      SUPABASE_URL: "https://example.supabase.co",
      SUPABASE_PUBLISHABLE_KEY: "publishable-key",
      SUPABASE_SECRET_KEY: "secret-key",
      TURNSTILE_SECRET_KEY: "turnstile-secret",
      BREVO_API_KEY: "brevo-api-key",
      BREVO_SIGNUPS_LIST_ID: "12",
    });

    await service.createMarketingSignup({
      email: "repeat@example.com",
      firstName: "Repeat",
      source: "landing_page",
      consentTextVersion: "landing-page-v1",
      turnstileToken: "turnstile-token",
      roleInterests: ["player"],
    });

    await service.createMarketingSignup({
      email: "repeat@example.com",
      firstName: "Repeat",
      source: "landing_page",
      consentTextVersion: "landing-page-v1",
      turnstileToken: "turnstile-token",
      roleInterests: ["player"],
    });

    expect(fetchMock).toHaveBeenCalledTimes(5);
    expect(fetchMock).toHaveBeenNthCalledWith(3, "https://api.brevo.com/v3/smtp/email", expect.any(Object));
    expect(fetchMock).not.toHaveBeenNthCalledWith(5, "https://api.brevo.com/v3/smtp/email", expect.any(Object));
  });

  it("suppresses deploy-smoke support emails in staging", async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    const service = new WorkerAppService({
      APP_ENV: "staging",
      SUPABASE_URL: "https://example.supabase.co",
      SUPABASE_PUBLISHABLE_KEY: "publishable-key",
      SUPABASE_SECRET_KEY: "secret-key",
      BREVO_API_KEY: "brevo-api-key",
      BREVO_SIGNUPS_LIST_ID: "12",
      SUPPORT_REPORT_RECIPIENT: "support@boardenthusiasts.com",
      SUPPORT_REPORT_SENDER_EMAIL: "noreply@boardenthusiasts.com",
      SUPPORT_REPORT_SENDER_NAME: "Board Enthusiasts",
    });

    await expect(
      service.reportSupportIssue(
        {
          category: "email_signup",
          firstName: "Deploy Smoke",
          email: "deploy-smoke@example.com",
          pageUrl: "https://staging.boardenthusiasts.com",
          apiBaseUrl: "https://api.staging.boardenthusiasts.com",
          occurredAt: "2026-03-14T07:49:44.445613+00:00",
          errorMessage: "Post-deploy smoke validation",
          technicalDetails: "Automated deploy smoke verification",
          userAgent: "board-enthusiasts-dev-cli",
        },
        { isDeploySmoke: true },
      ),
    ).resolves.toEqual({ accepted: true });

    expect(fetchMock).not.toHaveBeenCalled();
  });
});

describe("WorkerAppService.getCurrentUserResponse", () => {
  beforeEach(() => {
    resetTables();
    vi.restoreAllMocks();
    supabaseAuthMocks.getUser.mockReset();
    supabaseAuthMocks.updateUserById.mockReset();
    supabaseAuthMocks.listUsers.mockReset();
    supabaseAuthMocks.signInWithPassword.mockReset();
  });

  it("includes the current user's avatar URL when present", async () => {
    const service = new WorkerAppService({
      APP_ENV: "staging",
      SUPABASE_URL: "https://example.supabase.co",
      SUPABASE_PUBLISHABLE_KEY: "publishable-key",
      SUPABASE_SECRET_KEY: "secret-key",
    });

    vi.spyOn(service as never, "requireUser" as never).mockResolvedValue({
      appUser: {
        id: "user-1",
        auth_user_id: "auth-user-1",
        user_name: "ava.garcia",
        display_name: "Ava Garcia",
        first_name: "Ava",
        last_name: "Garcia",
        email: "ava@example.com",
        email_verified: true,
        identity_provider: "email",
        avatar_url: "https://cdn.example.com/avatars/ava.png",
      },
      roles: ["player"],
    });

    await expect(service.getCurrentUserResponse("test-token")).resolves.toEqual({
      subject: "auth-user-1",
      displayName: "Ava Garcia",
      email: "ava@example.com",
      emailVerified: true,
      identityProvider: "email",
      roles: ["player"],
      avatarUrl: "https://cdn.example.com/avatars/ava.png",
    });
  });

  it("syncs auth-owned email fields into the projected profile for existing users", async () => {
    tables.app_users.push({
      id: "user-1",
      auth_user_id: "auth-user-1",
      user_name: "ava.garcia",
      display_name: "Ava Garcia",
      first_name: "Ava",
      last_name: "Garcia",
      email: "old@example.com",
      email_verified: true,
      identity_provider: "email",
      avatar_url: null,
      avatar_storage_path: null,
      updated_at: "2026-03-01T00:00:00Z",
    });

    supabaseAuthMocks.getUser.mockResolvedValue({
      data: {
        user: {
          id: "auth-user-1",
          email: "new@example.com",
          email_confirmed_at: null,
          app_metadata: { provider: "email" },
          user_metadata: {},
          identities: [{ provider: "email" }],
        },
      },
      error: null,
    });

    const service = new WorkerAppService({
      APP_ENV: "staging",
      SUPABASE_URL: "https://example.supabase.co",
      SUPABASE_PUBLISHABLE_KEY: "publishable-key",
      SUPABASE_SECRET_KEY: "secret-key",
    });

    await expect(service.getCurrentUserProfile("test-token")).resolves.toMatchObject({
      profile: {
        subject: "auth-user-1",
        email: "new@example.com",
        emailVerified: false,
      },
    });

    expect(tables.app_users[0]).toMatchObject({
      email: "new@example.com",
      email_verified: false,
    });
  });

  it("leaves first and last name blank when oauth metadata only provides a full name", async () => {
    supabaseAuthMocks.getUser.mockResolvedValue({
      data: {
        user: {
          id: "auth-user-1",
          email: "matt@example.com",
          email_confirmed_at: "2026-04-01T00:00:00Z",
          app_metadata: { provider: "github" },
          user_metadata: {
            full_name: "Matt Stroman",
          },
          identities: [{ provider: "github" }],
        },
      },
      error: null,
    });

    const service = new WorkerAppService({
      APP_ENV: "staging",
      SUPABASE_URL: "https://example.supabase.co",
      SUPABASE_PUBLISHABLE_KEY: "publishable-key",
      SUPABASE_SECRET_KEY: "secret-key",
    });

    await expect(service.getCurrentUserProfile("test-token")).resolves.toMatchObject({
      profile: {
        subject: "auth-user-1",
        displayName: "Matt Stroman",
        firstName: null,
        lastName: null,
        email: "matt@example.com",
        emailVerified: true,
      },
    });

    expect(tables.app_users[0]).toMatchObject({
      auth_user_id: "auth-user-1",
      display_name: "Matt Stroman",
      first_name: null,
      last_name: null,
      email: "matt@example.com",
      email_verified: true,
      identity_provider: "github",
    });
  });

  it("captures an oauth avatar URL from provider metadata when creating the projected user", async () => {
    supabaseAuthMocks.getUser.mockResolvedValue({
      data: {
        user: {
          id: "auth-user-1",
          email: "matt@example.com",
          email_confirmed_at: "2026-04-01T00:00:00Z",
          app_metadata: { provider: "discord" },
          user_metadata: {
            full_name: "Matt Stroman",
            avatar_url: "https://cdn.discordapp.com/avatars/123/avatar.png",
          },
          identities: [{ provider: "discord" }],
        },
      },
      error: null,
    });

    const service = new WorkerAppService({
      APP_ENV: "staging",
      SUPABASE_URL: "https://example.supabase.co",
      SUPABASE_PUBLISHABLE_KEY: "publishable-key",
      SUPABASE_SECRET_KEY: "secret-key",
    });

    await expect(service.getCurrentUserProfile("test-token")).resolves.toMatchObject({
      profile: {
        subject: "auth-user-1",
        avatarUrl: "https://cdn.discordapp.com/avatars/123/avatar.png",
      },
    });

    expect(tables.app_users[0]).toMatchObject({
      auth_user_id: "auth-user-1",
      avatar_url: "https://cdn.discordapp.com/avatars/123/avatar.png",
      identity_provider: "discord",
    });
  });

  it("fills in a provider avatar for existing users when the local profile does not have one", async () => {
    tables.app_users.push({
      id: "user-1",
      auth_user_id: "auth-user-1",
      user_name: "ava.garcia",
      display_name: "Ava Garcia",
      first_name: "Ava",
      last_name: "Garcia",
      email: "ava@example.com",
      email_verified: true,
      identity_provider: "discord",
      avatar_url: null,
      avatar_storage_path: null,
      updated_at: "2026-03-01T00:00:00Z",
    });

    supabaseAuthMocks.getUser.mockResolvedValue({
      data: {
        user: {
          id: "auth-user-1",
          email: "ava@example.com",
          email_confirmed_at: "2026-04-01T00:00:00Z",
          app_metadata: { provider: "discord" },
          user_metadata: {
            avatar_url: "https://cdn.discordapp.com/avatars/456/avatar.png",
          },
          identities: [{ provider: "discord" }],
        },
      },
      error: null,
    });

    const service = new WorkerAppService({
      APP_ENV: "staging",
      SUPABASE_URL: "https://example.supabase.co",
      SUPABASE_PUBLISHABLE_KEY: "publishable-key",
      SUPABASE_SECRET_KEY: "secret-key",
    });

    await expect(service.getCurrentUserProfile("test-token")).resolves.toMatchObject({
      profile: {
        subject: "auth-user-1",
        avatarUrl: "https://cdn.discordapp.com/avatars/456/avatar.png",
      },
    });

    expect(tables.app_users[0]).toMatchObject({
      avatar_url: "https://cdn.discordapp.com/avatars/456/avatar.png",
      avatar_storage_path: null,
    });
  });
});

describe("WorkerAppService.verifyCurrentUserPassword", () => {
  beforeEach(() => {
    resetTables();
    vi.restoreAllMocks();
    supabaseAuthMocks.getUser.mockReset();
    supabaseAuthMocks.updateUserById.mockReset();
    supabaseAuthMocks.listUsers.mockReset();
    supabaseAuthMocks.signInWithPassword.mockReset();
  });

  it("verifies the current password for the signed-in user", async () => {
    const service = new WorkerAppService({
      APP_ENV: "staging",
      SUPABASE_URL: "https://example.supabase.co",
      SUPABASE_PUBLISHABLE_KEY: "publishable-key",
      SUPABASE_SECRET_KEY: "secret-key",
    });

    vi.spyOn(service as never, "requireUser" as never).mockResolvedValue({
      appUser: {
        id: "user-1",
        auth_user_id: "auth-user-1",
        user_name: "emma.torres",
        display_name: "Emma Torres",
        first_name: "Emma",
        last_name: "Torres",
        email: "emma.torres@boardtpl.local",
        email_verified: true,
        identity_provider: "email",
        avatar_url: null,
      },
      roles: ["developer"],
    });

    supabaseAuthMocks.signInWithPassword.mockResolvedValue({
      data: { user: { id: "auth-user-1" } },
      error: null,
    });

    await expect(service.verifyCurrentUserPassword("test-token", { currentPassword: "Developer!123" })).resolves.toEqual({
      verified: true,
    });

    expect(supabaseAuthMocks.signInWithPassword).toHaveBeenCalledWith({
      email: "emma.torres@boardtpl.local",
      password: "Developer!123",
    });
    expect(createClient).toHaveBeenNthCalledWith(
      2,
      "https://example.supabase.co",
      "publishable-key",
      expect.objectContaining({
        auth: expect.objectContaining({
          autoRefreshToken: false,
          persistSession: false,
        }),
      })
    );
  });
});

describe("WorkerAppService.unarchiveTitle", () => {
  beforeEach(() => {
    resetTables();
    vi.restoreAllMocks();
    supabaseAuthMocks.getUser.mockReset();
    supabaseAuthMocks.updateUserById.mockReset();
    supabaseAuthMocks.listUsers.mockReset();
    supabaseAuthMocks.signInWithPassword.mockReset();
  });

  it("moves an archived title back to draft and keeps it unlisted", async () => {
    const service = new WorkerAppService({
      APP_ENV: "staging",
      SUPABASE_URL: "https://example.supabase.co",
      SUPABASE_PUBLISHABLE_KEY: "publishable-key",
      SUPABASE_SECRET_KEY: "secret-key",
    });

    tables.titles.push({
      id: "title-1",
      lifecycle_status: "archived",
      visibility: "unlisted",
      updated_at: "2026-03-25T00:00:00.000Z",
    });

    vi.spyOn(service as never, "requireUser" as never).mockResolvedValue({
      appUser: {
        id: "user-1",
        auth_user_id: "auth-user-1",
        user_name: "emma.torres",
        display_name: "Emma Torres",
        first_name: "Emma",
        last_name: "Torres",
        email: "emma.torres@boardtpl.local",
        email_verified: true,
        identity_provider: "email",
        avatar_url: null,
      },
      roles: ["developer"],
    });
    vi.spyOn(service as never, "requireDeveloperTitleAccess" as never).mockResolvedValue({
      id: "title-1",
      lifecycle_status: "archived",
      visibility: "unlisted",
    });
    vi.spyOn(service as never, "getDeveloperTitleDetails" as never).mockResolvedValue({
      id: "title-1",
      studioId: "studio-1",
      studioSlug: "blue-harbor-games",
      slug: "lantern-drift",
      displayName: "Lantern Drift",
      shortDescription: "A thoughtful puzzle adventure.",
      description: "A thoughtful puzzle adventure.",
      genreSlugs: ["adventure", "puzzle"],
      contentKind: "game",
      lifecycleStatus: "draft",
      visibility: "unlisted",
      genreDisplay: "Adventure, Puzzle",
      minPlayers: 1,
      maxPlayers: 4,
      ageRatingAuthority: null,
      ageRatingValue: null,
      minAgeYears: null,
      playerCountDisplay: "1-4 players",
      ageDisplay: "Ages 10+",
      currentMetadataRevision: 3,
      acquisitionUrl: null,
      currentRelease: null,
    });

    await expect(service.unarchiveTitle("test-token", "title-1")).resolves.toEqual({
      title: expect.objectContaining({
        id: "title-1",
        lifecycleStatus: "draft",
        visibility: "unlisted",
      }),
    });

    expect(tables.titles[0]).toMatchObject({
      id: "title-1",
      lifecycle_status: "draft",
      visibility: "unlisted",
    });
  });
});

describe("WorkerAppService.deleteTitle", () => {
  beforeEach(() => {
    resetTables();
    vi.restoreAllMocks();
    supabaseAuthMocks.getUser.mockReset();
    supabaseAuthMocks.updateUserById.mockReset();
    supabaseAuthMocks.listUsers.mockReset();
    supabaseAuthMocks.signInWithPassword.mockReset();
  });

  it("removes the title row after password confirmation", async () => {
    const service = new WorkerAppService({
      APP_ENV: "staging",
      SUPABASE_URL: "https://example.supabase.co",
      SUPABASE_PUBLISHABLE_KEY: "publishable-key",
      SUPABASE_SECRET_KEY: "secret-key",
    });

    tables.titles.push({
      id: "title-1",
      lifecycle_status: "draft",
      visibility: "unlisted",
      updated_at: "2026-03-25T00:00:00.000Z",
    });

    vi.spyOn(service as never, "requireUser" as never).mockResolvedValue({
      appUser: {
        id: "user-1",
        auth_user_id: "auth-user-1",
        user_name: "emma.torres",
        display_name: "Emma Torres",
        first_name: "Emma",
        last_name: "Torres",
        email: "emma.torres@boardtpl.local",
        email_verified: true,
        identity_provider: "email",
        avatar_url: null,
      },
      roles: ["developer"],
    });
    vi.spyOn(service as never, "requireDeveloperTitleAccess" as never).mockResolvedValue({
      id: "title-1",
      display_name: "Lantern Drift",
    });
    vi.spyOn(service as never, "verifyCurrentUserPasswordValue" as never).mockResolvedValue(undefined);

    await expect(
      service.deleteTitle("test-token", "title-1", {
        currentPassword: "Developer!123",
        confirmationTitleName: "Lantern Drift",
      }),
    ).resolves.toBeUndefined();

    expect(tables.titles).toHaveLength(0);
  });
});

describe("canViewerAccessTitleReportMessageAudience", () => {
  it("hides developer-only messages from players", () => {
    expect(canViewerAccessTitleReportMessageAudience("developer", "player")).toBe(false);
    expect(canViewerAccessTitleReportMessageAudience("all", "player")).toBe(true);
    expect(canViewerAccessTitleReportMessageAudience("player", "player")).toBe(true);
  });

  it("hides player-only messages from developers", () => {
    expect(canViewerAccessTitleReportMessageAudience("player", "developer")).toBe(false);
    expect(canViewerAccessTitleReportMessageAudience("all", "developer")).toBe(true);
    expect(canViewerAccessTitleReportMessageAudience("developer", "developer")).toBe(true);
  });

  it("keeps moderator visibility across all report audiences", () => {
    expect(canViewerAccessTitleReportMessageAudience("all", "moderator")).toBe(true);
    expect(canViewerAccessTitleReportMessageAudience("player", "moderator")).toBe(true);
    expect(canViewerAccessTitleReportMessageAudience("developer", "moderator")).toBe(true);
    expect(canViewerAccessTitleReportMessageAudience("unexpected", "moderator")).toBe(true);
  });
});

describe("WorkerAppService.getContext", () => {
  it("defaults the typed storage buckets when they are not explicitly configured", () => {
    const service = new WorkerAppService({
      APP_ENV: "local",
      SUPABASE_URL: "https://example.supabase.co",
      SUPABASE_PUBLISHABLE_KEY: "publishable-key",
      SUPABASE_SECRET_KEY: "secret-key",
    });

    expect(service.getContext()).toMatchObject({
      supabaseAvatarsBucket: "avatars",
      supabaseCardImagesBucket: "card-images",
      supabaseHeroImagesBucket: "hero-images",
      supabaseLogoImagesBucket: "logo-images",
      deploySmokeSecret: null,
    });
  });

  it("respects explicit typed storage bucket overrides", () => {
    const service = new WorkerAppService({
      APP_ENV: "staging",
      SUPABASE_URL: "https://example.supabase.co",
      SUPABASE_PUBLISHABLE_KEY: "publishable-key",
      SUPABASE_SECRET_KEY: "secret-key",
      SUPABASE_AVATARS_BUCKET: "custom-avatars",
      SUPABASE_CARD_IMAGES_BUCKET: "custom-card-images",
      SUPABASE_HERO_IMAGES_BUCKET: "custom-hero-images",
      SUPABASE_LOGO_IMAGES_BUCKET: "custom-logo-images",
    });

    expect(service.getContext()).toMatchObject({
      supabaseAvatarsBucket: "custom-avatars",
      supabaseCardImagesBucket: "custom-card-images",
      supabaseHeroImagesBucket: "custom-hero-images",
      supabaseLogoImagesBucket: "custom-logo-images",
    });
  });

  it("normalizes deploy-smoke and integration placeholders as unset values", () => {
    const service = new WorkerAppService({
      APP_ENV: "staging",
      SUPABASE_URL: "https://example.supabase.co",
      SUPABASE_PUBLISHABLE_KEY: "publishable-key",
      SUPABASE_SECRET_KEY: "secret-key",
      BREVO_API_KEY: "optional-for-staging",
      BREVO_SIGNUPS_LIST_ID: "replace-me",
      TURNSTILE_SECRET_KEY: "replace-with-turnstile-secret",
      DEPLOY_SMOKE_SECRET: "replace-me",
    });

    expect(service.getContext()).toMatchObject({
      brevoApiKey: null,
      brevoSignupsListId: null,
      turnstileSecretKey: null,
      deploySmokeSecret: null,
    });
  });
});
