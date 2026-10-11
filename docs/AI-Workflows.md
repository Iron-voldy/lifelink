# LifeLink AI Workflows

LifeLink uses a small team of **4 AI agents** to look at a hospital's blood request and **suggest** what to do.
The agents only **propose** actions. A blood-bank admin must **approve** before any stock is moved or any donor is messaged.

- **Built with:** Python, FastAPI, LangGraph (agent pipeline), Ollama `llama3.1:8b` (local AI model)
- **Code:** `agentic-ai/` (agents) and `backend/LifeLink.Infrastructure/Workflows/` (starts and manages workflows)
- **Admin screen:** Web → *Agent workflows*

---

## 1. How a workflow is created

A workflow starts **automatically**. Nobody has to press a button.

| When this happens | Reason saved |
|---|---|
| A hospital **creates** a blood request | `request-created` |
| A hospital **edits** the blood type, units or urgency | `request-updated` |
| A hospital **escalates** a request (makes it more urgent) | `request-escalated` |
| An admin asks for a **revision** of a proposal | `revision requested: …` |
| An admin starts one by hand | `POST /api/agent-workflows/start` |

**What happens step by step**

1. The backend saves the request and marks it **Under review**.
2. It collects the facts: eligible donors with a matching blood type, and compatible blood stock that hasn't expired.
3. It sends these to the agent service (`POST /v1/workflows/run`), protected by an internal API key and traced with a correlation ID.
4. The 4 agents run **one after another**: Coordinator → Domain Analysis → Dispatch → Validation.
5. The backend saves every step and the result, then updates the request status.

```
Hospital request
      │
      ▼
 [1] Coordinator ──► [2] Domain Analysis ──► [3] Dispatch ──► [4] Validation
     (plan)            (find matches)          (draft actions)    (safety check)
                                                                    │
                          ┌─────────────────────────────────────────┼──────────────────┐
                          ▼                                         ▼                  ▼
                  Pending approval                          Escalation required      Completed
                (admin decides)                          (no safe match / error)   (nothing to do)
```

---

## 2. The 4 agents

### 1) Coordinator Agent: *makes the plan*
- **Purpose:** Reads the request (blood type, units, urgency) and writes a short plan with 3 fixed steps: *analyse → draft → validate*.
- **AI used:** Ollama `llama3.1:8b`. If the AI is slow (over 5 s), offline, or gives an unexpected answer, a built-in safe plan is used instead, so the workflow never gets stuck.
- **Cannot:** add extra steps or approve anything.

### 2) Domain Analysis Agent: *finds what can help*
- **Purpose:** Finds donors who **can safely give** to this patient (ABO/Rh compatibility rules) and the **compatible stock lots**, sorted so the blood that expires first is used first.
- **Tool:** `analyse_candidates`
- **Note:** The hospital's free-text notes are treated as untrusted data and never as instructions, which blocks prompt-injection attempts.

### 3) Dispatch Agent: *drafts the actions*
- **Purpose:** Drafts which stock lots to **reserve**. If there's not enough stock, it drafts a **message to matching donors** asking them to donate.
- **Tools:** `propose_inventory_reservation`, `propose_donor_broadcast`
- **Cannot:** actually reserve stock or send messages. It only writes proposals.

### 4) Validation / Safety Agent: *the gatekeeper*
- **Purpose:** Checks the proposal with **fixed code rules**, not AI, and decides the outcome.
- **Tool:** `deterministic_safety_gate`
- **Rules:**
  - Any stock reservation → **needs admin approval**
  - Any donor message → **needs admin approval**
  - Rare blood (O− or AB−) or **Critical** urgency → **needs admin approval**
  - Nothing safe to do → **escalate** to a human

---

## 3. Workflow results (statuses)

| Status | Meaning |
|---|---|
| **Running** | The agents are working on it |
| **Pending approval** | A proposal is ready; an admin must decide |
| **Approved** | The admin said yes; the actions are being applied |
| **Completed** | Done (actions applied, or nothing needed) |
| **Rejected** | The admin said no; the request is closed as unfulfilled |
| **Revising** | The admin asked for changes; a new workflow attempt starts |
| **Escalation required** | No safe match, or the AI service failed or timed out; a human must handle it |

---

## 4. What the admin can do

| Action | Result |
|---|---|
| **Approve** | Stock is reserved and dispatched, and approved donors get a notification. The request becomes **Dispatched**. |
| **Reject** | The proposal is dropped and the request is closed. |
| **Revise** (comment required) | The proposal is set aside and the agents run again with fresh data. |

Before applying an approval, the backend checks again that the stock still exists and the donors are still eligible.
If something changed, nothing is dispatched and the request is escalated.

---

## 5. Safety in one glance

- AI **suggests**, a human **approves**. Every real action needs an admin.
- Agents can only use **5 allow-listed tools**, and those tools have no database, file or internet access.
- Each step has a **timeout and retry limit**. Any failure leads to **Escalation required**, never an endless loop.
- Hidden instructions in request notes (prompt injection) are ignored.
- No AI reasoning, passwords or tokens are stored. Each step's input, output and tool calls are saved for audit.
- Tested with **12 golden scenarios** in `agentic-ai/tests/`, covering exact, partial and no stock, rare types, and approval-bypass attempts.
