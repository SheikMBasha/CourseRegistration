# Course Waitlist Service

Enroll, drop, and FIFO-waitlist promotion for one course offering, with prerequisite
eligibility and a documented skip-and-retain policy for ineligible waitlisted learners.
See `DESIGN.md` for the full design spec and `LIMITATIONS.md` for known boundaries.

## Prerequisites

- .NET 8 SDK (built and tested against SDK 9.0.102 targeting `net8.0`; .NET 8 runtime
  must be installed alongside if your SDK is newer than 8.x — check with
  `dotnet --list-runtimes`)

## Build

```
cd CourseRegistration
dotnet build
```

## Run the tests

```
dotnet test
```

18 tests across two files:
- `tests/CourseRegistration.Tests/RegistrationServiceTests.cs` — the 14 core business
  scenarios (capacity, waitlist, duplicates, skip-and-retain, concurrency, etc.)
- `tests/CourseRegistration.Tests/DtoValidationTests.cs` — 4 tests confirming malformed
  request bodies fail validation (surfaces as `400` once bound through the real HTTP
  pipeline)

## Run the API

```
cd src/CourseRegistration.Api
dotnet run --urls http://localhost:5299
```

## Try it out

`src/CourseRegistration.Api/CourseRegistration.Api.http` replays a full worked example
(seed two courses and five learners, then exercise every scenario — duplicates,
skip-and-retain, a learner becoming eligible mid-wait, invalid drops, nonexistent
course/learner references, a malformed body). Open it in VS Code with the REST Client
extension (or Visual Studio's built-in `.http` support) and run requests top to bottom
while the API is running.

## Project structure

```
CourseRegistration/
├── src/CourseRegistration.Api/
│   ├── Models/              Course, Learner, EnrollmentResult, Dtos/
│   ├── Store/                ICourseStore, InMemoryCourseStore (thread-safe, holds the
│                              decision-table + skip-and-retain algorithm)
│   ├── Services/              IRegistrationService, RegistrationService (thin DI seam)
│   └── Controllers/           RegistrationController (HTTP translation only)
└── tests/CourseRegistration.Tests/
    ├── RegistrationServiceTests.cs
    └── DtoValidationTests.cs
```

## Other deliverables

- `DESIGN.md` — service boundary, data/API design, invariants, promotion rule,
  concurrency approach, assumptions and tradeoffs.
- `artifacts/AI-USE.md` — the AI prompt-and-use record: every design question raised,
  how it was resolved, and verification evidence for each implementation phase.
- `LIMITATIONS.md` — known limitations and follow-up risks discovered within scope.

## `artifacts/`

Process documents, kept separate from the top-level deliverables above:
- `AI-USE.md` — the AI prompt-and-use record (also listed as a deliverable above).
- `course-registration-plan.md` — the working design-planning document the candidate and
  Claude built up across the whole session, before and during implementation.
- `TASKS.md` — the resumable, phase-by-phase implementation checklist used to build this
  solution, with notes on what was verified at each step.
