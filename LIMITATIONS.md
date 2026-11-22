# Limitations & Follow-Up Risks

## In-memory only — no persistence across restarts

State lives in a single `InMemoryCourseStore` singleton inside the process. Restarting
the API loses all courses, learners, enrollments, and waitlists. This is the explicitly
allowed fallback for this assessment (no starter project, no required database), but a
real deployment would need a persistent backing store.

## Single-process concurrency only

The `lock` inside `InMemoryCourseStore` guarantees correctness within one process. It
does **not** coordinate across multiple instances of the service — running more than
one instance (e.g., behind a load balancer) would give each instance its own independent
store, silently reintroducing the exact race conditions the lock exists to prevent
(e.g., two instances could each believe they filled the "last seat"). Horizontal scaling
would require a shared, transactionally-consistent data store, not in-memory locking.

## Eligibility becoming true after waitlisting is not promoted immediately

A waitlisted learner who becomes eligible via `PATCH /learners/{learnerId}` is only
promoted on the *next drop* for that course — there is no second trigger that re-scans
the waitlist when eligibility changes. If no further drop ever occurs for that course,
that learner remains waitlisted indefinitely even though they're now eligible. This was
a deliberate scope decision (see `DESIGN.md`) to avoid introducing a promotion trigger
beyond what the brief describes ("if a seat opens, promote..."), not an oversight — but
it's a real gap a production system would likely need to close.

## No retroactive eviction for already-enrolled learners

If an enrolled learner's eligibility later regresses (e.g., a completed-course credit is
reversed), they keep their seat. Eligibility is checked only at the moment a seat is
gained, never re-validated afterward. Documented as an intentional boundary in
`DESIGN.md`, not a bug.

## Minimal seeding semantics

`POST /courses` and `POST /learners` are test helpers, not a hardened catalog API.
Re-seeding an existing course ID overwrites its capacity/prerequisite but preserves
existing enrollment/waitlist state (not reset); there's no conflict response (`409`) for
re-seeding an existing ID. Fine for constructing test scenarios; would need hardening
for any real multi-user seeding workflow.

## No authentication, authorization, or rate limiting

Explicitly out of scope per the assessment brief (no identity integration). Every
endpoint is open. Not suitable to expose as-is beyond this exercise.

## Status endpoint has no pagination

`GET /courses/{courseId}/status` returns the full enrolled/waitlist lists in one
response. Fine at this scale (one bounded course offering); would not scale to a
course with a very large waitlist.

## Referenced rubric file was not available

The assessment PDF references `Course-Registration-Rubric-Score.md` for self-scoring.
That file was not present among the provided materials and could not be completed as
part of this submission. It does not affect any of the listed Deliverables (code, tests,
`DESIGN.md`, `artifacts/AI-USE.md`, `README.md`, this file), which are all complete.
