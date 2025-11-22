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
    public class StudentProfileService : IStudentProfileService
    {
        private readonly IGenericRepository<StudentProfile> _studentRepository;

        public StudentProfileService(IGenericRepository<StudentProfile> studentRepository)
        {
            _studentRepository = studentRepository;
        }

        public async Task<string?> GetStudentProfileIdByUserIdAsync(string userId)
        {
            // Logic tra cứu được đóng gói an toàn ở đây
            var studentProfile = await _studentRepository.GetAsync(t => t.UserId == userId);
            return studentProfile?.Id;
        }
    }
}
