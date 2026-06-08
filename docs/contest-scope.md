# Contest Scope

## Product

Zero â€“ A Local-First Reasoning Agent

## Scope For This Foundation

This repository is a copied and narrowed version of the existing Progress application. The first milestone preserves the working ASP.NET Core app and focuses on contest presentation readiness.

Included demo modules:

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

Hidden or excluded from the visible demo surface:

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

## Explicitly Deferred

- Microsoft Foundry integration
- Microsoft Graph integration
- Copilot-specific implementation
- Namespace-wide rename
- Deep deletion of internal code paths

The current cleanup prefers hiding non-demo surfaces instead of deleting internals so the copied app remains buildable and runnable.

