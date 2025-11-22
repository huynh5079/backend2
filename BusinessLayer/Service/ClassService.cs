using BusinessLayer.DTOs.Class;
using BusinessLayer.DTOs.API;
using DataLayer.Entities;
using DataLayer.Repositories.GenericType.Abstraction;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BusinessLayer.Service;

public interface IClassService
{
    Task<ApiResponse<ClassResponseDto>> CreateClassAsync(CreateClassDto createClassDto, string tutorId);
    Task<ApiResponse<List<ClassResponseDto>>> GetClassesByTutorAsync(string tutorId);
    Task<ApiResponse<ClassResponseDto>> GetClassByIdAsync(string classId);
    Task<ApiResponse<List<ClassResponseDto>>> GetAvailableClassesAsync();
}

public class ClassService : IClassService
{
    private readonly IGenericRepository<Class> _classRepository;
    private readonly IGenericRepository<ClassSchedule> _classScheduleRepository;
    private readonly IGenericRepository<User> _userRepository;
    private readonly IGenericRepository<TutorProfile> _tutorProfileRepository;
    //private readonly IGenericRepository<Subject> _subjectRepository;
    //private readonly IGenericRepository<EducationLevel> _educationLevelRepository;
    private readonly TpeduContext _context;

    public ClassService(
        IGenericRepository<Class> classRepository,
        IGenericRepository<ClassSchedule> classScheduleRepository,
        IGenericRepository<User> userRepository,
        IGenericRepository<TutorProfile> tutorProfileRepository,
        //IGenericRepository<Subject> subjectRepository,
        //IGenericRepository<EducationLevel> educationLevelRepository,
        TpeduContext context)
    {
        _classRepository = classRepository;
        _classScheduleRepository = classScheduleRepository;
        _userRepository = userRepository;
        _tutorProfileRepository = tutorProfileRepository;
        //_subjectRepository = subjectRepository;
        //_educationLevelRepository = educationLevelRepository;
        _context = context;
    }

    public async Task<ApiResponse<ClassResponseDto>> CreateClassAsync(CreateClassDto createClassDto, string tutorId)
    {
        try
        {
            // Kiểm tra user có tồn tại không
            var user = await _userRepository.GetByIdAsync(tutorId);
            if (user == null)
            {
                return ApiResponse<ClassResponseDto>.Fail("Người dùng không tồn tại");
            }

            // Tìm TutorProfile từ UserId
            var tutorProfile = await _context.TutorProfiles
                .FirstOrDefaultAsync(tp => tp.UserId == tutorId);
            
            if (tutorProfile == null)
            {
                return ApiResponse<ClassResponseDto>.Fail("Gia sư chưa có hồ sơ");
            }

            //// Kiểm tra subject có tồn tại không
            //var subject = await _subjectRepository.GetByIdAsync(createClassDto.SubjectId);
            //if (subject == null)
            //{
            //    return ApiResponse<ClassResponseDto>.Fail("Môn học không tồn tại");
            //}

            //// Kiểm tra education level có tồn tại không
            //var educationLevel = await _educationLevelRepository.GetByIdAsync(createClassDto.EducationLevelId);
            //if (educationLevel == null)
            //{
            //    return ApiResponse<ClassResponseDto>.Fail("Cấp lớp không tồn tại");
            //}

            // Tạo lớp học mới
            var newClass = new Class
            {
                Id = Guid.NewGuid().ToString(),
                TutorId = tutorProfile.Id,
                //Title = $"{subject.SubjectName} - {educationLevel.LevelName}",
                //SubjectId = createClassDto.SubjectId,
                //EducationLevelId = createClassDto.EducationLevelId,
                //TeachingFormat = createClassDto.TeachingFormat,
                //Address = createClassDto.Address,
                StudentLimit = createClassDto.StudentLimit,
                CurrentStudentCount = 0,
                Description = createClassDto.Description,
                Status = "Active",
                //TotalSessions = createClassDto.ScheduleSlots.Count,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Thêm lớp học vào database
            try
            {
                await _classRepository.CreateAsync(newClass);
            }
            catch (Exception ex)
            {
                return ApiResponse<ClassResponseDto>.Fail($"Lỗi khi tạo lớp học: {ex.Message}. Inner: {ex.InnerException?.Message}");
            }

            // Tạo lịch dạy
            try
            {
                foreach (var slot in createClassDto.ScheduleSlots)
                {
                    var classSchedule = new ClassSchedule
                    {
                        Id = Guid.NewGuid().ToString(),
                        ClassId = newClass.Id,
                        //DayOfWeek = slot.DayOfWeek,
                        //Session = slot.Session,
                        //Slot = slot.Slot,
                        //StartTime = slot.StartTime,
                        //EndTime = slot.EndTime,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await _classScheduleRepository.CreateAsync(classSchedule);
                }
            }
            catch (Exception ex)
            {
                return ApiResponse<ClassResponseDto>.Fail($"Lỗi khi tạo lịch dạy: {ex.Message}. Inner: {ex.InnerException?.Message}");
            }

            // Lấy thông tin lớp học vừa tạo để trả về
            var createdClass = await _classRepository.GetByIdAsync(newClass.Id);
            var responseDto = await MapToResponseDtoAsync(createdClass);

            return ApiResponse<ClassResponseDto>.Ok(responseDto, "Tạo lớp học thành công");
        }
        catch (Exception ex)
        {
            return ApiResponse<ClassResponseDto>.Fail($"Lỗi khi tạo lớp học: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<ClassResponseDto>>> GetClassesByTutorAsync(string tutorId)
    {
        try
        {
            var classes = await _context.Classes
                .Include(c => c.Subject)
                .Include(c => c.EducationLevel)
                .Include(c => c.ClassSchedules)
                .Where(c => c.TutorId == tutorId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            var responseDtos = new List<ClassResponseDto>();

            foreach (var classEntity in classes)
            {
                var responseDto = await MapToResponseDtoAsync(classEntity);
                responseDtos.Add(responseDto);
            }

            return ApiResponse<List<ClassResponseDto>>.Ok(responseDtos, "Lấy danh sách lớp học thành công");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<ClassResponseDto>>.Fail($"Lỗi khi lấy danh sách lớp học: {ex.Message}");
        }
    }

    public async Task<ApiResponse<ClassResponseDto>> GetClassByIdAsync(string classId)
    {
        try
        {
            var classEntity = await _classRepository.GetByIdAsync(classId);
            if (classEntity == null)
            {
                return ApiResponse<ClassResponseDto>.Fail("Lớp học không tồn tại");
            }

            var responseDto = await MapToResponseDtoAsync(classEntity);
            return ApiResponse<ClassResponseDto>.Ok(responseDto, "Lấy thông tin lớp học thành công");
        }
        catch (Exception ex)
        {
            return ApiResponse<ClassResponseDto>.Fail($"Lỗi khi lấy thông tin lớp học: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<ClassResponseDto>>> GetAvailableClassesAsync()
    {
        try
        {
            var classes = await _context.Classes
                .Include(c => c.Subject)
                .Include(c => c.EducationLevel)
                .Include(c => c.ClassSchedules)
                .Where(c => c.Status == "Active" && c.CurrentStudentCount < c.StudentLimit)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            var responseDtos = new List<ClassResponseDto>();

            foreach (var classEntity in classes)
            {
                var responseDto = await MapToResponseDtoAsync(classEntity);
                responseDtos.Add(responseDto);
            }

            return ApiResponse<List<ClassResponseDto>>.Ok(responseDtos, "Lấy danh sách lớp học có sẵn thành công");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<ClassResponseDto>>.Fail($"Lỗi khi lấy danh sách lớp học có sẵn: {ex.Message}");
        }
    }

    private async Task<ClassResponseDto> MapToResponseDtoAsync(Class classEntity)
    {
        //var subject = await _subjectRepository.GetByIdAsync(classEntity.SubjectId);
        //var educationLevel = await _educationLevelRepository.GetByIdAsync(classEntity.EducationLevelId);
        var schedules = await _context.ClassSchedules
            .Where(cs => cs.ClassId == classEntity.Id)
            .ToListAsync();

        return new ClassResponseDto
        {
            Id = classEntity.Id,
            Title = classEntity.Title ?? "",
            //SubjectName = subject?.SubjectName ?? "",
            //EducationLevelName = educationLevel?.LevelName ?? "",
            //TeachingFormat = classEntity.TeachingFormat ?? "",
            //Address = classEntity.Address ?? "",
            StudentLimit = classEntity.StudentLimit,
            CurrentStudentCount = classEntity.CurrentStudentCount,
            Description = classEntity.Description,
            Status = classEntity.Status ?? "",
            CreatedAt = classEntity.CreatedAt,
            ScheduleSlots = schedules.Select(s => new ScheduleSlotResponseDto
            {
                //DayOfWeek = s.DayOfWeek,
                //Session = s.Session,
                //Slot = s.Slot,
                //StartTime = s.StartTime,
                //EndTime = s.EndTime
            }).ToList()
        };
    }
}
