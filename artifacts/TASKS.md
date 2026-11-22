# Implementation Checklist — Course Waitlist

Durable, resumable task list. Work one task at a time, check it off (`[x]`) immediately
after it's verified done, and stop. If this session restarts, re-read this file plus
`course-registration-plan.md` and `AI-USE.md` — no need to re-derive design decisions,
they're already settled there. Pick up at the first unchecked `[ ]` task.

Each task should be small enough to finish and verify in one sitting. Don't batch
multiple unrelated tasks into one sitting even if it's tempting.

**Scope discipline:** every task below traces back to a specific PDF Acceptance Criterion,
Scope Boundary, or Deliverable — see the "Acceptance Criteria Traceability" table in
`course-registration-plan.md`. If implementing a task seems to require something not in
that table (a new endpoint, a new abstraction, a new dependency), stop and flag it instead
of building it — that's scope creep, not a missed requirement.

**Review checkpoint:** stop at the end of every phase and wait for explicit review/sign-off
before starting the next phase — do not self-approve and continue. If anything within a
phase is ambiguous or underspecified by the plan, stop and ask rather than guessing.

---

## Phase 0 — Design (done)
- [x] Resolve all open design questions (gate-at-the-seat, skip-and-retain, status code
      scheme, core invariants, engineering practices) — see `course-registration-plan.md`
- [x] Record design deliberation in `AI-USE.md`

## Phase 1 — Scaffold (done, pending review)
- [x] 1.1 Confirm `dotnet` SDK is available — SDK 9.0.102 installed, with .NET 8 runtime
      (8.0.12) also present; confirmed with user to target net8.0 as planned
- [x] 1.2 Create solution + both projects. Note: default `webapi` template uses minimal
      APIs, not controllers — regenerated the API project with `-controllers` flag to
      match the plan's Controllers-folder structure. Also passed `--no-openapi` to skip
      Swagger (not required by the brief, keeps dependencies minimal)
- [x] 1.3 `dotnet build` — succeeded, 0 warnings, 0 errors
- [x] 1.4 Removed template cruft: `WeatherForecastController.cs`, `WeatherForecast.cs`,
      default `UnitTest1.cs`. Rebuilt — still 0 warnings, 0 errors

## Phase 2 — Domain Models (done, pending review)
- [x] 2.1 `Models/Course.cs` — `CourseId`, `Capacity`, `PrerequisiteCourseId?`
- [x] 2.2 `Models/Learner.cs` — `LearnerId`, `CompletedCourses` (`HashSet<string>`)
- [x] 2.3 `Models/EnrollmentResult.cs` — `EnrollmentOutcome` enum (`Enrolled`,
      `Waitlisted`, `Rejected`, `Conflict`, `NotFound`, `Dropped`) + `EnrollmentResult`
      record (`Outcome`, `Message`); used for both enroll and drop operations
- [x] 2.4 `Models/Dtos/*.cs` — `SeedCourseRequest`, `SeedLearnerRequest`,
      `UpdateLearnerRequest`, `EnrollRequest`, `CourseStatusResponse`. Data-annotation
      validation added (`[Required(AllowEmptyStrings = false)]` on IDs,
      `[Range(1, int.MaxValue)]` on `Capacity`) so malformed bodies 400 automatically via
      `[ApiController]` model validation — no custom validation code needed
- [x] 2.5 `dotnet build` — 0 warnings, 0 errors

## Phase 3 — Store (thread-safe in-memory persistence + the atomic algorithm) — done, pending review
*(Architecture clarified with candidate: the lock must live in the Store, and each
compound operation must run under one lock acquisition, so the decision-table and
skip-and-retain algorithm is implemented directly in `InMemoryCourseStore`, not in
`RegistrationService` — see "Clarification on where the algorithm lives" in the plan.)*
- [x] 3.1 `Store/ICourseStore.cs` — `AddCourse`, `AddLearner`,
      `UpdateLearnerCompletedCourses`, `Enroll`, `Drop`, `GetStatus`
- [x] 3.2 `Store/InMemoryCourseStore.cs` — dictionaries for courses/learners/enrolled-sets/
      waitlists, single `_lock` object
- [x] 3.3 Seed methods implemented under the lock. `AddCourse` upserts course metadata but
      only initializes the enrolled/waitlist collections if they don't already exist, so
      re-seeding a course never silently wipes existing enrollment state
- [x] 3.4 `GetStatus` returns a snapshot (`.ToList()` copies), never a live reference —
      under the lock
- [x] 3.5 `Enroll` implements the decision table exactly (404 course/learner missing, 409
      duplicate enrolled/waitlisted, 422 ineligible+capacity-available, else
      enroll-if-eligible or waitlist-regardless-of-eligibility when full) — entirely
      inside the lock
- [x] 3.6 `Drop` implements promotion via `PromoteFirstEligible` — skip-and-retain, FIFO,
      promotes at most one learner per drop — entirely inside the lock. Caught and fixed
      a subtle bug before building: the first draft mutated the waitlist `List<string>`
      inside a `foreach` over that same list; switched to an index-based loop so the
      correctness doesn't depend on returning before the next `MoveNext()` call
- [x] 3.7 `dotnet build` — 0 warnings, 0 errors

## Phase 4 — Service (thin DI seam over the Store) — done, pending review
- [x] 4.1 `Services/IRegistrationService.cs` — mirrors `ICourseStore`'s signatures
- [x] 4.2 `Services/RegistrationService.cs` — thin one-line delegation to `ICourseStore`
      for every method; Controller will depend on this interface, not the store directly
- [x] 4.3 Registered `ICourseStore` → `InMemoryCourseStore` and `IRegistrationService` →
      `RegistrationService`, both singleton, in `Program.cs` (store must be singleton —
      it's the app's shared state; service is stateless so singleton is simplest there too)
- [x] 4.4 `dotnet build` — 0 warnings, 0 errors

## Phase 5 — Tests — done, pending review (all 14 passed on first run)
- [x] 5.1 Test 1 — enroll, capacity available → Enrolled
- [x] 5.2 Test 2 — enroll, full, eligible → Waitlisted
- [x] 5.3 Test 3 — duplicate enroll (already enrolled) → Conflict/409
- [x] 5.4 Test 4 — duplicate waitlist (already waitlisted) → Conflict/409
- [x] 5.5 Test 5 — drop enrolled learner → next eligible promoted
- [x] 5.6 Test 6 — skip-and-retain (first waitlisted ineligible, second eligible promoted,
      first stays in place)
- [x] 5.7 Test 7 — prerequisite not met, capacity available → Rejected/422
- [x] 5.8 Test 8 — prerequisite not met, course full → Waitlisted (gate-at-the-seat)
- [x] 5.9 Test 9 — waitlisted ineligible learner becomes eligible (via patch), then
      promoted on next drop
- [x] 5.10 Test 10 — invalid drop (never enrolled/waitlisted) → NotFound/404
- [x] 5.11 Test 11 — repeated drop → NotFound/404
- [x] 5.12 Test 12 — concurrency: parallel enroll requests for the last seat → exactly one
      Enrolled, one Waitlisted. Used a shared `ManualResetEventSlim` so both threads are
      released together instead of relying on scheduler timing to create contention
- [x] 5.13 Test 13 — enroll referencing nonexistent course/learner → NotFound/404
- [x] 5.14 Test 14 — Invariant #5, strengthened beyond the plan's minimum: exercises the
      trickier state where a seat is open AND the waitlist is non-empty (a prior drop had
      no eligible learner to promote), then confirms a new ineligible request is still
      rejected rather than appended to the existing waitlist
- [x] 5.15 `dotnet test` — 14/14 passed on first run, 362ms

*(Note: Test 15 — malformed request body → 400 — moved to Phase 6. It's a DTO/model-binding
concern that only exists once a Controller is bound to these DTOs; the Service methods take
plain strings, not request bodies, so there's nothing for this test to exercise yet. Testing
it via a full HTTP pipeline would need a new `Microsoft.AspNetCore.Mvc.Testing` package
dependency not named in the plan — instead it'll be a direct `Validator.TryValidateObject`
check against the DTOs, using the BCL's existing `System.ComponentModel.DataAnnotations`,
no new dependency.)*

*(Note: Test 15 — malformed request body → 400 — moved to Phase 6. It's a DTO/model-binding
concern that only exists once a Controller is bound to these DTOs; the Service methods take
plain strings, not request bodies, so there's nothing for this test to exercise yet. Testing
it via a full HTTP pipeline would need a new `Microsoft.AspNetCore.Mvc.Testing` package
dependency not named in the plan — instead it'll be a direct `Validator.TryValidateObject`
check against the DTOs, using the BCL's existing `System.ComponentModel.DataAnnotations`,
no new dependency.)*

## Phase 6 — Controller (thin HTTP layer) — done, pending review
- [x] 6.1 `POST /courses` → 201
- [x] 6.2 `POST /learners` → 201
- [x] 6.3 `PATCH /learners/{learnerId}` → 200, or 404 if learner doesn't exist
- [x] 6.4 `POST /enrollments` → 201 (Enrolled/Waitlisted), 409 (Conflict), 422 (Rejected),
      404 (NotFound)
- [x] 6.5 `DELETE /enrollments/{courseId}/{learnerId}` → 200 (Dropped), 404 (NotFound)
- [x] 6.6 `GET /courses/{courseId}/status` → 200, or 404 if course doesn't exist
- [x] 6.7 `MapResult` switches on `EnrollmentOutcome` → HTTP status code, exhaustive over
      all 6 outcomes with a 500 fallback arm; no business logic duplicated in the
      controller, just translation
- [x] 6.8 Added `DtoValidationTests.cs` (the deferred Test 15) — 4 tests directly against
      the DTOs' data annotations via `Validator.TryValidateObject`, no new package.
      `dotnet build` — 0 warnings/0 errors. `dotnet test` — 18/18 passed (14 service +
      4 DTO validation)

## Phase 7 — Security / robustness pass — done, pending review (clean pass, no issues found)
- [x] 7.1 Confirmed by reading `RegistrationController.cs`: every `[FromBody]` parameter
      is a DTO (`SeedCourseRequest`, `SeedLearnerRequest`, `UpdateLearnerRequest`,
      `EnrollRequest`); no domain model (`Course`/`Learner`) is ever bound directly
- [x] 7.2 Verified live, not just inferred: ran the API, POSTed `{"courseId":"",...}` to
      `/enrollments` → got real `400` with `"The CourseId field is required."` through the
      actual ASP.NET Core pipeline (not just the `Validator.TryValidateObject` unit
      tests). Also confirmed a valid seed+enroll round trip still succeeds, a duplicate
      correctly returns `409`, and `/courses/{id}/status` reflects the singleton store's
      state correctly across requests
- [x] 7.3 `grep` for `await` in `InMemoryCourseStore.cs` — zero matches outside comments;
      no lock is ever held across an await
- [x] 7.4 `grep` across the whole `CourseRegistration/` tree for
      password/secret/apikey/connectionstring/credential/email — zero matches;
      `appsettings.json` contains only default logging config, nothing sensitive

## Phase 8 — Smoke test — done, pending review (30/30 checks passed)
- [x] 8.1 `dotnet run` the API (twice — once for an ad hoc check, once fresh for the
      full walkthrough)
- [x] 8.2 `CourseRegistration.Api.http` rewritten with the full Alice/Bob/Carol/Dave/Erin
      walkthrough (replaced the template's leftover `/weatherforecast` request). Ran the
      exact same sequence against the live API via a scripted check (status code +
      resulting state asserted at every step) — **30/30 passed**, matching every outcome
      already verified in the design conversation exactly (including the trickier
      mid-sequence states: Carol retained on the waitlist after being skipped, then
      promoted after completing the prerequisite via `PATCH`; Dave/Carol ending up
      enrolled with an empty waitlist)

## Phase 9 — Documentation deliverables — done, pending review
- [x] 9.1 `CourseRegistration/DESIGN.md` written — condensed from
      `course-registration-plan.md` into a clean final spec (service boundary, data/API
      design, decision table, invariants, gate-at-the-seat rationale, skip-and-retain
      rule, already-enrolled boundary, concurrency, persistence, key tradeoffs)
- [x] 9.2 This phase logged in `AI-USE.md` (Interaction 17)
- [x] 9.3 `CourseRegistration/README.md` written (prerequisites, build, test, run,
      `.http` smoke-test instructions, project structure, pointers to the other
      deliverables)
- [x] 9.4 `CourseRegistration/LIMITATIONS.md` written — in-memory/no persistence,
      single-process-only concurrency, delayed promotion after a learner becomes
      eligible via `PATCH` (no second trigger), no retroactive eviction, minimal seeding
      semantics, no auth, no pagination, missing rubric file

**Packaging note (flagging, not yet resolved):** `DESIGN.md`/`README.md`/`LIMITATIONS.md`
were written inside `CourseRegistration/` (the actual submittable folder), but
`AI-USE.md` and `course-registration-plan.md` still live one level up, in `CharlesSchwabb/`
— where they've been edited throughout this session. Phase 10 should decide whether to
copy the final `AI-USE.md` into `CourseRegistration/` so the submission folder is fully
self-contained, or leave it referenced from one level up.

## Phase 10 — Final review
- [ ] 10.1 Re-read the PDF once more against the finished deliverables, confirm nothing
      missed
- [ ] 10.2 Confirm every deliverable file is present and runnable from a clean checkout
