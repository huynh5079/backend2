using BusinessLayer.Service.Interface;
using DataLayer.Entities;
using DataLayer.Repositories.GenericType.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service
{
    public class TutorProfileService : ITutorProfileService
    {
        private readonly IGenericRepository<TutorProfile> _tutorRepository;

        public TutorProfileService(IGenericRepository<TutorProfile> tutorRepository)
        {
            _tutorRepository = tutorRepository;
        }

        public async Task<string?> GetTutorProfileIdByUserIdAsync(string userId)
        {
            // Logic tra cứu được đóng gói an toàn ở đây
            var tutorProfile = await _tutorRepository.GetAsync(t => t.UserId == userId);
            return tutorProfile?.Id;
        }
    }
}
