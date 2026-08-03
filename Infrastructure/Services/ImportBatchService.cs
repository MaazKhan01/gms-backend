using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.Noification;
using DomainPersistence.Entities;

namespace Infrastructure.Services;

public class ImportBatchService(
    IUnitOfWork _unitOfWork,
    INotificationManagerService _notificationManager,
    ILogger<ImportBatchService> _logger) : IImportBatchService
{
    public async Task<ApiResponse<ImportBatchStatusDto>> GetStatusAsync(Guid batchId, CancellationToken ct = default)
    {
        var batch = await _unitOfWork.ImportBatches.Query()
            .FirstOrDefaultAsync(b => b.PublicId == batchId, ct);
        if (batch == null)
            return ApiResponse<ImportBatchStatusDto>.NotFoundResponse("Import batch not found.");

        var rows = await _unitOfWork.ImportBatchRows.Query()
            .Where(r => r.ImportBatchId == batch.Id)
            .OrderBy(r => r.RowNumber)
            .Select(r => new ImportBatchRowDto
            {
                Row = r.RowNumber, Title = r.Title, Success = r.Success,
                Error = r.Error, ErrorCategory = r.ErrorCategory,
            })
            .ToListAsync(ct);

        return ApiResponse<ImportBatchStatusDto>.SuccessResponse(new ImportBatchStatusDto
        {
            Id = batch.PublicId,
            Kind = batch.Kind,
            Status = batch.Status,
            Total = batch.Total,
            Imported = batch.Imported,
            Failed = batch.Failed,
            ErrorMessage = batch.ErrorMessage,
            Rows = rows,
        });
    }

    public async Task NotifyFinishedAsync(ImportBatch batch, string label, string redirectPath, CancellationToken ct = default)
    {
        try
        {
            var failed = batch.Status == "failed";
            var message = failed
                ? batch.ErrorMessage ?? "The import failed."
                : $"{batch.Imported} imported, {batch.Failed} failed out of {batch.Total}.";

            await _notificationManager.SendToUserAsync(batch.CreatedByUserId, new NotificationContent
            {
                Title = failed ? $"{label} import failed" : $"{label} import finished",
                Message = message,
                Type = $"{batch.Kind}-import",
                RedirectUrl = $"{redirectPath}?importBatch={batch.PublicId}",
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send import-finished notification for batch {BatchId}", batch.PublicId);
        }
    }
}
