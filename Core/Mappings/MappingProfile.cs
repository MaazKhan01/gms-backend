using AutoMapper;
using Core.ViewModel.AccountRequest;
using Core.ViewModel.Event;
using Core.ViewModel.Noification;
using Core.ViewModel.Permission;
using Core.ViewModel.Role;
using Core.ViewModel.User;
using DomainPersistence.Entities;
using System;
using System.Linq;

namespace Core.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // User mappings
        CreateMap<User, UserResponse>()
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role != null ? src.Role.Code : null))
            .ForMember(dest => dest.RoleName, opt => opt.MapFrom(src => src.Role != null ? src.Role.Name : null));

        CreateMap<CreateUserRequest, User>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => Guid.NewGuid()))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
            .ForMember(dest => dest.Role, opt => opt.Ignore());

        // Role mappings
        CreateMap<Role, RoleResponse>()
            .ForMember(dest => dest.Permissions, opt => opt.MapFrom(src => src.RolePermissions.Select(rp => new PermissionDto
            {
                Id = rp.Permission.Id,
                Name = rp.Permission.Name,
                Code = rp.Permission.Code,
                Module = rp.Permission.Module
            }).ToList()))
            .ForMember(dest => dest.UserCount, opt => opt.Ignore());

        CreateMap<CreateRoleRequest, Role>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => Guid.NewGuid()))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow));

        // Permission mappings
        CreateMap<Permission, PermissionResponse>();

        CreateMap<CreatePermissionRequest, Permission>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => Guid.NewGuid()))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow));

        // Notification mappings
        CreateMap<Notification, NotificationResponse>();

        // Event mappings
        CreateMap<Event, EventResponse>();
        CreateMap<Session, SessionResponse>();
        CreateMap<CreateEventRequest, Event>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => Guid.NewGuid()))
            .ForMember(dest => dest.AppKey, opt => opt.Ignore())
            .ForMember(dest => dest.Sessions, opt => opt.Ignore());
        CreateMap<CreateSessionRequest, Session>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => Guid.NewGuid()))
            .ForMember(dest => dest.Event, opt => opt.Ignore());

        // Account request mappings
        CreateMap<AccountRequest, AccountRequestResponse>();
    }
}
