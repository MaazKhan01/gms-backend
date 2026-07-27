using AutoMapper;
using Azure;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.User;
using Core.ViewModel.Venue;
using DomainPersistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Smtp2Go.Api.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class VenueService(IUnitOfWork _unitOfWork,IMapper _mapper, ILogger<VenueService> _logger ) : IVenueService
    {
        public async Task<ApiResponse<GetVenueResonse>> GetVenueByIdAsync(Guid venueId, CancellationToken ct)
        {
            try {
                // AsSplitQuery: Blocks and VenueLayouts are sibling collections under
                // VenueBoxes — in a single query EF joins both, and their Seats collections
                // multiply together (rows_A × rows_B) instead of adding, which is what made
                // this take ~20s on venues with a lot of seats. Splitting into separate
                // SELECTs per collection makes the cost additive again.
                var venue = await _unitOfWork.Venues.Query()
     .Include(v => v.Type)
     .Include(v => v.Location)
     .Include(v => v.VenueBoxes!)
         .ThenInclude(vb => vb.Event)
     .Include(v => v.VenueBoxes!)
         .ThenInclude(vb => vb.Session)

     .Include(v => v.VenueBoxes!)
         .ThenInclude(vb => vb.Blocks)
             .ThenInclude(b => b.Props)
                 .ThenInclude(p => p.Seats)

     .Include(v => v.VenueBoxes!)
         .ThenInclude(vb => vb.VenueLayouts)
             .ThenInclude(l => l.VenueLayoutProps)
                 .ThenInclude(p => p.Seats)

     .AsSplitQuery()
     .FirstOrDefaultAsync(v => v.PublicId == venueId, ct);
                if (venue == null)
                {
                    return ApiResponse<GetVenueResonse>.NotFoundResponse("Venue doesn't existed");
                }
                return ApiResponse<GetVenueResonse>.SuccessResponse(_mapper.Map<GetVenueResonse>(venue));
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error Get Venue");
                return ApiResponse<GetVenueResonse>.ServerErrorResponse("Error: Failed to Get Venue.");

            }
        }

        public async Task<ApiResponse<List<GetVenueResonse>>> GetVenuesAsync(CancellationToken ct)
        {
            try
            {
                // Lightweight list (scalars only) — the editor fetches full boxes via GetById on select.
                var venues = await _unitOfWork.Venues.Query()
                    .Include(v => v.Type)
                    .Include(v => v.Location)
                    .OrderBy(v => v.Name)
                    .ToListAsync(ct);
                var list = venues.Select(v => _mapper.Map<GetVenueResonse>(v)).ToList();
                return ApiResponse<List<GetVenueResonse>>.SuccessResponse(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing venues");
                return ApiResponse<List<GetVenueResonse>>.ServerErrorResponse("Error: Failed to list venues.");
            }
        }
        public async Task<ApiResponse<bool>> DeleteVenueAsync(Guid id, CancellationToken ct)
        {
            try
            {
                if (id == Guid.Empty)
                    return ApiResponse<bool>.ErrorResponse("Venue id is required.");

                // Resolve the public id to the internal int key (used for all FK filters below).
                var venueEntity = await _unitOfWork.Venues.Query()
                    .Select(v => new { v.Id, v.PublicId })
                    .FirstOrDefaultAsync(v => v.PublicId == id, ct);
                if (venueEntity == null)
                    return ApiResponse<bool>.NotFoundResponse("Venue not found.");
                var venueId = venueEntity.Id;

                // Refuse to delete a venue whose seats are already assigned to guests —
                // checked directly in SQL via VenueLayoutProp (the only entity in this
                // chain with a navigable path back up to VenueBox/Venue; SeatProperties
                // itself has no upward nav). Nothing is materialized into memory here,
                // so this stays fast regardless of how many seats the venue has.
                var venueSeats = _unitOfWork.VenueLayoutProps.Query()
                    .Where(p => (p.Layout != null && p.Layout.VenueBox.VenueId == venueId)
                             || (p.Block != null && p.Block.VenueBox.VenueId == venueId))
                    .SelectMany(p => p.Seats);

                var hasAssignedSeat = await venueSeats
                    .AnyAsync(s => s.Status != null && s.Status.Trim().ToLower() == "assigned", ct);
                if (hasAssignedSeat)
                    return ApiResponse<bool>.ConflictResponse(
                        "Cannot delete this venue — one or more seats are already assigned to guests.");

                // Defensive check: a seat can be assigned without its Status text being kept
                // in sync, so also look for real SeatAssign rows referencing these seats —
                // as a correlated EXISTS subquery, not a giant in-memory seatIds IN-list.
                var hasSeatAssign = await _unitOfWork.SeatAssigns.Query()
                    .AnyAsync(sa => venueSeats.Any(s => s.Id == sa.SeatId), ct);
                if (hasSeatAssign)
                    return ApiResponse<bool>.ConflictResponse(
                        "Cannot delete this venue — one or more seats are already assigned to guests.");

                // A Seating row means this venue was actually used for an event's seating;
                // refuse rather than silently destroying that record (Seating->Venue is also
                // a Restrict FK, so leaving this unchecked would otherwise crash on SaveChanges).
                var hasSeating = await _unitOfWork.Seatings.Query().AnyAsync(s => s.VenueId == venueId, ct);
                if (hasSeating)
                    return ApiResponse<bool>.ConflictResponse(
                        "Cannot delete this venue — it already has event seating data.");

                // Safe to delete. Set-based deletes straight in SQL — nothing is loaded
                // or tracked in memory, so this scales with the DB engine's own row
                // processing instead of EF materializing/tracking every seat first (the
                // previous ClearBoxChildren approach, which is what was still timing out
                // even after the read paths got AsSplitQuery). Order follows the Restrict
                // FK graph bottom-up (props → layouts → blocks → boxes → venue); Props →
                // Seats is the one Cascade edge, so deleting a prop auto-deletes its seats.
                await _unitOfWork.BeginTransactionAsync();
                try
                {
                    await _unitOfWork.VenueLayoutProps.Query()
                        .Where(p => (p.Layout != null && p.Layout.VenueBox.VenueId == venueId)
                                 || (p.Block != null && p.Block.VenueBox.VenueId == venueId))
                        .ExecuteDeleteAsync(ct);

                    await _unitOfWork.VenueLayouts.Query()
                        .Where(l => l.VenueBox!.VenueId == venueId)
                        .ExecuteDeleteAsync(ct);

                    await _unitOfWork.VenueBlocks.Query()
                        .Where(b => b.VenueBox.VenueId == venueId)
                        .ExecuteDeleteAsync(ct);

                    await _unitOfWork.VenueBoxes.Query()
                        .Where(b => b.VenueId == venueId)
                        .ExecuteDeleteAsync(ct);

                    await _unitOfWork.Venues.Query()
                        .Where(v => v.Id == venueId)
                        .ExecuteDeleteAsync(ct);

                    await _unitOfWork.CommitTransactionAsync();
                }
                catch
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    throw;
                }

                return ApiResponse<bool>.SuccessResponse(true, "Venue deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting venue {VenueId}", id);
                return ApiResponse<bool>.ServerErrorResponse("An error occurred while deleting the venue.");
            }
        }
        public async Task<ApiResponse<bool>> DeleteVenueBoxAsync(  Guid id, Guid venueId, Guid eventId, Guid sessionId, CancellationToken ct)
        {
            try
            {
                if (id == Guid.Empty)
                    return ApiResponse<bool>.ErrorResponse("Venue box id is required.");

                if (venueId == Guid.Empty)
                    return ApiResponse<bool>.ErrorResponse("Venue id is required.");

                if (eventId == Guid.Empty && sessionId == Guid.Empty)
                    return ApiResponse<bool>.ErrorResponse("Either eventId or sessionId must be provided.");

                // Resolve the public ids to internal int keys used by the FK filters.
                var venue = await _unitOfWork.Venues.Query()
                    .FirstOrDefaultAsync(v => v.PublicId == venueId, ct);
                if (venue == null)
                    return ApiResponse<bool>.NotFoundResponse("Venue box not found.");

                IQueryable<VenueBox> query = _unitOfWork.VenueBoxes.Query()
                    .Where(x => x.PublicId == id && x.VenueId == venue.Id)
                    .Include(x => x.Blocks!)
                        .ThenInclude(b => b.Props)
                            .ThenInclude(p => p.Seats)
                    .Include(x => x.VenueLayouts!)
                        .ThenInclude(l => l.VenueLayoutProps)
                            .ThenInclude(p => p.Seats)
                    .AsSplitQuery();

                if (eventId != Guid.Empty)
                {
                    var ev = await _unitOfWork.Events.Query()
                        .FirstOrDefaultAsync(e => e.PublicId == eventId, ct);
                    if (ev == null)
                        return ApiResponse<bool>.NotFoundResponse("Venue box not found.");
                    query = query.Where(x => x.EventId == ev.Id);
                }

                if (sessionId != Guid.Empty)
                {
                    var session = await _unitOfWork.Sessions.Query()
                        .FirstOrDefaultAsync(s => s.PublicId == sessionId, ct);
                    if (session == null)
                        return ApiResponse<bool>.NotFoundResponse("Venue box not found.");
                    query = query.Where(x => x.SessionId == session.Id);
                }

                var venueBox = await query.SingleOrDefaultAsync(ct);

                if (venueBox is null)
                    return ApiResponse<bool>.NotFoundResponse("Venue box not found.");

                if (await BoxHasAssignedSeatAsync(venueBox, ct))
                    return ApiResponse<bool>.ConflictResponse(
                        "Cannot delete this box — one or more seats are already assigned to guests.");

                ClearBoxChildren(venueBox);
                _unitOfWork.VenueBoxes.Remove(venueBox);

                await _unitOfWork.SaveChangesAsync(ct);

                return ApiResponse<bool>.SuccessResponse(true, "Venue box deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting venue box. VenueBoxId: {VenueBoxId}", id);
                return ApiResponse<bool>.ServerErrorResponse("An error occurred while deleting the venue box.");
            }
        }
        public async Task<ApiResponse<GetVenueResonse>> AddVenueBlockAsync(Guid eventId, Guid? sessionId, Guid venueId, CreateVenueBlockDto request, CancellationToken ct)
        {
            try
            {
                if (eventId == Guid.Empty)
                    return ApiResponse<GetVenueResonse>.ErrorResponse("Event id is required.");

                if (venueId == Guid.Empty)
                    return ApiResponse<GetVenueResonse>.ErrorResponse("Venue id is required.");

                if (string.IsNullOrWhiteSpace(request.Label))
                    return ApiResponse<GetVenueResonse>.ErrorResponse("Block label is required.");

                // Scope the box lookup to venue + event + session — a box is unique per
                // (venue, event, session), and different venues can each have their own
                // box under the very same event/session, so venueId is required too
                // (without it, this used to just grab whichever box matched first).
                // Resolve public ids (venue/event/session) to internal int keys for the box lookup.
                var venue = await _unitOfWork.Venues.Query()
                    .FirstOrDefaultAsync(v => v.PublicId == venueId, ct);

                var ev = await _unitOfWork.Events.Query()
                    .FirstOrDefaultAsync(e => e.PublicId == eventId, ct);

                int? normalizedSessionId = null;
                if (sessionId.HasValue && sessionId.Value != Guid.Empty)
                {
                    var session = await _unitOfWork.Sessions.Query()
                        .FirstOrDefaultAsync(s => s.PublicId == sessionId.Value, ct);
                    normalizedSessionId = session?.Id;
                }

                VenueBox? venueBox = null;
                if (venue != null && ev != null)
                {
                    venueBox = await _unitOfWork.VenueBoxes.Query()
                        .Include(vb => vb.Blocks)
                        .FirstOrDefaultAsync(vb => vb.VenueId == venue.Id
                            && vb.EventId == ev.Id
                            && vb.SessionId == normalizedSessionId, ct);
                }

                if (venueBox == null)
                    return ApiResponse<GetVenueResonse>.NotFoundResponse("No venue box found for this venue/event/session. Create one first.");

                // Two blocks can't sit on the same spot in the same box.
                var positionTaken = venueBox.Blocks != null
                    && venueBox.Blocks.Any(b => b.X == request.X && b.Y == request.Y);
                if (positionTaken)
                    return ApiResponse<GetVenueResonse>.ConflictResponse(
                        "A block already exists at this position (X, Y). Choose a different position.");

                // Same "stadium" block shape as CreateVenueAsync/CreateVenueBoxAsync — a
                // single prop holding the grid, with seats auto-generated from Rows × SeatsPerRow.
                var block = new VenueBlock
                {
                    VenueBoxId = venueBox.Id,
                    Type = "stadium",
                    X = request.X,
                    Y = request.Y,
                    Rotation = request.Rotation,
                    Label = request.Label.Trim(),
                    Category = request.Category,
                    Rows = request.Rows,
                    SeatsPerRow = request.SeatsPerRow,
                };
                block.Props = new List<VenueLayoutProp>
                {
                    new()
                    {
                        // VenueBlockId FK is set by EF from the block navigation on save.
                        Code = block.Label,
                        Label = block.Label,
                        Row = request.Rows,
                        SeatsQuantity = request.SeatsPerRow,
                        RowNames = new(),
                        Seats = GenerateSeats(new CreateVenueLayoutPropDto
                        {
                            Row = request.Rows,
                            SeatsQuantity = request.SeatsPerRow,
                            RowNames = new(),
                        }),
                    }
                };

                await _unitOfWork.VenueBlocks.AddAsync(block, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                // Reload the venue with the full box graph for the response, same shape
                // GetVenueByIdAsync/CreateVenueBoxAsync return.
                var venueResult = await _unitOfWork.Venues.Query()
                    .Include(v => v.Type)
                    .Include(v => v.VenueBoxes!)
                        .ThenInclude(b => b.Event)
                    .Include(v => v.VenueBoxes!)
                        .ThenInclude(b => b.Session)
                    .Include(v => v.VenueBoxes!)
                        .ThenInclude(b => b.Blocks)
                            .ThenInclude(bl => bl.Props)
                                .ThenInclude(p => p.Seats)
                    .Include(v => v.VenueBoxes!)
                        .ThenInclude(b => b.VenueLayouts)
                            .ThenInclude(l => l.VenueLayoutProps)
                                .ThenInclude(p => p.Seats)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(v => v.Id == venueBox.VenueId, ct);

                return ApiResponse<GetVenueResonse>.SuccessResponse(_mapper.Map<GetVenueResonse>(venueResult), "Block added");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, " Error creating block");
                return ApiResponse<GetVenueResonse>.ServerErrorResponse("Error creating block");
            }
        }
        public async Task<ApiResponse<GetVenueResonse>> CreateVenueAsync(CreateVenueRequest request, int userId, CancellationToken ct= default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.VenueName))
                    return ApiResponse<GetVenueResonse>.ErrorResponse("Venue name is required");

                // userId is the internal user id (0 = unauthenticated).
                int? creatorId = userId == 0 ? null : userId;

                // VenueType is a VenueType public id; resolve to its internal int FK.
                int? typeId = null;
                if (request.VenueType != Guid.Empty)
                {
                    typeId = await _unitOfWork.VenueTypes.Query()
                        .Where(t => t.PublicId == request.VenueType)
                        .Select(t => (int?)t.Id)
                        .FirstOrDefaultAsync(ct);
                }

                int? locationId = null;
                if (request.LocationId.HasValue && request.LocationId.Value != Guid.Empty)
                {
                    locationId = await _unitOfWork.Locations.Query()
                        .Where(l => l.PublicId == request.LocationId.Value)
                        .Select(l => (int?)l.Id)
                        .FirstOrDefaultAsync(ct);
                }

                var venue = new Venue
                {
                    Name = request.VenueName.Trim(),
                    TypeId = typeId,
                    LocationId = locationId,
                    // Drop empty/blank category entries (e.g. Swagger's placeholder "")
                    Category = request.Category?
                        .Where(c => !string.IsNullOrWhiteSpace(c))
                        .Select(c => c.Trim())
                        .ToList() ?? new(),
                    Color = request.Color,
                };
                if (creatorId.HasValue) venue.SetCreationAudit(creatorId.Value);

                // Ignore blank blocks (a block must have a label); only build a box if real blocks remain
                var blocks = request.Blocks?
                    .Where(b => !string.IsNullOrWhiteSpace(b.Label))
                    .ToList();

                if (blocks is { Count: > 0 })
                {
                    // Scope to whichever event/session this venue was being configured for.
                    // EventId/SessionId arrive as public ids; resolve them to internal int FKs.
                    int? boxEventId = null;
                    if (request.EventId.HasValue && request.EventId.Value != Guid.Empty)
                    {
                        boxEventId = await _unitOfWork.Events.Query()
                            .Where(e => e.PublicId == request.EventId.Value)
                            .Select(e => (int?)e.Id)
                            .FirstOrDefaultAsync(ct);
                    }

                    int? boxSessionId = null;
                    if (request.SessionId.HasValue && request.SessionId.Value != Guid.Empty)
                    {
                        boxSessionId = await _unitOfWork.Sessions.Query()
                            .Where(s => s.PublicId == request.SessionId.Value)
                            .Select(s => (int?)s.Id)
                            .FirstOrDefaultAsync(ct);
                    }

                    //create a box canvas to store blocks in it
                    var defaultBox = new VenueBox
                    {
                        // VenueId FK is set by EF from the venue navigation on save.
                        // Scope to whichever event/session this venue was being configured
                        // for — without this the box is event-agnostic and never matches
                        // pickBox's per-event lookup, so it silently never shows in the editor.
                        EventId = boxEventId,
                        SessionId = boxSessionId,

                        // Each block is a stadium unit: type + rotation + a prop holding
                        // the grid, with seats auto-generated (A1, B1 …) from Rows × SeatsPerRow.
                        Blocks = blocks.Select(b =>
                        {
                            var block = new VenueBlock
                            {
                                Type = "stadium",
                                X = b.X,
                                Y = b.Y,
                                Rotation = b.Rotation,
                                Label = b.Label.Trim(),
                                Category = b.Category,
                                Rows = b.Rows,
                                SeatsPerRow = b.SeatsPerRow,
                            };
                            block.Props = new List<VenueLayoutProp>
                            {
                                new()
                                {
                                    // VenueBlockId FK is set by EF from the block navigation on save.
                                    Code = block.Label,
                                    Label = block.Label,
                                    Row = b.Rows,
                                    SeatsQuantity = b.SeatsPerRow,
                                    RowNames = new(),
                                    Seats = GenerateSeats(new CreateVenueLayoutPropDto
                                    {
                                        Row = b.Rows,
                                        SeatsQuantity = b.SeatsPerRow,
                                        RowNames = new(),
                                    }),
                                }
                            };
                            return block;
                        }).ToList(),
                    };
                    if (creatorId.HasValue) defaultBox.SetCreationAudit(creatorId.Value);
                    venue.VenueBoxes = new List<VenueBox> { defaultBox };
                }

                await _unitOfWork.Venues.AddAsync(venue, ct);

                await _unitOfWork.SaveChangesAsync(ct);

                var dto = _mapper.Map<GetVenueResonse>(venue);
                // No blocks added → no box; return null instead of an empty array
                if (venue.VenueBoxes is not { Count: > 0 }) dto.VenueBoxes = null;

                return ApiResponse<GetVenueResonse>.SuccessResponse(dto, "Venue created");
            }
            catch(Exception ex) {
                _logger.LogError(ex, "Error Creating Venue");
                return ApiResponse<GetVenueResonse>.ServerErrorResponse("Error: Failed to Create Venue.");
            }

        }

        public async Task<ApiResponse<GetVenueResonse>> CreateVenueBoxAsync(CreateVenueBoxRequest request, Guid eventId, int userId, CancellationToken ct)
        {
            try
            {
                if (request.VenueId == Guid.Empty)
                    return ApiResponse<GetVenueResonse>.ErrorResponse("VenueId is required");

                // Resolve the venue public id to its internal int key (used for FKs/filters).
                var venueEntity = await _unitOfWork.Venues.Query()
                    .FirstOrDefaultAsync(v => v.PublicId == request.VenueId, ct);
                if (venueEntity == null)
                    return ApiResponse<GetVenueResonse>.NotFoundResponse("Venue not found");
                var venueIntId = venueEntity.Id;

                // userId is the internal user id (0 = unauthenticated).
                int? creatorId = userId == 0 ? null : userId;

                // Empty eventId = venue default arrangement; otherwise event-scoped.
                // eventId arrives as a public id; resolve to the internal int FK.
                int? normalizedEventId = null;
                if (eventId != Guid.Empty)
                {
                    normalizedEventId = await _unitOfWork.Events.Query()
                        .Where(e => e.PublicId == eventId)
                        .Select(e => (int?)e.Id)
                        .FirstOrDefaultAsync(ct);
                }

                // SessionId arrives as a public id; resolve to the internal int FK.
                int? normalizedSessionId = null;
                if (request.SessionId.HasValue && request.SessionId.Value != Guid.Empty)
                {
                    normalizedSessionId = await _unitOfWork.Sessions.Query()
                        .Where(s => s.PublicId == request.SessionId.Value)
                        .Select(s => (int?)s.Id)
                        .FirstOrDefaultAsync(ct);
                }

                // Each (venue, event, session) scope has exactly one box. If one already
                // exists, replace it: delete it outright (bottom-up, same as
                // DeleteVenueBoxAsync) and insert a fresh box below. Mutating the existing
                // box in place (reassigning its child collections while it's still tracked)
                // trips EF's automatic cascade-delete fixup on the loaded children and
                // produces a spurious DbUpdateConcurrencyException — deleting outright and
                // re-inserting sidesteps that entirely.
                var existingBox = await _unitOfWork.VenueBoxes.Query()
                    .Include(x => x.Blocks!)
                        .ThenInclude(b => b.Props)
                            .ThenInclude(p => p.Seats)
                    .Include(x => x.VenueLayouts!)
                        .ThenInclude(l => l.VenueLayoutProps)
                            .ThenInclude(p => p.Seats)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(x => x.VenueId == venueIntId
                        && x.EventId == normalizedEventId
                        && x.SessionId == normalizedSessionId, ct);

                var isNewBox = existingBox == null;
                if (existingBox != null)
                {
                    if (await BoxHasAssignedSeatAsync(existingBox, ct))
                        return ApiResponse<GetVenueResonse>.ConflictResponse(
                            "Cannot save over this layout — one or more seats are already assigned to guests. Unassign them first.");

                    ClearBoxChildren(existingBox);
                    _unitOfWork.VenueBoxes.Remove(existingBox);
                }

                var box = new VenueBox
                {
                    VenueId = venueIntId,
                    EventId = normalizedEventId,
                    SessionId = normalizedSessionId,
                    Width = request.Width,
                    Height = request.Height,
                };
                if (creatorId.HasValue) box.SetCreationAudit(creatorId.Value);

                // Blocks (skip blank labels)
                var blocks = request.VenueBlocks?
                    .Where(b => !string.IsNullOrWhiteSpace(b.Label))
                    .Select(b => new VenueBlock
                    {
                        // VenueBoxId FK is set by EF from the box navigation on save.
                        Type = "stadium",
                        X = b.X,
                        Y = b.Y,
                        Rotation = b.Rotation,
                        Label = b.Label.Trim(),
                        Category = b.Category,
                        Rows = b.Rows,
                        SeatsPerRow = b.SeatsPerRow,
                    }).ToList();
                box.Blocks = blocks ?? new List<VenueBlock>();

                // Layout elements (skip blank types), each with its props → seats
                var layouts = request.VenueLayouts?
                    .Where(l => !string.IsNullOrWhiteSpace(l.Type))
                    .Select(l => new VenueLayout
                    {
                        // VenueBoxId FK is set by EF from the box navigation on save.
                        Type = l.Type.Trim(),
                        X = l.X,
                        Y = l.Y,
                        Rotation = l.Rotation,
                        ScaleX = l.ScaleX,
                        ScaleY = l.ScaleY,
                        OffsetX = l.OffsetX,
                        OffsetY = l.OffsetY,
                        VenueLayoutProps = (l.Props ?? new()).Select(p => new VenueLayoutProp
                        {
                            Code = p.Code,
                            Label = p.Label,
                            Row = p.Row,
                            SeatsQuantity = p.SeatsQuantity,
                            RowNames = p.RowNames ?? new(),
                            PitchW = p.PitchW,
                            PitchH = p.PitchH,
                            StageW = p.StageW,
                            StageH = p.StageH,
                            Color = p.Color,
                            Seats = GenerateSeats(p),
                        }).ToList(),
                    }).ToList();
                box.VenueLayouts = layouts ?? new List<VenueLayout>();

                await _unitOfWork.VenueBoxes.AddAsync(box, ct);   // cascades blocks + layouts + props + seats
                await _unitOfWork.SaveChangesAsync(ct);

                // Reload venue with its boxes → blocks + layouts → props → seats for the response
                var venue = await _unitOfWork.Venues.Query()
                    .Include(v => v.Type)
                    .Include(v => v.VenueBoxes!)
                        .ThenInclude(b => b.Event)
                    .Include(v => v.VenueBoxes!)
                        .ThenInclude(b => b.Session)
                    .Include(v => v.VenueBoxes!)
                        .ThenInclude(b => b.Blocks)
                            .ThenInclude(bl => bl.Props)
                                .ThenInclude(p => p.Seats)
                    .Include(v => v.VenueBoxes!)
                        .ThenInclude(b => b.VenueLayouts)
                            .ThenInclude(l => l.VenueLayoutProps)
                                .ThenInclude(p => p.Seats)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(v => v.Id == venueIntId, ct);

                return ApiResponse<GetVenueResonse>.SuccessResponse(_mapper.Map<GetVenueResonse>(venue),
                    isNewBox ? "Venue box created" : "Venue box updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving venue box");
                return ApiResponse<GetVenueResonse>.ServerErrorResponse("Error: Failed to save venue box.");
            }
        }

        // True if any seat under this (already-loaded, with Blocks/VenueLayouts →
        // Props → Seats included) box is assigned to a guest — checked both via the
        // seat's own Status text and, defensively, via a real SeatAssign row (a seat
        // can be assigned without Status being kept in sync). Mirrors the same
        // two-pronged check DeleteVenueAsync uses for a whole venue, scoped to one box.
        private async Task<bool> BoxHasAssignedSeatAsync(VenueBox box, CancellationToken ct)
        {
            var blockProps = box.Blocks?.SelectMany(b => b.Props) ?? Enumerable.Empty<VenueLayoutProp>();
            var layoutProps = box.VenueLayouts?.SelectMany(l => l.VenueLayoutProps) ?? Enumerable.Empty<VenueLayoutProp>();
            var seats = blockProps.Concat(layoutProps).SelectMany(p => p.Seats ?? new List<SeatProperties>()).ToList();

            if (seats.Count == 0) return false;

            if (seats.Any(s => s.Status != null && s.Status.Trim().ToLower() == "assigned"))
                return true;

            var seatIds = seats.Select(s => s.Id).ToList();
            return await _unitOfWork.SeatAssigns.Query().AnyAsync(sa => seatIds.Contains(sa.SeatId), ct);
        }

        // Mark an entire box subtree (blocks/layouts → props → seats) for removal.
        // The FK relationships under a box use Restrict (Block/Layout → Box, and the
        // two paths into VenueLayoutProp) to avoid SQL Server's multiple-cascade-path
        // error, so the whole subtree has to be removed explicitly, bottom-up, in the
        // same SaveChanges call as whatever happens to the box itself (delete, or a
        // replace-in-place update).
        private void ClearBoxChildren(VenueBox box)
        {
            var blockProps  = box.Blocks?.SelectMany(b => b.Props) ?? Enumerable.Empty<VenueLayoutProp>();
            var layoutProps = box.VenueLayouts?.SelectMany(l => l.VenueLayoutProps) ?? Enumerable.Empty<VenueLayoutProp>();
            var allProps = blockProps.Concat(layoutProps).ToList();
            var allSeats = allProps.SelectMany(p => p.Seats ?? new List<SeatProperties>()).ToList();

            if (allSeats.Count > 0) _unitOfWork.SeatProperties.RemoveRange(allSeats);
            if (allProps.Count > 0) _unitOfWork.VenueLayoutProps.RemoveRange(allProps);
            if (box.Blocks is { Count: > 0 }) _unitOfWork.VenueBlocks.RemoveRange(box.Blocks);
            if (box.VenueLayouts is { Count: > 0 }) _unitOfWork.VenueLayouts.RemoveRange(box.VenueLayouts);
        }

        // Build the seat list for a prop.
        // - Explicit seats from the client win (re-indexed in order).
        // - Otherwise auto-generate: total = rows × seatsPerRow (rows defaults to 1),
        //   with codes like "A1, A2 … B1" (row letter from RowNames or A,B,C…; plain
        //   number when there's a single row). Index is 0-based = draw/order sequence.
        private static List<SeatProperties> GenerateSeats(CreateVenueLayoutPropDto p)
        {
            if (p.Seats is { Count: > 0 })
            {
                return p.Seats
                    .OrderBy(s => s.Index ?? 0)
                    .Select((s, i) => new SeatProperties
                    {
                        Code = string.IsNullOrWhiteSpace(s.Code) ? $"{i + 1}" : s.Code,
                        Index = i,
                        Color = s.Color ?? p.Color,
                        Status = s.Status,
                        IsDisabled = s.IsDisabled,
                        SeatInfo = s.SeatInfo,
                    })
                    .ToList();
            }

            var perRow = p.SeatsQuantity ?? 0;
            if (perRow <= 0) return new();

            var rows = (p.Row is > 0) ? p.Row.Value : 1;
            var seats = new List<SeatProperties>(rows * perRow);

            for (var i = 0; i < rows * perRow; i++)
            {
                var rowIdx = i / perRow;
                var colIdx = i % perRow;
                var rowName = (p.RowNames != null && rowIdx < p.RowNames.Count && !string.IsNullOrWhiteSpace(p.RowNames[rowIdx]))
                    ? p.RowNames[rowIdx]
                    : ((char)('A' + rowIdx)).ToString();
                var code = rows > 1 ? $"{rowName}{colIdx + 1}" : $"{colIdx + 1}";

                seats.Add(new SeatProperties
                {
                    Code = code,
                    Index = i,
                    Color = p.Color,
                    Status = "available",
                });
            }
            return seats;
        }

        // ── Venue reference data (dedicated tables) ──────────────────────────
        public async Task<ApiResponse<List<VenueTypeDto>>> GetVenueTypesAsync(CancellationToken ct)
        {
            var data = await _unitOfWork.VenueTypes.Query()
                .OrderBy(x => x.Name)
                .Select(x => new VenueTypeDto { Id = x.PublicId, Name = x.Name, NameAr = x.NameAr })
                .ToListAsync(ct);
            return ApiResponse<List<VenueTypeDto>>.SuccessResponse(data);
        }

        public async Task<ApiResponse<VenueTypeDto>> CreateVenueTypeAsync(CreateVenueTypeRequest request, int userId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return ApiResponse<VenueTypeDto>.ErrorResponse("Name is required");

            var entity = new VenueType { Name = request.Name.Trim(), NameAr = request.NameAr?.Trim() };
            if (userId != 0) entity.SetCreationAudit(userId);
            await _unitOfWork.VenueTypes.AddAsync(entity, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<VenueTypeDto>.SuccessResponse(
                new VenueTypeDto { Id = entity.PublicId, Name = entity.Name, NameAr = entity.NameAr }, "Venue type created");
        }

        public async Task<ApiResponse<List<ElementTypeDto>>> GetElementTypesAsync(CancellationToken ct)
        {
            var data = await _unitOfWork.ElementTypes.Query()
                .OrderBy(x => x.Name)
                .Select(x => new ElementTypeDto { Id = x.PublicId, Code = x.Code, Name = x.Name, NameAr = x.NameAr })
                .ToListAsync(ct);
            return ApiResponse<List<ElementTypeDto>>.SuccessResponse(data);
        }

        public async Task<ApiResponse<ElementTypeDto>> CreateElementTypeAsync(CreateElementTypeRequest request, int userId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return ApiResponse<ElementTypeDto>.ErrorResponse("Name is required");

            var entity = new ElementType { Code = request.Code?.Trim(), Name = request.Name.Trim(), NameAr = request.NameAr?.Trim() };
            if (userId != 0) entity.SetCreationAudit(userId);
            await _unitOfWork.ElementTypes.AddAsync(entity, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<ElementTypeDto>.SuccessResponse(
                new ElementTypeDto { Id = entity.PublicId, Code = entity.Code, Name = entity.Name, NameAr = entity.NameAr }, "Element type created");
        }
    }
}
