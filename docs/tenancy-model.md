# Multi-Tenant Architecture Model

## 1. Overview

Manufacturing Core Suite is a multi-tenant SaaS platform designed to host multiple
independent industrial operator organizations on a shared application infrastructure
while enforcing strict data isolation, per-tenant configuration boundaries, and
independent lifecycle management.

Each tenant represents a single subscribing organization. Tenants may represent
a single plant site, a multi-site enterprise division, or an isolated demo
environment. All tenant data, configuration, and state are scoped and isolated
at the data layer. No tenant can read or write data belonging to another tenant
under any application code path.

---

## 2. Tenant Isolation Strategy

### 2.1 Global Query Filter Enforcement

Tenant isolation at the data layer is enforced using **EF Core global query filters**.
Every entity that carries operational data implements the `ITenantScoped` interface,
which exposes a `TenantId` property of type `Guid`. The application `DbContext`
applies a global query filter to every `ITenantScoped` entity type at model
configuration time:

```
entity.HasQueryFilter(e => e.TenantId == _tenantProvider.TenantId)
```

This filter is applied automatically to all LINQ queries issued through EF Core.
It is not the responsibility of individual controllers, services, or repositories
to apply tenant filtering manually. The filter is structural and cannot be silently
omitted through ordinary application code paths.

Explicit filter suppression via `IgnoreQueryFilters()` is restricted to
infrastructure-level services such as tenant provisioning, system health checks,
and platform administration. It is never used in application-facing request handlers.

### 2.2 Database Separation

Manufacturing Core Suite uses a two-database architecture:

**Directory Database**
The directory database is a platform-level shared database containing:

- Tenant registration records
- Tenant lifecycle state
- Subscription records and tier assignments
- User-to-tenant membership and role assignments
- Tenant invitation records
- Provisioning audit log

The directory database is accessed only by infrastructure-level services including
the tenant provisioning pipeline, the platform administration control plane, and
identity middleware. Application-level request handlers do not access the directory
database directly.

**Operational Database**
The operational database is the primary application database containing all
tenant-owned operational data:

- Maintenance requests and work orders
- Equipment and asset records
- Parts, BOM structures, and inventory
- Production records and work center data
- AI conversation sessions and model audit logs
- Workforce and user context data

All records in the operational database carry a `TenantId` column. EF Core global
query filters ensure all read and write operations are scoped to the active tenant.

---

## 3. Tenant Context Flow

### 3.1 Middleware Pipeline

Tenant context is resolved once per HTTP request and injected into a scoped
`ITenantContext` service that is available throughout the request lifetime.

The middleware pipeline resolves tenant context in the following order:

```
HTTP Request
  │
  ├── Authentication Middleware         (resolves authenticated user identity)
  │
  ├── TenantContextMiddleware           (reads TenantId claim from authenticated principal)
  │       │
  │       └── Resolves TenantId → registers ITenantContext in DI scope
  │
  ├── ProvisioningStatusGateMiddleware  (checks tenant lifecycle state)
  │       │
  │       └── Blocks requests if tenant is not in Ready state
  │
  └── Application Request Handler
```

### 3.2 TenantId Claim Propagation

When a user authenticates, the `TenantClaimsTransformation` service enriches
the claims principal with the user's associated `TenantId`. This claim is read
by `TenantContextMiddleware` on every subsequent request to populate the scoped
`ITenantContext`.

The `TenantContext` service exposes the resolved `TenantId` to EF Core, service
layer classes, and any component that needs to scope its operations to the
current tenant.

### 3.3 Provisioning Gate

`ProvisioningStatusGateMiddleware` intercepts all non-administrative requests
and verifies that the current tenant's lifecycle state permits normal application
access. Requests from tenants in `Pending`, `Provisioning`, `Suspended`, or
`Failed` states are redirected to an appropriate status page rather than
reaching application controllers.

---

## 4. Tenant Lifecycle

### 4.1 Lifecycle States

Tenant lifecycle is governed by a `ProvisioningStatus` enumeration with the
following states:

```
Pending
  │
  └──► Provisioning
          │
          ├──► Ready          ← normal operational state
          │       │
          │       └──► Suspended
          │               │
          │               └──► Ready (reactivated)
          │
          └──► Failed
```

| State | Description |
|---|---|
| **Pending** | Tenant record created; provisioning not yet started |
| **Provisioning** | Operational database schema and seed data being initialized |
| **Ready** | Tenant is fully provisioned and operational |
| **Suspended** | Tenant access temporarily disabled; data retained |
| **Failed** | Provisioning encountered a fatal error; manual intervention required |

### 4.2 Provisioning Sequence

When a new tenant subscription is registered, the provisioning pipeline executes
the following sequence:

```
1. Tenant record created in directory database (state: Pending)
2. Provisioning job initiated (state: Provisioning)
3. Operational database schema applied via EF Core migrations
4. Seed data initialized: default roles, master data, configuration defaults
5. Provisioning audit event recorded
6. Tenant state updated to Ready
7. Welcome notification dispatched to tenant administrator
```

If any step in the provisioning sequence fails, the tenant state transitions to
`Failed` and a provisioning audit event records the failure context. The platform
administrator control plane exposes tools to inspect failed tenants and retry
provisioning where safe to do so.

### 4.3 Suspension and Reactivation

Tenant suspension is initiated by the platform administration control plane.
Suspension preserves all tenant data and does not remove records from the
operational database. Suspended tenants are blocked at the provisioning gate
middleware and cannot access application functionality.

Reactivation returns the tenant to `Ready` state. No data migration or
re-provisioning is required for reactivation unless schema changes occurred
during the suspension period.

### 4.4 Demo-Mode Tenant Lifecycle

Demo tenants are short-lived tenant instances provisioned automatically for
evaluation purposes. Demo tenants follow the same provisioning lifecycle as
production tenants with the following additional behaviors:

- Demo seed data is applied during provisioning, populating representative
  operational records for evaluation purposes
- Demo tenants may be provisioned with a preconfigured role context allowing
  evaluators to switch between operator, maintainer, and supervisor perspectives
  within a single session
- Demo tenant sessions are optionally time-bounded with configurable expiry
- Demo tenant records are independently identifiable in the directory database
  and can be purged independently of production tenant data

---

## 5. Per-Tenant Configuration Boundaries

Each tenant maintains independent configuration for the following properties:

- **Subscription tier** — determines which feature sets and resource limits apply
- **Default locale and language** — controls localized UI resource resolution
- **Feature flags** — enable or disable specific platform capabilities per tenant
- **AI model configuration** — endpoint, model selection, and inference parameters
- **Notification routing** — email and in-application notification preferences
- **Master data** — work centers, areas, sites, equipment categories, and location
  structures are tenant-owned and not shared across tenants

Configuration boundaries are enforced at the service layer. No tenant configuration
value is readable or writable by any other tenant's request context.

---

## 6. Subscription Lifecycle

Tenant subscriptions are associated with a billing tier and a cadence. The subscription
record in the directory database governs:

- which tier-gated features the tenant may access
- whether the tenant subscription is currently active
- the subscription renewal or expiry date where applicable

The `ITierProvider` service resolves the current tenant's active subscription tier
and exposes it to feature gate logic throughout the application. Tier resolution
is cached within the request scope and does not issue a directory database query
on every feature check.

When a subscription lapses, the platform may transition the tenant to a `Suspended`
state pending renewal, preserving all operational data for the configured grace period.

---

## 7. Operator Control Plane

The platform administration control plane is a restricted area of the application
accessible only to users holding the platform administrator role. It exposes:

- Tenant registry listing with lifecycle state visibility
- Tenant health summary (provisioning state, subscription tier, active user count)
- Provisioning history and audit log per tenant
- Tenant suspension and reactivation controls
- Failed provisioning inspection and retry tooling
- User invitation management across tenants

The control plane accesses the directory database directly and may use
`IgnoreQueryFilters()` on the operational database for cross-tenant health
monitoring queries. All control plane operations are audited.

---

## 8. Design Constraints and Invariants

The following constraints are enforced by design and must not be circumvented:

- `TenantId` filtering is applied at the `DbContext` layer, not the controller layer
- No application controller or service may accept a `TenantId` parameter from
  an HTTP request payload and use it to cross tenant boundaries
- The directory database and operational database are accessed through separate
  `DbContext` instances registered with separate connection strings
- Demo tenant records are structurally identical to production tenant records
  and are distinguished only by metadata, not by separate code paths
- Tenant provisioning state transitions are recorded in the audit log and cannot
  be modified directly through the application data layer
