using AutoMapper;
using Core.Constants;
using Core.ViewModel.AccountRequest;
using Core.ViewModel.Event;
using Core.ViewModel.Guest;
using Core.ViewModel.InvitationTemplate;
using Core.ViewModel.Nationality;
using Core.ViewModel.Noification;
using Core.ViewModel.Permission;
using Core.ViewModel.Role;
using Core.ViewModel.User;
using Core.ViewModel.Venue;
using DomainPersistence.Entities;
using System;
using System.Linq;

namespace Core.Mappings;

public class MappingProfile : Profile
{
    // Convention: response DTOs carry the entity's public Guid in their `Id`.
    // FK id fields carry the *related* entity's PublicId (via navigation).
    public MappingProfile()
    {
        // User mappings
        CreateMap<User, UserResponse>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PublicId))
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role != null ? src.Role.Code : null))
            .ForMember(dest => dest.RoleName, opt => opt.MapFrom(src => src.Role != null ? src.Role.Name : null))
            // Not src.RoleId — that's the internal int; the DTO carries public ids.
            .ForMember(dest => dest.RoleId, opt => opt.MapFrom(src => src.Role != null ? src.Role.PublicId : (Guid?)null));

        CreateMap<CreateUserRequest, User>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
            .ForMember(dest => dest.Role, opt => opt.Ignore());

        // Role mappings
        CreateMap<Role, RoleResponse>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PublicId))
            .ForMember(dest => dest.Permissions, opt => opt.MapFrom(src => src.RolePermissions.Select(rp => new PermissionDto
            {
                Id = rp.Permission.PublicId,
                Name = rp.Permission.Name,
                Code = rp.Permission.Code,
                Module = rp.Permission.Module
            }).ToList()))
            .ForMember(dest => dest.UserCount, opt => opt.Ignore());

        CreateMap<CreateRoleRequest, Role>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow));

        // Permission mappings
        CreateMap<Permission, PermissionResponse>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PublicId));

        CreateMap<CreatePermissionRequest, Permission>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow));

        // Notification mappings
        CreateMap<Notification, NotificationResponse>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PublicId))
            .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.User != null ? src.User.PublicId : Guid.Empty));

        // Event mappings
        CreateMap<Event, EventResponse>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PublicId))
            .ForMember(dest => dest.VenueId, opt => opt.MapFrom(src => src.Venue != null ? (Guid?)src.Venue.PublicId : null));
        CreateMap<Session, SessionResponse>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PublicId))
            .ForMember(dest => dest.EventId, opt => opt.MapFrom(src => src.Event != null ? src.Event.PublicId : Guid.Empty))
            .ForMember(dest => dest.VenueId, opt => opt.MapFrom(src => src.Venue != null ? (Guid?)src.Venue.PublicId : null));
        CreateMap<CreateEventRequest, Event>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.AppKey, opt => opt.Ignore())
            .ForMember(dest => dest.Sessions, opt => opt.Ignore())
            // Guid on the request, int FK on the entity — resolved and set explicitly
            // in EventService.CreateEventAsync instead.
            .ForMember(dest => dest.VenueId, opt => opt.Ignore());
        CreateMap<CreateSessionRequest, Session>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.Event, opt => opt.Ignore())
            .ForMember(dest => dest.VenueId, opt => opt.Ignore());

        // Account request mappings
        CreateMap<AccountRequest, AccountRequestResponse>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PublicId))
            .ForMember(dest => dest.RequestedRoleId, opt => opt.Ignore())
            .ForMember(dest => dest.ReviewedBy, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedUserId, opt => opt.Ignore());

        // Guest mappings — sourced from EventGuest (the participation), not Guest
        // (the person), because that is what GuestResponse describes: one person's
        // involvement in one event. Person-level fields come through src.Guest.
        CreateMap<EventGuest, GuestResponse>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PublicId))
            .ForMember(dest => dest.PersonId, opt => opt.MapFrom(src => src.Guest != null ? src.Guest.PublicId : Guid.Empty))
            .ForMember(dest => dest.EventId, opt => opt.MapFrom(src => src.Event != null ? src.Event.PublicId : Guid.Empty))
            // ── person-level, reached through the join ─────────────────────────
            .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.Guest != null ? src.Guest.FirstName : null))
            .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.Guest != null ? src.Guest.LastName : null))
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Guest != null ? src.Guest.Email : null))
            .ForMember(dest => dest.PhotoUrl, opt => opt.MapFrom(src => src.Guest != null ? src.Guest.PhotoUrl : null))
            .ForMember(dest => dest.NationalityId,
                opt => opt.MapFrom(src => src.Guest != null && src.Guest.Nationality != null ? (Guid?)src.Guest.Nationality.PublicId : null))
            .ForMember(dest => dest.NationalityName,
                opt => opt.MapFrom(src => src.Guest != null && src.Guest.Nationality != null ? src.Guest.Nationality.Name : null))
            .ForMember(dest => dest.NationalityCode,
                opt => opt.MapFrom(src => src.Guest != null && src.Guest.Nationality != null ? src.Guest.Nationality.Code : null))
            .ForMember(dest => dest.NationalityFlag,
                opt => opt.MapFrom(src => src.Guest != null && src.Guest.Nationality != null ? src.Guest.Nationality.Flag : null))
            // ── per-event ─────────────────────────────────────────────────────
            .ForMember(dest => dest.SessionIds,
                opt => opt.MapFrom(src => src.GuestSessions.Select(gs => gs.Session.PublicId).ToList()))
            .ForMember(dest => dest.OrganizationId,
                opt => opt.MapFrom(src => src.OrganizationRef != null ? (Guid?)src.OrganizationRef.PublicId : null))
            // Stored as JSON on the entity, a plain int list on the wire.
            .ForMember(dest => dest.AllowedServices,
                opt => opt.MapFrom(src => GuestServices.Parse(src.AllowedServicesJson)))
            .ForMember(dest => dest.ServiceLevelId,
                opt => opt.MapFrom(src => src.ServiceLevel != null ? (Guid?)src.ServiceLevel.PublicId : null))
            .ForMember(dest => dest.ServiceLevelName,
                opt => opt.MapFrom(src => src.ServiceLevel != null ? src.ServiceLevel.Name : null))
            .ForMember(dest => dest.ServiceLevelNameAr,
                opt => opt.MapFrom(src => src.ServiceLevel != null ? src.ServiceLevel.NameAr : null))
            .ForMember(dest => dest.ServiceLevelColor,
                opt => opt.MapFrom(src => src.ServiceLevel != null ? src.ServiceLevel.Color : null));

        // Nationality mappings
        CreateMap<Nationality, NationalityResponse>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PublicId));

        // InvitationTemplate mappings
        CreateMap<InvitationTemplate, InvitationTemplateResponse>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PublicId))
            .ForMember(dest => dest.EventId, opt => opt.MapFrom(src => src.Event != null ? src.Event.PublicId : Guid.Empty))
            .ForMember(dest => dest.TargetTiers,
                opt => opt.MapFrom(src => string.IsNullOrEmpty(src.TargetTiers)
                    ? new List<string>()
                    : src.TargetTiers.Split(',', System.StringSplitOptions.RemoveEmptyEntries).ToList()));

        // Venue mappings
        CreateMap<Venue, GetVenueResonse>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PublicId))
            .ForMember(dest => dest.VenueName, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.VenueType, opt => opt.MapFrom(src => src.Type != null ? src.Type.PublicId : Guid.Empty))
            .ForMember(dest => dest.LocationId, opt => opt.MapFrom(src => src.Location != null ? (Guid?)src.Location.PublicId : null))
            .ForMember(dest => dest.Location, opt => opt.MapFrom(src => src.Location))
            .ForMember(dest => dest.Category, opt => opt.MapFrom(src => src.Category ?? new List<string>()));
        CreateMap<Location, Core.ViewModel.Travel.LocationDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PublicId));
        CreateMap<VenueBox, VenueBoxDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PublicId))
            .ForMember(dest => dest.EventId, opt => opt.MapFrom(src => src.Event != null ? (Guid?)src.Event.PublicId : null))
            .ForMember(dest => dest.SessionId, opt => opt.MapFrom(src => src.Session != null ? (Guid?)src.Session.PublicId : null))
            .ForMember(dest => dest.VenueElements, opt => opt.MapFrom(src => src.VenueLayouts));
        CreateMap<VenueBlock, VenueBlockDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PublicId));
        CreateMap<VenueLayout, VenueLayoutDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PublicId))
            .ForMember(dest => dest.Props, opt => opt.MapFrom(src => src.VenueLayoutProps));
        CreateMap<VenueLayoutProp, VenueLayoutPropDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PublicId))
            // Seats always returned in order
            .ForMember(dest => dest.Seats, opt => opt.MapFrom(src => src.Seats.OrderBy(s => s.Index)));
        CreateMap<SeatProperties, SeatPropertyDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PublicId));
    }
}
