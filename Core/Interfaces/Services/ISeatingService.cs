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
        Task<ApiResponse<List<GuestSeatAssignmentDto>>> GetGuestSeatAssignmentsAsync(Guid guestId, CancellationToken ct);
    }
}
