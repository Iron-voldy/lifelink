# LifeLink Entity-Relationship Diagram

This reviewed model is the source of truth for the initial EF Core migration. Audit timestamps, JSON payload columns, indexes, and check constraints remain authoritative in `LifeLinkDbContext`.

```mermaid
erDiagram
    USER ||--o| DONOR : has
    USER ||--o| HOSPITAL_STAFF : has
    USER ||--o{ REFRESH_TOKEN : owns
    USER ||--o{ DEVICE_TOKEN : registers
    HOSPITAL ||--o{ HOSPITAL_STAFF : employs
    HOSPITAL ||--o{ BLOOD_REQUEST : submits
    DONOR ||--o{ DONOR_ELIGIBILITY_HISTORY : records
    DONOR ||--o{ DONATION_RECORD : makes
    BLOOD_REQUEST ||--o{ REQUEST_STATUS_HISTORY : records
    BLOOD_BANK_LOCATION ||--o{ INVENTORY_LOT : stores
    BLOOD_REQUEST ||--o{ INVENTORY_RESERVATION : requires
    INVENTORY_LOT ||--o{ INVENTORY_RESERVATION : allocates
    INVENTORY_RESERVATION ||--o| DISPATCH_RECORD : becomes
    DONATION_CAMP ||--o{ CAMP_SLOT : offers
    DONOR ||--o{ CAMP_SLOT : books
    BLOOD_REQUEST ||--o{ AGENT_WORKFLOW_EXECUTION : retries
    AGENT_WORKFLOW_EXECUTION ||--o{ AGENT_STEP : contains
    AGENT_WORKFLOW_EXECUTION ||--o{ AGENT_APPROVAL : reviews
    AGENT_WORKFLOW_EXECUTION ||--o{ NOTIFICATION : authorizes
    USER ||--o{ NOTIFICATION : receives
```

## Important invariants

- Emails, hospital registration numbers, device tokens, reservation idempotency keys, notification idempotency keys, and workflow correlation IDs are unique.
- A user has at most one donor or hospital-staff profile.
- Inventory is lot-based and reservations are independently auditable, expiring, and concurrency-protected.
- A request can trigger multiple workflow attempts; `(request_id, attempt_number)` is unique.
- Agent plan/input/output/tool data is structured JSONB. Raw chain-of-thought and credentials are never stored.
- Audit-linked records use restricted deletes. Synthetic data only is permitted for development and demonstrations.
