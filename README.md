# Zero - A Local-First Reasoning Agent

Zero is an ASP.NET Core web application that turns a personal productivity workspace into a local-first reasoning agent. It is built from the existing Progress application foundation and narrowed for a Microsoft Agents League contest demo around Zero Chat, memory, tasks, calendar, search, finance, email, settings, dashboard, and notifications.

The goal is not to create another generic chatbot. Zero gives the assistant access to the user's local workspace context so it can reason over real tasks, schedules, reminders, saved memory, finance records, and configured search results.

## Project Purpose

Most AI assistant demos are disconnected from the tools people actually use every day. They answer questions, but they do not understand the user's current workload, local notes, calendar pressure, reminders, or operational context.

Zero addresses that by combining:

- A local-first workspace for daily planning and personal operations.
- A reasoning assistant that can use saved memory and recent conversation context.
- Existing app modules for tasks, calendar, finance, email, search, settings, dashboard, and notifications.
- A demo-ready UI with visible Zero branding and Demo Mode.

## Product Name

**Zero - A Local-First Reasoning Agent**

Zero is positioned as a practical local assistant for people who want AI help inside their own working context rather than a detached chat window.

## Core Features

### Zero Chat

Zero Chat is the main assistant interface. It supports text interaction, conversation history, saved memory, local tool actions, and optional voice-related settings already present in the copied application.

Key capabilities:

- Ask questions about local workspace context.
- Use saved memory during future conversations.
- Ask for task, calendar, finance, and notification summaries.
- Use configured web search for current public information.
- Keep the assistant workflow inside the local app.

### Memory

Zero includes saved memory so the assistant can remember persistent user preferences or important context.

Examples:

- Preferred timezone.
- Normal planning style.
- Important project constraints.
- Personal reminders or operating preferences.

### Dashboard

The dashboard gives a quick overview of daily work and context. It keeps the contest demo grounded in real application data instead of only assistant responses.

Included dashboard areas:

- Task progress and status.
- Upcoming work.
- Priority focus.
- Reminders.
- Finance summary widgets.
- Calendar-adjacent planning context.

### Tasks

The task system is based on the existing scheduling/event workflow. It supports creating, editing, tracking, and organizing work items.

Task data is used by both the dashboard and assistant context.

### Calendar

The calendar surface provides schedule visibility and supports daily planning. It works with task/event data and display preferences configured in Settings.

### Finance

The finance module provides local finance visibility for the demo. It supports accounts, budgets, categories, recurring items, and transactions from the copied application.

Zero can use this data for practical summaries such as monthly expense overviews or upcoming finance context.

### Email

The email hub and integration settings remain part of the contest scope. The current foundation keeps the existing email/calendar integration configuration without adding Microsoft Graph yet.

### Search

The Search page is a simple UI over the existing SearXNG-backed search service. Search is also available to the assistant workflow for public, current information when configured.

### Settings

Settings centralizes user preferences and assistant configuration.

Contest-visible settings include:

- Calendar and display preferences.
- AI assistant settings.
- Email and calendar integration settings.
- Zero-related local configuration.

Non-demo operational settings were hidden from the visible navigation/settings surface for the first contest foundation.

### Notifications

Zero includes a notification center and proactive reminder workflow from the existing app. This helps demonstrate that the agent is connected to actual user context and not only a standalone chat screen.

## Demo Mode

The application includes a visible Demo Mode flag in `AgentZero/appsettings.json`:

```json
"DemoMode": true
```

When enabled, the shared app layout displays a Demo Mode badge in the header. This makes the contest scope explicit during presentation.

## Demo Scenarios

### Scenario 1: Daily Priority Briefing

Open the dashboard and ask Zero Chat:

```text
What should I focus on today?
```

Zero should use the local workspace context, such as tasks, calendar items, reminders, and finance signals, to produce a grounded summary.

### Scenario 2: Task Creation And Tracking

Create or edit a task, then show it in the Tasks or Calendar surface. This demonstrates that Zero is connected to the existing productivity workflow.

Example prompt:

```text
Create a task to prepare my demo checklist tomorrow at 9 AM.
```

### Scenario 3: Memory-Aware Follow-up

Save a memory item, then ask a follow-up that depends on it.

Example:

```text
Remember that I prefer planning in Auckland time.
```

Then:

```text
Schedule my next focus block for tomorrow morning.
```

### Scenario 4: Finance Context Summary

Open Finance or ask Zero for a financial summary.

Example:

```text
Summarize this month's business expenses.
```

### Scenario 5: Search For Current Information

Use the Search page or ask Zero for current public information when SearXNG is configured.

Example:

```text
Search for the latest ASP.NET Core release notes.
```

### Scenario 6: Settings And Demo Mode

Open Settings and show the local assistant configuration, display preferences, integration settings, and the visible Demo Mode badge.

## Architecture Overview

The repository folder, solution, project folder, project file, and assembly are named `AgentZero`. The original `ScheduleApp` namespaces remain in code for now to reduce migration risk during the contest foundation stage.

High-level structure:

- `AgentZero/Controllers` - MVC controllers and API endpoints.
- `AgentZero/Views` - Razor views for dashboard, tasks, calendar, finance, email, settings, search, and Zero Chat.
- `AgentZero/Services` - Business logic for assistant workflows, local tools, memory, dashboard, notifications, finance, search, and settings.
- `AgentZero/Data` - Entity Framework Core database context and data initialization.
- `AgentZero/Models` - Domain models and view models.
- `AgentZero/wwwroot` - Static CSS, JavaScript, icons, logos, and web manifest.
- `docs` - Contest scope, architecture notes, and demo scenario notes.

## Local-First Data

The copied foundation uses SQLite for the local database. The application creates and uses app data under `AgentZero/App_Data`.

The repo currently includes existing demo/local data where available. Additional seed data should only be added when existing data is not enough for a clear demo.

## Technology Stack

- ASP.NET Core MVC
- .NET target currently configured as `net10.0`
- Entity Framework Core
- SQLite
- Bootstrap
- Bootstrap Icons
- JavaScript and Razor views
- log4net
- OllamaSharp
- Microsoft.Extensions.AI
- Model Context Protocol package references already present in the copied project

## Local Setup

### Prerequisites

- Windows or any environment that can run the configured .NET SDK.
- .NET SDK compatible with the project target.
- Optional SearXNG instance if you want live web search.
- Optional local or configured AI provider depending on the assistant settings.

### Build

From the repository root:

```powershell
dotnet build .\AgentZero.sln
```

### Run

```powershell
dotnet run --project .\AgentZero\AgentZero.csproj
```

Open the local URL printed by `dotnet run`.

### App Data

The application uses:

```text
AgentZero/App_Data
```

for local database-related runtime files, data protection keys, and logs.

## Contest Scope

This foundation intentionally focuses on copy, cleanup, branding, and contest readiness.

Included visible modules:

- Zero Chat / AI assistant
- Dashboard
- Tasks
- Calendar
- Finance
- Email
- Search
- Memory
- Settings
- Notifications

Hidden from the visible demo surface:

- FlowQ
- SCADA
- S3
- Nx Witness
- Milestone
- Robot control
- Experimental video/security modules
- Trading
- Maintenance/projects admin surface
- Operational database and market-data admin tabs

## Explicitly Not Added Yet

This task does not add:

- Microsoft Foundry
- Microsoft Graph
- Copilot-specific code

Those integrations are intentionally deferred until the base contest project is stable.

## Current Build Status

The solution builds successfully with:

```powershell
dotnet build .\AgentZero.sln
```



