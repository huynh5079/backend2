using BusinessLayer.DTOs.Admin.Tutors;
using BusinessLayer.Service.Interface;
using DataLayer.Entities;
using DataLayer.Repositories.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service
{
    public class PublicTutorService : IPublicTutorService
    {
        private readonly IUnitOfWork _uow;
        public PublicTutorService(IUnitOfWork uow) => _uow = uow;

        public async Task<PaginationResult<PublicTutorListItemDto>> GetApprovedTutorsPagedAsync(int page, int pageSize = 6)
        {
            var rs = await _uow.TutorProfiles.GetApprovedPagedAsync(page, pageSize);
            var mapped = rs.Data.Select(tp => new PublicTutorListItemDto
            {
                TutorId = tp.UserId!,
                Username = tp.User?.UserName,
                Email = tp.User?.Email ?? "",
                TeachingSubjects = tp.TeachingSubjects,
                TeachingLevel = tp.TeachingLevel,
                CreateDate = tp.User!.CreatedAt,
                AvatarUrl = tp.User!.AvatarUrl
            }).ToList();

            return new PaginationResult<PublicTutorListItemDto>(mapped, rs.TotalCount, rs.PageNumber, rs.PageSize);
        }

        public async Task<PaginationResult<PublicTutorListItemDto>> SearchAndFilterTutorsAsync(TutorSearchFilterDto filter)
        {
            var rs = await _uow.TutorProfiles.SearchAndFilterApprovedAsync(
                filter.Keyword,
                filter.Subject,
                filter.EducationLevel,
                filter.Mode,
                filter.Area,
                filter.Gender,
                filter.MinRating,
                filter.MinPrice,
                filter.MaxPrice,
                filter.Page,
                filter.PageSize);

            var mapped = rs.Data.Select(tp => new PublicTutorListItemDto
            {
                TutorId = tp.UserId!,
                Username = tp.User?.UserName,
                Email = tp.User?.Email ?? "",
                TeachingSubjects = tp.TeachingSubjects,
                TeachingLevel = tp.TeachingLevel,
                CreateDate = tp.User!.CreatedAt,
                AvatarUrl = tp.User!.AvatarUrl
            }).ToList();

            return new PaginationResult<PublicTutorListItemDto>(mapped, rs.TotalCount, rs.PageNumber, rs.PageSize);
        }

        public async Task<PublicTutorDetailDto?> GetApprovedTutorDetailAsync(string userId)
        {
            var tp = await _uow.TutorProfiles.GetApprovedByUserIdAsync(userId);
            if (tp == null) return null;

            var u = tp.User!;
            return new PublicTutorDetailDto
            {
                TutorId = u.Id,
                TutorProfileId = tp.Id,
                Username = u.UserName,
                Email = u.Email!,
                AvatarUrl = u.AvatarUrl,

                Gender = u.Gender?.ToString().ToLowerInvariant(),
                DateOfBirth = u.DateOfBirth,

                EducationLevel = tp.EducationLevel,
                University = tp.University,
                Major = tp.Major,

                TeachingExperienceYears = tp.TeachingExperienceYears,
                TeachingSubjects = tp.TeachingSubjects,
                TeachingLevel = tp.TeachingLevel,
                Bio = tp.Bio,
                Rating = tp.Rating,

                CreateDate = u.CreatedAt
            };
        }
    }
}
