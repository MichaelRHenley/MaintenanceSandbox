# AI Orchestrator Architecture

## 1. Overview

Manufacturing Core Suite includes an embedded AI orchestration layer designed to
deliver contextual, operationally grounded assistance across maintenance, production,
inventory, and workforce domains. The orchestration layer is not a general-purpose
chatbot interface. It is a structured inference pipeline that assembles verified
plant operational context, routes intent to appropriate tooling, and presents
responses that are traceable to structured data within the platform.

The AI orchestration layer operates as an assistive intelligence service.
It does not issue commands to operational systems, modify records autonomously,
or take actions outside of explicitly defined tool boundaries. All AI-generated
outputs that influence operational records require human review or explicit
approval within defined workflow boundaries.

---

## 2. Architectural Layers

The AI orchestration pipeline processes a user interaction through the following
sequential layers:

```
User Prompt
    │
    ▼
Intent Resolver
    │
    ▼
Context Builder
    │
    ▼
Tool Router
    │
    ▼
Model Interface
    │
    ▼
Response Formatter
    │
    ▼
User Interface (SignalR / HTTP)
```

### 2.1 User Prompt

The user prompt is the natural language input submitted by an operator, maintainer,
supervisor, or administrator through the platform UI. Prompts may be free-form
questions, structured operational queries, or commands directed at specific
workflow-integrated assistant functions.

Prompts are associated with a session context that carries the current tenant
identity, user role, active module context (maintenance, production, parts, etc.),
and optional entity context such as the active incident ID or equipment asset ID.

### 2.2 Intent Resolver

The intent resolver classifies the incoming prompt to determine the appropriate
handling pathway. Classification may be performed through lightweight local
inference, rule-based keyword matching, or a structured prompt submitted to the
configured model interface.

Supported intent categories include:

| Intent Class | Description |
|---|---|
| `incident.summarize` | Summarize an active or historical maintenance incident |
| `incident.diagnose` | Identify probable causes and recommended actions |
| `equipment.history` | Retrieve and summarize equipment maintenance history |
| `parts.lookup` | Answer parts availability, BOM context, or manufacturer reference queries |
| `production.status` | Summarize current or recent production activity |
| `workforce.context` | Surface responder availability or assignment context |
| `knowledge.search` | Search operational knowledge, documentation, or historical context |
| `general.assist` | Handle conversational or unclassified operational queries |

Unrecognized intent falls through to `general.assist` handling. The intent resolver
does not reject ambiguous input; it degrades gracefully to a broader context assembly.

### 2.3 Context Builder

The context builder assembles structured operational context from the platform
data layer before any model inference is invoked. Context is tenant-scoped and
entity-scoped, ensuring that the model receives real plant data relevant to the
query rather than relying on general-purpose training knowledge.

Context builders are composable and domain-specific. The following context
providers are registered in the orchestration pipeline:

**IncidentContextProvider**
Retrieves the active maintenance request or incident record including description,
current status, assigned responder, work center, and equipment reference.

**EquipmentContextProvider**
Retrieves the equipment asset record, maintenance history, linked BOM entries,
associated parts usage, and recent work order activity for the asset in question.

**PartsContextProvider**
Retrieves parts records, inventory levels, bin locations, BOM linkage, and
manufacturer reference data relevant to the active maintenance or parts query.

**WorkforceContextProvider**
Retrieves current responder assignments, user role context, and team structure
relevant to the active incident or operational query.

**KnowledgeContextProvider**
Retrieves relevant documentation fragments, historical incident summaries, and
operational knowledge records through semantic search over stored embeddings.

**IncidentVectorSearch**
Performs vector similarity search over stored incident embeddings to surface
historically similar incidents and their resolutions.

Context is assembled as a structured document that forms the system prompt prefix
passed to the model interface. Token budget management trims context sections in
priority order when the assembled context exceeds the configured model token limit.

### 2.4 Tool Router

The tool router is invoked when the intent resolver or model interface determines
that a structured tool call is appropriate for fulfilling the user request. Tools
are discrete, bounded functions that execute queries against the operational data
layer or invoke specific workflow actions.

Tool invocations are logged in the AI tool audit table with the invoking session ID,
tool name, input parameters, output summary, and timestamp. This audit trail supports
operational review, debugging, and accountability for AI-assisted actions.

Supported tool categories include:

- **Read tools** — query maintenance history, equipment records, parts availability,
  workforce assignments, and production context
- **Summarization tools** — generate structured summaries of incident history or
  equipment maintenance trends
- **Recommendation tools** — produce ranked corrective action suggestions based on
  historical patterns and equipment context
- **Escalation tools** — surface escalation thresholds or recommend escalation
  based on response time and severity context

Write tools that modify operational records are not available through the AI
assistant interface without an explicit human-approval workflow wrapping the
tool invocation.

### 2.5 Model Interface

The model interface is an abstraction layer that decouples the orchestration
pipeline from any specific AI model backend. The `IChatModel` interface defines
a single async invocation contract that accepts a structured prompt and returns
a text completion.

Supported model backends are registered through the dependency injection
configuration:

**OllamaChatModel**
Invokes a locally-hosted LLM through the Ollama runtime. Supports any model
available in the local Ollama model registry. Appropriate for environments with
data residency requirements, air-gapped deployments, or low-latency local
inference requirements.

**ClaudeChatModel**
Invokes Anthropic Claude through the Messages API. Appropriate for production
environments where higher reasoning capability is required and external model
access is acceptable under organizational data governance policy.

**NullChatModel**
A no-op implementation that returns a static placeholder response. Used when
no model backend is configured or when AI features are explicitly disabled for
a deployment environment.

The active model backend is selected through application configuration and can
be changed without modifying application code. Both local and external backends
implement the same `IChatModel` interface.

### 2.6 Response Formatter

The response formatter post-processes the raw model output before returning it
to the user interface. Formatting responsibilities include:

- Applying Markdown-to-HTML conversion where the UI expects rendered output
- Stripping model reasoning artifacts or internal chain-of-thought output that
  should not be presented to users
- Appending data attribution context where responses are grounded in specific
  retrieved records
- Applying content safety filters where configured

---

## 3. Local and Hybrid Inference

### 3.1 Local Inference

Manufacturing Core Suite supports fully local inference through the Ollama runtime.
When configured for local inference:

- No user data, operational records, or prompt content leaves the local runtime
  environment
- Inference latency is bounded by local hardware capability
- Model selection is limited to models downloaded to the local Ollama instance
- The orchestration pipeline behaves identically to remote inference from the
  application code perspective

Local inference is the recommended configuration for environments with strict
data residency requirements or where network connectivity to external AI services
is restricted.

### 3.2 Hybrid Inference

In hybrid configurations, the intent resolver may route different intent classes
to different model backends based on sensitivity, complexity, or latency
requirements. Low-sensitivity summarization queries may be handled locally while
complex diagnostic reasoning queries are routed to a more capable external model.

Hybrid routing configuration is expressed through the application settings layer
and does not require code changes.

### 3.3 Offline Capability

When no model backend is available — due to network conditions, configuration
absence, or explicit disablement — the `NullChatModel` implementation ensures the
application continues to function. AI-assisted features degrade gracefully with
a user-visible indicator rather than producing application errors.

---

## 4. SignalR Real-Time Interaction

AI assistant interactions are delivered through SignalR WebSocket connections to
support streaming response display and real-time update behavior.

When a user submits a prompt through the AI assistant interface:

1. The prompt is dispatched to the orchestration pipeline via the AI controller
2. A SignalR hub connection carries response tokens or completion events back to
   the client as they become available
3. The client UI renders response content progressively rather than waiting for
   the full completion to be available
4. Session state is maintained on the server and referenced by session ID,
   allowing conversation history to inform subsequent turns

SignalR connections are tenant-scoped and authenticated. No cross-tenant message
delivery is possible through the hub infrastructure.

---

## 5. Conversation Session Management

AI conversations are persisted as `AiConversationSession` and `AiConversationMessage`
records in the operational database, scoped to the current tenant and user.
Session records support:

- Multi-turn conversation history providing context continuity across prompts
- Session replay for debugging and audit purposes
- User-initiated session clearing to reset context
- Platform administrator visibility into session activity for governance review

Session records are subject to tenant data isolation through EF Core global query
filters. Session data is not accessible across tenants or users.

---

## 6. AI Tool Audit

Every tool invocation executed by the AI orchestration pipeline is recorded in
the `AiToolAudit` table with the following fields:

| Field | Description |
|---|---|
| SessionId | Associated conversation session |
| ToolName | Identifier of the invoked tool |
| InputSummary | Sanitized representation of the tool input parameters |
| OutputSummary | Sanitized representation of the tool output |
| TenantId | Tenant scope of the invocation |
| UserId | Authenticated user who initiated the interaction |
| Timestamp | UTC timestamp of the invocation |

Tool audit records support operational governance review, incident investigation,
and compliance reporting. They are retained according to the configured data
retention policy and are not modifiable through normal application interfaces.

---

## 7. Guardrails and Operational Boundaries

The following constraints are enforced by the AI orchestration architecture:

- The AI orchestration layer does not issue direct commands to equipment or
  control systems
- AI-generated content is not written to operational records without explicit
  human confirmation or an approved workflow action
- All model invocations are logged with input context and output summary
- No cross-tenant data is included in any context assembly operation
- Prompt injection attempts that attempt to override system prompts or access
  out-of-scope data are mitigated through structured prompt assembly that
  separates system context from user input at the model interface layer
- AI assistant features may be disabled at the tenant configuration level without
  affecting any other platform functionality

---

## 8. Future Capabilities

The following capabilities are targeted for future development within the AI
orchestration layer:

**Predictive Maintenance Signal Analysis**
Integration with the asset telemetry layer will enable the AI orchestration pipeline
to analyze time-series equipment signals and generate early warning recommendations
before fault events occur.

**Automated Incident Pattern Detection**
Periodic background analysis of maintenance history to identify recurring equipment
failure patterns, surface unresolved root cause candidates, and generate trend
summaries for supervisor review.

**Retrieval-Augmented Generation over Documentation**
Indexing of maintenance procedures, equipment manuals, and operational knowledge
documents to enable AI-assisted documentation lookup alongside structured data
retrieval.

**Multi-Step Reasoning Workflows**
Structured agentic workflows where the AI orchestration layer executes a defined
sequence of tool invocations and reasoning steps to produce complex analytical
outputs such as root cause analysis reports or maintenance planning recommendations.
