using Core.ViewModel.Common;
using Core.ViewModel.Seating;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Interfaces.Services
{
    public interface ISeatingService
    {
        Task<ApiResponse<bool>> AssignSeatToGuestAsync(RequestSeatAssignDto request, int userId, CancellationToken ct);
        Task<ApiResponse<bool>> UnassignSeatAsync(Guid seatId, Guid venueBoxId, Guid eventId, Guid? sessionId, CancellationToken ct);
        Task<ApiResponse<List<SeatAssignmentDto>>> GetSeatAssignmentsAsync(Guid venueBoxId, Guid eventId, Guid? sessionId, CancellationToken ct);
        /// <summary>Every seat this participation holds. Seating is event-scoped,
        /// so the id is an EventGuest.PublicId.</summary>
        Task<ApiResponse<List<GuestSeatAssignmentDto>>> GetGuestSeatAssignmentsAsync(Guid eventGuestId, CancellationToken ct);
    }
}
