# Virtual Factory Simulation Environment for Manufacturing Core Suite

## 1. Purpose

The Virtual Factory Simulation Environment provides a deterministic, reproducible
simulation layer that mimics the data infrastructure of a real industrial plant.
It is designed to support active development and testing of Manufacturing Core Suite
components without requiring access to live MES systems, ERP environments, PLC
networks, SCADA infrastructure, or enterprise historians.

This environment allows engineers to work against a realistic, ISA-95 aligned
manufacturing data layer from a local development machine or shared lab server.
It is not a toy environment. It is intended to replicate the structural properties
of a production plant data stack closely enough that applications and services
built against it require no significant rearchitecting when connected to real
industrial infrastructure.

The Virtual Factory is the primary development and validation target for:

- **Portable manufacturing applications** built to operate against open industrial
  interfaces rather than proprietary system APIs
- **Unified Namespace (UNS) aligned data models** following ISA-95 hierarchy and
  MQTT topic conventions
- **i3X-compatible API development** exposing structured asset data through
  standardized interoperability primitives
- **Context builder validation** for Manufacturing Core Suite AI orchestration
  components that depend on structured equipment, telemetry, and event data
- **Equipment telemetry simulation** providing realistic time-series signals for
  temperature, vibration, pressure, machine state, and production counts
- **AI-assisted maintenance workflow development** where the AI layer requires
  live or near-live operational context to generate useful outputs
- **Training and demonstration environments** that do not expose production data
  or create risk on live manufacturing infrastructure

---

## 2. Architectural Overview

The Virtual Factory represents a complete layered industrial data stack. Each layer
mirrors a corresponding layer present in a real smart factory deployment. Building
against these layers in simulation ensures that the application architecture remains
valid when connected to live plant infrastructure.

```
Field Simulation Layer      →  Simulated PLC tags, sensor signals, OPC UA nodes
Transport Layer             →  MQTT broker (Eclipse Mosquitto)
Unified Namespace Layer     →  Structured topic hierarchy, normalized payloads
API Layer                   →  .NET i3X wrapper service exposing UNS data
Application Layer           →  Manufacturing Core Suite, AI copilots, context builders
```

### ISA-95 Asset Hierarchy

The simulated factory represents the following ISA-95 hierarchy:

```
Enterprise
 └── Site Alpha
     └── Production Line A
         └── Asset Conveyor01
             ├── Temperature
             ├── Vibration
             └── Status
```

This hierarchy is not just illustrative. It is the structural schema used to
organize MQTT topics, asset metadata records, context builder queries, and
AI context assembly. Every simulated data point is anchored to a specific
node in this hierarchy.

The hierarchy can be extended with additional areas, lines, and assets as
development requirements grow, without restructuring the transport or API layers.

---

## 3. Core Simulation Stack

The Virtual Factory is assembled from well-supported open-source and freely
available industrial software components. The recommended stack is documented
below with the specific role each component plays in the simulated environment.

### 3.1 Eclipse Mosquitto — MQTT Broker

**Role:** Central event transport and message distribution hub.

Eclipse Mosquitto is a lightweight, production-grade open-source MQTT broker.
In the Virtual Factory, Mosquitto serves as the Unified Namespace backbone.
All simulated telemetry, equipment state changes, and operational events are
published to Mosquitto by the field simulation layer and consumed from Mosquitto
by the API layer, Manufacturing Core Suite services, and AI context builders.

Mosquitto is configured with topic-level access control and persistent session
support to allow subscribers to recover missed messages after reconnection.

### 3.2 Node-RED — Tag Generator and Event Simulator

**Role:** Simulated field data source producing deterministic or randomized
telemetry signals and discrete operational events.

Node-RED provides the visual flow programming environment used to simulate PLC
tag output, sensor telemetry, and equipment state transitions. Flows are
configured to produce time-series values for temperature, vibration, pressure,
run state, and production counts at configurable intervals.

Node-RED publishes all simulated values directly to the Mosquitto MQTT broker
using the UNS topic structure defined in Section 4. Node-RED also supports
injection of synthetic failure events — unexpected temperature spikes, vibration
anomalies, equipment fault states — for testing emergency maintenance workflows
and AI diagnostic pipelines.

### 3.3 OPC UA Simulator — Edge Protocol Source

**Role:** Simulates OPC UA compliant equipment for edge-level protocol testing.

The OPC UA simulator exposes a browsable server namespace with simulated nodes
representing equipment assets, tag values, and diagnostic data. Supported
implementations include:

- **Prosys OPC UA Simulation Server** — feature-complete free edition suitable
  for most development scenarios
- **Kepware KEPServerEX** — if a Kepware license is available for local testing
- **open62541** — open-source OPC UA stack for custom node configuration

The OPC UA simulator connects to Manufacturing Core Suite edge connectivity
components or to a Node-RED OPC UA input node to bridge simulated equipment
data into the MQTT transport layer.

### 3.4 HighByte Intelligence Hub — UNS Transformation Layer (Optional)

**Role:** Provides a managed UNS configuration environment and data transformation
layer if a more structured approach to namespace management is required.

HighByte Intelligence Hub is an industrial data operations platform that can
serve as the transformation and normalization layer between raw OPC UA or MQTT
sources and a structured UNS topology. It supports Sparkplug B payload encoding,
asset contextualization, and namespace federation.

HighByte is optional in the base Virtual Factory configuration. It is recommended
when the simulation environment needs to validate Manufacturing Core Suite behavior
against Sparkplug B encoded UNS payloads or when testing HighByte-specific
integration patterns.

### 3.5 .NET i3X Wrapper Service — API Layer

**Role:** Exposes UNS data through standardized industrial interoperability
primitives via a .NET minimal API service.

The i3X wrapper service is a custom .NET 8 minimal API application that subscribes
to the Mosquitto MQTT broker, maintains a current-value cache of simulated tag
states, and exposes that state through HTTP endpoints aligned with i3X
interoperability principles.

This service is the integration point for Manufacturing Core Suite components that
need to query current asset values, browse the asset hierarchy, or subscribe to
change notifications through a REST or WebSocket interface rather than consuming
MQTT directly.

The wrapper service is described in detail in Section 5.

### Stack Integration Flow

```
Node-RED (telemetry flows)
    │
    ├──► OPC UA Simulator (optional protocol bridge)
    │
    └──► Mosquitto MQTT Broker (UNS topic hierarchy)
              │
              ├──► .NET i3X Wrapper Service (HTTP/WS API layer)
              │         │
              │         └──► Manufacturing Core Suite
              │              AI Context Builders
              │              Work Order Services
              │
              └──► Direct MQTT subscribers (dashboards, monitors)
```

---

## 4. Unified Namespace Strategy

### Namespace as Canonical Plant Interface

The Unified Namespace is the authoritative source of truth for all simulated
plant data. Every data point produced by the Virtual Factory is addressed through
a topic path that encodes its full ISA-95 context. Applications and services should
never depend on point-to-point connections to individual simulated data sources.
They should subscribe to and query the UNS exclusively.

### Topic Structure

MQTT topics follow a hierarchical path encoding the ISA-95 structure of the
simulated factory:

```
{enterprise}/{site}/{line}/{asset}/{signal}
```

Example topics for the Conveyor01 asset on Site Alpha, Line A:

```
enterprise/site-alpha/line-a/conveyor01/temperature
enterprise/site-alpha/line-a/conveyor01/vibration
enterprise/site-alpha/line-a/conveyor01/status
```

Operational event topics follow the same hierarchy with an event-type segment:

```
enterprise/site-alpha/line-a/conveyor01/events/fault
enterprise/site-alpha/line-a/conveyor01/events/maintenance-requested
enterprise/site-alpha/line-a/conveyor01/events/maintenance-completed
enterprise/site-alpha/maintenance/workorders/created
enterprise/site-alpha/maintenance/workorders/updated
enterprise/site-alpha/inventory/parts/stock-update
```

### Payload Convention

All MQTT payloads in the Virtual Factory are structured JSON objects. A minimal
telemetry payload includes:

- `timestamp` — ISO 8601 UTC
- `value` — the current signal value
- `quality` — `good`, `uncertain`, or `bad`
- `unit` — engineering unit where applicable

This convention is consistent with Sparkplug B semantics without requiring full
Sparkplug B compliance in the base simulation configuration.

### Why This Matters

Applications built against this UNS topology connect to the real plant namespace
by changing a broker address and confirming that the real plant follows equivalent
topic conventions. No application-level rearchitecting is required if the naming
contract is maintained.

---

## 5. i3X Compatibility Layer

### Purpose

The i3X wrapper service bridges the MQTT-based UNS and Manufacturing Core Suite
application components that prefer HTTP or WebSocket interfaces. It also provides
a reference implementation for how Manufacturing Core Suite exposes and consumes
industrial data through i3X-aligned primitives.

### Service Design

The wrapper service is implemented as a .NET 8 minimal API project. It subscribes
to all topics under the simulated UNS at startup, maintains an in-memory current-value
store keyed by asset path, and exposes the following interaction primitives through
HTTP endpoints:

**Browse Assets**
Returns the list of known assets and their tag paths within the simulated hierarchy.

```
GET /assets
GET /assets/{site}/{line}
GET /assets/{site}/{line}/{asset}
```

**Read Values**
Returns the current cached value for one or more tag paths.

```
GET /values/{site}/{line}/{asset}/{signal}
POST /values/batch
```

**Write Values**
Publishes a value update to the Mosquitto broker on behalf of an authorized
application client. Used primarily for simulating operator actions or test
scenario injection.

```
POST /values/{site}/{line}/{asset}/{signal}
```

**Subscribe to Updates**
Provides a WebSocket endpoint that streams value change notifications for
subscribed tag paths.

```
WS /subscribe?topics=enterprise/site-alpha/line-a/conveyor01/temperature,...
```

**Discover Metadata**
Returns asset metadata including engineering units, data type, and simulated
signal range for a given asset path.

```
GET /metadata/{site}/{line}/{asset}
GET /metadata/{site}/{line}/{asset}/{signal}
```

### Portability Note

Applications that target the i3X wrapper service endpoints are architecturally
decoupled from the MQTT broker and simulation layer. Replacing the simulated broker
with a production plant MQTT broker and updating the wrapper service connection
string is the only change required to point these applications at real plant data.

---

## 6. Example Simulated Equipment Model: Conveyor01

Conveyor01 is the primary reference asset in the Virtual Factory. It represents a
production conveyor on Line A at Site Alpha and is used as the canonical example
for all documentation, context builder validation, and AI experiment scenarios.

### Asset Metadata

| Field | Value |
|---|---|
| Asset ID | `conveyor01` |
| Display Name | Conveyor 01 |
| Site | Site Alpha |
| Area | Production |
| Line | Line A |
| Asset Type | Conveyor |
| Manufacturer | Simulated |

### Simulated Signals

**Temperature**

| Property | Value |
|---|---|
| Topic | `enterprise/site-alpha/line-a/conveyor01/temperature` |
| Unit | °C |
| Normal Range | 35–65 °C |
| Fault Injection Threshold | > 85 °C |
| Update Interval | 5 seconds |

**Vibration**

| Property | Value |
|---|---|
| Topic | `enterprise/site-alpha/line-a/conveyor01/vibration` |
| Unit | mm/s RMS |
| Normal Range | 1.0–4.5 mm/s |
| Fault Injection Threshold | > 7.5 mm/s |
| Update Interval | 5 seconds |

**Machine State / Status**

| Property | Value |
|---|---|
| Topic | `enterprise/site-alpha/line-a/conveyor01/status` |
| Values | `running`, `stopped`, `faulted`, `maintenance` |
| Update Interval | On change |

### Node-RED Flow Design

The Conveyor01 simulation is implemented as a Node-RED flow containing:

- An **inject node** firing on a 5-second interval
- A **function node** generating a random value within the normal operating range
  with occasional drift toward fault thresholds based on a configurable fault
  probability parameter
- An **MQTT out node** publishing the structured JSON payload to the Mosquitto broker

Fault injection is supported through a separate inject node that forces temperature
or vibration above threshold values for a configurable duration, triggering
downstream fault detection and emergency maintenance workflow tests.

---

## 7. Integration with Manufacturing Core Suite

The Virtual Factory is the primary development environment for Manufacturing Core Suite
feature development. Each Manufacturing Core Suite domain has specific integration
points with the simulated environment.

### Emergency Maintenance Workflows

Node-RED fault injection events publish MQTT messages that trigger Manufacturing Core
Suite emergency maintenance workflow creation. This allows full end-to-end testing of
the maintenance request pipeline — from equipment fault signal to work order creation,
responder assignment, parts lookup, and resolution — without accessing live equipment.

### Predictive Maintenance Experiments

The time-series telemetry produced by Conveyor01 and future simulated assets provides
the signal data required to experiment with anomaly detection, trend analysis, and
predictive failure modeling. AI models can be trained or evaluated against simulated
degradation scenarios injected through Node-RED fault flows.

### Parts Intelligence Systems

Equipment asset records in the Virtual Factory reference manufacturer part numbers
aligned with the Manufacturing Core Suite parts and inventory database. Parts lookup
workflows, image enrichment pipelines, and BOM-to-asset linkage can be validated using
the simulated asset hierarchy without requiring real equipment records.

### Work Order Analytics

All simulated maintenance events are recorded through the Manufacturing Core Suite
operational database. Analytics queries, AI summarization, and trend detection
components can be validated against a growing history of simulated work orders
generated through repeatable fault injection test scenarios.

### AI Copilots and Context Builders

Manufacturing Core Suite AI context builders query equipment records, maintenance
history, parts availability, and workforce data to assemble context for AI model
invocations. The Virtual Factory provides deterministic test data for each context
provider, allowing AI outputs to be evaluated for correctness and relevance against
known ground-truth inputs.

### Intent Routing Pipelines

The AI intent router classifies incoming natural language queries and routes them to
appropriate handlers. The Virtual Factory supports test suites that submit known queries
and validate that routing, context assembly, and response generation behave as expected
across different operational scenarios.

### Development Without Enterprise Dependencies

Manufacturing Core Suite engineering teams can operate the full development workflow —
feature development, integration testing, AI experimentation, and demonstration —
using only locally installed Virtual Factory components. No VPN access to plant
networks, no MES credentials, no ERP sandbox, and no live historian connections are
required.

---

## 8. Alignment with Factory OS Architecture

The Virtual Factory is not only a convenience tool for local development. It is a
reference implementation of the architectural patterns that Manufacturing Core Suite
targets for production deployment. Building against the Virtual Factory enforces
architectural discipline that makes production deployments more reliable.

### ISA-95 Structural Compliance

The simulated hierarchy — Enterprise, Site, Area, Work Center, Equipment — is
implemented as a first-class structural model, not as flat metadata fields. All
context builders, work order records, and AI queries navigate this hierarchy
explicitly, ensuring that the application code remains structurally valid against
real ISA-95 aligned plant data sources.

### CESMII Smart Manufacturing Profile Alignment

The Virtual Factory telemetry model and asset contextualization approach are
designed to be compatible with CESMII (Clean Energy Smart Manufacturing Innovation
Institute) Smart Manufacturing Profile principles, which emphasize semantic data
contextualization, standardized information models, and open interoperability for
manufacturing applications.

### Unified Namespace Adoption

Every simulated data point is accessible through the UNS topic hierarchy and never
through direct point-to-point connections. Engineering teams that work against the
Virtual Factory consistently internalize the UNS-first integration pattern, which
directly reduces integration debt when connecting to real plant infrastructure.

### i3X Interoperability Readiness

The .NET i3X wrapper service is the reference implementation for how Manufacturing
Core Suite exposes and consumes industrial data through i3X-aligned interaction
primitives. Applications developed against this wrapper are structurally compatible
with future i3X-compliant industrial data brokers.

### Portable Manufacturing Applications

Applications built against the Virtual Factory use no proprietary APIs, no
vendor-specific SDKs, and no plant-specific connection logic. The only external
dependencies are standard MQTT client libraries and the i3X wrapper service HTTP
interface. This portability is a deliberate design constraint enforced by the
Virtual Factory architecture.

### AI-Native Maintenance Platforms

The Virtual Factory provides the context data density required for meaningful AI
development. Deterministic fault scenarios, reproducible equipment histories, and
structured asset hierarchies allow AI components to be developed and evaluated
rigorously before they are connected to real plant data streams.

---

## 9. Future Expansion Roadmap

The base Virtual Factory configuration described in this document is the starting
point. The following enhancements are planned as the simulation environment matures
alongside Manufacturing Core Suite development.

### Digital Twin Synchronization

Integrate with an asset digital twin framework to maintain a persistent, stateful
model of each simulated equipment asset. Twin state updates in response to simulated
events, and AI context builders query the twin state rather than raw telemetry
topics for richer contextual information.

### Historian Replay Simulation

Implement a historian replay component that reads historical time-series data
from a file or database source and re-emits it through the MQTT broker on demand.
This enables testing of trend analysis, anomaly detection, and AI summarization
components against realistic multi-week or multi-month signal histories without
generating new simulation data in real time.

### Synthetic Failure Injection Framework

Develop a structured failure injection library for Node-RED that defines named
failure scenarios — bearing wear progression, overheating events, belt slippage
vibration patterns, motor fault sequences — and executes them as reproducible
test cases with documented expected outcomes.

### Multi-Site Namespace Federation

Extend the simulated factory to include multiple independent sites — Site Alpha,
Site Beta, Site Gamma — each with its own UNS topic hierarchy, asset records, and
operational database schema. This enables development and testing of Manufacturing
Core Suite multi-site visibility, cross-site benchmarking, and federated AI query
patterns.

### LLM-Driven Diagnostics

Develop validation test suites that submit simulated fault scenarios to the AI
orchestration layer and evaluate the quality, accuracy, and operational relevance
of LLM-generated diagnostic outputs. These suites become part of the standard
pull request validation workflow for AI feature changes.

### Predictive Maintenance Modeling

Introduce degradation models in Node-RED that simulate long-duration equipment
wear patterns — gradual temperature trend increases, progressive vibration
amplitude growth — to provide the signal characteristics required for training
and validating predictive maintenance detection algorithms.

### Cross-Site Benchmarking Simulations

Generate synthetic operational history across multiple simulated sites with
deliberately varied performance profiles to enable development and testing of
cross-site benchmarking, outlier detection, and comparative analytics features
in Manufacturing Core Suite.

---

## 10. Summary

The Virtual Factory Simulation Environment is the reference development platform
for Manufacturing Core Suite and the broader Factory OS initiative. It provides
a complete, self-contained industrial data stack that mirrors the structural and
behavioral properties of a real smart manufacturing environment without requiring
access to live plant infrastructure.

By building against the Virtual Factory consistently, engineering teams ensure that
Manufacturing Core Suite components are architecturally portable, structurally aligned
with ISA-95 and UNS conventions, and operationally valid when connected to real
industrial systems. The simulation environment enforces the open, standards-based
integration discipline that is central to the Manufacturing Core Suite architectural
mission.

The Virtual Factory is not a temporary scaffold. It is a permanent component of the
Manufacturing Core Suite engineering infrastructure, intended to grow in fidelity
and scope as the platform evolves toward full Factory OS capability.
