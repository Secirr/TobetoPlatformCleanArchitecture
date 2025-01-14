﻿
using Application.Features.Auth.Commands.InstructorRegister;
using Application.Features.Auth.Commands.Register;
using Application.Features.Auth.Commands.StudentRegister;
using Application.Features.Auth.Rules;
using Application.Services.AuthService;
using Application.Services.Instructors;
using Application.Services.Repositories;
using Application.Services.Students;
using Core.Application.Dtos;
using Core.Security.Entities;
using Core.Security.Hashing;
using Core.Security.JWT;
using Domain.Entities;
using MediatR;

namespace Application.Features.Auth.Commands.Register;

public class InstructorRegisterCommand : IRequest<InstructorRegisteredResponse>
{
    public InstructorForRegisterDto InstructorForRegisterDto { get; set; }
    public string IpAddress { get; set; }

    public InstructorRegisterCommand()
    {
        InstructorForRegisterDto = null!;
        IpAddress = string.Empty;
    }

    public InstructorRegisterCommand(InstructorForRegisterDto instructorForRegisterDto, string ipAddress)
    {
        InstructorForRegisterDto = instructorForRegisterDto;
        IpAddress = ipAddress;
    }

    public class InstructorRegisterCommandHandler : IRequestHandler<InstructorRegisterCommand, InstructorRegisteredResponse>
    {
        private readonly IUserRepository _userRepository;
        private readonly IInstructorsService _instructorsService;
        private readonly IOperationClaimRepository _operationClaimRepository;
        private readonly IUserOperationClaimRepository _userOperationClaimRepository;
        private readonly IAuthService _authService;
        private readonly AuthBusinessRules _authBusinessRules;

        public InstructorRegisterCommandHandler(IUserRepository userRepository, IInstructorsService instructorsService, IOperationClaimRepository operationClaimRepository, IUserOperationClaimRepository userOperationClaimRepository, IAuthService authService, AuthBusinessRules authBusinessRules)
        {
            _userRepository = userRepository;
            _instructorsService = instructorsService;
            _operationClaimRepository = operationClaimRepository;
            _userOperationClaimRepository = userOperationClaimRepository;
            _authService = authService;
            _authBusinessRules = authBusinessRules;
        }

        public async Task<InstructorRegisteredResponse> Handle(InstructorRegisterCommand request, CancellationToken cancellationToken)
        {

            await _authBusinessRules.InstructorEmailShouldBeNotExists(request.InstructorForRegisterDto.Email);

            HashingHelper.CreatePasswordHash(
                request.InstructorForRegisterDto.Password,
                passwordHash: out byte[] passwordHash,
                passwordSalt: out byte[] passwordSalt
            );
            User newUser =
                new()
                {
                    Email = request.InstructorForRegisterDto.Email,
                    FirstName = request.InstructorForRegisterDto.FirstName,
                    LastName = request.InstructorForRegisterDto.LastName,
                    PasswordHash = passwordHash,
                    PasswordSalt = passwordSalt,
                    Status = true
                };

            User createdUser = await _userRepository.AddAsync(newUser);

            Instructor newInstructor =
               new()
               {
                   UserId = createdUser.Id
               };

            var createdInstructor = await _instructorsService.AddAsync(newInstructor);

            AccessToken createdAccessToken = await _authService.CreateAccessToken(createdUser);

            // Instructor yetkisini bulma
            OperationClaim instructorClaim = await _operationClaimRepository.GetAsync(c => c.Name == "Instructor");

            // Eğer Student yetkisi bulunamazsa bir hata fırlat
            if (instructorClaim == null)
            {
                throw new Exception("instructor yetkisi bulunamadı.");
            }

            // UserOperationClaim nesnesini oluştur ve veritabanına kaydet
            UserOperationClaim userOperationClaim = new UserOperationClaim(createdUser.Id, instructorClaim.Id);
            await _userOperationClaimRepository.AddAsync(userOperationClaim);

            Core.Security.Entities.RefreshToken createdRefreshToken = await _authService.CreateRefreshToken(createdUser, request.IpAddress);
            Core.Security.Entities.RefreshToken addedRefreshToken = await _authService.AddRefreshToken(createdRefreshToken);

            InstructorRegisteredResponse instructorRegisteredResponse = new() { AccessToken = createdAccessToken, RefreshToken = addedRefreshToken };
            return instructorRegisteredResponse;
        }
    }
}
