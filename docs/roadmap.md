# Manufacturing Core Suite Platform Roadmap

## Overview

This document describes the phased evolution of Manufacturing Core Suite from its
initial maintenance workflow foundation toward a full Factory OS runtime layer.
Each phase builds on the preceding phases, increasing the platform's portability,
interoperability, automation capability, decision support maturity, and cross-site
scalability.

Phases represent logical capability milestones, not fixed calendar schedules.
Progression through phases may be concurrent in some areas. Individual modules
may reach higher phase maturity before the platform as a whole.

---

## Phase 1 — Maintenance Workflow Platform

**Focus:** Establish the operational foundation for structured maintenance request
management in a multi-tenant SaaS environment.

### Capabilities Delivered

- Multi-tenant ASP.NET Core application with directory and operational database separation
- Tenant provisioning lifecycle with state management (Pending → Provisioning → Ready)
- Emergency maintenance request creation, assignment, and lifecycle tracking
- Real-time maintenance dashboard with SignalR-enabled status updates
- Role-based access control for operators, maintainers, supervisors, and administrators
- Equipment and asset registry with work center hierarchy
- Parts and inventory management with BOM linkage
- Audit logging for maintenance actions and tenant operations
- Localization framework supporting multiple languages
- Demo tenant provisioning with representative seed data
- Deployment-ready on IIS, Azure App Service, and Kestrel

### Architectural Properties Established

- ISA-95 aligned data model (Enterprise → Site → Area → Work Center → Equipment)
- EF Core global query filter tenant isolation
- Middleware pipeline for tenant context resolution and provisioning gate enforcement
- Provider pipeline pattern for extensible service composition (image enrichment,
  notification routing)

### Portability and Scalability

- Single-node and cloud-hosted deployment supported
- Single-tenant and multi-tenant operation from the same codebase
- Stateless application runtime enabling horizontal scale

---

## Phase 2 — Work Order Intelligence Services

**Focus:** Extend the maintenance platform with AI-assisted diagnostics, context-aware
decision support, and structured knowledge retrieval over operational history.

### Capabilities Delivered

- AI orchestration layer with intent routing, context builders, and tool invocation
- Local LLM inference support through Ollama runtime integration
- Hybrid inference model supporting both local and external API-backed models
- Incident summarization and equipment history analysis
- AI-assisted corrective action recommendations grounded in maintenance history
- Vector search over incident embeddings for similar-incident retrieval
- AI conversation session persistence with multi-turn context continuity
- Tool audit logging for all AI-invoked data operations
- Real-time AI response streaming through SignalR
- Configurable model selection and inference parameters per deployment

### Architectural Properties Added

- `IChatModel` abstraction enabling pluggable model backends
- Composable context builder pipeline with token budget management
- `AiToolAudit` infrastructure for AI governance and accountability
- Privacy-safe local inference path requiring no external network dependency

### Portability and Scalability

- AI features available in air-gapped deployments via local inference
- Model backend switchable through configuration without code changes
- AI layer degradation-safe — application fully functional when no model is configured

---

## Phase 3 — Asset Telemetry Integration

**Focus:** Connect Manufacturing Core Suite to plant-floor data sources, enabling
real-time equipment state visibility within maintenance and operational workflows.

### Capabilities Delivered

- MQTT client integration subscribing to plant telemetry topics
- Equipment state change consumption from edge connectivity sources (OPC-UA gateways,
  Ignition, Kepware, or equivalent)
- Real-time equipment status display within maintenance and production dashboards
- Automated maintenance event creation triggered by equipment fault signals
- Telemetry-enriched work order context (temperature, vibration, state at fault time)
- Production count and shift activity ingestion from MQTT topic feeds
- Equipment telemetry history storage for AI context retrieval

### Architectural Properties Added

- MQTT subscription client integrated into application service layer
- Telemetry ingestion pipeline with signal normalization and database persistence
- Event-driven maintenance workflow triggers from equipment signal conditions
- Asset context enriched with real-time and historical telemetry data

### Portability and Scalability

- MQTT integration is optional and additive — core application functions without it
- Broker endpoint configurable per deployment; no code changes required
- Telemetry data stored in tenant-scoped operational database with existing isolation model

---

## Phase 4 — Unified Namespace Adoption

**Focus:** Migrate asset telemetry and operational event architecture to a full
Unified Namespace (UNS) topology, enabling all platform components to subscribe
to a canonical industrial event infrastructure.

### Capabilities Delivered

- Full ISA-95-aligned UNS topic hierarchy for all operational events
- MQTT topic structures representing Enterprise / Site / Area / Work Center /
  Equipment / Signal paths
- Maintenance events (work order created, updated, completed) published to UNS
- Production events, inventory updates, and quality signals published to UNS
- All platform components consuming events exclusively through UNS subscription
  rather than point-to-point API calls
- UNS topic browser for platform administrators to inspect active namespace
- Sparkplug B payload convention adoption (optional) for edge compatibility

### Architectural Properties Added

- UNS as the canonical event backbone replacing direct service-to-service calls
  for operational event notification
- Topic contract as the primary integration surface for external consumers
- Platform event model aligned with i3X-compatible industrial data exchange patterns

### Portability and Scalability

- Applications built against the UNS topic contract can be connected to real
  plant infrastructure by pointing to a production MQTT broker
- No application-level changes required when UNS is backed by a production broker
  rather than a local simulation broker
- Multiple application instances can consume the same UNS events independently

---

## Phase 5 — i3X Interoperability Layer

**Focus:** Expose platform asset data and operational state through standardized
industrial interoperability primitives aligned with i3X architectural principles,
enabling portable application development and cross-platform integration.

### Capabilities Delivered

- .NET minimal API service exposing UNS data through i3X-aligned interaction primitives:
  browse, read, write, subscribe, and discover
- Asset metadata discovery endpoint for hierarchy-aware external consumers
- WebSocket-based subscription API for real-time value change notification
- Batch value read API for context builder and analytics consumer use cases
- Semantic asset model with engineering unit and data type metadata
- Conformance documentation for i3X-compatible ecosystem participation

### Architectural Properties Added

- i3X wrapper service as the external-facing API boundary over UNS data
- Portable application integration contract independent of internal MQTT topology
- Standards-aligned data exchange model enabling third-party application integration

### Portability and Scalability

- Applications targeting the i3X wrapper API are decoupled from internal transport details
- Replacing the simulation environment with production plant data requires only a
  configuration change at the broker connection layer
- Multiple external applications can consume the same i3X API concurrently

---

## Phase 6 — Digital Twin Coordination Layer

**Focus:** Introduce persistent, stateful digital representations of physical assets
that aggregate telemetry, maintenance history, AI-generated insights, and configuration
into a structured asset model accessible across all platform domains.

### Capabilities Delivered

- Persistent digital twin records per equipment asset
- Twin state updated in response to UNS telemetry events and maintenance transactions
- Maintenance history, parts history, and AI-generated insight attribution per twin
- Twin-level health score derived from telemetry patterns and maintenance frequency
- Twin state available as first-class context in AI orchestration context builders
- Cross-domain twin queries spanning maintenance, production, inventory, and quality
- Twin synchronization with external digital twin platforms where available

### Architectural Properties Added

- Digital twin as the authoritative stateful asset context model
- Twin state consumed by AI context builders, reducing direct database query volume
- Cross-domain operational context unified under the twin record

### Portability and Scalability

- Twin records are tenant-scoped and follow the existing isolation model
- Twin synchronization with external platforms uses the i3X API layer as the
  integration surface
- Twin-level context enables multi-site asset comparison through federated query patterns

---

## Phase 7 — AI-Native Factory Operations Layer

**Focus:** Evolve the AI orchestration layer from an assistive inference service into
a proactive operational intelligence layer capable of detecting patterns, generating
continuous operational awareness summaries, and coordinating multi-step reasoning
workflows across all plant domains.

### Capabilities Delivered

- Continuous background analysis of equipment telemetry for anomaly and trend detection
- Proactive maintenance alert generation before fault events occur
- Automated shift-level operational summary generation for supervisor distribution
- Multi-step reasoning workflows for root cause analysis report generation
- LLM-driven diagnostic pipelines consuming digital twin state, telemetry history,
  and maintenance records
- Predictive maintenance model integration with configurable risk threshold alerting
- Cross-site benchmarking intelligence comparing operational performance across sites
- Structured AI recommendation approval workflows for supervisors and administrators
- Retrieval-augmented generation over maintenance procedures and equipment documentation

### Architectural Properties Added

- Background AI processing pipeline operating outside the request-response cycle
- Proactive notification dispatch from AI-generated operational insights
- Multi-step agentic tool orchestration with human approval gates
- AI-generated content subject to structured review workflows before influencing records

### Portability and Scalability

- AI processing pipeline horizontally scalable through background worker separation
- Model backend remains pluggable; Phase 7 capabilities available with local or
  external inference depending on deployment requirements
- Cross-site intelligence operates through federated UNS topic subscriptions without
  requiring centralized data aggregation

---

## Long-Term Vision

The long-term target state of Manufacturing Core Suite is a **Factory OS runtime layer**
that serves as the structured operational intelligence foundation for manufacturing
organizations operating across multiple sites, domains, and connected industrial ecosystems.

In this target state, Manufacturing Core Suite provides:

- **Real-time operational visibility** across all production, maintenance, inventory,
  quality, and workforce activity at plant and multi-site levels, available to any
  authorized consumer through standardized interfaces
- **Cross-domain workflow intelligence** where operational events in one domain
  automatically surface relevant context, recommended actions, and escalation triggers
  in adjacent domains without manual correlation
- **Predictive manufacturing operations** where AI-driven analysis of equipment
  telemetry, maintenance patterns, and production signals produces actionable
  advance warning of production risks before they materialize as downtime events
- **Portable industrial applications** that can be developed against the platform's
  UNS and i3X API layer and deployed across any compatible industrial site or
  partner ecosystem without re-implementation
- **Multi-site operational benchmarking** that enables organizations to compare
  performance, maintenance effectiveness, and inventory efficiency across sites
  using a shared, semantically consistent operational data model
- **Composable Factory OS modules** where maintenance, production, inventory,
  quality, training, and scheduling capabilities can be independently adopted
  and combined into a coherent operational platform without requiring a monolithic
  system replacement initiative

Manufacturing Core Suite is designed to grow into this vision through the phased
capability progression described in this roadmap. Each phase delivers independent
operational value while contributing to the structural foundation that makes the
long-term Factory OS vision achievable.

The platform will reach its long-term target state when any manufacturing organization
can connect their plant data infrastructure to Manufacturing Core Suite, adopt the
domains relevant to their operational priorities, and immediately begin operating with
structured real-time visibility, AI-assisted decision support, and cross-system
interoperability — without dependency on any single vendor, protocol, or cloud platform.
