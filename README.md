# Zero – A Local-First Reasoning Agent

Zero is a contest-focused fork of the existing Progress ASP.NET Core application. It keeps the working application foundation and narrows the product around a local-first reasoning agent for personal productivity.

## Problem Solved

Modern assistants often depend on remote context, disconnected tools, or one-off chat sessions. Zero gives a user a local workspace where the assistant can reason over tasks, calendar context, finance data, email context, search results, saved memory, and notifications without replacing the existing application workflow.

## Features

- Zero Chat / AI assistant with saved local memory and conversation history.
- Dashboard for task, calendar, reminder, and finance visibility.
- Task and calendar management.
- Finance dashboard and transaction workflows.
- Email and calendar integration settings.
- Search-oriented local tools exposed through the assistant.
- Settings workspace for AI, memory, display, and integration preferences.
- Notification center and proactive reminders.
- Visible Demo Mode flag for contest presentation.

## Demo Scenarios

- Ask Zero to summarize current priorities from tasks, calendar events, reminders, and finance context.
- Add or edit a task, then show the update reflected in the dashboard and calendar.
- Save a memory item in Zero Chat and ask a follow-up that uses that local context.
- Review finance status from the dashboard and ask the assistant for a plain-English summary.
- Open settings to show local AI/provider configuration and demo-focused controls.

## Local Setup

1. Install the .NET SDK used by the project. The copied source currently targets `net10.0`.
2. Restore and build the solution:

```powershell
dotnet build .\Zero.ReasoningAgent.sln
```

3. Run the web app:

```powershell
dotnet run --project .\ScheduleApp\ScheduleApp.csproj
```

4. Open the local URL printed by `dotnet run`.

## Technology Stack

- ASP.NET Core MVC
- Entity Framework Core
- SQLite local database
- Bootstrap and Bootstrap Icons
- log4net
- OllamaSharp and Microsoft.Extensions.AI integration already present in the copied codebase
- Model Context Protocol server package already present in the copied codebase

## Contest Scope Note

This foundation does not add Microsoft Foundry, Microsoft Graph, or Copilot-specific code. Those integrations are intentionally deferred until after the copy, cleanup, rename, and contest-scoping step is stable.
