# ADR-02: React State Management

- Status: Accepted
- Date: 18 August 2026

## Context

The React client is an administrative console dominated by server-owned data: donor records, request queues, inventory, camps, and persisted agent workflows. It also has a small amount of client-owned state: the authenticated administrator and short-lived view filters.

## Options Considered

- Redux Toolkit for all local and remote state.
- Zustand for client state plus handwritten request caching.
- React Context for session state and TanStack Query for remote state.

## Decision

Use React Context for the small authentication/session boundary and TanStack Query for server state, caching, invalidation, background refresh, and mutation feedback. Keep ephemeral filters and selections local to each screen.

## Consequences

- Server data has one consistent cache and explicit invalidation after mutations.
- The workflow monitor can poll active executions without a custom global store.
- Authentication remains small and auditable instead of becoming a general-purpose state container.
- The team must keep business authority in the API; cached client data is never treated as authoritative.
- If substantial cross-screen client-only workflows emerge, the decision should be revisited before adding ad-hoc context providers.
