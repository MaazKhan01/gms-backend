# Shared context — session log

## 2026-07-30 — Guest/User merge, notification centralization, Firebase completion, driver<->guest chat

**What changed**: `Guest` is now a 1:1 profile extension of `User`
(`Guest.UserId`, mirrors `DriverProfile`). Guest-only chat/notification/device
tables (`GuestNotification`, `GuestDevice`) folded into the shared
`Notification`/new `Device` tables. `SupportConversation`/`SupportMessage`
re-keyed from `GuestId` to `UserId`, with a new `Type`
(`AdminSupport`/`DriverGuest`) so the same tables now also serve a new
driver<->guest chat endpoint. Firebase push is now a real `FirebaseAdmin` SDK
call (previously a stub). Full writeup: `docs/guest-user-merge-and-chat-refactor.md`.

**Decisions worth remembering**:
- Chose extension-table merge (Guest keeps its own table) over full identity
  merge, because `Guests.Email` has no uniqueness constraint (same person can
  have multiple Guest rows across events) — a full merge into `Users.Email`
  (which IS unique) would have collided.
- Guest JWT carries only `Guest.UserId` (`sub` + `NameIdentifier`) plus
  `role=="guest"`; no `Guest.Id` claim at all (`GuestClaims` deleted).
  `ICurrentGuest`/`RealTimeHubService` derive `Guest.Id` from `Guests.UserId`.
  Necessary for `Clients.User(...)`/`ICurrentUser` to resolve a guest, and it
  removes the Guest.Id/User.Id collision risk entirely. Keep `sub` and
  `NameIdentifier` identical — the JWT handler maps `sub` onto `NameIdentifier`,
  so a stale `sub` silently wins `FindFirst`.
- EF Core's SqlServer provider auto-filters a unique index to exclude NULLs
  whenever it covers a nullable column — caught this because it would have
  silently broken "one AdminSupport conversation per guest". Split into two
  filtered unique indexes instead of one composite index. Worth remembering
  for any future nullable-column unique index in this codebase.

**Open follow-ups not done (out of the requested scope)**:
- No list/inbox endpoint for `DriverGuest` conversations (task asked for one
  endpoint only — send/start-or-reuse).
- No "blocked users" concept exists anywhere in this codebase; not added.
- Login/logout don't auto-register/deregister a push device — client's
  responsibility via the new `POST/DELETE /api/v1/notifications/devices`.
- Pre-existing (unrelated) issue noticed: `appsettings.json`/`appsettings.Development.json`
  commit a live-looking DB connection string with a plaintext password. Not
  touched, flagged in the docs deliverable's Security section.

**State**: solution builds clean (`dotnet build gms.sln`, 0 errors). Migration
`20260730125545_MergeGuestIntoUserAndUnifyNotifications` is scaffolded and
hand-verified via `dotnet ef migrations script`, but **not yet applied** to
any database — do that as its own reviewed step, with a backup first (see
docs deliverable §13).

**Standing rule**: never push to development unless told.

## 2026-10-19 — Delegate app: Figma (DMS_DELEGATION) gap pass + mission APIs

**What changed**: `DelegateAppController` gained mission-scoped endpoints —
`missions/{eventId}/home|accreditation|journal|report`, `help-requests`,
`stays`, `transport-requests/{id}`, `PATCH/DELETE journal/{id}`. `/events` now
carries code, venue, destination, delegate-facing `missionStatus`
(nominated/upcoming/live/completed/cancelled), day X of Y, mission role.
New table `MissionJournalEntries` (migration `20261019160424_DelegateAppMissionJournal`,
**scaffolded, not applied**). OTP is now 6 digits + 30s resend cooldown.
Help requests land as `Incidents` with `RaisedVia = app`; new incident category `transport`.

**Decisions**: mission code derived from title initials + year (no Event.Code
column yet). Report writes go through `MissionReportService.SaveReportAsync`.
Readiness checklist reused via new `IReadinessService.GetForParticipationAsync`.

**Open (no DB source — need product/schema decision)**: accreditation badge no. /
zones, flight PNR / terminal / gate, hotel room no. / booking ref / front-desk
contact, allowance (per-diem/advances), itinerary "Provisional" status, invite
activation flag, QR scanner verify endpoint. Figma review was partial — MCP View-seat
call limit hit after ~11 of ~55 frames.

## 2026-10-22 — Delegate app: full pass against DMS-Delegatev2 HTML prototype

**Source**: `DMS-Delegatev2-theme-teal.html` (all screens + data model), Figma MCP was rate-limited.
**Schema** (migration `*_DelegateAppPrototypeFields`, **scaffolded, not applied**; additive only):
Flight.Airline/Pnr/ETicketNumber, FlightLeg.BaggageAllowance/BoardingTime,
Accommodation.RoomNumber/ConfirmationNumber/BoardBasis/CheckInTime/CheckOutTime,
Event.CoordinatorUserId/CancellationReason/CancelledAt, EventGuest accreditation
(number/category/label/zones JSON) + per diem/settlement, PostMissionReport.KeyOutcomes,
MissionJournalEntry.Visibility, new table BookingChangeRequests.
**Portal write paths added**: travel save (FlightInput/LegInput/AccommodationInput new fields,
null = untouched), PUT events (coordinatorUserId), PUT events/{id}/status (reason),
PUT nominations/{id} (accreditation + per diem, null = untouched),
GET travel/event/{id}/change-requests + PUT travel/change-requests/{id}.
**Delegate endpoints added**: missions/{id}/places, missions/{id}/hotels, change-requests (POST/GET);
home now has allowance, nomination steps, cancellation, completed summary; visa steps read
from the visa service's form schema; itinerary?eventId= returns full programme + provisional flag.
**Rules**: journal writable only upcoming/live; report writable only after mission completed.
**Flagged, not fixed**: `[Authorize]`-only portal controllers (Travel, Lookup, ...) accept a
delegate (role=guest) token — no global gate exists.
