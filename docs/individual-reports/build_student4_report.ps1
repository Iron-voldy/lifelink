$ErrorActionPreference = 'Stop'
$outPath = Join-Path $PSScriptRoot 'SE3090_LifeLink_Student4_Individual_Report_EDITABLE.docx'
$htmlPath = Join-Path $env:TEMP ('lifelink-student4-report-' + [guid]::NewGuid().ToString('N') + '.html')
$html = @'
<!DOCTYPE html><html><head><meta charset="utf-8"><style>
@page { size:A4; margin:1.8cm 2cm; }
body { font-family:'Times New Roman',serif; color:#000; font-size:12pt; line-height:1.18; }
h1 { font-family:'Times New Roman',serif; font-size:16pt; color:#000; margin:15pt 0 6pt; page-break-after:avoid; }
h2 { font-family:'Times New Roman',serif; font-size:14pt; color:#000; margin:11pt 0 4pt; page-break-after:avoid; }
h3 { font-family:'Times New Roman',serif; font-size:12pt; color:#000; margin:8pt 0 3pt; }
p,li,td,th { font-size:12pt; color:#000; }
p { margin:4pt 0 7pt; } li { margin:2pt 0; }
table { border-collapse:collapse; width:100%; margin:7pt 0 10pt; }
td,th { border:1px solid #000; padding:4pt; vertical-align:top; }
th { font-weight:bold; text-align:left; }
a { color:#000; text-decoration:underline; }
.cover { text-align:center; padding-top:75pt; page-break-after:always; }
.cover h1 { font-size:24pt; }
.small { font-size:12pt; }
.evidence { border:1px dashed #000; padding:7pt; margin:7pt 0; }
.note { border:1px solid #000; padding:7pt; margin:7pt 0; }
.newpage { page-break-before:always; }
code { font-family:Consolas,monospace; font-size:10pt; }
</style></head><body>
<div class="cover"><p><b>SE3090 · ASSIGNMENT 1</b></p><h1>LifeLink Individual Contribution Report</h1><p><b>Camps and Notifications · Validation and Safety Agent</b></p><p>Editable individual report</p><p style="margin-top:55pt">Student name: [ENTER YOUR FULL NAME]<br/>Student ID: [ENTER ID]<br/>Group: [ENTER GROUP]<br/>Module / tutor: [ENTER DETAILS]<br/>Submission date: [ENTER DATE]</p><p style="margin-top:40pt">Prepared from the supplied assignment guidance and the LifeLink project repository. Complete all square-bracket fields, attach authentic evidence and confirm every personal contribution before submission.</p></div>
<h1 id="contents">Contents</h1>
<ol>
<li><a href="#s1">Contribution Statement</a></li><li><a href="#s2">Owned Component</a></li><li><a href="#s3">Technical Implementation</a></li><li><a href="#s4">Backend/API Evidence</a></li><li><a href="#s5">Database Evidence</a></li><li><a href="#s6">React Evidence</a></li><li><a href="#s7">Flutter Evidence</a></li><li><a href="#s8">Agentic AI Contribution</a></li><li><a href="#s9">Testing Evidence</a></li><li><a href="#s10">Git/PR Evidence</a></li><li><a href="#s11">Challenges &amp; Learning</a></li><li><a href="#s12">Individual AI Usage Log</a></li><li><a href="#s13">AI Reflection</a></li><li><a href="#s14">Signed Declaration</a></li>
</ol>
<p><b>Evidence rule:</b> this report distinguishes repository facts from the student’s personal work. Replace prompts only with evidence you can explain. The local checkout contains one initial commit, so it cannot establish individual authorship or PR review history.</p>

<h1 id="s1">1. Contribution Statement</h1>
<p>My assigned area in the LifeLink group design is <b>Camps and Notifications</b>, with the <b>Validation and Safety Agent</b> as my agent responsibility. These assignments are recorded for Student 4 in the group’s domain ownership decision record [1]. My work in this area was: [describe, in first person, the code, design, tests, reviews and integration tasks you personally completed].</p>
<p>The component supports organized blood-donation events and controlled communication with donors. The agent checks a proposed workflow outcome and identifies when an action needs a human decision. It does not make a clinical decision or independently send a broadcast.</p>
<div class="evidence"><b>Insert Evidence 1 — Individual contribution summary:</b> add a short project-board extract or team allocation record showing your name/ID, the component and agent. Caption the source and date. Do not use another student’s work as personal evidence.</div>

<h1 id="s2">2. Owned Component</h1>
<h2>2.1 Camps</h2><p>A camp is a scheduled event with a location, optional coordinates, start/end times, capacity and lifecycle status. Each camp has bookable time slots. A donor can book one slot, cancel their own booking before it begins, and be checked in by an authorized administrator when the camp is in progress. Administrators can view a roster and attendance report. The design separates the camp from its slots, so capacity and attendance can be represented at slot level.</p>
<h2>2.2 Notifications</h2><p>Notifications cover device-token registration, an in-application history and push delivery through a provider abstraction. The service stores a notification before delivery and a background outbox worker processes pending items. A broadcast request is accepted only when it references a workflow with an approved decision. Idempotency keys help prevent replayed requests from creating duplicate notification rows.</p>
<h2>2.3 Why this work matters</h2><p>Donation events need predictable capacity, times and attendance records. Reliable donor communication can help people act on relevant event or request updates, but a mistaken or duplicate message can create confusion. The design therefore combines explicit booking states, ownership checks, approval before sensitive broadcasts, persisted delivery status and bounded retry attempts. This is an operational coordination feature; it does not replace blood-service procedures or clinical judgment.</p>
<p><b>Ownership boundary:</b> ADR-01 assigns camps and notifications plus the Validation and Safety Agent to Student 4. Cross-cutting work must still be attributed to the actual implementer and reviewer. The project repository does not identify the student’s legal name, so fill it in from the group record [1].</p>

<h1 id="s3">3. Technical Implementation</h1>
<h2>3.1 Request and booking path</h2><p>The React and Flutter clients call authenticated API endpoints. The controller applies role checks and request validation, then delegates business behavior to <code>CampService</code>. The service validates camp state, time and donor eligibility; persists camp/slot changes through Entity Framework Core; and queues a donor notification after a successful booking. A database concurrency token is present on each slot, and a database update conflict is translated into a booking conflict instead of silently accepting two reservations.</p>
<h2>3.2 Notification delivery path</h2><p>Controllers pass device and broadcast requests to <code>NotificationService</code>. Approved broadcasts are checked against the persisted workflow approval. The service writes queued messages with recipient-level idempotency. <code>NotificationOutboxWorker</code> polls in bounded batches and asks an <code>INotificationProvider</code> to send. Firebase Cloud Messaging is provided by the server-side FCM HTTP v1 adapter; a logging provider supports local development. A failed attempt is recorded and eligible messages can be tried again, with the service limiting attempts to three.</p>
<h2>3.3 Safety and failure handling</h2><p>The safety decision is deterministic code. It flags rare blood types or critical urgency, any proposed stock reservation, and any recipient broadcast as requiring approval. If no action is proposed, the outcome is escalation-required. Safety policy is not delegated to free-form model text. The workflow graph runs Coordinator, Domain Analysis, Dispatch and then Validation in a fixed sequence [2].</p>
<p>The current implementation includes useful controls, but production readiness would still require operational monitoring, delivery-token cleanup, load and concurrency testing against PostgreSQL, data-retention rules, and review of notification content to avoid exposing sensitive health details on lock screens.</p>

<h1 id="s4">4. Backend/API Evidence</h1>
<p>Primary implementation evidence is <code>backend/LifeLink.Api/Controllers/CampsController.cs</code>, <code>backend/LifeLink.Infrastructure/Camps/CampService.cs</code>, <code>backend/LifeLink.Api/Controllers/NotificationsController.cs</code>, <code>backend/LifeLink.Infrastructure/Notifications/NotificationService.cs</code> and <code>backend/LifeLink.Infrastructure/Notifications/NotificationOutboxWorker.cs</code>.</p>
<table><tr><th>API area</th><th>Implemented behavior evidenced in source</th><th>Authorization / validation focus</th></tr>
<tr><td>Camps</td><td><code>/api/camps</code>: create, list, retrieve, update and change status; slot list, booking, cancellation, check-in, roster and attendance report.</td><td>Admin-only management and check-in; donor-only booking/cancellation; drafts hidden from non-admin users; booking only for eligible donors and valid camp states.</td></tr>
<tr><td>Notifications</td><td><code>/api/notifications/devices</code>, <code>/broadcast</code>, <code>/process-outbox</code> and <code>/history</code>.</td><td>Users manage their own device and history; broadcasts/outbox processing are admin-only; broadcast requires workflow approval.</td></tr></table>
<p>Camp inputs constrain name/location lengths, coordinate ranges, capacity (1–1,000) and slot interval (5–240 minutes). Listing supports search, status, date range and bounded pagination. API conflicts distinguish missing resources, invalid transitions, unavailable/duplicate bookings and forbidden ownership.</p>
<div class="evidence"><b>Insert Evidence 2 — Backend/API:</b> add one screenshot of the relevant controller/service code or API client request/response, showing the route and validation/authorization behavior. Redact tokens, email addresses and real donor data. Suggested file: <code>CampsController.cs</code> or <code>NotificationService.cs</code>.</div>

<h1 id="s5">5. Database Evidence</h1>
<p>The domain entities are in <code>backend/LifeLink.Domain/Entities/CampNotificationEntities.cs</code>. <code>DonationCamp</code> stores organizer, name, location, coordinates, UTC start/end, capacity and status. <code>CampSlot</code> stores the camp reference, optional donor, UTC slot time, status and a version value. <code>Notification</code> stores recipient, optional workflow execution, type/channel, payload, status, attempt count, sent time, idempotency key and last error.</p>
<p>The slot version is used as an optimistic concurrency marker. A migration named <code>20260818044240_AddCampSlotConcurrency</code> records the schema change. Notification state provides a delivery audit trail, while the idempotency key supports replay handling. Exact relational constraints and indexes should be confirmed from <code>backend/LifeLink.Infrastructure/Persistence/LifeLinkDbContext.cs</code> and migrations before claiming a specific uniqueness guarantee. PostgreSQL primary and unique constraints are enforced by indexes [3].</p>
<p>Time values are normalized to UTC in the camp service, reducing ambiguity when devices operate in local time zones. The project test suite contains cases for offset conversion. Keep payloads minimal and avoid storing unnecessary personal or clinical data.</p>
<div class="evidence"><b>Insert Evidence 3 — Database:</b> add a readable screenshot of the EER subset for DonationCamp, CampSlot, Notification, User and relevant workflow/approval relationship, or a migration/database view. Label primary and foreign keys and cardinality; do not present a conceptual link as a verified database constraint.</div>

<h1 id="s6">6. React Evidence</h1>
<p>The relevant screen is <code>web/src/pages/CampsPage.tsx</code>. In the project, the web application is the administrative surface for coordinating camps. The page should be demonstrated with a valid administrator account and synthetic data. The repository alone does not prove which React changes were personally authored by the report’s student, so describe your exact work in the field below.</p>
<p><b>My React work:</b> [identify the components, API integration, state or validation you personally implemented/reviewed; if none, write “No direct React implementation in my assigned contribution” and explain how the feature is used.]</p>
<div class="evidence"><b>Insert Evidence 4 — React:</b> add a screenshot of the camp administration page, showing a useful state such as camp list, create/edit form, lifecycle status or attendance. Use synthetic data and include a figure caption. Do not use this placeholder as a substitute for the screenshot.</div>

<h1 id="s7">7. Flutter Evidence</h1>
<p>Relevant mobile files include <code>mobile/lib/screens/camps/camps_screen.dart</code>, <code>mobile/lib/screens/camps/camp_detail_screen.dart</code>, <code>mobile/lib/screens/notifications_screen.dart</code> and <code>mobile/lib/services/push_notification_service.dart</code>. Together, these files provide the mobile camp and notification experience. The client’s actual capability and current visual state should be checked on a running build before submission.</p>
<p><b>My Flutter work:</b> [state exactly which screens, service calls, notification handling or fixes you personally completed; include task/commit evidence.]</p>
<div class="evidence"><b>Insert Evidence 5 — Flutter:</b> add a simulator/device screenshot of camp discovery/details or notification history/push handling. Show the app state and caption it. Remove personal tokens, phone numbers and real user details.</div>

<h1 id="s8">8. Agentic AI Contribution</h1>
<h2>8.1 Agent purpose and contract</h2><p>The <code>ValidationSafetyAgent</code> is the final node in the LangGraph workflow. It reads the validated request and the preceding proposal, evaluates fixed safety rules, records the result in the workflow steps and returns a status. Its input schema restricts blood type and urgency to enumerated values, limits quantity and input list sizes, and rejects unexpected fields. Its output contributes approval state and validation facts to the workflow response.</p>
<h2>8.2 Policy behavior</h2><table><tr><th>Condition</th><th>Safety behavior in <code>agentic-ai/tools/safety_gate.py</code></th></tr>
<tr><td>Any recipient broadcast proposed</td><td>Requires human approval.</td></tr><tr><td>Any stock reservation proposed</td><td>Requires human approval.</td></tr><tr><td>Rare type (O negative or AB negative) or critical urgency</td><td>Marked rare/critical and requires approval.</td></tr><tr><td>No stock action and no recipients</td><td>Escalation required; the workflow cannot claim an actionable completion.</td></tr></table>
<p>The safety check is deterministic and auditable. It does not trust the planner to authorize tool use. ADR-04 states that business rules, tool authorization and approval gates stay in deterministic code; model planning is constrained and has a deterministic fallback [2]. The agent reports structured facts and an outcome. It is not a medical decision-maker, and approval by an authorized human remains necessary before consequential actions.</p>
<p><b>My agent work:</b> [identify the exact rules, graph integration, schema, tests or review you personally authored. Explain one input and output you can demonstrate.]</p>
<div class="evidence"><b>Insert Evidence 6 — Agent:</b> add a screenshot or short code excerpt from <code>validation_agent/agent.py</code> and <code>tools/safety_gate.py</code>, plus a sample synthetic workflow result showing <code>PendingApproval</code> or <code>EscalationRequired</code>. Never show real donor information.</div>

<h1 id="s9">9. Testing Evidence</h1>
<p>Repository test sources relevant to this scope include <code>backend/LifeLink.Tests/Camps/CampServiceTests.cs</code>, <code>CampAdminValidationTests.cs</code>, <code>backend/LifeLink.Tests/Notifications/NotificationServiceTests.cs</code>, <code>agentic-ai/tests/test_tools.py</code>, <code>test_golden_scenarios.py</code> and <code>test_workflow.py</code>. The repository contains test cases, but this report preparation did not execute them; no pass result is claimed.</p>
<table><tr><th>Evidence scenario in source</th><th>What it checks</th><th>Record actual run result</th></tr>
<tr><td>Eligible donor books once, checks in, appears in attendance</td><td>Booking uniqueness at service level and attendance state/reporting.</td><td>[command, date, pass/fail]</td></tr><tr><td>Ineligible donor cannot book; invalid event times rejected</td><td>Business validation and schedule boundaries.</td><td>[command, date, pass/fail]</td></tr><tr><td>Timezone conversion, early start and camp transitions</td><td>UTC normalization, lifecycle and check-in rules.</td><td>[command, date, pass/fail]</td></tr><tr><td>Broadcast without approval rejected; approved broadcast idempotent and outbox-delivered</td><td>Approval gate, duplicate suppression and delivery path.</td><td>[command, date, pass/fail]</td></tr><tr><td>Safety gate rejects automatic stock/broadcast actions</td><td>Approval requirement and escalation outcome.</td><td>[command, date, pass/fail]</td></tr></table>
<p><b>Evidence standard:</b> include the real test command, environment, date, result summary and a CI run link or terminal screenshot. A unit test using an in-memory EF provider is not a substitute for testing PostgreSQL transaction/concurrency behavior under parallel requests. The database concurrency migration and conflicting booking path should be verified against the configured PostgreSQL database before describing them as proven in production.</p>
<div class="evidence"><b>Insert Evidence 7 — Tests:</b> attach test output/CI screenshot with the command visible. Do not edit a failing result to look successful; note the failure and its resolution or status.</div>

<h1 id="s10">10. Git/PR Evidence</h1>
<p>The local history available during report preparation shows a single commit: <code>731c58c — Initial commit: LifeLink blood and emergency coordination platform</code>. It contains the project files but does not separate the author’s individual work. No feature-specific commit or pull-request history was available in this checkout. This is a repository limitation, not evidence that no PR exists remotely.</p>
<table><tr><th>Required item</th><th>Student to complete with verifiable evidence</th></tr><tr><td>Git hosting account/name and student mapping</td><td>[name/ID and profile URL, if required]</td></tr><tr><td>Owned commits</td><td>[commit SHA, date, link and concise contribution]</td></tr><tr><td>Pull requests and reviews</td><td>[PR URL, title, your role, review/merge state]</td></tr><tr><td>Issue/task association</td><td>[board issue URL/ID linked to code and tests]</td></tr></table>
<p>Attach a screenshot of the repository history or PR page only after checking it is the correct project and account. Do not claim the initial commit as an individual contribution without evidence that supports that claim.</p>

<h1 id="s11">11. Challenges &amp; Learning</h1>
<p>Use this section to describe your own experience, not a generic project summary. The technical issues visible in this area suggest useful topics to address: keeping camp state transitions consistent; preventing duplicate or conflicting slot bookings; handling local time input as UTC; ensuring the outbox does not lose a message after an API request; requiring approval before a broadcast; and designing agent output so a model cannot override safety policy.</p>
<p><b>Challenge I faced:</b> [specific issue and where it occurred].<br/><b>How I investigated it:</b> [code, test, discussion or documentation used].<br/><b>What I changed or recommended:</b> [specific action and evidence].<br/><b>What I learned and would improve next:</b> [your own reflection on the technical and teamwork outcome].</p>

<h1 id="s12">12. Individual AI Usage Log</h1>
<p>This report itself was prepared with Codex (GPT-6) on 6 October 2026. The assistant inspected repository files and tests, summarized technical evidence, and drafted this editable document. The report intentionally leaves personal authorship, test execution results, Git evidence and the student reflection for the student to verify or complete. The student should add any other AI use and correct this log if the account of their work is incomplete.</p>
<table><tr><th>Date</th><th>Tool/model</th><th>Task</th><th>Output used / changed</th><th>How verified</th></tr><tr><td>6 Oct 2026</td><td>Codex, GPT-6</td><td>Inspect LifeLink source and prepare this individual report.</td><td>Drafted editable structure and summarized project evidence. Student must verify each claim, replace placeholders and add personal evidence.</td><td>Compared claims with cited repository files, tests and ADRs; no test run or PR authorship was inferred.</td></tr><tr><td>[add date]</td><td>[tool/model]</td><td>[your task]</td><td>[what was accepted, edited or rejected]</td><td>[how you independently checked it]</td></tr></table>

<h1 id="s13">13. AI Reflection</h1>
<div class="note"><b>Student-authored section required.</b> The assignment guidance says the reflection must be written by the student; AI-generated reflection receives no credit. The report therefore does not supply a first-person reflection. Write this section yourself in your own words, based on the tools you actually used.</div>
<p>Suggested points to consider: Which AI tools did you use and for what tasks? What suggestions were useful? What was inaccurate, incomplete or unsuitable? What did you change or reject, and why? How did you verify generated code or explanations? What did you learn about using AI responsibly in a safety-sensitive coordination system? Which decisions remained yours and your team’s?</p>
<p>[Write approximately one page in your own words. Mention a real example and the evidence you checked. Remove this prompt before final submission.]</p>

<h1 id="s14">14. Signed Declaration</h1>
<p>I declare that this report describes my own contribution accurately, that I have identified work completed by other people, and that all sources and AI assistance have been acknowledged. I have checked that the evidence I submit is genuine and that I can explain the work described. I understand that this document must be completed and reviewed against the assignment rules before submission.</p>
<p>Student name: __________________________________________</p><p>Student ID: ______________________________________________</p><p>Signature: _______________________________________________</p><p>Date: ____________________________________________________</p>

<h1>References</h1>
<p>[1] LifeLink project, <i>ADR-01: Domain and agent ownership</i>, <code>docs/adr/01-domain-and-component-assignment.md</code>; repository source and test files cited in Sections 4–10. Local Git history inspected: commit 731c58c.</p>
<p>[2] LifeLink project, <i>ADR-04: Agent framework, model, and fallback</i>, <code>docs/adr/04-agentic-ai-framework-and-orchestration.md</code>; agent graph, schema and safety-gate source under <code>agentic-ai/</code>.</p>
<p>[3] PostgreSQL Global Development Group, <i>CREATE TABLE — Constraints</i>, <a href="https://www.postgresql.org/docs/current/sql-createtable.html">https://www.postgresql.org/docs/current/sql-createtable.html</a> (accessed 6 October 2026).</p>
<p>[4] Google Firebase, <i>Send a message using the FCM HTTP v1 API</i>, <a href="https://firebase.google.com/docs/cloud-messaging/send/v1-api">https://firebase.google.com/docs/cloud-messaging/send/v1-api</a> (accessed 6 October 2026).</p>
<p>[5] LangChain, <i>LangGraph Reference</i>, <a href="https://langchain-ai.github.io/langgraph/reference/">https://langchain-ai.github.io/langgraph/reference/</a> (accessed 6 October 2026).</p>
<p>[6] Microsoft, <i>Rate limiting middleware in ASP.NET Core</i>, <a href="https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit">https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit</a> (accessed 6 October 2026). Cited as further implementation guidance; the report does not claim the project configures rate limiting.</p>
<p><a href="#contents">Return to Contents</a></p>
</body></html>
'@
[System.IO.File]::WriteAllText($htmlPath, $html, [System.Text.UTF8Encoding]::new($false))
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $outPath) { Remove-Item -LiteralPath $outPath -Force }
$stream = [System.IO.File]::Open($outPath, [System.IO.FileMode]::CreateNew)
$zip = [System.IO.Compression.ZipArchive]::new($stream, [System.IO.Compression.ZipArchiveMode]::Create, $false)
try {
  $entries = @{
    '[Content_Types].xml' = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Default Extension="htm" ContentType="text/html"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>'
    '_rels/.rels' = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>'
    'word/document.xml' = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><w:body><w:altChunk r:id="rId2"/><w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1020" w:right="1134" w:bottom="1020" w:left="1134"/></w:sectPr></w:body></w:document>'
    'word/_rels/document.xml.rels' = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/aFChunk" Target="afchunk.htm"/></Relationships>'
  }
  foreach ($entryName in $entries.Keys) {
    $entry = $zip.CreateEntry($entryName, [System.IO.Compression.CompressionLevel]::Optimal)
    $writer = [System.IO.StreamWriter]::new($entry.Open(), [System.Text.UTF8Encoding]::new($false))
    try { $writer.Write($entries[$entryName]) } finally { $writer.Dispose() }
  }
  $entry = $zip.CreateEntry('word/afchunk.htm', [System.IO.Compression.CompressionLevel]::Optimal)
  $writer = [System.IO.StreamWriter]::new($entry.Open(), [System.Text.UTF8Encoding]::new($false))
  try { $writer.Write($html) } finally { $writer.Dispose() }
} finally { $zip.Dispose(); $stream.Dispose() }
Remove-Item -LiteralPath $htmlPath -Force
Write-Output $outPath
