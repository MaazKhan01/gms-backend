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
    Task<ApiResponse<bool>> MarkReadByAdminAsync(Guid conversationId, CancellationToken ct = default);
    Task<ApiResponse<bool>> CloseAsync(Guid conversationId, CancellationToken ct = default);
    Task<ApiResponse<bool>> ReopenAsync(Guid conversationId, CancellationToken ct = default);
}
