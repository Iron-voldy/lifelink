# LifeLink Postman QA collection

Import `LifeLink-Complete-QA.postman_collection.json` into Postman and run the
entire collection in its displayed order. The requests chain JWTs, entity IDs,
row versions, workflow IDs, reservations, camp slots, and notification devices.

The collection targets the hosted backend:

- `https://lifelink-api.72-62-255-113.sslip.io`

The internal agent service is intentionally not called directly. Coordinator,
domain-analysis, dispatch, and validation/safety behavior is verified through
the backend's authenticated workflow endpoints.

The collection defaults to the documented hosted demo admin account. Change the
collection variables if the VPS uses a different demo password. `adminPassword`
and `qaPassword` should be marked as secret variables after import.

This is a stateful QA run. Each run creates timestamped donor, hospital,
request, inventory, workflow, camp, and audit data. Run it only against a
development/test database. The QA donor is deactivated at the end, while other
records remain intentionally available for database and audit-trail review.

To regenerate the collection after API changes:

```powershell
node postman/generate-lifelink-qa-collection.mjs
```
