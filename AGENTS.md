# Agent Guidance

## Product Boundary

This repository is for a Windows-local "execution continuity saver", not a generic to-do list. It helps a person resume a route they prepared while clear-headed when they lose context, freeze, or become distracted.

The governing product rule is:

> Planning mode writes the route. Guide mode only reads, executes, pauses, and captures. Guide mode does not re-plan, re-choose, or require the user to explain their state.

Guide mode is an interaction mode the user enters explicitly or the application enters during recovery. It is not a medical state. Do not infer, diagnose, or automatically determine Guide mode from behavior, medical information, brain state, or any other personal signal.

## Non-Negotiable Behavior

- The system may store many routes, projects, paused plans, inbox entries, and archives, but it has at most one active route. `activeRouteId` is the sole source of truth: when it points to a route, that route is the only route with lifecycle status `active`; when it is null, no route may remain `active` and the no-active-route state is valid and required.
- Guide mode shows only the active route's one executable next action. It must not expose a choice of routes, tasks, priorities, or suggestions.
- Capturing an idea must persist the raw text and timestamp before confirming success, then return to the same current action. It may only add an inbox entry; it must never change `activeRouteId`, `currentStepId`, route lifecycle, step completion, or an existing execution snapshot.
- Pausing must persist a complete return anchor even when the user leaves no note.
- A completed fallback returns to the original current step. It must never complete, skip, or replace that step; only an explicit completion against the original step's completion standard can complete it.
- When stuck, use the step's single pre-written fallback. Without one, allow one sentence describing the block, save a pause anchor, and reduce choices to returning or pausing.
- Completing the final step completes and preserves the route, clears the active route, and shows a safe no-active-route state. Never auto-activate another route.
- In the no-active-route state, entering planning mode is the primary entry point and capture is an always-available secondary entry point. Such a capture must not create, activate, or switch a route.
- Switching active routes is allowed only in planning mode. Snapshot the old route first; guide mode and capture can never switch routes.

## Scope Guardrails

- Local-first and offline-capable are required for MVP-0 through MVP-2.
- Do not add accounts, cloud sync, collaboration, gamification, scoring, analytics dashboards, notification spam, automatic AI, or autonomous prioritization.
- User-triggered AI decomposition is an optional MVP-3 capability only; it cannot be required by earlier phases.
- Preserve captured ideas so they can be reviewed, converted, or archived in planning mode.
- Archiving is reversible. Inbox entries, routes, steps, and execution snapshots must never be implicitly deleted. Deletion requires explicit user initiation and confirmation; interface simplification, automatic cleanup, and state transitions are never deletion reasons.

## Engineering Guardrails

- Model route state explicitly and test state transitions before UI integrations.
- Use atomic, verified durable writes: a success confirmation may appear only after the local storage layer confirms the relevant write. The eventual implementation may use database transactions or atomic-file replacement according to the approved storage choice.
- Keep technical choices documented as confirmed, proposed, or open. Do not convert candidates into facts without an explicit decision.
- Do not create application code in this documentation phase.
- Before implementation, read the approved product specification, acceptance criteria, and implementation plan. Map every testable acceptance criterion to automated coverage or a manual acceptance script. Do not claim implementation completion without actual test and build evidence.

## Agent Collaboration

- Follow `docs/AGENT_COLLABORATION_PLAYBOOK.md` for prompts, handoffs, same-workspace coordination, independent worktrees, and cross-computer work.
- Treat the playbook as an operational protocol; treat `PRODUCT_SPEC.md`, `ACCEPTANCE.md`, and `IMPLEMENTATION_PLAN.md` as the product and delivery authorities.
