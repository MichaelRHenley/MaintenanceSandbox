# Manufacturing Core Suite — Architecture

## 1. Overview

Manufacturing Core Suite is a unified operational platform designed to connect machines,
people, workflows, and enterprise systems within industrial manufacturing environments.
It is not a standalone application. It is designed to function as a **Factory OS layer** —
a structured operational and data backbone that sits between plant-floor equipment and
enterprise systems, organizing information, events, and workflows into a coherent
operational architecture.

The platform is intended to support the full scope of manufacturing operations including:

- Maintenance (emergency, preventive, and predictive)
- Production coordination and visibility
- Inventory and parts management
- Quality signal integration
- Training and workforce coordination
- Production scheduling awareness
- AI-assisted decision support

Manufacturing Core Suite enables these domains to share structured data and event
infrastructure rather than operating as separate disconnected systems.

---

## 2. Architectural Goals

Manufacturing Core Suite is built to achieve the following architectural outcomes:

- **Unify fragmented manufacturing data sources** across maintenance, production,
  inventory, workforce, and quality domains
- **Enable real-time operational visibility** at the work center, department, site,
  and multi-site level
- **Support open interoperability** through alignment with ISA-95, MQTT, OPC-UA,
  UNS, and I3X-compatible industrial data exchange patterns
- **Reduce vendor lock-in** by avoiding proprietary integration dependencies and
  favoring standards-based connectivity
- **Allow modular deployment** so individual domains such as Emergency Maintenance
  can be adopted incrementally without requiring full platform rollout
- **Support plant-level and multi-site scaling** from a single production line to
  multi-department plants and eventually multi-site manufacturing networks
- **Provide a foundation for AI-driven operational intelligence** where AI assists
  human operators and supervisors rather than operating autonomously

---

## 3. Core Architecture Principles

### ISA-95 Aligned Structure

The platform organizes data and workflows according to the ISA-95 manufacturing
operations management hierarchy. Enterprise, Site, Area, Work Center, and Equipment
levels are first-class structural concepts, not implementation afterthoughts.

### Open Architecture First

Manufacturing Core Suite is designed around open industrial standards. Where
proprietary integrations are used, they are encapsulated behind defined interfaces
so they can be replaced without architectural restructuring.

### Event-Driven Integration

Operational events — equipment state changes, work order status transitions,
inventory updates, production counts — propagate through an event infrastructure
based on MQTT and Unified Namespace principles rather than point-to-point API calls.

### Edge-to-Enterprise Continuity

The architecture spans from plant-floor edge devices and OPC-UA sources through
event distribution infrastructure to operational databases and enterprise-facing
application and analytics layers.

### Modular Bounded Domains

Maintenance, production, inventory, quality, workforce, and scheduling are treated
as bounded operational domains. Each domain has defined data ownership and interacts
with other domains through shared event and data infrastructure, not direct coupling.

### Plant-Centric Deployment

Manufacturing Core Suite is designed to be deployed close to plant operations.
It does not require enterprise-level system replacement as a prerequisite for adoption.

### AI as Assistive Intelligence

Artificial intelligence within Manufacturing Core Suite is positioned as an
operational multiplier. AI enhances the speed and quality of human decisions.
It does not replace operational authority, safety procedures, or human judgment.

### Standards-Based Interoperability

All integration patterns reference established industrial standards where available.
Custom integration logic is encapsulated and documented rather than embedded silently
across the system.

---

## 4. Logical Architecture Layers

```
Shop Floor → Edge Connectivity → MQTT / UNS → Operational Data Layer → Workflow / AI Layer → User Experiences
```

### 4.1 Edge / Shop Floor Layer

The edge layer represents all physical data-producing systems at the plant floor level:

- PLCs, sensors, actuators, and embedded machine controllers
- SCADA systems and distributed control systems
- Industrial historians (OSIsoft PI, Wonderware, and equivalents)
- OPC-UA compliant equipment and devices
- Edge connectivity platforms such as **Ignition** (Inductive Automation) and
  **Kepware** (PTC) acting as OPC-UA servers and protocol bridges
- Barcode scanners, RFID readers, and operator input terminals

This layer is the origin of all real-time operational signal data. Manufacturing
Core Suite does not own or manage this layer directly but defines the interfaces
through which data is ingested from it.

### 4.2 Connectivity and Event Layer

The connectivity layer normalizes and distributes operational events from the edge
to all consumers within the platform:

- **MQTT broker** as the primary event transport mechanism
- **Unified Namespace (UNS)** as the semantic topic structure organizing all events
  by site, area, work center, equipment, and event type
- Equipment telemetry streams (temperatures, speeds, pressures, utilization)
- Equipment state changes (running, stopped, faulted, maintenance mode)
- Production events (cycle completions, count increments, scrap signals)
- Maintenance events (work order creation, response, completion)
- Inventory and quality events where applicable

This layer eliminates direct point-to-point connections between systems. Any
authorized consumer can subscribe to relevant events without requiring custom
integration code per connection.

### 4.3 Operational Data Layer

The operational data layer provides structured, persistent storage for manufacturing
operations data:

- **SQL operational databases** for maintenance records, work orders, inventory,
  parts data, BOM structures, and workforce data
- **MES tables** for production counts, shift records, and job tracking where
  an MES source is available
- **Local site databases** supporting plant-level operation independent of
  cloud availability
- **Snowflake** or equivalent data warehouse integration for analytical workloads,
  cross-site reporting, and long-horizon trend analysis
- Maintenance history, equipment downtime records, and corrective action logs
- Inventory levels, storage location data, manufacturer part references, and
  reorder context

The operational data layer is the system of record for all structured manufacturing
operations data managed by Manufacturing Core Suite.

### 4.4 Application and Workflow Layer

The workflow layer provides the business logic and coordination capabilities
that operate on top of the event and data infrastructure:

- Emergency maintenance response workflows
- Work order creation, assignment, escalation, and completion tracking
- Production dashboards and shift-level operational awareness displays
- Inventory lookup, availability queries, and parts request workflows
- Approval and notification routing across maintenance, production, and management
- Escalation paths for unresolved maintenance issues or safety-critical events
- Cross-functional workflows linking maintenance to production and inventory context

This layer consumes events from the connectivity layer and reads and writes from
the operational data layer. It does not connect directly to edge systems.

### 4.5 AI Orchestration Layer

The AI orchestration layer provides structured, context-aware intelligence
capabilities across all operational domains:

- **Intent routing** to direct user queries and system events to appropriate
  AI handlers
- **Context builders** that assemble relevant operational context from maintenance
  history, equipment records, parts data, workforce, and production signals
- **Retrieval patterns** over structured SQL data and semi-structured operational
  knowledge
- Natural language query support for operational questions
- Summarization of maintenance history, recurring issues, and incident trends
- Recommendation generation for corrective actions and parts usage
- Future support for predictive maintenance signal processing
- Integration with local or enterprise-controlled AI models as appropriate
  for data governance requirements

The AI orchestration layer does not operate as a standalone autonomous system.
It is a service layer consumed by workflow and experience components that retain
human oversight and approval authority.

### 4.6 Experience Layer

The experience layer delivers operational information and workflow capabilities
to human users and system consumers:

- **Web applications** for supervisors, maintenance coordinators, inventory
  managers, and administrators
- **Operator interfaces** for shop floor access to work requests, status, and
  instructions
- **Responder interfaces** for maintenance technicians working active requests
- **Supervisor dashboards** for real-time visibility into active incidents,
  equipment state, and team activity
- **Mobile interfaces** for field-accessible workflows where appropriate
- **API consumers** for integration with ERP, CMMS, or third-party enterprise
  systems

---

## 5. ISA-95 Alignment

Manufacturing Core Suite uses the ISA-95 manufacturing operations management
standard as its primary structural model for organizing data, hierarchies, and
workflows.

The ISA-95 hierarchy is reflected directly in the platform's data models and
event naming structures:

| ISA-95 Level | Manufacturing Core Suite Representation |
|---|---|
| **Enterprise** | Tenant or organization record; ERP integration boundary |
| **Site** | Physical plant or facility; site-level database and event namespace |
| **Area** | Functional manufacturing area (e.g., Assembly, Packaging, Machining) |
| **Work Center** | Specific production or maintenance work center within an area |
| **Line / Cell / Equipment** | Individual machine, cell, or equipment asset with its own operational record |

This alignment ensures that data produced at the equipment level can be correctly
attributed and aggregated upward through the hierarchy for reporting, analytics,
and AI context retrieval.

ISA-95 levels also inform access control boundaries, ensuring that operational
data is visible at the appropriate organizational scope and not inadvertently
exposed across site or tenant boundaries.

---

## 6. Unified Namespace (UNS) Strategy

### Why UNS Matters

Traditional manufacturing integration architectures rely on point-to-point
connections between systems. Each new integration requires custom development
and increases overall system fragility. The Unified Namespace pattern eliminates
this by providing a single, centrally structured event infrastructure that all
systems publish to and consume from.

### MQTT as Event Transport

Manufacturing Core Suite uses **MQTT** as the primary event transport mechanism
for the UNS. MQTT is a lightweight, well-supported protocol appropriate for both
plant-floor and enterprise connectivity. A centrally managed MQTT broker receives
all operational events and makes them available to authorized subscribers.

### Topic Structure Patterns

UNS topic structures within Manufacturing Core Suite represent the full ISA-95
hierarchy combined with event type classification. Topic patterns follow the form:

```
{site}/{area}/{workcenter}/{equipment}/{event-type}
```

Example topic patterns (plain text representation):

    plantA/assembly/line1/press-cell-03/state
    plantA/assembly/line1/press-cell-03/production-count
    plantA/maintenance/workcenter-02/workorder/created
    plantA/maintenance/workcenter-02/workorder/completed
    plantA/inventory/parts/6207-2RS1/stock-update
    plantA/quality/line1/inspection/result
    enterprise/planning/schedule/shift-release

### Reducing Point-to-Point Integrations

With a UNS in place, a maintenance system, a production dashboard, an AI context
builder, and an ERP integration can all receive the same equipment state event
by subscribing to the relevant topic. No bilateral integration agreement between
each pair of systems is required.

---

## 7. MQTT and OPC-UA Integration Strategy

### OPC-UA at the Edge

**OPC-UA** is the primary protocol for connectivity between Manufacturing Core Suite
infrastructure and industrial equipment. OPC-UA provides:

- Standardized data models for equipment, alarms, and historian data
- Secure, reliable communication between edge devices and connectivity platforms
- Compatibility with the broad range of modern and legacy industrial equipment

Edge connectivity platforms such as **Ignition** (Inductive Automation) and
**Kepware** (PTC) serve as OPC-UA servers and protocol translation bridges,
normalizing data from PLCs and other devices before it enters the Manufacturing
Core Suite connectivity layer.

### MQTT for Normalized Event Distribution

Once data crosses from the edge layer into the Manufacturing Core Suite connectivity
infrastructure, **MQTT** is the preferred transport mechanism. OPC-UA signals are
translated into normalized MQTT events carrying structured payloads and published
into the UNS topic hierarchy.

This separation of concerns means that:

- Edge connectivity uses the most appropriate industrial protocol for equipment access
- Operational event distribution uses a lightweight, scalable, widely supported
  message transport
- Neither layer is required to understand the internal structure of the other

### Industrial Interoperability Compatibility

This combination of OPC-UA for edge access and MQTT for normalized event distribution
is consistent with modern industrial interoperability reference architectures including
the MQTT Sparkplug specification for structured payload conventions and the broader
direction of industrial data exchange standards.

---

## 8. I3X Interoperability Alignment

Manufacturing Core Suite is designed to align with the goals and principles of
**I3X** (Industrial Interoperability and Integration Exchange) and compatible
industrial data exchange ecosystems.

This alignment is expressed through the following design decisions:

### Portable Industrial Data Models

Core data models within Manufacturing Core Suite — equipment, maintenance records,
parts, work orders, production counts, workforce — are designed to be structurally
portable. They are not encoded in proprietary schemas that prevent extraction or
reuse by other systems.

### Semantic Interoperability

The use of ISA-95 hierarchy, standardized event topic structures, and consistent
naming conventions across domains creates a semantically coherent operational data
model. Data consumers — both internal and external — can interpret operational
events and records without requiring custom translation layers.

### Composable Industrial Applications

Manufacturing Core Suite is architected as a set of modular operational domains
rather than a monolithic system. Maintenance, production, inventory, and AI
orchestration can be adopted and integrated independently. This composability is
consistent with I3X principles supporting assembly of industrial application
capabilities from discrete, interoperable components.

### Data Fabric and Digital Thread Readiness

The event infrastructure and operational data layer of Manufacturing Core Suite
are designed to participate in broader industrial data fabric architectures.
Historical maintenance and production data, equipment context, and operational
events can be exposed to enterprise analytics platforms, digital twin initiatives,
and cross-site benchmarking infrastructure.

### Reduced Vendor Dependency

By building on MQTT, OPC-UA, ISA-95 structural models, and open API patterns,
Manufacturing Core Suite avoids architectural dependency on any single industrial
software vendor. Integration components — edge connectors, MQTT brokers, AI
models — can be substituted or extended without restructuring the core platform.

**Note:** Manufacturing Core Suite is designed to align with I3X-compatible
interoperability principles. It does not claim formal I3X certification unless
independently verified through applicable conformance processes.

---

## 9. Functional Domain Architecture

### 9.1 Maintenance

The Maintenance domain is the initial production domain of Manufacturing Core Suite
and the most fully realized at this stage.

- Manages the full lifecycle of maintenance requests including creation, assignment,
  response, resolution, and closure
- Supports emergency maintenance workflows with responder coordination and
  escalation paths
- Maintains equipment maintenance history for trend analysis and AI context retrieval
- Integrates with parts and inventory data to support parts lookup during active
  maintenance events
- Publishes and consumes maintenance events through the UNS event infrastructure

### 9.2 Production

The Production domain provides visibility into plant production activity and
coordinates information flow between production operations and maintenance teams.

- Tracks work center and line-level production counts and shift activity
- Surfaces production context alongside maintenance requests so responders
  understand the operational impact of equipment failures
- Consumes production events from the edge and UNS connectivity layer
- Supports schedule alignment so maintenance activities can be coordinated
  against planned production windows

### 9.3 Inventory

The Inventory domain manages maintenance-relevant parts and materials within
the plant operational context.

- Maintains parts records including manufacturer references, part numbers, and
  descriptions
- Tracks inventory levels and storage locations across site bin structures
- Supports Bill of Materials (BOM) linkage between parts and equipment assets
- Provides manufacturer image enrichment through an extensible provider pipeline
- Integrates with AI context to surface parts availability and usage patterns
  during maintenance workflows

### 9.4 Quality

The Quality domain is targeted for future integration within the platform.

- Intended to receive quality inspection signals from the edge layer via UNS
- Will link quality event data to equipment, work center, and production records
- Quality signals will be available as context to AI orchestration for root cause
  and trend analysis
- Integration with corrective action workflows in the Maintenance domain is planned

### 9.5 Training and Workforce

The Training and Workforce domain supports operational coordination and personnel
capability visibility.

- Tracks workforce assignments, responder availability, and team structure
- Supports escalation and notification routing based on role and availability
- Future scope includes training record visibility and certification status
  for safety-critical equipment and procedures
- Workforce context is available to AI orchestration when assembling operational
  response recommendations

### 9.6 Scheduling

The Scheduling domain provides alignment between production schedules and
operational workflows.

- Surfaces shift schedules and planned production windows within the operational
  context of maintenance and production workflows
- Enables maintenance coordinators to understand scheduling constraints before
  committing equipment to maintenance activity
- Future integration with external scheduling and MES systems is planned through
  the UNS event infrastructure

---

## 10. AI Architecture

### Role of AI in Manufacturing Core Suite

AI within Manufacturing Core Suite is a service layer that amplifies human
decision-making capability. It is not an autonomous control system. All AI
outputs are advisory. Human operators, supervisors, and coordinators retain
full operational authority.

### Intent Routing

The AI orchestration layer includes an intent router that classifies incoming
queries and events to direct them to the appropriate AI handler. A maintenance
status query is routed differently from a parts availability question or a
production trend summarization request.

### Context Builders

Before invoking an AI model, Manufacturing Core Suite assembles structured
operational context from the relevant data domains. Context builders retrieve:

- Active and historical maintenance records for the equipment in question
- Equipment specification and asset records
- Parts availability and usage history
- Workforce assignments and responder availability
- Production schedule and work center context where relevant

Context assembly ensures AI responses are grounded in actual plant operational
data rather than relying on general-purpose model knowledge alone.

### Retrieval and Query Patterns

Manufacturing Core Suite supports retrieval-augmented generation (RAG) patterns
over structured SQL operational data and semi-structured knowledge sources.
This allows natural language queries over plant history and operational state
without requiring users to write database queries.

### Summarization and Trend Analysis

The AI orchestration layer supports summarization of:

- Maintenance event history for a specific equipment asset
- Recurring failure patterns across a work center or area
- Corrective action effectiveness over time
- Shift-level operational summaries for supervisors

### Recommendations

Based on retrieved context, the AI orchestration layer can generate:

- Recommended corrective actions for recurring equipment issues
- Suggested parts based on equipment history and BOM data
- Escalation recommendations when response time thresholds are approached

### Predictive Maintenance Readiness

The AI architecture is designed to evolve toward predictive maintenance
capabilities as edge telemetry data becomes available through the UNS connectivity
layer. Signal pattern analysis and anomaly detection over equipment telemetry
are targeted future capabilities.

### AI Model Governance

Manufacturing Core Suite supports both locally-hosted AI models (such as Ollama
with Llama-series models) and externally-hosted models (such as Claude) through
a model abstraction interface. This allows organizations to select AI models
consistent with their data governance, privacy, and latency requirements.

All AI interactions are logged for auditability. AI outputs are never applied
to operational records without human review or explicit approval workflows.

---

## 11. Deployment Model

### Supported Deployment Patterns

Manufacturing Core Suite is designed to support multiple deployment topologies
depending on site infrastructure, IT governance requirements, and adoption stage:

**Local Plant Deployment**
The full application stack — web application, operational database, AI orchestration,
and MQTT broker — is deployed on-premises within the plant network. This pattern
is appropriate for environments with strict data residency requirements or limited
cloud connectivity.

**Hybrid Deployment**
Edge connectivity and operational databases remain on-premises. The application
layer, AI orchestration, and analytics integration are hosted in a cloud or
datacenter environment. Event distribution via MQTT bridges the edge and cloud
tiers.

**Cloud-Hosted Application Layer**
The web application and AI services are cloud-hosted with on-prem MQTT and
database connectivity. This pattern supports centrally managed multi-site
deployments where per-site application maintenance is impractical.

**Multi-Tenant Evolution**
The platform is architected to support multi-tenant isolation. Future deployments
across multiple organizations or business units can enforce data and operational
boundaries at the tenant level.

### Incremental Adoption

Manufacturing Core Suite does not require full platform deployment to deliver
value. Adoption can begin with a single domain — Emergency Maintenance is the
recommended starting point — and expand incrementally as infrastructure matures
and organizational confidence grows. Each domain contributes to the shared data
and event infrastructure from the point of adoption.

---

## 12. Security and Governance Considerations

### Role-Based Access Control

Manufacturing Core Suite enforces role-based access across all operational domains.
Supervisor, coordinator, responder, operator, and administrator roles are defined
with distinct permission boundaries. Sensitive operational data is not broadly
accessible across roles or organizational boundaries.

### Auditability

All significant operational transactions — work order creation, status changes,
AI interactions, administrative modifications — are logged with user identity,
timestamp, and action context. Audit logs are retained for compliance and
operational review purposes.

### Operational Data Boundaries

Data isolation between tenants and organizational units is enforced at the data
layer. Multi-tenant deployments ensure that operational data from one site or
organization is not accessible to other tenants.

### Controlled AI Usage

AI model invocations are logged. AI-generated content that influences operational
records is subject to human review workflows. Autonomous AI modification of
operational data without explicit human approval is not permitted.

### Standards-Based Integration

All external integrations are implemented through defined interface contracts —
OPC-UA, MQTT, REST APIs, and documented data exchange schemas. Uncontrolled
direct coupling to external databases or proprietary APIs is avoided. This limits
the blast radius of changes in connected systems.

### Enterprise Policy Alignment

Manufacturing Core Suite is designed to operate within enterprise IT governance
frameworks including Active Directory integration, SSO compatibility, network
segmentation support, and configurable data retention policies.

---

## 13. Long-Term Target State

The long-term target for Manufacturing Core Suite is a complete **Factory OS**
that serves as the structured operational intelligence layer for an entire
manufacturing enterprise. This target state includes:

- **Real-time visibility** across all production, maintenance, inventory, quality,
  and workforce activity at plant and multi-site levels
- **Cross-domain workflows** that automatically surface relevant context from
  adjacent domains when an operational event occurs
- **Predictive maintenance** driven by equipment telemetry patterns and AI
  signal analysis
- **Production intelligence** linking production counts, quality signals, and
  downtime events into coherent operational performance views
- **Inventory awareness** with automatic parts consumption tracking and
  proactive reorder visibility
- **Quality integration** connecting inspection results, corrective actions,
  and equipment history
- **Training and certification visibility** ensuring safety-critical maintenance
  and production tasks are assigned to qualified personnel
- **Multi-site benchmarking** enabling performance comparison across sites using
  a shared operational data model
- **AI-assisted decision support** at every operational level from individual
  responders to plant directors
- **Participation in I3X-aligned industrial data ecosystems** enabling the platform
  to interoperate with external partners, suppliers, and enterprise systems through
  standardized industrial data exchange

---

## 14. Conclusion

Manufacturing Core Suite is designed to become the structured operational
intelligence layer that connects plant operations, industrial data infrastructure,
and AI-enabled decision support into a coherent Factory OS.

It is not a point solution for a single domain. It is an architectural foundation
that allows manufacturing organizations to incrementally build operational
capability on a consistent, open, standards-aligned infrastructure — from
emergency maintenance coordination today to predictive production intelligence
and cross-site operational benchmarking in the future.

The architecture described in this document represents the intended target state
and design direction of the platform. Individual capabilities will reach
production maturity on a phased basis aligned with organizational priorities
and infrastructure readiness.
