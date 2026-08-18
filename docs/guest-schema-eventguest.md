# Guest schema: person vs participation

`Guest` is now the **master person record**. `EventGuest` is one person's
**participation in one event**. Everything that could differ per event moved off
`Guest` and onto `EventGuest`, and every event-scoped child record now keys off
the participation instead of the person.

This is a breaking API change for the portal and the guest app. What follows is
the contract.

---

## The two ids

| | `Guest.PublicId` | `EventGuest.PublicId` |
|---|---|---|
| Called | `personId` | `id` on guest CRUD, `eventGuestId` elsewhere |
| Means | the human | the human *on one event* |
| Stable across events | yes | no — one per event |
| Used by | guest overview detail, notifications, support chat | guest CRUD, travel, seating, meetings, transport, services, accreditation |

They are both GUIDs and they are **not interchangeable**. `GuestResponse`
returns both, so a screen never has to derive one from the other.

---

## Data ownership

**Person (`Guest`)** — identity (first/last name, email, photo, nationality),
the linked `User`, OTP login and refresh tokens, preferences, language,
notification settings, device registrations, support-chat identity.

**Participation (`EventGuest`)** — guest type, organisation, service level (and
its override audit), tier, accreditation requirement, allowed self-service,
arrival/departure dates; plus every child record: invitations, sessions,
seating, dynamic service entries, flights, accommodation, transports (a
transport row is also what records the driver assigned to the guest), meeting
attendance.

Removing a participation is a **soft delete**: `EventGuest` and its
event-scoped records are flagged `IsDeleted`, never dropped, so past events
stay auditable. The `Guest` and its `User` are never deleted as a side effect —
one person attends many events. Seat assignments and session picks are the
exception and are removed physically (no `IsDeleted` of their own; a lingering
seat row would also keep the seat occupied).

---

## Email rules

- `email` is **required** on create.
- It is trimmed and lowercased server-side, and is **unique among active guests**.
- It is also the linked `User`'s `email` *and* `userName`.
- It is **immutable**. `PUT /api/v1/guest/{id}` rejects a different email with a
  400 rather than silently re-pointing the person's login, tokens, notifications
  and support thread at someone else. To fix a wrong address, remove the
  participation and add the correct person.

---

## Adding a guest

`POST /api/v1/guest` — the email decides which person:

| Case | Result |
|---|---|
| Email unknown | New `Guest` + linked guest-role `User` + `EventGuest`. |
| Email known, **different** event | Existing `Guest`/`User` **reused**; only a new `EventGuest`. This *is* "add an existing guest to this event". |
| Email known, **same** event | `409` with error code `GUEST_ALREADY_ON_EVENT`. |
| Email belongs to a non-guest portal account | `409` with error code `GUEST_EMAIL_CONFLICT`. |

There is no separate "link existing guest" endpoint — posting the email is the
whole flow. `GET /api/v1/guest/other-events` feeds the picker and already
excludes people who are on the current event.

The create path is concurrency-safe: filtered unique indexes on `Guests.Email`
and `EventGuests (GuestId, EventId)` mean two simultaneous adds collide in the
database, and the loser is translated into the same `409` the pre-check would
have produced rather than a 500.

An existing person is **reused as-is**. Name/photo/nationality on the create
request are not applied to a person who already exists — one careless import row
must not rename them on every other event. Editing the person is what `PUT` is
for.

## Updating a guest

`PUT /api/v1/guest/{id}` (`{id}` = `eventGuestId`) writes:

- **to the shared person**: first name, last name, photo, nationality — these
  change everywhere that person appears, deliberately;
- **to the selected participation only**: guest type, organisation, service
  level, tier, override audit, arrival/departure, accreditation requirement,
  allowed services, sessions.

---

## Import

`POST /api/v1/guest/import` follows exactly the same rules as the UI:

- same email + **same** event → that row fails with "already on this event";
- same email + **another** event → the master `Guest` and its login are reused,
  and only the participation is added.

No deduplication or merging happens anywhere in the import.

---

## Endpoint id changes

URLs are unchanged. What changed is **which id belongs in them**.

### Now take an `eventGuestId` (was a guest id)

| Endpoint | |
|---|---|
| `GET/PUT/DELETE /api/v1/guest/{id}` | also `POST /{id}/accreditation/issue`, `/revoke` |
| `DELETE /api/v1/guest/delete` | body `selectedGuestsToDelete` |
| `GET/POST /api/v1/travel/guest/{eventGuestId}` | |
| `GET /api/v1/seating/guest/{eventGuestId}` | |
| `GET/POST /api/v1/transportation/guest/{eventGuestId}[/drivers]` | |
| `GET/POST/DELETE /api/v1/guests/{eventGuestId}/services` | |

Deleting a guest now removes the **participation**. The person, their login and
their other events survive — and re-adding them later reuses the same identity.

### Now take a `personId` (unchanged meaning, clearer name)

| Endpoint | |
|---|---|
| `GET /api/v1/guest-overview/{personId}` | the person's whole cross-event history |
| `POST /api/v1/supportchat/conversations/by-guest/{personId}/messages` | one thread per human |
| `POST /api/v1/notification` with `guestIds` | person ids |

---

## Renamed response/request fields

| DTO | was | now |
|---|---|---|
| `GuestResponse` | `id` | `id` (now the participation) **+ new `personId`** |
| `GuestPickerResponse`, `OtherEventGuestRow` | `id` | `id` (participation) **+ new `personId`** |
| `GuestOverviewRow` | `id` (latest booking) | `id` = `personId`, stable |
| `GuestOverviewEventBlock` | `guestId` | `eventGuestId` |
| `RequestSeatAssignDto`, `SeatAssignmentDto` | `guestId` | `eventGuestId` |
| `CreateScheduleRequest`, `ScheduleRow` | `guestId` | `eventGuestId` (+ `personId`, `eventId` on the row) |
| `EventFlightRow`, `EventAccommodationRow`, `EventTransportRow`, `ArrivalDepartureRow` | `guestId` | `eventGuestId` |
| `TransportRow` (vehicles) | `guestId` | `eventGuestId` |
| `GuestServicePlanResponse`, `ServiceEntryRow` | `guestId` | `eventGuestId` |
| `CreateMeetingRequest`, `EditMeetingRequest` | `guestIds` | `eventGuestIds` |
| `GuestInfo` (meeting attendee) | `id` | `id` (participation) **+ new `personId`** |

`SupportConversationSummaryResponse.guestId` keeps its name and still means the
**person** — support chat is person-level.

---

## Meetings

A meeting belongs to one event, so its attendees are participations. Passing an
`eventGuestId` from a **different** event is rejected with
"One or more guests are not on this meeting's event" rather than being silently
accepted or dropped.

---

## Transport

- `Transport.eventGuestId` is **mandatory** — that is the only place a ride's
  event is recorded, which is what makes the dispatch board's event filter,
  the driver app's event list and per-event conflict checking possible.
- Double-booking checks are participation-scoped: the same person attending two
  events on one day keeps two independent schedules.
- Transport notifications resolve `EventGuest → Guest` before addressing the
  recipient, and carry `eventId` / `eventGuestId` in their payload.

---

## Guest app

Login stays person-level: OTP resolves the `Guest` by email (normalised the same
way it is stored), and the token carries the linked `User.Id` as before.

Event-scoped endpoints resolve the caller's participation for the event they
name:

- `GET /vip-app/events/{eventId}/sessions` and session selection now `404` with
  "You are not registered for this event" instead of showing another event's picks.
- **`POST /vip-app/transport-requests` requires `eventId`.** The server resolves
  that participation and checks `allowedServices` on it. It never picks "the
  latest eligible event" — booking a car against the wrong event means the wrong
  drivers, the wrong dispatch board and the wrong permissions.
- Itinerary/agenda/flights/accommodation/transport still accept an optional
  `eventId`; omitted, they span every event the person is on.
- `GET /vip-app/profile` returns `organization` and `tier` from the person's most
  recent participation (both are per-event now). `PUT /vip-app/profile` writes
  `organization` to that participation, not to the person.

---

## Database

The database is dropped and recreated for this change. There is no data
migration, deduplication or backfill path, and none should be added.
