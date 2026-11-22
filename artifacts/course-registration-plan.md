# Course Registration – AI-Assisted Assessment Plan

## Context
This document captures all decisions and the full implementation plan for the
**Course Waitlist** assessment. Use it as a briefing for Claude in VS Code so you
don't have to re-explain context each time.

---

## Assessment Summary
Build one bounded feature: **enroll, drop, and FIFO-waitlist** for a single course
offering. No starter project was provided, so we use the minimal service fallback.

**Out of scope:** frontend, catalog, payments, identity, multi-term, deployment,
configurable policy engine, new database.

**Suggested effort:** 7–8 hours.

---

## Decisions Made

| Decision | Choice | Rationale |
|---|---|---|
| Language / Runtime | .NET 8 | Requested by candidate |
| Service style | ASP.NET Core Web API | REST is easy to test and demonstrate |
| Persistence | In-memory store (plain C# class) | Starter not provided; in-memory is the allowed fallback |
| Concurrency | Single `lock` on the store | Simple, correct, no over-engineering |
| Test framework | xUnit | Standard for .NET |
| Prerequisite rule | Learner must have completed a named prerequisite course ID | Simple, testable, realistic |
| Ineligible learner policy | **Skip-and-retain**: walk the waitlist from the front; skip anyone who fails the prerequisite; promote the first eligible one; skipped learners stay in their original waitlist position | Documents the policy, easy to test both paths |
| Eligibility gate point | **Gate at the seat**: prerequisite is enforced for *direct enrollment* and at *promotion*, but NOT as a condition for joining the waitlist. An ineligible learner can join the waitlist when the course is full; they simply can't be promoted until eligible. | Matches the brief's framing ("what happens when an ineligible learner reaches the front of the queue") as a routine case, not a rare edge case. Makes the skip-and-retain policy something that actually gets exercised. |

---

## Prerequisite Rule Detail
- Each `Course` has an optional `PrerequisiteCourseId` (nullable string).
- Each `Learner` carries a `HashSet<string> CompletedCourses`.
- A learner is **eligible** if `PrerequisiteCourseId == null` OR
  `CompletedCourses.Contains(PrerequisiteCourseId)`.
- **Gate at the seat:** eligibility is checked when a learner requests *direct* enrollment
  (capacity available) and again at *promotion* time. It is **not** checked when a learner
  joins the waitlist (capacity full) — any learner, eligible or not, may join the waitlist.
- Because eligibility can change after a learner joins the waitlist (e.g. they complete the
  prerequisite while waiting), there must be a way to update a `Learner`'s `CompletedCourses`
  after seeding — see the new `PATCH /learners/{learnerId}` endpoint below. Without it, the
  "becomes eligible later" path can't be exercised by a test.

---

## Waitlist Promotion Rule
When a seat opens (a learner drops):
1. Walk the waitlist in insertion order (FIFO).
2. Find the **first** entry where the learner is still eligible.
3. Promote that learner to enrolled; remove them from the waitlist.
4. Learners who were skipped remain in the waitlist at their current position.
5. If no eligible learner exists, the seat stays open.

### Enrollment request decision table
| Capacity available? | Learner eligible? | Outcome |
|---|---|---|
| Yes | Yes | Enrolled |
| Yes | No | Rejected (prerequisite not met) — no seat taken, no waitlist entry |
| No | Yes | Waitlisted |
| No | No | Waitlisted (gate-at-the-seat: allowed to wait, can't be promoted until eligible) |

---

## Error / Status Code Scheme

| Situation | Status Code | Notes |
|---|---|---|
| Enroll/waitlist succeeds | `200`/`201` | Returns resulting status (`Enrolled` / `Waitlisted`) |
| Duplicate enroll or waitlist request (learner already enrolled or already waitlisted for this course) | `409 Conflict` | Record already exists; request conflicts with current state |
| Prerequisite not met, capacity available (direct-enrollment rejection) | `422 Unprocessable Entity` | Request is well-formed but fails a business rule |
| Drop request where no matching enrollment/waitlist record exists (never enrolled/waitlisted, or already dropped once) | `404 Not Found` | The resource named by `DELETE /enrollments/{courseId}/{learnerId}` doesn't exist at request time |

Full split (404/409/422) chosen over a single blanket code so each failure mode is distinguishable by clients and by tests.

Enroll/drop requests referencing a `courseId` or `learnerId` that was never seeded also return
`404 Not Found` — same rationale as invalid drop: the resource named in the request doesn't exist.

A genuinely malformed request body (missing/null `courseId` or `learnerId`, non-positive
`capacity`) returns `400 Bad Request` — distinct from 404/409/422, which all assume a
well-formed request that fails for a business reason.

---

## Stated Assumption: Resolving a Tension in the Brief

The Problem Statement says "When the course is full, eligible learners join the waitlist,"
which read literally would mean ineligible learners never reach the waitlist at all. Taken
together with Acceptance Criterion 4 ("An ineligible learner is not enrolled **or promoted**"
— notably not "or waitlisted") and the Candidate Expectations line asking what happens
"when an ineligible learner reaches the front of the queue," the more specific and testable
reading is that ineligible learners *can* be on the waitlist, and the prerequisite gate
applies at promotion (and direct enrollment), not at waitlist entry. We resolved this
ambiguity in favor of the more specific AC4 / Candidate Expectations wording — this is the
"gate at the seat" decision above. Documenting this explicitly here since the brief asks
assumptions to be stated.

---

## Acceptance Criteria Traceability (PDF → this plan)

| PDF Acceptance Criterion | Covered by |
|---|---|
| 1. Enrollment succeeds when capacity remains and prerequisite satisfied | Decision table row 1; Test 1 |
| 2. Eligible learner joins waitlist once when full; duplicate enroll doesn't duplicate | Decision table row 3; Tests 2, 3, 4 |
| 3. Drop opens seat, promotes earliest eligible per documented policy | Waitlist Promotion Rule; Test 5 |
| 4. Ineligible learner not enrolled/promoted; skip/retain policy applied consistently | Skip-and-retain policy; Tests 6, 8, 9 |
| 5. Invalid requests/duplicate drops return clear results, no state corruption | 404/409/422/400 scheme; Tests 3, 4, 10, 11, 13, 15 |
| 6. Tests cover capacity available/full, duplicates, FIFO, prerequisite failure, invalid/repeated drops, concurrency | Tests 1–15 (Phase 5) |
| 7. AI used for ≥1 engineering activity, with prompt/response/decision/verification record | `AI-USE.md`, logged throughout design + build |

| PDF Scope Boundary | How we stay inside it |
|---|---|
| One offering's enroll/drop/waitlist slice only, not the full product | No catalog, search, multi-course UX beyond what's needed to seed test data; `Course`/`Learner` seeding is a test helper, not a catalog feature |
| No frontend, payments, identity, multi-term, deployment, new DB unless starter requires | None of these exist in the plan; no starter was provided, so none are pulled in |
| No separate greenfield/brownfield/ambiguous scenario builds | Single fallback path (minimal service, in-memory store) — the only path we build |
| State assumptions; no configurable policy engine | Assumptions documented throughout this plan; "Open/Closed, deliberately bounded" in Engineering Practices explicitly rules out a pluggable policy abstraction |

| PDF Deliverable | File |
|---|---|
| Runnable code + focused automated tests | `src/`, `tests/` (Phases 1–8) |
| Design specification | `DESIGN.md` (Phase 9.1) |
| AI prompt and use record | `AI-USE.md` (ongoing) |
| Setup/run instructions incl. test commands | `README.md` (Phase 9.3) |
| Limitations / follow-up risks | `LIMITATIONS.md` (Phase 9.4) |

**Guardrail while implementing:** if a task starts to require something not traceable to a
row above (an extra endpoint, a new abstraction, a dependency not already named in this
plan), stop and flag it rather than building it — that's a signal of scope creep, not a
missed requirement, since every stated requirement is already mapped above.

---

## Engineering Practices

**SOLID, applied proportionately (not as an excuse to over-engineer):**
- **Single Responsibility** — four layers, each with one job: `Models` (data shape),
  `Store` (thread-safe persistence), `Services` (business rules: eligibility, promotion),
  `Controllers` (thin HTTP translation only — no business logic in controllers).
- **Dependency Inversion** — `RegistrationController` depends on `IRegistrationService`,
  `RegistrationService` depends on `ICourseStore`, both registered via DI (ASP.NET Core
  built-in container). This is what makes the service unit-testable without spinning up HTTP.
- **Open/Closed, deliberately bounded** — we do NOT build a pluggable "eligibility policy"
  abstraction (e.g. `IEligibilityPolicy` with swappable strategies). The brief explicitly
  says "do not build a configurable policy engine." One hard-coded rule
  (`PrerequisiteCourseId` + `CompletedCourses`), one hard-coded promotion policy
  (skip-and-retain). SOLID is a guide, not a license to add abstraction the task doesn't need.
- **Liskov / Interface Segregation** — `IRegistrationService` and `ICourseStore` each expose
  only the operations their consumers actually need; no speculative methods.

**Result type instead of exceptions for expected business outcomes.** Enroll/drop return a
small result type (e.g. `EnrollmentResult { Outcome, Message }` with `Outcome` one of
`Enrolled | Waitlisted | Rejected | Conflict | NotFound`) rather than throwing for 409/422/404
cases. Exceptions are reserved for truly unexpected failures, not normal control flow — this
keeps the controller's mapping to HTTP status codes a simple, visible switch, and keeps
service-layer tests assertion-based rather than exception-based.

**Thread safety, concretely:**
- One `private readonly object _lock = new();` inside `InMemoryCourseStore`.
- Every store method that reads or mutates shared state takes the lock for its *entire* body
  — no check-then-act gap between reading capacity/membership and writing the result.
- Store methods are synchronous and fast (in-memory dictionary ops only) — the lock is never
  held across an `await`, so there's no risk of a sync-over-async deadlock.
- Read methods (e.g. course status) return copies/snapshots, never live references to internal
  collections, so a caller can't mutate store state outside the lock.
- Test #12 (concurrency) must use real parallel execution (`Task.WhenAll` over concurrent
  calls), not sequential calls dressed up as a concurrency test.
- **Clarification on where the algorithm lives:** since the lock must live in
  `InMemoryCourseStore` and each compound operation (Enroll, Drop) must run under one lock
  acquisition to avoid a check-then-act gap, the decision-table and skip-and-retain
  algorithm is implemented directly inside `InMemoryCourseStore.Enroll()`/`Drop()`, not in
  `RegistrationService`. `RegistrationService` still exists as a thin delegation layer so
  the Controller depends on `IRegistrationService` (an abstraction, not a concrete store) —
  this preserves Dependency Inversion and testability without risking a lock gap.

**Input validation / injection surface.** There is no SQL, shell command, or file-path
construction anywhere in this service — all state is in-memory dictionaries keyed by plain
strings, so classic injection vectors (SQL injection, command injection, path traversal) do
not apply by construction. What we still do defensively:
- Request bodies are bound to dedicated DTOs, never directly to domain models — avoids
  over-posting/mass-assignment and keeps the API contract explicit and decoupled from the
  internal model shape.
- Basic validation on all incoming IDs (non-null, non-empty, reasonable length) and on
  `capacity` (must be a positive integer) — malformed requests return `400 Bad Request`,
  distinct from the business-rule codes (404/409/422) used for valid-but-rejected requests.
- No secrets, credentials, or real personal data anywhere in seed data, code, or the
  AI-USE.md record (matches the brief's explicit instruction).

---

## Project Structure

```
CourseRegistration/
├── src/
│   └── CourseRegistration.Api/
│       ├── CourseRegistration.Api.csproj
│       ├── Program.cs
│       ├── Models/
│       │   ├── Course.cs
│       │   ├── Learner.cs
│       │   ├── Enrollment.cs
│       │   └── WaitlistEntry.cs
│       ├── Store/
│       │   └── InMemoryCourseStore.cs
│       ├── Services/
│       │   ├── IRegistrationService.cs
│       │   └── RegistrationService.cs
│       └── Controllers/
│           └── RegistrationController.cs
└── tests/
    └── CourseRegistration.Tests/
        ├── CourseRegistration.Tests.csproj
        └── RegistrationServiceTests.cs
```

---

## REST API Endpoints

| Method | Path | Body | Purpose |
|---|---|---|---|
| POST | `/courses` | `{ courseId, capacity, prerequisiteCourseId? }` | Seed a course (test helper) |
| POST | `/learners` | `{ learnerId, completedCourses: [] }` | Seed a learner (test helper) |
| PATCH | `/learners/{learnerId}` | `{ completedCourses: [] }` | Update a learner's completed courses (test helper — lets a waitlisted learner become eligible) |
| POST | `/enrollments` | `{ courseId, learnerId }` | Enroll or join waitlist |
| DELETE | `/enrollments/{courseId}/{learnerId}` | — | Drop enrollment or remove from waitlist |
| GET | `/courses/{courseId}/status` | — | View seats, enrolled, waitlist |

---

## Core Invariants (must always hold)
1. `enrolledCount <= capacity` at all times.
2. A learner appears at most once across enrolled + waitlist for a given course.
3. Waitlist order is insertion order (FIFO).
4. Capacity and enrollment counts are updated atomically under the store lock.
5. A learner is placed on the waitlist only when the course is at full capacity at the
   time of the request. When a seat is available, the only possible outcomes are
   `Enrolled` or `Rejected (422)` — never `Waitlisted` — regardless of eligibility.

---

## Test Cases Required by Assessment

| # | Scenario | Expected |
|---|---|---|
| 1 | Enroll with capacity available | Returns `Enrolled` |
| 2 | Enroll when course is full, learner eligible | Returns `Waitlisted` |
| 3 | Duplicate enroll request (already enrolled) | Returns error / no duplicate |
| 4 | Duplicate waitlist request (already waitlisted) | Returns error / no duplicate |
| 5 | Drop enrolled learner; next eligible waitlisted learner promoted | Promoted learner now enrolled |
| 6 | Drop enrolled learner; first waitlisted is ineligible, second is eligible | Second learner promoted; first stays on waitlist |
| 7 | Prerequisite not met, capacity available (direct enrollment) | Rejected; not enrolled, not waitlisted |
| 8 | Prerequisite not met, course full | Learner joins waitlist (gate-at-the-seat) |
| 9 | Waitlisted ineligible learner later completes prerequisite, then a seat opens | Learner is promoted |
| 10 | Invalid drop (learner not enrolled or waitlisted) | Returns clear error; state unchanged |
| 11 | Repeated drop request | Returns clear error; state unchanged |
| 12 | Concurrency: two simultaneous enrollments for last seat | Exactly one enrolled, one waitlisted |
| 13 | Enroll request referencing a nonexistent courseId or learnerId | Returns 404; state unchanged |

---

## Deliverables Checklist

- [ ] Runnable .NET 8 Web API with in-memory store
- [ ] xUnit test project covering all 13 scenarios above
- [ ] `DESIGN.md` — service boundary, data model, invariants, promotion rule,
      concurrency approach, assumptions & tradeoffs
- [ ] `AI-USE.md` — prompts used, response summaries, accepted/edited/rejected,
      files affected, verification evidence
- [ ] `README.md` — setup and run instructions, test commands
- [ ] `LIMITATIONS.md` — known limitations and follow-up risks

---

## Suggested Build Order (for Claude in VS Code)

1. **Scaffold** — `dotnet new` commands, solution file, project references
2. **Models** — `Course`, `Learner`, `Enrollment`, `WaitlistEntry`
3. **Store** — `InMemoryCourseStore` with seed helpers and the lock
4. **Service** — `RegistrationService`: `Enroll`, `Drop` (with promotion logic)
5. **Tests** — write all xUnit tests (they will fail initially)
6. **Fix until green** — iterate on the service until all tests pass
7. **Controller** — thin HTTP layer wiring up the service
8. **Smoke test** — run the API and hit endpoints manually or with `.http` file
9. **Docs** — `DESIGN.md`, `AI-USE.md`, `README.md`, `LIMITATIONS.md`

---

## Prompt to Paste into Claude (VS Code) to Start

> I am working on a .NET 8 ASP.NET Core Web API for a course registration
> assessment. There is no starter project; I am building from scratch.
>
> The feature scope is: enroll a learner in a course, drop an enrollment, and
> manage a FIFO waitlist with prerequisite-based eligibility.
>
> Key decisions already made:
> - In-memory store with a single `lock` for thread safety
> - Prerequisite rule: each course has an optional `PrerequisiteCourseId`;
>   a learner is eligible if they have that course in their `CompletedCourses` set
> - Ineligible waitlist policy: skip-and-retain (skip ineligible learners when
>   promoting, they stay in their waitlist position)
> - xUnit for tests
>
> Please start at Step 1: scaffold the solution.
> Run these commands and show me the output:
> ```
> mkdir CourseRegistration && cd CourseRegistration
> dotnet new sln -n CourseRegistration
> dotnet new webapi -n CourseRegistration.Api -o src/CourseRegistration.Api
> dotnet new xunit -n CourseRegistration.Tests -o tests/CourseRegistration.Tests
> dotnet sln add src/CourseRegistration.Api
> dotnet sln add tests/CourseRegistration.Tests
> dotnet add tests/CourseRegistration.Tests reference src/CourseRegistration.Api
> ```
> After scaffolding, move on to Step 2: create the model classes.

---

## Notes for AI-USE.md (fill in as you go)

Track each AI interaction in this format:

```
### Interaction N
**Activity:** (e.g. "Generate model classes")
**Prompt:** (paste the prompt you used)
**Response summary:** (1–3 sentences on what it produced)
**Decision:** Accepted / Edited / Rejected
**What changed:** (if edited/rejected, what you changed and why)
**Verification:** (how you confirmed correctness — compiled, test passed, manual review)
**Files affected:** (list files)
```
