using System;
using System.Threading;
using System.Threading.Tasks;
using Core.ViewModel.Common;
using DomainPersistence.Entities;

namespace Core.Interfaces.Services;

// Shared by every bulk-import feature (Events, Guests, …) — the actual import
// logic lives per-module (IEventService/IGuestService); this is just the
// polling endpoint both controllers hit, plus the "tell the user it's done"
// notification both jobs send on completion.
public interface IImportBatchService
{
    Task<ApiResponse<ImportBatchStatusDto>> GetStatusAsync(Guid batchId, CancellationToken ct = default);

    Task NotifyFinishedAsync(ImportBatch batch, string label, string redirectPath, CancellationToken ct = default);
}
