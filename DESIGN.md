# Design Specification — Course Waitlist Service

## Service Boundary

A single bounded slice: enroll, drop, and FIFO waitlist promotion for one course
offering at a time. The data model technically supports multiple `Course` records (so
test scenarios can seed more than one course), but this is a byproduct of making the
feature testable, not a catalog — there is no search, listing, or multi-term capability.
No frontend, no authentication/identity, no payments, no persistence beyond process
memory, no deployment concerns. These are deliberately out of scope per the assessment
brief.

## Data Model

| Type | Fields |
|---|---|
| `Course` | `CourseId` (string), `Capacity` (int), `PrerequisiteCourseId` (string?, nullable) |
| `Learner` | `LearnerId` (string), `CompletedCourses` (`HashSet<string>`) |

Enrollment and waitlist membership are **not** separate entity classes — they're
store-internal state: an enrolled set (`HashSet<string>` of learner IDs) and an ordered
waitlist (`List<string>`, insertion order = FIFO), one pair per course. There's no
standalone "Enrollment" record to persist; membership in one of these two collections
*is* the enrollment state.

Request/response DTOs (`SeedCourseRequest`, `SeedLearnerRequest`, `UpdateLearnerRequest`,
`EnrollRequest`, `CourseStatusResponse`) are kept separate from these domain models, so
the HTTP contract can't accidentally be widened by exposing internal model shape
(over-posting/mass-assignment protection), and so request validation lives with the
request shape, not the domain.

## API

| Method | Path | Body | Purpose |
|---|---|---|---|
| `POST` | `/courses` | `{ courseId, capacity, prerequisiteCourseId? }` | Seed a course (test helper) |
| `POST` | `/learners` | `{ learnerId, completedCourses: [] }` | Seed a learner (test helper) |
| `PATCH` | `/learners/{learnerId}` | `{ completedCourses: [] }` | Update a learner's completed courses — the only way eligibility changes after seeding |
| `POST` | `/enrollments` | `{ courseId, learnerId }` | Enroll or join the waitlist |
| `DELETE` | `/enrollments/{courseId}/{learnerId}` | — | Drop an enrollment, or voluntarily leave the waitlist |
| `GET` | `/courses/{courseId}/status` | — | View capacity, enrolled learners, and waitlist (in order) |

### Status codes

| Situation | Code |
|---|---|
| Enroll/waitlist succeeds | `201` |
| Drop succeeds | `200` |
| Malformed request body (missing/empty ID, non-positive capacity) | `400` |
| Referenced course or learner doesn't exist | `404` |
| Drop request with no matching enrollment/waitlist record | `404` |
| Duplicate enroll or waitlist request | `409` |
| Prerequisite not met, capacity available (direct enrollment rejected) | `422` |

`400` comes for free from `[ApiController]`'s automatic model validation against data
annotations on the DTOs (`[Required]`, `[Range(1, int.MaxValue)]`) — no custom validation
code. `404`/`409`/`422` are business outcomes returned explicitly by the service/store,
represented as an `EnrollmentResult { Outcome, Message }` rather than thrown exceptions,
since these are expected, normal-flow outcomes, not failures.

## Enrollment Decision Table

| Capacity available? | Learner eligible? | Outcome |
|---|---|---|
| Yes | Yes | Enrolled |
| Yes | No | Rejected (`422`) — no seat taken, no waitlist entry |
| No | Yes | Waitlisted |
| No | No | Waitlisted — see "Gate at the Seat" below |

**Invariant:** a learner is placed on the waitlist *only* when the course is at full
capacity at request time. When a seat is open, the only possible outcomes are Enrolled
or Rejected — never Waitlisted, regardless of eligibility. This holds even when the
waitlist already has entries from an earlier full period (see Core Invariants).

## Eligibility Rule

A learner is eligible for a course if it has no prerequisite, or the learner's
`CompletedCourses` contains the prerequisite course ID.

## Gate at the Seat

Eligibility is enforced for **direct enrollment** and at **promotion**, but *not* as a
condition for joining the waitlist. An ineligible learner can join the waitlist while a
course is full; they simply can't be promoted until eligible.

This resolves a real tension in the brief: the Problem Statement and Acceptance
Criterion 2 both say "eligible learners join the waitlist," which read literally would
exclude ineligible learners from the waitlist entirely. But Acceptance Criterion 4 says
an ineligible learner is "not enrolled or promoted" — notably not "or waitlisted" — and
the Candidate Expectations section asks what happens "when an ineligible learner reaches
the front of the queue," which only describes a meaningful, routinely-exercised scenario
if ineligible learners can legitimately be waitlisted. "Gate at the seat" was chosen
deliberately over the stricter alternative ("gate at the door") because it lets a
learner wait and complete a prerequisite before their turn comes, rather than forcing
them to restart from the back of the line the moment they're found ineligible.

## Waitlist Promotion Rule (Skip-and-Retain)

On a drop:
1. Scan the waitlist front-to-back (FIFO order).
2. Promote the **first eligible** learner found — move them to enrolled, remove from
   the waitlist.
3. Any learner skipped along the way (found ineligible during the scan) **stays in
   their original waitlist position** — never removed, never reordered.
4. If no eligible learner exists, the seat stays open and the waitlist is left
   unchanged.

A promoted or dropped learner's eligibility is never re-checked afterward — see
"Already-Enrolled Learners," below.

Skip-and-retain was chosen over "skip-and-remove" (evicting an ineligible learner from
the waitlist the first time a scan reaches them) because the latter effectively gives a
learner exactly one chance — whatever their eligibility happens to be the instant a seat
first opens — which can unfairly cut off someone who would have completed the
prerequisite given a bit more time. The repeated-rescan cost of skip-and-retain is
O(waitlist length) per drop, which is negligible at this scope (one bounded course
offering, not a platform-scale queue).

## Already-Enrolled Learners

Eligibility is checked only at the moment a seat is gained — direct enrollment, or
promotion from the waitlist. There is no continuous monitoring and no retroactive
eviction: once a learner holds a seat, they keep it regardless of any later change to
their `CompletedCourses`. The only trigger for the promotion scan is a drop; nothing
re-scans already-enrolled learners.

Similarly, a waitlisted learner who becomes eligible via `PATCH /learners/{id}` is not
immediately promoted — promotion only happens on the next drop for that course. See
`LIMITATIONS.md` for the consequence of this if no further drop ever occurs.

## Concurrency

A single `lock` (`Monitor`) inside `InMemoryCourseStore` guards every method that reads
or mutates shared state, held for the method's entire body — there is no
check-then-act gap between reading capacity/membership and writing the result. The full
decision-table and skip-and-retain algorithm runs *inside* `InMemoryCourseStore.Enroll()`
/`Drop()`, not in `RegistrationService`, specifically so the whole compound operation
executes under one lock acquisition; `RegistrationService` is a thin delegation layer
that exists only so the Controller depends on an abstraction (`IRegistrationService`)
rather than the concrete store.

`lock`/`Monitor` was chosen over `SemaphoreSlim` because every critical section here is
purely synchronous in-memory work with no `await` inside it — `SemaphoreSlim`'s value is
async-compatible waiting (`WaitAsync()`), which buys nothing when nothing ever awaits,
and would add overhead plus manual `Release()`/`Dispose()` discipline for no benefit.
For the same reason, no part of this service uses `async`/`await` — there is no I/O to
wait on.

## Persistence

In-memory only (a `Dictionary`/`HashSet`/`List`-backed store registered as a DI
singleton), per the assessment's explicit fallback allowance when no starter project
and no required database exists. State does not survive a process restart. See
`LIMITATIONS.md`.

## Key Assumptions & Tradeoffs

- **DataAnnotations over FluentValidation.** Validation needs are a handful of
  required-string and one positive-integer check — the built-in attributes plus
  `[ApiController]`'s automatic model validation fully cover this; a third-party
  validation library would be an unjustified new dependency for this scope.
- **No configurable policy engine.** The prerequisite rule and skip-and-retain policy
  are hard-coded, not built behind a pluggable `IEligibilityPolicy`-style abstraction,
  per the brief's explicit instruction not to build one.
- **Seeding endpoints are test helpers, not a catalog feature.** `POST /courses` and
  `POST /learners` upsert minimally (overwrite course metadata, but never wipe existing
  enrollment/waitlist state on re-seed) — there's no conflict-detection or catalog
  semantics layered on top, since none is required.
