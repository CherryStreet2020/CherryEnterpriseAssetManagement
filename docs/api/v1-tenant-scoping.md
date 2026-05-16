# API v1 Tenant Scoping (PR #101)

## TL;DR

Every `X-API-Key` is now bound to the tenant that issued it, and optionally
to a single company inside that tenant. Requests to `/api/v1/assets*` only
see data inside that boundary. **Existing keys issued before this PR must be
re-issued by an admin before they will work again.**

If you are an integration partner: your code does not change. Continue
sending the same `X-API-Key` header. Response shapes are unchanged.

## What changed and why

Before this PR, the `ApiKey` model had no `TenantId`. The bearer-token check
in `AssetsApiController` returned a valid key object, and then every query
ran as `_context.Assets.AsQueryable()` — with no tenant filter on top. A key
issued in Tenant A could be used to `GET /api/v1/assets` and read every
asset in every tenant on the deployment. That is a cross-tenant data leak.

PR #101 closes the leak by adding two columns to `ApiKeys`:

| Column      | Type    | Nullable | Purpose                                                              |
|-------------|---------|----------|----------------------------------------------------------------------|
| `TenantId`  | int     | NOT NULL | The tenant that issued this key. `0` is the legacy sentinel.         |
| `CompanyId` | int     | NULL     | Optional narrower scope. `NULL` = every company visible to tenant.   |

On every request, `AssetsApiController.RequireApiKeyWithTenantScope()`:

1. Validates the bearer token against `ApiKeys` (unchanged).
2. Refuses any key whose `TenantId == 0` with a `403` (see below).
3. Calls `ITenantContext.SetContext` and `SetHierarchyContext` to bind the
   current request's tenant context to the key's scope.
4. Every data query in the controller filters by
   `_tenantContext.VisibleCompanyIds` — when the key has a `CompanyId`, that
   is a list of one; when it does not, that is all companies whose
   `Company.TenantId` matches the key's `TenantId`.

The signature of `ApiService.CreateApiKeyAsync` is a breaking change inside
the application: callers must now pass the issuing admin's `TenantId` and
may pass an optional `CompanyId`. There is no fallback — if a call site
cannot determine which tenant to bind, it should not be issuing a key.

## What happens to existing keys

Existing rows in `ApiKeys` are backfilled with `TenantId = 0`. They still
authenticate (`ApiService.ValidateKeyAsync` returns them), but the controller
gate then rejects them with HTTP 403 and this message:

> This API key was issued before tenant scoping was enforced. Re-issue the
> key in your admin console — pre-#101 keys do not carry a tenant binding
> and are refused for safety.

The admin UI at `/API` flags these as "Needs reissue" in the Tenant column.
To re-issue: revoke the old key, generate a new one with the same name (and
the desired Company scope, if any), and rotate the new value to the partner.
There is no automated migration — the deliberate friction is the point. The
old key never had a tenant binding, so binding it now would require a
guess, and the safest guess in a multi-tenant system is "refuse."

## Partner impact

Zero, beyond the one-time key rotation:

- Same `X-API-Key` header name.
- Same request shape, same response shape.
- Same authentication failure on a bad key (`401`); plus the new `403` for
  un-rotated legacy keys.
- Asset writes (`POST /api/v1/assets`) now stamp the new asset with the
  key's `CompanyId`, when set. Previously they did not stamp company at
  all, which left orphaned rows visible across tenants.

A future PR (#120) will add database-level row-level security on top of
this application-level scoping. That work is out of scope here; this PR
closes the breach without waiting on RLS.
