using AutoMapper;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.ViewModel.Common;
using Core.ViewModel.InvitationTemplate;
using DomainPersistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class InvitationTemplateService(IUnitOfWork _unitOfWork, IMapper _mapper, ILogger<InvitationTemplateService> _logger) : IInvitationTemplateService
{
    public async Task<ApiResponse<List<InvitationTemplateResponse>>> GetByEventAsync(Guid eventId, CancellationToken ct = default)
    {
        try
        {
            var list = await _unitOfWork.InvitationTemplates.Query()
                .Include(t => t.Event)
                .Where(t => t.Event.PublicId == eventId && t.IsActive)
                .OrderBy(t => t.Name)
                .ToListAsync(ct);

            return ApiResponse<List<InvitationTemplateResponse>>.SuccessResponse(_mapper.Map<List<InvitationTemplateResponse>>(list));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving templates for event {EventId}", eventId);
            return ApiResponse<List<InvitationTemplateResponse>>.ServerErrorResponse("An error occurred while retrieving templates");
        }
    }

    public async Task<ApiResponse<InvitationTemplateResponse>> CreateAsync(CreateInvitationTemplateRequest request, int createdBy, CancellationToken ct = default)
    {
        try
        {
            // request.EventId is a public Guid; resolve it to the internal Event.
            var ev = await _unitOfWork.Events.GetByPublicIdAsync(request.EventId, ct);
            if (ev == null)
                return ApiResponse<InvitationTemplateResponse>.NotFoundResponse("Event not found");

            var template = new InvitationTemplate
            {
                EventId     = ev.Id,
                Event       = ev,
                Name        = request.Name,
                NameAr      = request.NameAr,
                Language    = request.Language,
                Subject     = request.Subject,
                SubjectAr   = request.SubjectAr,
                Body        = request.Body,
                BodyAr      = request.BodyAr,
                TargetTiers = request.TargetTiers.Count > 0 ? string.Join(",", request.TargetTiers) : null,
                Color       = request.Color,
                IsActive    = true,
                CreatedAt   = DateTime.UtcNow,
                CreatedBy   = createdBy,
                IsDeleted   = false
            };

            await _unitOfWork.InvitationTemplates.AddAsync(template, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<InvitationTemplateResponse>.SuccessResponse(_mapper.Map<InvitationTemplateResponse>(template), "Template created");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating invitation template");
            return ApiResponse<InvitationTemplateResponse>.ServerErrorResponse("An error occurred while creating the template");
        }
    }

    public async Task<ApiResponse<InvitationTemplateResponse>> UpdateAsync(Guid id, UpdateInvitationTemplateRequest request, int updatedBy, CancellationToken ct = default)
    {
        try
        {
            var template = await _unitOfWork.InvitationTemplates.GetByPublicIdAsync(id, t => t.Event, ct);
            if (template == null)
                return ApiResponse<InvitationTemplateResponse>.NotFoundResponse("Template not found");

            if (request.Name != null) template.Name = request.Name;
            if (request.NameAr != null) template.NameAr = request.NameAr;
            if (request.Language != null) template.Language = request.Language;
            if (request.Subject != null) template.Subject = request.Subject;
            if (request.SubjectAr != null) template.SubjectAr = request.SubjectAr;
            if (request.Body != null) template.Body = request.Body;
            if (request.BodyAr != null) template.BodyAr = request.BodyAr;
            if (request.TargetTiers != null) template.TargetTiers = request.TargetTiers.Count > 0 ? string.Join(",", request.TargetTiers) : null;
            if (request.Color != null) template.Color = request.Color;
            if (request.IsActive.HasValue) template.IsActive = request.IsActive.Value;

            template.SetUpdateAudit(updatedBy);
            _unitOfWork.InvitationTemplates.Update(template);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<InvitationTemplateResponse>.SuccessResponse(_mapper.Map<InvitationTemplateResponse>(template), "Template updated");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating invitation template {Id}", id);
            return ApiResponse<InvitationTemplateResponse>.ServerErrorResponse("An error occurred while updating the template");
        }
    }

    public async Task<ApiResponse<bool>> DeleteAsync(Guid id, int deletedBy, CancellationToken ct = default)
    {
        try
        {
            var template = await _unitOfWork.InvitationTemplates.GetByPublicIdAsync(id, ct);
            if (template == null)
                return ApiResponse<bool>.NotFoundResponse("Template not found");

            template.MarkAsDeleted(deletedBy);
            _unitOfWork.InvitationTemplates.Update(template);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApiResponse<bool>.SuccessResponse(true, "Template deleted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting invitation template {Id}", id);
            return ApiResponse<bool>.ServerErrorResponse("An error occurred while deleting the template");
        }
    }
}
