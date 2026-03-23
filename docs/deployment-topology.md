# Deployment Topology Reference Architecture

## 1. Overview

Manufacturing Core Suite is designed to support multiple deployment topologies
spanning fully local single-node installations, cloud-hosted multi-tenant SaaS
deployments, isolated enterprise environments, and edge-connected industrial
configurations. The application runtime is portable across these topologies
without code-level changes. Topology-specific behavior is controlled through
configuration, infrastructure layout, and optional component inclusion.

This document defines the reference topology for each supported deployment model,
describes the runtime components present in each, and specifies the security
boundaries that must be enforced regardless of hosting environment.

---

## 2. Logical Architecture

All deployment topologies share a common logical architecture. The specific
infrastructure instantiation of each layer varies by deployment model.

```
Client Browser / Mobile Client / API Consumer
          │
          ▼
   Reverse Proxy Layer
   (IIS / nginx / Azure Front Door / Application Gateway)
          │
          ▼
   Application Runtime
   (ASP.NET Core on Kestrel)
          │
          ▼
   Service Layer
   (Tenant Services / AI Orchestration / Workflow / Notifications)
          │
          ├──────────────────┬─────────────────────┐
          ▼                  ▼                     ▼
  Operational Database  Identity Database     Directory Database
  (SQL Server)          (ASP.NET Identity)    (Tenant Registry)
          │
          ▼
   Optional AI Runtime
   (Ollama / Local LLM)
```

### Layer Descriptions

**Client Layer**
Web browsers, mobile clients, and API consumers. Communicates with the application
runtime over HTTPS. Real-time updates are delivered through SignalR WebSocket
connections.

**Reverse Proxy Layer**
Handles TLS termination, request routing, static asset delivery, and optionally
WAF filtering. The application runtime is not exposed directly to public internet
traffic in production configurations.

**Application Runtime**
The ASP.NET Core application hosted on Kestrel. Handles all HTTP request
processing, SignalR hub management, authentication, authorization, tenant context
resolution, and service orchestration.

**Service Layer**
The business logic tier within the application runtime. Includes tenant lifecycle
services, AI orchestration, maintenance workflow services, notification dispatch,
and domain-specific service implementations. The service layer is not separately
deployable in the base architecture; it runs within the application process.

**Database Layer**
Three logically separate databases. The operational database stores all tenant
application data. The identity database stores ASP.NET Core Identity records.
The directory database stores platform-level tenant registry, subscription, and
provisioning records. All three may be hosted on the same database server in
lower-scale deployments or on separate servers in production configurations.

**Optional AI Runtime**
A locally-hosted inference runtime such as Ollama, deployed on the application
server or on a dedicated inference host. Required only when local LLM inference
is configured. Not required for deployments using external model APIs.

---

## 3. Deployment Models

### 3.1 Single-Node Pilot Deployment

The single-node deployment hosts all runtime components on one server. This model
is appropriate for initial pilot programs, evaluation environments, and small-scale
industrial site deployments.

```
┌──────────────────────────────────────────────────────────┐
│  Application Server                                      │
│                                                          │
│  ┌──────────────────┐   ┌───────────────────────────┐   │
│  │  IIS / Kestrel   │   │  SQL Server (LocalDB or   │   │
│  │  (ASP.NET Core)  │   │  Express or Standard)     │   │
│  └──────────────────┘   └───────────────────────────┘   │
│                                                          │
│  ┌──────────────────┐                                    │
│  │  Ollama Runtime  │  (optional)                        │
│  │  (local LLM)     │                                    │
│  └──────────────────┘                                    │
└──────────────────────────────────────────────────────────┘
```

**Characteristics:**
- All components on a single host
- IIS or Kestrel serves HTTP/HTTPS directly
- SQL Server co-located with application process
- Local AI inference available without network dependency
- Suitable for single-tenant or small multi-tenant deployments
- No horizontal scaling in this configuration

### 3.2 Cloud-Hosted SaaS Deployment

The cloud-hosted deployment distributes runtime components across managed cloud
services. This is the reference production deployment model for multi-tenant SaaS
operation.

```
┌─────────────────────────────────────────────────────────────────┐
│  Cloud Environment                                              │
│                                                                 │
│  ┌──────────────────────┐                                       │
│  │  Application Gateway │  (TLS termination, WAF, routing)      │
│  │  / Front Door        │                                       │
│  └──────────┬───────────┘                                       │
│             │                                                   │
│  ┌──────────▼───────────┐   ┌────────────────────────────────┐  │
│  │  App Service /       │   │  Azure SQL / Managed SQL       │  │
│  │  Container Instance  │   │  (Operational DB)              │  │
│  │  (ASP.NET Core)      │   │  (Identity DB)                 │  │
│  └──────────────────────┘   │  (Directory DB)                │  │
│                             └────────────────────────────────┘  │
│  ┌──────────────────────┐                                       │
│  │  Blob / File Storage │  (static assets, exports, uploads)    │
│  └──────────────────────┘                                       │
│                                                                 │
│  ┌──────────────────────┐                                       │
│  │  External AI API     │  (optional — if local not used)       │
│  └──────────────────────┘                                       │
└─────────────────────────────────────────────────────────────────┘
```

**Characteristics:**
- Managed compute, database, and storage services
- Horizontal scaling through app service plan or container orchestration
- Managed TLS, patching, and infrastructure availability
- External AI model API used unless a dedicated inference node is provisioned
- Multi-tenant isolation enforced at application and database layers

### 3.3 Isolated Enterprise Deployment

The isolated enterprise deployment hosts the platform within an organization's
own infrastructure — either on-premises datacenter or a private cloud environment
— without dependency on shared SaaS infrastructure.

```
┌─────────────────────────────────────────────────────────────────┐
│  Enterprise Datacenter / Private Cloud                          │
│                                                                 │
│  ┌─────────────────┐   ┌──────────────────────────────────┐    │
│  │  Load Balancer  │   │  SQL Server (Enterprise Edition) │    │
│  │  / Reverse Proxy│   │  (all three databases)           │    │
│  └────────┬────────┘   └──────────────────────────────────┘    │
│           │                                                     │
│  ┌────────▼─────────────────────────────┐                       │
│  │  Application Server Cluster          │                       │
│  │  (ASP.NET Core, IIS, Kestrel)         │                       │
│  └──────────────────────────────────────┘                       │
│                                                                 │
│  ┌──────────────────────┐                                       │
│  │  Local Inference     │  (Ollama on dedicated GPU node)       │
│  │  Runtime             │                                       │
│  └──────────────────────┘                                       │
│                                                                 │
│  ┌──────────────────────┐                                       │
│  │  Identity Provider   │  (Active Directory / LDAP / SAML)     │
│  └──────────────────────┘                                       │
└─────────────────────────────────────────────────────────────────┘
```

**Characteristics:**
- No dependency on shared cloud infrastructure
- All data remains within the organization's network boundary
- Local LLM inference on dedicated hardware for data residency compliance
- Enterprise identity integration via SAML, LDAP, or OIDC
- Suitable for regulated industries or sites with restricted internet access

### 3.4 Edge-Connected Industrial Deployment

The edge-connected deployment extends the standard application topology with
connectivity to plant-floor data sources through an edge gateway layer. This model
supports integration with MQTT brokers, OPC-UA sources, and Unified Namespace
infrastructure.

```
┌─────────────────────────────────────────────────────────────────┐
│  Plant Network                                                  │
│                                                                 │
│  ┌──────────────────────┐   ┌────────────────────────────────┐  │
│  │  PLCs / Sensors /    │──►│  OPC-UA Server / Edge          │  │
│  │  Equipment           │   │  Connectivity Platform         │  │
│  └──────────────────────┘   └───────────────┬────────────────┘  │
│                                             │                   │
│                                 ┌───────────▼──────────────┐    │
│                                 │  MQTT Broker (UNS layer) │    │
│                                 └───────────┬──────────────┘    │
└───────────────────────────────────────────┬─┘───────────────────┘
                                            │
                         ┌──────────────────▼──────────────────┐
                         │  Manufacturing Core Suite            │
                         │  Application Runtime                 │
                         │  (cloud or on-prem)                  │
                         └──────────────────────────────────────┘
```

**Characteristics:**
- MQTT broker provides the Unified Namespace event backbone
- Application runtime subscribes to UNS topics for real-time telemetry and events
- Edge gateway handles OPC-UA protocol translation and normalization
- Application deployment may be cloud-hosted or on-premises; only the MQTT
  subscription path requires network connectivity to the plant environment

---

## 4. Runtime Components Reference

| Component | Role | Required |
|---|---|---|
| ASP.NET Core Application | Primary application runtime serving HTTP, WebSocket, and SignalR | Always |
| Kestrel | Embedded HTTP server within the application process | Always |
| IIS / Reverse Proxy | TLS termination, request routing, static content | Recommended in production |
| SQL Server (Operational DB) | Tenant operational data storage | Always |
| SQL Server (Identity DB) | ASP.NET Identity user and role storage | Always |
| SQL Server (Directory DB) | Tenant registry, subscriptions, provisioning state | Always |
| Blob / File Storage | Static assets, uploaded files, export artifacts | Recommended in production |
| Ollama Runtime | Local LLM inference for AI orchestration | Optional |
| MQTT Broker | Unified Namespace event transport for telemetry integration | Optional |
| Edge Connectivity Platform | OPC-UA to MQTT bridge (Ignition, Kepware, or equivalent) | Optional |

---

## 5. Security Boundaries

### 5.1 Application Runtime Boundary

The application runtime process is the primary trust boundary. All requests
entering the application runtime must be authenticated and authorized before
reaching application controllers or services. Unauthenticated requests are
redirected to the login endpoint.

The application runtime does not trust values supplied in HTTP headers, cookies,
or request bodies that claim to represent tenant identity. Tenant identity is
derived exclusively from authenticated session claims.

### 5.2 Tenant Data Boundary

Tenant data isolation is enforced at the EF Core query layer. The application
runtime physically shares a single SQL Server instance and database among tenants.
Isolation is logical rather than physical. This is a deliberate design choice
consistent with standard multi-tenant SaaS architecture practices.

Physical database-per-tenant isolation is a future deployment option for
enterprise or regulated deployments where logical isolation is not sufficient
to meet compliance requirements.

### 5.3 Identity Services Boundary

The identity database is accessed only through the ASP.NET Core Identity layer.
Direct SQL access to identity tables from application business logic is not
permitted. The directory database is accessed only through designated infrastructure
services and the platform administration control plane.

### 5.4 Operator Control Plane Boundary

The platform administration control plane is accessible only to users holding
the platform administrator role. This role is distinct from tenant administrator
roles and is not grantable by tenant-level administrators. Control plane routes
are separately authorized and are not reachable through normal tenant application
navigation.

### 5.5 AI Runtime Boundary

When a local inference runtime is deployed, it is accessible only from the
application server network. The inference endpoint is not exposed to the public
internet or to plant network segments. When an external AI API is used, all
requests pass through the application service layer; no client-side code
communicates directly with the external model API.

### 5.6 Edge Connectivity Boundary

MQTT broker and edge connectivity infrastructure operate within the plant network
boundary. The application runtime subscribes to the MQTT broker through a defined
broker endpoint. Inbound MQTT traffic from the plant network does not traverse
the application network boundary. The MQTT subscription client within the application
runtime initiates the outbound connection to the broker.

---

## 6. Availability and Resilience Considerations

- The application runtime is stateless with respect to tenant operational data.
  All persistent state resides in the database layer. Horizontal scaling of the
  application tier does not require session affinity beyond SignalR connection
  stickiness, which is handled through a distributed backplane in multi-instance
  deployments.
- Database availability is the primary resilience dependency. Standard SQL Server
  high availability patterns — Always On Availability Groups, Failover Cluster
  Instances, or managed cloud database HA tiers — apply without application-level
  changes.
- Local AI inference runtime failure does not impair core application functionality.
  The `NullChatModel` fallback ensures graceful degradation when the inference
  runtime is unavailable.
- MQTT broker disconnection does not impair maintenance, inventory, or user-facing
  workflow functionality. Telemetry-dependent features degrade gracefully with a
  connectivity status indicator.
