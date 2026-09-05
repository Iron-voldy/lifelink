# ADR 03: Flutter state management

- Status: Accepted
- Date: 2026-08-18

## Decision

Use Riverpod for application/session state, an explicit typed HTTP service for backend access, and `flutter_secure_storage` for refresh tokens. Access tokens remain in memory. UI routing and available operations are selected from the authenticated backend role.

## Consequences

State remains testable without widget context, backend calls stay centralized, and long-lived credentials are not placed in shared preferences. The application must restore sessions through refresh-token rotation and add feature-specific providers as the donor and hospital workflows are implemented.
