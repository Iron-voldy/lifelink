# ADR-01: Domain and agent ownership

- Status: Accepted
- Date: 18 August 2026

## Context

Four students must each own a relational business component and a distinct agent that
they can independently explain, test, and demonstrate. The repository does not contain
the students' legal names, so stable student numbers are used in code and planning; the
group report must map those numbers to names before submission.

## Decision

| Owner | Business component | Agent |
|---|---|---|
| Student 1 | Donor Registry and Eligibility | Domain Analysis Agent |
| Student 2 | Hospital Requests and Triage | Coordinator Agent |
| Student 3 | Blood Inventory and Matching | Dispatch Agent |
| Student 4 | Camps and Notifications | Validation and Safety Agent |

Cross-cutting authentication, workflow persistence, clients, CI, deployment, and report
work must name both an implementer and reviewer in the project board. Pull requests and
individual reports must link each contribution to its owner and tests.

## Consequences

Ownership matches the component/agent pairing in the design document and provides a
clear viva boundary. Student numbers must be replaced or mapped to actual names in the
consolidated report; this cannot be inferred safely from repository contents.
