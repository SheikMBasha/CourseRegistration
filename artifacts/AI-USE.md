# AI Prompt and Use Record — Course Waitlist Assessment

This record captures real interactions with Claude (in VS Code) used to design and
verify the Course Waitlist feature before implementation. Each entry reflects an
actual prompt/response exchange, the decision made, and how it was verified.

**Note on implementation-phase entries (from Interaction 8 onward):** during design
(Interactions 1–7), the "prompt" was a direct question or pushback from the candidate.
During implementation, Claude is writing the code directly against `TASKS.md` and
`course-registration-plan.md`, so entries are logged per build phase rather than per
checklist line — one entry per phase (Models, Store, Service, Tests, Controller,
Security pass, Smoke test), each covering what was produced and, most importantly,
**objective verification**: whether it compiled, which tests passed or failed on first
run, and what was fixed as a result. A phase that turns up a real bug or correction gets
its own entry even if that means splitting it from the rest of that phase, so the record
stays accurate to what actually happened rather than a reconstructed summary.

---

### Interaction 1
**Activity:** Review assessment PDF and existing draft plan (`course-registration-plan.md`)
to confirm the plan satisfies all stated acceptance criteria and deliverables before coding.
**Prompt:** "Go through the PDF and md file in the current folder 'CharlesSchwabb'. I would
like to discuss the plan and the deliverables."
**Response summary:** Claude read both files, confirmed the plan maps onto every acceptance
criterion and deliverable in the brief, and flagged several open design questions before
building: (1) ineligible-learner waitlist policy, (2) concurrency approach, (3) response
shape for duplicate/invalid requests, (4) whether eligibility could ever change after
waitlisting, (5) how to log AI use.
**Decision:** Accepted the plan as a sound starting point; proceeded to resolve the flagged
open questions before any code was written, rather than building first and discovering gaps.
**Verification:** Manual review of the plan against the PDF's Acceptance Criteria and
Deliverables sections, item by item.
**Files affected:** `course-registration-plan.md` (reviewed, not yet edited)

---

### Interaction 2
**Activity:** Decide the ineligible-learner waitlist policy (skip-and-retain vs.
skip-and-remove) and, more fundamentally, *when* eligibility is enforced.
**Prompt (paraphrased across several turns):** "If we apply a certain policy [to a learner
found ineligible during promotion], do we apply the same and remove then as ineligible
learners?" ... "Aren't we checking eligibility while a candidate is enrolling? If yes, is
checking it again at drop/promotion time just a defensive check?"
**Response summary:** Claude identified that the plan, as drafted, checked eligibility both
at waitlist-join time and at promotion time — which meant no ineligible learner could ever
reach the waitlist, making the promotion-time check dead/defensive code. Claude proposed two
consistent alternative designs: "gate at the door" (eligibility required to join the
waitlist; promotion check is a pure safety net) vs. "gate at the seat" (anyone may join the
waitlist; eligibility is only enforced at the moment of promotion). Recommended gate-at-the-
seat as the better match for the brief's own framing ("what happens when an ineligible
learner reaches the front of the queue").
**Decision:** Accepted "gate at the seat." This also surfaced a gap: the plan needed a way
for a learner's `CompletedCourses` to be updated *after* they join the waitlist, otherwise
the "becomes eligible later" path could never be tested. Added a `PATCH /learners/{learnerId}`
endpoint and a corresponding test case to the plan.
**Verification:** Re-read the PDF's Acceptance Criteria #2 and #4 and the Candidate
Expectations section to confirm "gate at the seat" is consistent with the literal wording
(AC4 says an ineligible learner is not "enrolled or promoted" — notably not "waitlisted").
**Files affected:** `course-registration-plan.md` (Decisions Made table, Prerequisite Rule
Detail, Waitlist Promotion Rule, REST API Endpoints, Test Cases table)

---

### Interaction 3
**Activity:** Decide HTTP status codes for duplicate, invalid, and rule-rejected requests.
**Prompt:** "409 makes sense to me, but 400 looks generic, I am bit confused. how about 422
or 412?"
**Response summary:** Claude explained the semantic distinction between 400 (malformed
request), 404 (referenced resource doesn't exist), 409 (conflicts with existing state), 422
(well-formed request that fails a business rule), and 412 (HTTP conditional-request headers
— not a fit here). Recommended: 409 for duplicate enroll/waitlist, 422 for prerequisite-
rejected direct enrollment, 404 for invalid/repeated drop.
**Decision:** Accepted after a follow-up clarifying question about why repeated drop should
be 404 rather than 409 (resolved: `DELETE /enrollments/{courseId}/{learnerId}` names a
record; if that record doesn't exist — whether it never existed or was already deleted —
404 is correct either way; 409 is reserved for when the record *does* exist and the new
request conflicts with it).
**Verification:** Checked the reasoning against standard HTTP semantics (RFC 7231/7232) for
each code before accepting; no code written yet to test against.
**Files affected:** `course-registration-plan.md` (new "Error / Status Code Scheme" section)

---

### Interaction 4
**Activity:** Re-examine the skip-and-retain policy for a performance concern after the
gate-at-the-seat decision made it an actively-exercised code path (not just a rare edge case).
**Prompt:** "should we keep skip and retain? isnt it adding overhead of checking ineligible
learners again and again?"
**Response summary:** Claude confirmed the overhead is real (an ineligible learner at the
front gets re-checked on every subsequent promotion scan) but argued it's negligible at this
scope (single bounded course offering, small waitlist) and that the alternative
(skip-and-remove) trades that trivial performance cost for a real behavioral regression —
removing a learner's chance to wait until they complete the prerequisite, which was the
entire point of choosing gate-at-the-seat.
**Decision:** Kept skip-and-retain. Accepted the reasoning that retaining an ineligible
learner preserves their queue position/opportunity instead of forcing them to restart from
the back of the line — this was the deciding factor over raw performance concerns, which
are not meaningful at this scale.
**Verification:** Reasoned through the brief's own guidance ("prioritize correct...behavior
...over extra infrastructure", "do not over-engineer") to confirm the performance tradeoff
is not a real-world constraint at the stated scope.
**Files affected:** None yet (confirms existing `course-registration-plan.md` entry; no
change needed)

---

### Interaction 5
**Activity:** Re-examine the "gate at the door" vs. "gate at the seat" decision after
re-reading the Problem Statement and AC2 more literally ("eligible learners join the
waitlist," stated twice), and a follow-up question about whether AC4 implies retroactive
eviction of an already-enrolled learner who later becomes ineligible.
**Prompt (paraphrased):** "does this mean ineligible learner will not be able to join the
waitlist?" ... "does this mean if an eligible candidate becomes ineligible post enrollment,
should we drop them when we promote a candidate from waitlist?" ... "Gate at the seat is
better, it allows the candidates to join the waitlist and also ensures we check the
eligibility before promoting again."
**Response summary:** Claude re-weighed the textual evidence for both designs (two explicit
statements favoring "gate at the door" vs. an argument-from-silence and an ambiguous phrase
favoring "gate at the seat"), initially recommended switching to gate-at-the-door, then
presented both sides for a final call. Separately clarified that AC4 ("not enrolled or
promoted") gates only the two admission actions (enroll, promote) and does not imply
continuous re-validation or retroactive eviction of an already-seated learner.
**Decision:** Kept **gate at the seat** as the final design (candidate's explicit call,
after weighing both readings) — chosen because it lets a learner wait and become eligible
before their turn, and still enforces the prerequisite at the actual moment of promotion.
Confirmed no retroactive eviction of already-enrolled learners under either design. No file
changes required (plan already reflected gate-at-the-seat; the door-ward revision was never
applied).
**Verification:** Compared literal wording across the Problem Statement, AC2, AC4, and
Candidate Expectations sections of the PDF before presenting the tradeoff; deferred the
final call to the candidate as a genuine judgment call rather than a purely textual one.
**Files affected:** `AI-USE.md` only (this entry) — `course-registration-plan.md` unchanged.

---

### Interaction 6
**Activity:** Confirm handling of a negative scenario — an already-enrolled learner
re-requesting enrollment in the same course.
**Prompt:** "Also a negative scenario, if an enrolled candidate tries to join/enroll again,
try to stop them."
**Response summary:** Claude confirmed this was already covered: the enroll flow checks
membership in both the enrolled set and the waitlist before admitting a request, returning
`409 Conflict` with no state mutation if the learner is already in either — this is test
case #3 in the plan's test table (and #4 for the already-waitlisted variant).
**Decision:** Accepted as already covered; no plan changes required.
**Verification:** Cross-checked against the existing test table and Error/Status Code
Scheme section in `course-registration-plan.md`.
**Files affected:** None (confirmation only).

---

### Interaction 7
**Activity:** Verify worked examples against the plan before implementation; resolve a
question about whether an ineligible learner should be waitlisted even when a seat is
open (so they "keep a place in line" for later).
**Prompt (paraphrased):** "should we still keep Erin into waitlist incase she comes back
later and still has a position in the queue?" ... "with this the design/decision would
remain consistent? or should we not keep waitlist if course is empty? Open to suggestions"
**Response summary:** Claude explained that waitlisting against an open seat would never
resolve under the existing drop-triggered promotion mechanism (no drop event ever fires
for an already-open seat), that a queue position is meaningless without competition for a
scarce resource, and that simply re-requesting enrollment later achieves the same outcome
with no new mechanism needed. Confirmed the existing decision table already guarantees
waitlist entries only occur when capacity is unavailable, and proposed making that an
explicit, testable invariant rather than leaving it implicit.
**Decision:** Accepted — rejection (422) stands for capacity-available + ineligible, no
waitlist entry. Added a new Core Invariant #5: a learner is waitlisted only when the
course is at full capacity at request time.
**Verification:** Re-derived the decision table row-by-row to confirm no combination of
inputs could produce a waitlist entry while capacity was available.
**Files affected:** `course-registration-plan.md` (Core Invariants section)

---

### Interaction 8
**Activity:** Phase 1 — scaffold the .NET solution (`TASKS.md` tasks 1.1–1.4).
**Prompt:** Execute `TASKS.md` Phase 1 against the decisions already recorded in
`course-registration-plan.md` ("Language/Runtime: .NET 8", Project Structure's
Controllers-folder layout).
**Response summary:** Ran `dotnet new sln/webapi/xunit`, wired up project references.
Two things surfaced that weren't fully specified by the plan and were checked before
proceeding rather than assumed:
1. The installed SDK is 9.0.102 (not 8.x) — confirmed the .NET 8 runtime is also present
   and asked the candidate whether to keep targeting net8.0 or move to net9.0; candidate
   chose to keep net8.0, matching the existing plan decision.
2. The default `webapi` template generates minimal-API-style code (`Program.cs` with
   `MapGet`), not MVC controllers — this conflicted with the plan's explicit
   `Controllers/RegistrationController.cs` structure, so the API project was regenerated
   with the `-controllers` flag. Also passed `--no-openapi` to skip Swagger, since it's
   not required by the brief and keeps the dependency surface minimal (judgment call, not
   asked, flagged here for visibility).
**Decision:** Accepted. Removed generated sample files (`WeatherForecastController.cs`,
`WeatherForecast.cs`, `UnitTest1.cs`) not needed for this feature.
**Verification:** `dotnet build` after initial scaffold — 0 warnings/0 errors. Re-ran
`dotnet build` after removing template cruft — still 0 warnings/0 errors.
**Files affected:** `CourseRegistration.sln`, `src/CourseRegistration.Api/**`,
`tests/CourseRegistration.Tests/**`

---

### Interaction 9
**Activity:** Phase 2 — domain models and DTOs (`TASKS.md` tasks 2.1–2.5).
**Prompt:** Execute `TASKS.md` Phase 2 against the plan's Prerequisite Rule Detail,
Engineering Practices (DTOs separate from domain models, input validation), and Error/
Status Code Scheme sections.
**Response summary:** Created `Course`, `Learner` domain models; `EnrollmentOutcome`
enum + `EnrollmentResult` record as the shared result type for enroll/drop (avoids
exceptions for expected business outcomes, per the plan); five DTOs under `Models/Dtos/`
with data-annotation validation (`[Required(AllowEmptyStrings = false)]` on IDs,
`[Range(1, int.MaxValue)]` on `Capacity`) so malformed requests get a `400` for free via
ASP.NET Core's built-in `[ApiController]` model validation, with no custom validation
code to write or test separately.
**Decision:** Accepted as designed — no deviation from the plan needed here, this phase
was mechanical given how much was already settled in `course-registration-plan.md`.
**Verification:** `dotnet build` — 0 warnings, 0 errors.
**Files affected:** `Models/Course.cs`, `Models/Learner.cs`,
`Models/EnrollmentResult.cs`, `Models/Dtos/SeedCourseRequest.cs`,
`Models/Dtos/SeedLearnerRequest.cs`, `Models/Dtos/UpdateLearnerRequest.cs`,
`Models/Dtos/EnrollRequest.cs`, `Models/Dtos/CourseStatusResponse.cs`

---

### Interaction 10
**Activity:** Phase 3 — thread-safe in-memory store, including the Enroll/Drop algorithm
(`TASKS.md` tasks 3.1–3.7).
**Prompt:** Execute `TASKS.md` Phase 3. Before writing code, surfaced an architectural
tension to the candidate: the plan's Engineering Practices section says the lock must
live in `InMemoryCourseStore`, with every store method holding the lock for its entire
body (no check-then-act gap) — but the plan's Project Structure section describes the
decision-table/eligibility/skip-and-retain algorithm as living in `RegistrationService`.
Running that algorithm across several separately-locked Store calls would reopen the
exact race the lock exists to prevent.
**Response summary:** Presented two options: (a) move the full algorithm into
`InMemoryCourseStore.Enroll()`/`Drop()`, each wrapped in one lock acquisition, with
`RegistrationService` reduced to a thin delegation layer (kept only for Dependency
Inversion/testability — Controller depends on `IRegistrationService`, not the concrete
store); (b) keep the algorithm in `RegistrationService` and add a new
`ExecuteAtomic<T>(Func<T>)` callback on the store. Candidate chose (a) — no new
abstraction needed, atomicity correct by construction.
**Decision:** Accepted. Implemented `Enroll` and `Drop` directly in
`InMemoryCourseStore`, matching the decision table and skip-and-retain rule from the
plan exactly. While implementing `Drop`'s promotion scan, caught a real bug before
building: the first draft removed an item from the `waitlist` list while a `foreach`
was still enumerating it. It happened to be safe only because the method returns
immediately after the mutation (no further `MoveNext()` call), which is a fragile,
non-obvious guarantee to depend on — rewrote as an index-based `for` loop so correctness
doesn't rely on that detail.
**Verification:** `dotnet build` — 0 warnings, 0 errors, both before and after the
foreach/mutation fix.
**Files affected:** `course-registration-plan.md` (added the architecture
clarification), `TASKS.md` (Phase 3/4 descriptions updated to match),
`Store/ICourseStore.cs`, `Store/InMemoryCourseStore.cs`

---

### Interaction 11
**Activity:** Lock vs. `SemaphoreSlim` question, and Phase 4 — the thin Service layer
(`TASKS.md` tasks 4.1–4.4).
**Prompt:** "do we need to use semaphore slim or is this lock sufficient?" ... "perfect,
add this reasoning and move to Phase 4."
**Response summary:** Explained that `lock`/`Monitor` is the correct choice because every
critical section in the store is purely synchronous (no `await` inside it) —
`SemaphoreSlim`'s value is its async `WaitAsync()`, which buys nothing here and adds
overhead plus manual `Release()`/`Dispose()` discipline for no benefit. Added that
reasoning as a code comment on `InMemoryCourseStore._lock`. Then implemented
`IRegistrationService`/`RegistrationService` as a one-line-per-method delegation to
`ICourseStore`, and registered both in DI as singletons in `Program.cs` (store must be
singleton to persist shared state across requests; service is stateless so singleton is
simplest there too).
**Decision:** Accepted both — no deviation from the agreed Phase 3 architecture.
**Verification:** `dotnet build` — 0 warnings, 0 errors, after the comment addition and
again after the Service layer + DI registration.
**Files affected:** `Store/InMemoryCourseStore.cs` (comment only),
`Services/IRegistrationService.cs`, `Services/RegistrationService.cs`, `Program.cs`

---

### Interaction 12
**Activity:** Phase 5 — full xUnit test suite (`TASKS.md` tasks 5.1–5.15).
**Prompt:** Execute `TASKS.md` Phase 5: write all planned test scenarios against
`IRegistrationService`, run them, and fix anything red.
**Response summary:** Wrote 14 tests in `RegistrationServiceTests.cs`, one per scenario
in the plan's test table, using a fresh `InMemoryCourseStore`/`RegistrationService` pair
per test (xUnit's per-`[Fact]` instantiation gives natural isolation). Two deviations
from a literal reading of the plan, both flagged rather than silently done:
1. Test 15 (malformed body → 400) moved to Phase 6 — it's a DTO/model-binding concern
   that doesn't exist yet at the Service layer (Service methods take plain strings, not
   request DTOs), and testing it properly would need a new `Microsoft.AspNetCore.Mvc.Testing`
   package not named in the plan. Deferred to a direct `Validator.TryValidateObject` check
   instead, avoiding the new dependency.
2. Test 14 (Invariant #5) was strengthened beyond the plan's minimum description. Rather
   than just repeating Test 7's scenario, it specifically constructs the trickier state
   where a seat is open AND the waitlist is non-empty (a prior drop found no eligible
   learner to promote), then confirms a new ineligible request is still rejected rather
   than silently appended to the already-nonempty waitlist — the case most likely to
   actually catch a bug in the invariant.
3. Test 12 (concurrency) uses a shared `ManualResetEventSlim` to release both threads
   together, rather than just firing two `Task.Run` calls and hoping the scheduler
   creates contention — makes the test actually exercise the race rather than possibly
   passing by accident due to serialized scheduling.
**Decision:** Accepted all three as improvements on the literal plan text, not
deviations from its intent.
**Verification:** `dotnet test` — **14/14 passed on the first run**, 362ms. No bugs
surfaced by test execution itself this phase (the one real bug this build caught — the
foreach/mutation issue — was caught during Phase 3 code review, before any test ran).
**Files affected:** `tests/CourseRegistration.Tests/RegistrationServiceTests.cs`,
`TASKS.md` (Phase 5/6 split noted)

---

### Interaction 13
**Activity:** Phase 6 — Controller and the deferred Test 15 (`TASKS.md` tasks 6.1–6.8).
**Prompt:** Execute `TASKS.md` Phase 6: build the thin HTTP layer per the plan's REST API
Endpoints table, map `EnrollmentOutcome` to the agreed status codes, and resolve the Test
15 deferral from Phase 5.
**Response summary:** Implemented `RegistrationController` with all six endpoints from
the plan, each a thin translation to `IRegistrationService` plus a `MapResult` switch
over `EnrollmentOutcome` (Enrolled/Waitlisted → 201, Dropped → 200, Conflict → 409,
Rejected → 422, NotFound → 404, with a 500 fallback arm to keep the switch exhaustive).
`[ApiController]` + the DTOs' existing data annotations give automatic 400 responses for
malformed bodies with no custom validation code. Added `DtoValidationTests.cs` — 4 tests
directly asserting the DTO annotations reject missing IDs and non-positive capacity,
using the BCL's `Validator.TryValidateObject` (no new test-hosting package).
**Decision:** Accepted as designed — no deviation from the plan's endpoint/status-code
scheme.
**Verification:** `dotnet build` — 0 warnings, 0 errors. `dotnet test` — **18/18
passed** (14 service-layer tests from Phase 5 + 4 new DTO validation tests), no
regressions in the existing suite.
**Files affected:** `Controllers/RegistrationController.cs`,
`tests/CourseRegistration.Tests/DtoValidationTests.cs`

---

### Interaction 14
**Activity:** Two scope-boundary questions after Phase 6: (1) whether to add in-process
startup seed data, (2) whether to add `CourseName` to `Course` and `Name`/`Email` to
`Learner`. Candidate then pressed on whether the PDF literally prohibits these additions.
**Prompt:** "should we also have a layer where we load some courses and learners when
the service starts..." ... "should I add coursename in coursemodel and in learner the
name, email fields?" ... "is it called out anywhere in the pdf nott to add anything?"
**Response summary:** Recommended against both additions, citing: no test/acceptance
criterion traces to either field; the existing `POST /courses`/`POST /learners` "test
helper" endpoints already cover the seeding need without a second, divergent seeding
path; and for `Email` specifically, the brief's explicit PII instruction. When pressed on
whether the PDF literally forbids this, gave an honest, precise answer: no literal
sentence bans extra fields — the objection rests on an inference from the bounded-scope
language ("not a complete registration platform," "prioritize correctness/tests over
feature count") plus a self-imposed traceability convention (`TASKS.md`'s guardrail,
invented for this build, not PDF text), and the PII clause is explicitly scoped to
"prompts or submitted prompt records," not model fields in general — stretched to argue
against `Email`, flagged as a stretch rather than overstated as a direct quote.
**Decision:** Candidate confirmed: no startup seeding layer, no extra fields on `Course`
or `Learner`. Models stay exactly as built in Phase 2.
**Verification:** Re-quoted the PDF's exact Scope Boundaries and Candidate Expectations
lines to distinguish what's literally stated from what's inferred, rather than asserting
a prohibition that doesn't exist in the source text.
**Files affected:** None — no code changes; decision was to keep existing models as-is.

---

### Interaction 15
**Activity:** Phase 7 — security/robustness pass (`TASKS.md` tasks 7.1–7.4).
**Prompt:** Execute `TASKS.md` Phase 7: verify each item concretely rather than asserting
it from memory.
**Response summary:** Read `RegistrationController.cs` fresh to confirm every endpoint
binds a DTO, never a domain model. Grepped `InMemoryCourseStore.cs` for `await` — none
found outside comments, confirming no lock is ever held across an async boundary.
Grepped the whole project tree for password/secret/apikey/connectionstring/
credential/email — zero matches; checked `appsettings.json` directly — only default
logging config. For the 400-on-malformed-body check, went further than re-asserting the
Phase-6 unit tests: actually started the API (`dotnet run` in the background) and sent a
real malformed POST to `/enrollments`, getting a genuine `400` with a clear validation
message through the real ASP.NET Core pipeline — not just the isolated
`Validator.TryValidateObject` check from Phase 6. Also sent a valid seed+enroll request
(succeeded), repeated it (correctly got `409`), and checked `/courses/{id}/status`
reflected the singleton store's state correctly across separate HTTP requests.
**Decision:** All four checks passed cleanly — no issues found, no code changes needed
this phase.
**Verification:** Live HTTP round trip against a running instance of the API (not just
unit tests) for the validation/duplicate/state-persistence checks; direct `grep` for the
lock/secrets checks.
**Files affected:** None — verification only, no code changes.

---

### Interaction 16
**Activity:** Phase 8 — full smoke test against the live API (`TASKS.md` tasks 8.1–8.2).
**Prompt:** Execute `TASKS.md` Phase 8: run the API and replay the Alice/Bob/Carol/Dave/
Erin worked examples from the earlier design conversation, confirming actual responses
match what was verified by reasoning through them earlier.
**Response summary:** Replaced the template's leftover `/weatherforecast` request in
`CourseRegistration.Api.http` with the full worked-example sequence (17 steps covering
every test scenario plus the trickier mid-sequence states). Ran the API live and
replayed the exact same sequence via a scripted check that asserts both the HTTP status
code and the resulting enrolled/waitlist state at each step — not just a visual
inspection of a `.http` file. First attempt's diagnostic output was accidentally
swallowed by piping to `Out-Null` (which captures everything a PowerShell function
writes to its output stream, not just its explicit return value) — fixed by switching
the script's diagnostics to `Write-Host`, then re-ran against a freshly restarted
instance for a clean run.
**Decision:** Accepted — **30/30 checks passed**, including the PowerShell script fix
itself (caught and corrected before trusting the first, silent run).
**Verification:** Live HTTP round trip through the full scenario sequence: every status
code (201/409/200/404/422/400) and every resulting `enrolledLearnerIds`/
`waitlistLearnerIds` state matched the outcomes already verified by reasoning through
the same walkthrough earlier in the design conversation.
**Files affected:** `CourseRegistration.Api.http`

---

### Interaction 17
**Activity:** Phase 9 — documentation deliverables (`TASKS.md` tasks 9.1–9.4).
**Prompt:** Execute `TASKS.md` Phase 9: write `DESIGN.md`, `README.md`, and
`LIMITATIONS.md`, condensing the working plan into final deliverables rather than
copy-pasting it wholesale.
**Response summary:** Wrote `DESIGN.md` as a clean final spec — service boundary,
data/API design, the decision table, core invariants, the "gate at the seat" rationale
(condensed from the multi-turn deliberation into one paragraph), skip-and-retain,
the already-enrolled-learner boundary, concurrency/persistence reasoning, and key
tradeoffs (DataAnnotations over FluentValidation, no policy engine, minimal seeding
semantics). Wrote `README.md` with build/test/run instructions and `.http`
smoke-test pointers. Wrote `LIMITATIONS.md` covering: in-memory/no persistence,
single-process-only concurrency (won't coordinate across multiple instances), delayed
promotion for a learner who becomes eligible via `PATCH` (no second trigger beyond
drop-driven promotion), no retroactive eviction, minimal seeding semantics, no auth, no
status-endpoint pagination, and the missing `Course-Registration-Rubric-Score.md` file.
**Decision:** Accepted. Flagged one open packaging question rather than deciding it
silently: these three files live in `CourseRegistration/` (the submittable folder), but
`AI-USE.md` and `course-registration-plan.md` still live one level up — left for Phase
10 to resolve (copy into `CourseRegistration/` for a self-contained submission, or leave
as-is).
**Verification:** Cross-checked each file's content against `course-registration-plan.md`
and this record to confirm nothing decided earlier in the conversation was dropped or
contradicted during condensation.
**Files affected:** `CourseRegistration/DESIGN.md`, `CourseRegistration/README.md`,
`CourseRegistration/LIMITATIONS.md`

---

### Interaction 18
**Activity:** Packaging — resolve where process documents live relative to the
submittable solution folder.
**Prompt:** "yes please move it to some artifacts folder or anything meaningful."
**Response summary:** Created `CourseRegistration/artifacts/` and moved this file,
`course-registration-plan.md`, and `TASKS.md` into it — keeping `DESIGN.md`,
`README.md`, and `LIMITATIONS.md` at the `CourseRegistration/` root as the primary,
easy-to-find deliverables, while process/working documents (the AI-use record, the
planning doc, and the resumable checklist) live together in `artifacts/`. Updated
`README.md` and `LIMITATIONS.md`'s cross-references to the new `artifacts/AI-USE.md`
path.
**Decision:** Accepted. The whole `CourseRegistration/` folder is now self-contained —
everything needed to run, test, and review the submission lives under it.
**Verification:** Directory listing confirmed all three files moved intact; grepped
`DESIGN.md` and `LIMITATIONS.md` for stale path references and fixed the one found.
**Files affected:** `AI-USE.md`, `course-registration-plan.md`, `TASKS.md` (all moved),
`README.md`, `LIMITATIONS.md` (path references updated)

---

<!-- Further interactions (model/service/test generation, etc.) to be appended as the
implementation proceeds. -->
