# Development Roadmap

Based on BRD §16 and TAD §27. **Each step starts only after explicit approval from the product owner.** No test-project work is included (TESTING.md).

| Step | Content | BRD phase / TAD stage | Gate |
|---|---|---|---|
| 0 | Analysis & architecture documentation | Phase 0 | Approve ARCHITECTURE, ERD, permission matrix, navigation |
| 1 | **Foundation**: solution, BuildingBlocks, API host, Serilog, exception middleware, validation filter, Swagger, Docker Postgres, Angular shell | Stage 1 | Build + run + Swagger up |
| 2 | **Identity**: auth, users, roles, permissions, login history, Angular login + security screens | Phase 0 deliverable | Manual checklist |
| 3 | **Audit** | Phase 0/1 | Manual checklist |
| 4 | **Administration** | Phase 1 | Manual checklist |
| 5 | **HR basic (Employees)** | Phase 1 | Manual checklist |
| 6 | **Patient** (architecture reference module) | Phase 1 / Stage 2 | Proves the architecture before anything else |
| 7 | Doctor profile + **Appointment & Queue** | Phase 2 / Stage 3 | First cross-module contract |
| 8 | **Clinical + OPD** | Phase 2 | |
| 9 | **Billing & Payments** | Phase 3 | Idempotency, financial immutability |
| 10 | **IPD + Bed + Nursing** | Phase 4 | Cross-schema transaction design |
| 11 | Laboratory, Radiology | Phase 5 | |
| 12 | Pharmacy, Inventory, Procurement | Phase 6 | |
| 13 | HR full, Asset | Phase 7 | |
| 14 | Emergency, OT, Blood Bank, Ambulance (as operated) | Phase 8 | |
| 15 | Finance & Reporting | Phase 9 | |
| 16 | Production readiness | Phase 10 | |
