using System;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.NominationLetter;

namespace Core.Interfaces.Services;

public interface INominationLetterService
{
    /// <summary>The letter for one mission, with versions and log. Returns an
    /// unsaved not_generated shell when none exists yet, so the screen has
    /// something to render before anyone presses Generate.</summary>
    Task<ApiResponse<NominationLetterResponse>> GetAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>One version's pinned roster.</summary>
    Task<ApiResponse<NominationLetterVersionDetailResponse>> GetVersionAsync(Guid versionId, CancellationToken ct = default);

    /// <summary>Issues a new version from the CURRENT roster and pins it.</summary>
    Task<ApiResponse<NominationLetterResponse>> GenerateAsync(GenerateLetterRequest request, int userId, CancellationToken ct = default);

    /// <summary>Records that the letter went to the host.</summary>
    Task<ApiResponse<NominationLetterResponse>> SendAsync(SendLetterRequest request, int userId, CancellationToken ct = default);

    /// <summary>Host accepted the delegation.</summary>
    Task<ApiResponse<NominationLetterResponse>> AcknowledgeAsync(LetterResponseRequest request, int userId, CancellationToken ct = default);

    /// <summary>Host asked for changes. The note is required.</summary>
    Task<ApiResponse<NominationLetterResponse>> RequestChangesAsync(LetterResponseRequest request, int userId, CancellationToken ct = default);
}
