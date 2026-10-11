import fs from 'node:fs';

const jsonHeaders = [{ key: 'Content-Type', value: 'application/json' }];
const testEvent = (lines) => [{ listen: 'test', script: { exec: lines } }];
const auth = (token = 'adminToken') => ({ type: 'bearer', bearer: [{ key: 'token', value: `{{${token}}}`, type: 'string' }] });
const req = (name, method, path, { body, token = 'adminToken', noAuth = false, tests = [], description = '' } = {}) => ({
  name,
  request: {
    method,
    header: body === undefined ? [] : jsonHeaders,
    ...(noAuth ? { auth: { type: 'noauth' } } : { auth: auth(token) }),
    url: `{{baseUrl}}${path}`,
    ...(body === undefined ? {} : { body: { mode: 'raw', raw: JSON.stringify(body, null, 2), options: { raw: { language: 'json' } } } }),
    description,
  },
  event: testEvent([
    "pm.test('Response time is acceptable', () => pm.expect(pm.response.responseTime).to.be.below(5000));",
    ...tests,
  ]),
});
const ok = (code = 200) => `pm.test('HTTP ${code}', () => pm.response.to.have.status(${code}));`;
const save = (field, variable) => `pm.collectionVariables.set('${variable}', pm.response.json().${field});`;
const problem = (code) => `pm.test('ProblemDetails ${code}', () => { const j=pm.response.json(); pm.expect(j.title).to.eql('${code}'); pm.expect(pm.response.headers.get('Content-Type')).to.include('application/problem+json'); });`;

const collection = {
  info: {
    _postman_id: '2f79ca8e-51e7-4c6b-b446-f66ee6d4850c',
    name: 'LifeLink Complete QA - CRUD, Agents, Validation & Safety',
    description: 'Sequential QA collection for the hosted LifeLink API. Run the complete collection in order. It creates timestamped QA records and intentionally leaves audit records in PostgreSQL. Agent behavior is exercised through the backend workflow endpoints because the internal agent service is not publicly exposed.',
    schema: 'https://schema.getpostman.com/json/collection/v2.1.0/collection.json',
  },
  variable: [
    { key: 'baseUrl', value: 'https://lifelink-api.72-62-255-113.sslip.io' },
    { key: 'adminEmail', value: 'admin01@demo.lifelink.local' },
    { key: 'adminPassword', value: 'Demo!Pass12345', type: 'secret' },
    { key: 'qaPassword', value: 'Qa!LifeLink2026Strong', type: 'secret' },
  ],
  item: [
    {
      name: '00 - Setup, Health & Authentication',
      item: [
        {
          ...req('Initialize QA run + API health', 'GET', '/health', { noAuth: true, tests: [ok(200), "pm.test('Health body returned', () => pm.expect(pm.response.text()).to.not.be.empty);"], description: 'Starts a unique data run. Execute this first.' }),
          event: [
            { listen: 'prerequest', script: { exec: [
              "const id = Date.now().toString();",
              "pm.collectionVariables.set('runId', id);",
              "pm.collectionVariables.set('donorEmail', `qa.donor.${id}@example.test`);",
              "pm.collectionVariables.set('hospitalEmail', `qa.hospital.${id}@example.test`);",
              "pm.collectionVariables.set('requiredByUtc', new Date(Date.now()+24*3600*1000).toISOString());",
              "const campDay = new Date(Date.now()+48*3600*1000); campDay.setUTCHours(3, 30, 0, 0); // 09:00 Sri Lanka time; camps must run 06:00-21:00",
              "pm.collectionVariables.set('campStartUtc', campDay.toISOString());",
              "pm.collectionVariables.set('campEndUtc', new Date(campDay.getTime()+2*3600*1000).toISOString());",
              "pm.collectionVariables.set('expiryDate', new Date(Date.now()+14*86400000).toISOString().slice(0,10));",
              "pm.collectionVariables.set('donationDate', new Date(Date.now()-100*86400000).toISOString().slice(0,10));",
            ] } },
            ...testEvent([ok(200)]),
          ],
        },
        req('Login - Blood bank admin', 'POST', '/api/auth/login', { noAuth: true, body: { email: '{{adminEmail}}', password: '{{adminPassword}}', deviceName: 'Postman QA' }, tests: [ok(200), save('accessToken','adminToken'), save('refreshToken','adminRefreshToken'), "pm.test('JWT token pair returned', () => { const j=pm.response.json(); pm.expect(j.accessToken).to.be.a('string').and.not.empty; pm.expect(j.refreshToken).to.be.a('string').and.not.empty; });"] }),
        req('Admin identity', 'GET', '/api/auth/me', { tests: [ok(200), "pm.test('Admin role', () => pm.expect(pm.response.json().role).to.eql('BloodBankAdmin'));", save('id','adminUserId')] }),
        req('Negative - login wrong password', 'POST', '/api/auth/login', { noAuth: true, body: { email: '{{adminEmail}}', password: 'DefinitelyWrong!123', deviceName: 'Postman QA' }, tests: [ok(401), problem('invalid_credentials')] }),
        req('Negative - protected endpoint without JWT', 'GET', '/api/donors', { noAuth: true, tests: [ok(401)] }),
      ],
    },
    {
      name: '01 - Donor CRUD & Domain Analysis Agent',
      item: [
        req('Register QA donor user', 'POST', '/api/auth/register', { noAuth: true, body: { email: '{{donorEmail}}', password: '{{qaPassword}}', role: 'Donor', deviceName: 'Postman Donor' }, tests: [ok(201), save('accessToken','donorToken'), save('refreshToken','donorRefreshToken')] }),
        req('Donor identity', 'GET', '/api/auth/me', { token: 'donorToken', tests: [ok(200), save('id','donorUserId'), "pm.test('Donor role', () => pm.expect(pm.response.json().role).to.eql('Donor'));" ] }),
        req('Create donor profile', 'POST', '/api/donors', { token: 'donorToken', body: { bloodType: 'OPositive', dateOfBirth: '1995-05-20', address: 'QA Donor Address, Colombo', latitude: 6.9271, longitude: 79.8612, medicalFlags: [] }, tests: [ok(201), save('id','donorId'), "pm.test('Profile defaults pending and active', () => { const j=pm.response.json(); pm.expect(j.eligibilityStatus).to.eql('PendingVerification'); pm.expect(j.isActive).to.eql(true); });"] }),
        req('Read own donor profile', 'GET', '/api/donors/me', { token: 'donorToken', tests: [ok(200), "pm.test('Correct donor ID', () => pm.expect(pm.response.json().id).to.eql(pm.collectionVariables.get('donorId')));" ] }),
        req('Update donor profile', 'PUT', '/api/donors/{{donorId}}', { token: 'donorToken', body: { bloodType: 'OPositive', dateOfBirth: '1995-05-20', address: 'Updated QA Address, Colombo', latitude: 6.93, longitude: 79.86, medicalFlags: [] }, tests: [ok(200), "pm.test('Address changed', () => pm.expect(pm.response.json().address).to.include('Updated'));" ] }),
        req('Evaluate donor eligibility (self)', 'POST', '/api/donors/{{donorId}}/check-eligibility', { token: 'donorToken', tests: [ok(200), "pm.test('Self-evaluation remains safe', () => pm.expect(['PendingVerification','TemporarilyIneligible','PermanentlyIneligible']).to.include(pm.response.json().status));" ] }),
        req('Evaluate donor eligibility (admin verification)', 'POST', '/api/donors/{{donorId}}/check-eligibility', { tests: [ok(200), "pm.test('Healthy donor eligible after staff check', () => pm.expect(pm.response.json().status).to.eql('Eligible'));" ] }),
        req('Eligibility audit history', 'GET', '/api/donors/{{donorId}}/eligibility-history', { token: 'donorToken', tests: [ok(200), "pm.test('History is non-empty', () => pm.expect(pm.response.json()).to.be.an('array').that.is.not.empty);" ] }),
        req('Admin list/filter donors', 'GET', '/api/donors?search={{donorEmail}}&bloodType=OPositive&eligibilityStatus=Eligible&page=1&pageSize=20', { tests: [ok(200), "pm.test('Created donor found', () => pm.expect(pm.response.json().items.some(x => x.id === pm.collectionVariables.get('donorId'))).to.eql(true));" ] }),
        req('Record donation (admin)', 'POST', '/api/donors/{{donorId}}/donations', { body: { donationDate: '{{donationDate}}', units: 1, location: 'QA Blood Centre', notes: 'Postman automated QA' }, tests: [ok(201), save('id','donationId')] }),
        req('Donation history', 'GET', '/api/donors/{{donorId}}/donation-history', { token: 'donorToken', tests: [ok(200), "pm.test('Donation persisted', () => pm.expect(pm.response.json().some(x => x.id === pm.collectionVariables.get('donationId'))).to.eql(true));" ] }),
        req('Negative - invalid donor latitude and missing address', 'PUT', '/api/donors/{{donorId}}', { token: 'donorToken', body: { bloodType: 'OPositive', dateOfBirth: '1995-05-20', address: '', latitude: 91, longitude: 79, medicalFlags: [] }, tests: [ok(400), problem('validation_failed')] }),
        req('Negative - donor cannot list all donors', 'GET', '/api/donors', { token: 'donorToken', tests: [ok(403)] }),
      ],
    },
    {
      name: '02 - Hospital Request CRUD & Coordinator Agent',
      item: [
        req('Register QA hospital user', 'POST', '/api/auth/register', { noAuth: true, body: { email: '{{hospitalEmail}}', password: '{{qaPassword}}', role: 'HospitalRequester', deviceName: 'Postman Hospital' }, tests: [ok(201), save('accessToken','hospitalToken')] }),
        req('Hospital requester identity', 'GET', '/api/auth/me', { token: 'hospitalToken', tests: [ok(200), save('id','hospitalUserId')] }),
        req('Register hospital profile', 'POST', '/api/hospitals/register', { token: 'hospitalToken', body: { name: 'QA General Hospital {{runId}}', registrationNumber: 'QA-HOSP-{{runId}}', address: 'QA Hospital Road, Colombo', latitude: 6.9, longitude: 79.9, position: 'QA Medical Officer' }, tests: [ok(201), save('id','hospitalId'), "pm.test('Starts pending verification', () => pm.expect(pm.response.json().verificationStatus).to.eql('Pending'));" ] }),
        req('Read own hospital', 'GET', '/api/hospitals/me', { token: 'hospitalToken', tests: [ok(200)] }),
        req('Negative - unverified hospital cannot request blood', 'POST', '/api/requests', { token: 'hospitalToken', body: { bloodType: 'APositive', quantityUnits: 2, requestedUrgency: 'Urgent', notes: 'Must fail before verification', requiredByUtc: '{{requiredByUtc}}' }, tests: [ok(400), problem('hospital_not_verified')] }),
        req('Admin verifies hospital', 'PUT', '/api/hospitals/{{hospitalId}}/verification', { body: { status: 'Verified' }, tests: [ok(200), "pm.test('Hospital verified', () => pm.expect(pm.response.json().verificationStatus).to.eql('Verified'));" ] }),
        req('Create blood request (triggers coordinator workflow)', 'POST', '/api/requests', { token: 'hospitalToken', body: { bloodType: 'APositive', quantityUnits: 2, requestedUrgency: 'Urgent', notes: 'QA request for coordinator and dispatch analysis', requiredByUtc: '{{requiredByUtc}}' }, tests: [ok(201), save('id','requestId'), "pm.test('Agent triage moved request forward', () => pm.expect(['PendingApproval','Escalated']).to.include(pm.response.json().status));" ] }),
        req('Read request', 'GET', '/api/requests/{{requestId}}', { token: 'hospitalToken', tests: [ok(200), "pm.test('Hospital owns request', () => pm.expect(pm.response.json().hospitalId).to.eql(pm.collectionVariables.get('hospitalId')));" ] }),
        req('List own requests (hospitalId override ignored)', 'GET', '/api/requests?hospitalId=00000000-0000-0000-0000-000000000000&page=1&pageSize=20', { token: 'hospitalToken', tests: [ok(200), "const items=pm.response.json().items; pm.test('Only own hospital data', () => pm.expect(items.every(x => x.hospitalId === pm.collectionVariables.get('hospitalId'))).to.eql(true)); const qa=items.find(x => x.notes === 'QA request for coordinator and dispatch analysis'); if(qa) pm.collectionVariables.set('requestId', qa.id);" ] }),
        req('Request status audit history', 'GET', '/api/requests/{{requestId}}/history', { token: 'hospitalToken', tests: [ok(200), "pm.test('Triage created status history', () => pm.expect(pm.response.json()).to.be.an('array').that.is.not.empty);" ] }),
        req('Request summary (hospital scoped)', 'GET', '/api/requests/reports/summary', { token: 'hospitalToken', tests: [ok(200), "pm.test('Summary contains request', () => pm.expect(pm.response.json().total).to.be.at.least(1));" ] }),
        req('Negative - invalid request quantity', 'POST', '/api/requests', { token: 'hospitalToken', body: { bloodType: 'APositive', quantityUnits: 0, requestedUrgency: 'Urgent', notes: 'invalid', requiredByUtc: '{{requiredByUtc}}' }, tests: [ok(400), problem('validation_failed')] }),
        req('Negative - donor cannot list hospital requests', 'GET', '/api/requests', { token: 'donorToken', tests: [ok(403)] }),
        req('Create cancellable request', 'POST', '/api/requests', { token: 'hospitalToken', body: { bloodType: 'ABPositive', quantityUnits: 90, requestedUrgency: 'Routine', notes: 'QA cancellation path', requiredByUtc: '{{requiredByUtc}}' }, tests: [ok(201), save('id','cancelRequestId')] }),
        req('Locate cancellable request', 'GET', '/api/requests?page=1&pageSize=100', { token: 'hospitalToken', tests: [ok(200), "const x=pm.response.json().items.find(x => x.notes === 'QA cancellation path'); pm.test('Cancellable request persisted', () => pm.expect(x).to.exist); if(x) pm.collectionVariables.set('cancelRequestId', x.id);" ] }),
        req('Close/cancel request', 'DELETE', '/api/requests/{{cancelRequestId}}', { token: 'hospitalToken', tests: [ok(204)] }),
      ],
    },
    {
      name: '03 - Inventory CRUD, Matching & Dispatch Agent',
      item: [
        req('Create blood bank location', 'POST', '/api/inventory/locations', { body: { name: 'QA Centre {{runId}}', address: 'QA Inventory Road, Colombo', latitude: 6.92, longitude: 79.87 }, tests: [ok(201), save('id','locationId')] }),
        req('List locations', 'GET', '/api/inventory/locations', { tests: [ok(200), "pm.test('Location persisted', () => pm.expect(pm.response.json().some(x => x.id === pm.collectionVariables.get('locationId'))).to.eql(true));" ] }),
        req('Update location', 'PUT', '/api/inventory/locations/{{locationId}}', { body: { name: 'QA Centre Updated {{runId}}', address: 'Updated Inventory Road, Colombo', latitude: 6.921, longitude: 79.871 }, tests: [ok(200)] }),
        req('Stock in compatible blood', 'POST', '/api/inventory/stock-in', { body: { locationId: '{{locationId}}', bloodType: 'APositive', units: 10, expiryDate: '{{expiryDate}}', source: 'QA Donation Batch {{runId}}' }, tests: [ok(201), save('id','lotId'), save('version','lotVersion')] }),
        req('List/filter inventory', 'GET', '/api/inventory?locationId={{locationId}}&bloodType=APositive&status=Available&includeExpired=false', { tests: [ok(200), "pm.test('Stock lot found', () => pm.expect(pm.response.json().items.some(x => x.id === pm.collectionVariables.get('lotId'))).to.eql(true));" ] }),
        req('Blood compatibility matching', 'GET', '/api/inventory/compatibility/APositive', { tests: [ok(200), "pm.test('A+ accepts A+/A-/O+/O-', () => pm.expect(pm.response.json().compatibleDonorTypes).to.include.members(['APositive','ANegative','OPositive','ONegative']));" ] }),
        req('Adjust lot with optimistic version', 'PUT', '/api/inventory/{{lotId}}/adjust', { body: { unitsAvailable: 9, expectedVersion: '{{lotVersion}}' }, tests: [ok(200), save('version','lotVersion'), "pm.test('Units adjusted', () => pm.expect(pm.response.json().unitsAvailable).to.eql(9));" ] }),
        req('Negative - stale version conflict', 'PUT', '/api/inventory/{{lotId}}/adjust', { body: { unitsAvailable: 8, expectedVersion: 1 }, tests: [ok(409), problem('concurrency_conflict')] }),
        req('Reserve stock for request', 'POST', '/api/inventory/reserve', { body: { bloodRequestId: '{{requestId}}', units: 2, idempotencyKey: 'qa-reserve-{{runId}}', holdMinutes: 30 }, tests: [ok(200), "const j=pm.response.json(); pm.test('Fully reserved', () => { pm.expect(j.fullyReserved).to.eql(true); pm.expect(j.reservedUnits).to.eql(2); }); pm.collectionVariables.set('reservationId', j.reservations[0].id);" ] }),
        req('Dispatch reservation', 'POST', '/api/inventory/reservations/{{reservationId}}/dispatch', { tests: [ok(200), "pm.test('Reservation dispatched', () => pm.expect(pm.response.json().status).to.eql('Dispatched'));" ] }),
        req('Stock-level report', 'GET', '/api/inventory/reports/stock-levels', { tests: [ok(200), "pm.test('Report is an array', () => pm.expect(pm.response.json()).to.be.an('array'));" ] }),
        req('Expiring-soon report', 'GET', '/api/inventory/expiring-soon?days=30', { tests: [ok(200)] }),
        req('Negative - stock-in invalid units and expired date', 'POST', '/api/inventory/stock-in', { body: { locationId: '{{locationId}}', bloodType: 'APositive', units: 0, expiryDate: '2020-01-01', source: '' }, tests: [ok(400), problem('validation_failed')] }),
        req('Create quarantine test lot', 'POST', '/api/inventory/stock-in', { body: { locationId: '{{locationId}}', bloodType: 'BNegative', units: 2, expiryDate: '{{expiryDate}}', source: 'QA Quarantine Batch {{runId}}' }, tests: [ok(201), save('id','quarantineLotId')] }),
        req('Quarantine inventory lot (delete semantics)', 'DELETE', '/api/inventory/{{quarantineLotId}}', { tests: [ok(200), "pm.test('Lot quarantined', () => pm.expect(pm.response.json().status).to.eql('Quarantined'));" ] }),
      ],
    },
    {
      name: '04 - Camps, Notifications & Validation/Safety Agent',
      item: [
        req('Create donation camp', 'POST', '/api/camps', { body: { name: 'QA Camp {{runId}}', location: 'QA Camp Hall, Colombo', latitude: 6.91, longitude: 79.88, startsAtUtc: '{{campStartUtc}}', endsAtUtc: '{{campEndUtc}}', capacity: 6, slotMinutes: 20 }, tests: [ok(201), save('id','campId'), "pm.test('Camp starts draft', () => pm.expect(pm.response.json().status).to.eql('Draft'));" ] }),
        req('Update camp', 'PUT', '/api/camps/{{campId}}', { body: { name: 'QA Camp Updated {{runId}}', location: 'QA Camp Hall, Colombo', latitude: 6.91, longitude: 79.88, startsAtUtc: '{{campStartUtc}}', endsAtUtc: '{{campEndUtc}}', capacity: 6, slotMinutes: 20 }, tests: [ok(200)] }),
        req('Schedule camp', 'PUT', '/api/camps/{{campId}}/status', { body: { status: 'Scheduled' }, tests: [ok(200), "pm.test('Camp scheduled', () => pm.expect(pm.response.json().status).to.eql('Scheduled'));" ] }),
        req('List public camps as donor', 'GET', '/api/camps?status=Scheduled&page=1&pageSize=100', { token: 'donorToken', tests: [ok(200), "pm.test('Scheduled camp visible', () => pm.expect(pm.response.json().items.some(x => x.id === pm.collectionVariables.get('campId'))).to.eql(true));" ] }),
        req('List camp slots', 'GET', '/api/camps/{{campId}}/slots', { token: 'donorToken', tests: [ok(200), "const j=pm.response.json(); pm.test('Slots generated', () => pm.expect(j).to.be.an('array').that.is.not.empty); pm.collectionVariables.set('slotId', j[0].id);" ] }),
        req('Book camp slot', 'POST', '/api/camps/{{campId}}/slots/book', { token: 'donorToken', body: { preferredSlotId: '{{slotId}}' }, tests: [ok(200), "pm.test('Slot booked', () => pm.expect(pm.response.json().status).to.eql('Booked'));" ] }),
        req('Negative - duplicate camp booking', 'POST', '/api/camps/{{campId}}/slots/book', { token: 'donorToken', body: { preferredSlotId: null }, tests: [ok(409), problem('already_booked')] }),
        req('Admin camp roster', 'GET', '/api/camps/{{campId}}/roster', { tests: [ok(200), "pm.test('Booked donor is on roster', () => pm.expect(pm.response.json().some(x => x.donorId === pm.collectionVariables.get('donorId'))).to.eql(true));" ] }),
        req('Start camp (required before check-in)', 'PUT', '/api/camps/{{campId}}/status', { body: { status: 'InProgress' }, tests: [ok(200), "pm.test('Camp in progress', () => pm.expect(pm.response.json().status).to.eql('InProgress'));" ] }),
        req('Check in donor', 'PUT', '/api/camps/{{campId}}/slots/{{slotId}}/check-in', { tests: [ok(200), "pm.test('Checked in', () => pm.expect(pm.response.json().status).to.eql('CheckedIn'));" ] }),
        req('Camp attendance report', 'GET', '/api/camps/{{campId}}/attendance-report', { tests: [ok(200), "pm.test('Attendance counted', () => pm.expect(pm.response.json().checkedIn).to.eql(1));" ] }),
        req('Register donor notification device', 'POST', '/api/notifications/devices', { token: 'donorToken', body: { token: 'qa-device-{{runId}}', platform: 'android' }, tests: [ok(204)] }),
        req('Find workflow for QA request', 'GET', '/api/agent-workflows?limit=100', { tests: [ok(200), "const list=pm.response.json(); const w=list.find(x => x.bloodRequestId === pm.collectionVariables.get('requestId')); pm.test('Workflow exists', () => pm.expect(w).to.exist); if(w) pm.collectionVariables.set('workflowId', w.id);" ] }),
        req('Validate all four agent steps and safety gate', 'GET', '/api/agent-workflows/{{workflowId}}/execution-summary', { tests: [ok(200), "const j=pm.response.json(); const names=j.steps.map(s=>s.agentName); pm.test('All agents executed in order', () => pm.expect(names).to.eql(['CoordinatorAgent','DomainAnalysisAgent','DispatchAgent','ValidationSafetyAgent'])); pm.test('Workflow reached auditable outcome', () => pm.expect(['PendingApproval','EscalationRequired','Completed']).to.include(j.status)); pm.test('Each step persisted tool/audit JSON', () => j.steps.forEach(s => { pm.expect(s.inputJson).to.be.a('string'); pm.expect(s.toolCallsJson).to.be.a('string'); }));" ] }),
        req('Approve pending workflow (human-in-the-loop)', 'POST', '/api/agent-workflows/{{workflowId}}/approve', { body: { comments: 'Approved by Postman QA after safety review' }, tests: ["pm.test('Approval handled', () => pm.expect([200,409]).to.include(pm.response.code));", "if(pm.response.code===200) pm.test('Approval recorded', () => pm.expect(pm.response.json().approvals).to.be.an('array').that.is.not.empty);" ] }),
        req('Notification history', 'GET', '/api/notifications/history', { token: 'donorToken', tests: [ok(200), "pm.test('Notification history is array', () => pm.expect(pm.response.json()).to.be.an('array'));" ] }),
        req('Remove donor notification device', 'DELETE', '/api/notifications/devices', { token: 'donorToken', body: { token: 'qa-device-{{runId}}' }, tests: [ok(204)] }),
        req('Negative - invalid camp time/capacity', 'POST', '/api/camps', { body: { name: '', location: '', latitude: 100, longitude: 200, startsAtUtc: '{{campEndUtc}}', endsAtUtc: '{{campStartUtc}}', capacity: 0, slotMinutes: 1 }, tests: [ok(400), problem('validation_failed')] }),
        req('Deactivate QA donor (admin delete)', 'DELETE', '/api/donors/{{donorId}}', { tests: [ok(204)] }),
      ],
    },
  ],
};

fs.mkdirSync('postman', { recursive: true });
fs.writeFileSync('postman/LifeLink-Complete-QA.postman_collection.json', JSON.stringify(collection, null, 2) + '\n');
