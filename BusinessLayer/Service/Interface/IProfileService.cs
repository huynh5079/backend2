using BusinessLayer.DTOs.Profile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface
{
    public interface IProfileService
    {
        Task<StudentProfileDto> GetStudentProfileAsync(string userId);
        Task<ParentProfileDto> GetParentProfileAsync(string userId);
        Task<TutorProfileDto> GetTutorProfileAsync(string userId);
        Task UpdateStudentAsync(string userId, UpdateStudentProfileRequest dto);
        Task UpdateParentAsync(string userId, UpdateParentProfileRequest dto);
        Task UpdateTutorAsync(string userId, UpdateTutorProfileRequest dto, CancellationToken ct = default);
    }
}
