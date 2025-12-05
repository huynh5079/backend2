using BusinessLayer.DTOs.FavoriteTutor;
using BusinessLayer.Service.Interface;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BusinessLayer.Service
{
    public class FavoriteTutorService : IFavoriteTutorService
    {
        private readonly IUnitOfWork _uow;

        public FavoriteTutorService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<FavoriteTutorDto> AddFavoriteAsync(string userId, string tutorProfileId)
        {
            // 1. Validate user
            var user = await _uow.Users.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException("Không tìm thấy user");

            // 2. Chỉ Student và Parent mới được lưu tutor yêu thích
            if (user.RoleName != "Student" && user.RoleName != "Parent")
                throw new UnauthorizedAccessException("Chỉ học sinh và phụ huynh mới được lưu gia sư yêu thích");

            // 3. Validate tutor profile exists và active
            var tutorProfile = await _uow.TutorProfiles.GetByIdAsync(tutorProfileId)
                ?? throw new KeyNotFoundException("Không tìm thấy gia sư");

            var tutorUser = await _uow.Users.GetByIdAsync(tutorProfile.UserId)
                ?? throw new KeyNotFoundException("Không tìm thấy thông tin user của gia sư");

            if (tutorUser.Status != AccountStatus.Active)
                throw new InvalidOperationException("Gia sư này hiện không hoạt động");

            // 4. Check xem đã có record chưa (kể cả đã soft delete)
            var existingFavorite = await _uow.FavoriteTutors.GetIncludingDeletedAsync(userId, tutorProfileId);

            if (existingFavorite != null)
            {
                // 4a. Nếu đã tồn tại và chưa bị xóa → duplicate
                if (existingFavorite.DeletedAt == null)
                    throw new InvalidOperationException("Bạn đã lưu gia sư này vào danh sách yêu thích");

                // 4b. Nếu đã bị soft delete → restore
                existingFavorite.DeletedAt = null;
                existingFavorite.UpdatedAt = DateTime.Now;
                await _uow.FavoriteTutors.UpdateAsync(existingFavorite);
                await _uow.SaveChangesAsync();

                return MapToDto(existingFavorite, tutorProfile, tutorUser);
            }

            // 5. Chưa tồn tại → Create new
            var favorite = new FavoriteTutor
            {
                UserId = userId,
                TutorProfileId = tutorProfileId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            await _uow.FavoriteTutors.CreateAsync(favorite);
            await _uow.SaveChangesAsync();

            // 6. Return DTO
            return MapToDto(favorite, tutorProfile, tutorUser);
        }

        public async Task<bool> RemoveFavoriteAsync(string userId, string tutorProfileId)
        {
            var favorite = await _uow.FavoriteTutors.GetAsync(userId, tutorProfileId);
            if (favorite == null)
                return false;

            // Soft delete
            favorite.DeletedAt = DateTime.Now;
            await _uow.FavoriteTutors.UpdateAsync(favorite);
            await _uow.SaveChangesAsync();

            return true;
        }

        public async Task<List<FavoriteTutorDto>> GetMyFavoritesAsync(string userId)
        {
            var favorites = await _uow.FavoriteTutors.GetByUserIdAsync(userId);

            return favorites.Select(f => MapToDto(
                f,
                f.TutorProfile,
                f.TutorProfile.User
            )).ToList();
        }

        public async Task<bool> IsFavoritedAsync(string userId, string tutorProfileId)
        {
            return await _uow.FavoriteTutors.IsFavoritedAsync(userId, tutorProfileId);
        }

        // Helper method
        private static FavoriteTutorDto MapToDto(FavoriteTutor favorite, TutorProfile tutorProfile, User tutorUser)
        {
            return new FavoriteTutorDto
            {
                Id = favorite.Id,
                TutorProfileId = tutorProfile.Id,
                TutorUserId = tutorUser.Id,
                TutorName = tutorUser.UserName ?? "Tutor",
                TutorAvatar = tutorUser.AvatarUrl,
                TutorBio = tutorProfile.Bio,
                TutorRating = tutorProfile.Rating,
                FavoritedAt = favorite.CreatedAt
            };
        }
    }
}
