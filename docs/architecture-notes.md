# Architecture Notes

## Current Foundation

Zero is currently a contest-focused copy of the existing Progress MVC application. The project folder and assembly are now `AgentZero`, while the original `ScheduleApp` namespaces remain in place to reduce migration risk during the first cleanup task.

## Application Layers

- MVC controllers and Razor views provide the main dashboard, task, calendar, finance, email, settings, and Zero Chat surfaces.
- Services hold business logic for scheduling, dashboard summaries, finance, notifications, AI settings, local tools, memory, and assistant workflows.
- Entity Framework Core uses SQLite for the local database.
- Data protection keys and logs are kept under `App_Data`.

## Demo Mode

`appsettings.json` contains:

```json
"DemoMode": true
```

The shared layout reads this flag and displays a Demo Mode badge in the application header.

## Cleanup Strategy

Task 1 intentionally hides non-demo modules from navigation and settings instead of deleting dependent code. This keeps the copied application compiling while creating a clear contest surface.

## Deferred Renames

Namespaces, project names, cookie names, data-protection names, database table names, and internal identifiers remain unchanged for now. These can be renamed in a later migration after the contest foundation is stable.

