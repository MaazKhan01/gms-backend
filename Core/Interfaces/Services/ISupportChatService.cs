using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using Core.ViewModel.SupportChat;
using Core.ViewModel.VipApp;

namespace Core.Interfaces.Services;

// Owns the full guest ↔ admin support chat lifecycle. Deliberately a single
// service (not split guest/admin) because both sides mutate the same
// SupportConversation row — get-or-create, append message, bump counters,
// notify. Splitting it would duplicate that sequence in two places.
public interface ISupportChatService
{
    // ---- Guest-facing (called from VipAppController) ----
    Task<ApiResponse<List<SupportConversationSummaryResponse>>> GetMyConversationsAsync(int guestId, CancellationToken ct = default);
    Task<ApiResponse<PaginatedResponse<SupportMessageResponse>>> GetGuestMessagesAsync(int guestId, PagedRequest request, CancellationToken ct = default);
    Task<ApiResponse<SupportMessageResponse>> SendGuestMessageAsync(int guestId, SendSupportMessageRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> MarkReadByGuestAsync(int guestId, CancellationToken ct = default);

    // ---- Admin-facing (called from SupportChatController) ----
    Task<ApiResponse<PaginatedResponse<SupportConversationSummaryResponse>>> GetConversationsAsync(SupportConversationPagedRequest request, CancellationToken ct = default);
    Task<ApiResponse<PaginatedResponse<SupportMessageResponse>>> GetMessagesAsync(Guid conversationId, PagedRequest request, CancellationToken ct = default);
    Task<ApiResponse<SupportMessageResponse>> ReplyAsync(Guid conversationId, SendSupportMessageRequest request, CancellationToken ct = default);

    // Admin-initiated: get-or-create the guest's conversation, then send. Also
    // the safe path when the admin's local list is stale and a conversation
    // already exists for this guest — it just continues that thread instead
    // of erroring or duplicating it.
    /// <summary>Support chat is person-level (one thread per human), so
    /// <paramref name="personId"/> is a Guest.PublicId — never an EventGuest id.</summary>
    Task<ApiResponse<SupportMessageResponse>> StartOrReplyByGuestAsync(Guid personId, SendSupportMessageRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> MarkReadByAdminAsync(Guid conversationId, CancellationToken ct = default);
    Task<ApiResponse<bool>> CloseAsync(Guid conversationId, CancellationToken ct = default);
    Task<ApiResponse<bool>> ReopenAsync(Guid conversationId, CancellationToken ct = default);

    // ---- Driver <-> Guest (called from ChatController; callerUserId/senderRole
    // are resolved by the controller from ICurrentUser, never trusted from the
    // request body) ----
    Task<ApiResponse<SupportMessageResponse>> SendDriverGuestMessageAsync(
        int senderUserId, string senderRole, SendDriverGuestMessageRequest request, CancellationToken ct = default);

    // The whole thread, both directions, for either participant. Each message
    // carries IsMine so the client can render sent vs received without knowing
    // which side of the conversation it is.
    Task<ApiResponse<PaginatedResponse<SupportMessageResponse>>> GetDriverGuestThreadAsync(
        int callerUserId, Guid conversationId, PagedRequest request, CancellationToken ct = default);
}
