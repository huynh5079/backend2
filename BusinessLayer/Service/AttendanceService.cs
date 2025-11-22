using BusinessLayer.DTOs.Attendance;
using BusinessLayer.Service.Interface;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service
{
    public class AttendanceService : IAttendanceService
    {
        private readonly IUnitOfWork _uow;
        private readonly TpeduContext _ctx;

        public AttendanceService(IUnitOfWork uow, TpeduContext ctx)
        {
            _uow = uow;
            _ctx = ctx;
        }

        public async Task<AttendanceRecordDto> MarkAsync(string tutorUserId, MarkAttendanceRequest req)
        {
            // 1) Validate lesson + quyền tutor
            var lesson = await _ctx.Lessons
                .Include(l => l.Class)
                .Include(l => l.ScheduleEntries)
                .FirstOrDefaultAsync(l => l.Id == req.LessonId)
                ?? throw new KeyNotFoundException("không tìm thấy buổi học");

            var owningTutorId = lesson.Class?.TutorId
                                ?? lesson.ScheduleEntries.FirstOrDefault()?.TutorId;
            if (string.IsNullOrEmpty(owningTutorId))
                throw new InvalidOperationException("lesson không gắn tutor");

            if (owningTutorId != tutorUserId)
                throw new UnauthorizedAccessException("bạn không có quyền điểm danh buổi này");

            // 2) Validate student
            var student = await _uow.StudentProfiles.GetByIdAsync(req.StudentId)
                          ?? throw new KeyNotFoundException("không tìm thấy học sinh");

            if (lesson.ClassId != null)
            {
                var inClass = await _ctx.ClassAssigns
                    .AnyAsync(x => x.ClassId == lesson.ClassId && x.StudentId == student.Id);
                if (!inClass) throw new InvalidOperationException("học sinh không thuộc lớp");
            }
            // Với 1-1, nếu bạn muốn ràng buộc Lesson–Student thì thêm validate tại đây.

            // 3) Parse status
            if (!Enum.TryParse<AttendanceStatus>(req.Status, true, out var status))
                throw new ArgumentException("trạng thái không hợp lệ");

            // 4) Upsert attendance
            var att = await _uow.Attendances.FindAsync(req.LessonId, req.StudentId);
            if (att == null)
            {
                att = new Attendance
                {
                    LessonId = req.LessonId,
                    StudentId = req.StudentId,
                    Status = status,
                    Notes = req.Notes,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                await _uow.Attendances.CreateAsync(att);
            }
            else
            {
                att.Status = status;
                att.Notes = req.Notes;
                att.UpdatedAt = DateTime.Now;
                await _uow.Attendances.UpdateAsync(att);
            }

            await _uow.SaveChangesAsync();

            var studentUser = await _uow.Users.GetByIdAsync(student.UserId!);
            return new AttendanceRecordDto
            {
                LessonId = att.LessonId,
                StudentId = att.StudentId,
                StudentName = studentUser?.UserName ?? "Student",
                Status = att.Status.ToString(),
                Notes = att.Notes,
                CreatedAt = att.CreatedAt,
                UpdatedAt = att.UpdatedAt
            };
        }

        public async Task<List<AttendanceRecordDto>> BulkMarkForLessonAsync(
            string tutorUserId, string lessonId,
            Dictionary<string, string> studentStatusMap, string? notes = null)
        {
            // Load lesson & quyền
            var lesson = await _ctx.Lessons
                .Include(l => l.Class)
                .Include(l => l.ScheduleEntries)
                .FirstOrDefaultAsync(l => l.Id == lessonId)
                ?? throw new KeyNotFoundException("không tìm thấy buổi học");

            var owningTutorId = lesson.Class?.TutorId
                                ?? lesson.ScheduleEntries.FirstOrDefault()?.TutorId;
            if (string.IsNullOrEmpty(owningTutorId) || owningTutorId != tutorUserId)
                throw new UnauthorizedAccessException("bạn không có quyền điểm danh buổi này");

            // DS học sinh thuộc lớp (nếu là lớp)
            HashSet<string>? classStudentIds = null;
            if (lesson.ClassId != null)
            {
                classStudentIds = (await _ctx.ClassAssigns
                    .Where(x => x.ClassId == lesson.ClassId)
                    .Select(x => x.StudentId!)
                    .ToListAsync())
                    .ToHashSet();
            }

            var result = new List<AttendanceRecordDto>();

            foreach (var kv in studentStatusMap)
            {
                var stuId = kv.Key;
                var statusText = kv.Value;

                if (lesson.ClassId != null && (classStudentIds == null || !classStudentIds.Contains(stuId)))
                    continue; // bỏ qua HS không thuộc lớp

                if (!Enum.TryParse<AttendanceStatus>(statusText, true, out var status))
                    continue;

                var att = await _uow.Attendances.FindAsync(lessonId, stuId);
                if (att == null)
                {
                    att = new Attendance
                    {
                        LessonId = lessonId,
                        StudentId = stuId,
                        Status = status,
                        Notes = notes,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    await _uow.Attendances.CreateAsync(att);
                }
                else
                {
                    att.Status = status;
                    att.Notes = notes;
                    att.UpdatedAt = DateTime.Now;
                    await _uow.Attendances.UpdateAsync(att);
                }
                result.Add(new AttendanceRecordDto
                {
                    LessonId = lessonId,
                    StudentId = stuId,
                    StudentName = "", // sẽ fill sau
                    Status = status.ToString(),
                    Notes = notes ?? att.Notes,
                    CreatedAt = att.CreatedAt,
                    UpdatedAt = att.UpdatedAt
                });
            }

            await _uow.SaveChangesAsync();

            // Fill tên HS
            if (result.Count > 0)
            {
                var stuIds = result.Select(r => r.StudentId).Distinct().ToList();
                var users = await _ctx.StudentProfiles
                    .Where(s => stuIds.Contains(s.Id))
                    .Include(s => s.User)
                    .ToDictionaryAsync(s => s.Id, s => s.User?.UserName ?? "Student");

                foreach (var r in result)
                {
                    if (users.TryGetValue(r.StudentId, out var name))
                        r.StudentName = name;
                }
            }

            return result;
        }

        public async Task<IEnumerable<AttendanceRecordDto>> GetLessonAttendanceAsync(string tutorUserId, string lessonId)
        {
            // quyền
            var lesson = await _ctx.Lessons
                .Include(l => l.Class)
                .Include(l => l.ScheduleEntries)
                .FirstOrDefaultAsync(l => l.Id == lessonId)
                ?? throw new KeyNotFoundException("không tìm thấy buổi học");

            var owningTutorId = lesson.Class?.TutorId
                                ?? lesson.ScheduleEntries.FirstOrDefault()?.TutorId;
            if (owningTutorId != tutorUserId)
                throw new UnauthorizedAccessException("không có quyền");

            var atts = await _uow.Attendances.GetByLessonAsync(lessonId);

            var rs = new List<AttendanceRecordDto>();
            foreach (var a in atts)
            {
                var name = a.Student?.User?.UserName ?? "Student";
                rs.Add(new AttendanceRecordDto
                {
                    LessonId = a.LessonId,
                    StudentId = a.StudentId,
                    StudentName = name,
                    Status = a.Status.ToString(),
                    Notes = a.Notes,
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt
                });
            }
            return rs;
        }

        public async Task<IEnumerable<ScheduleCellAttendanceDto>> GetTutorAttendanceOnCalendarAsync(
            string tutorUserId, DateTime start, DateTime end)
        {
            var startUtc = start.Date.ToUniversalTime();
            var endUtc = end.Date.AddDays(1).ToUniversalTime();

            // Lấy schedule entries thuộc tutor trong range
            var entries = await _ctx.ScheduleEntries
                .Where(se => se.TutorId == tutorUserId
                          && se.EntryType == EntryType.LESSON
                          && se.DeletedAt == null
                          && se.StartTime < endUtc
                          && se.EndTime > startUtc)
                .Include(se => se.Lesson)
                .OrderBy(se => se.StartTime)
                .ToListAsync();

            var lessonIds = entries.Where(e => e.LessonId != null).Select(e => e.LessonId!).Distinct().ToList();

            var atts = await _ctx.Attendances
                .Where(a => lessonIds.Contains(a.LessonId))
                .ToListAsync();

            // Với lớp: nếu muốn biết tổng sĩ số chuẩn, load ClassAssign; ở đây dùng số bản ghi attendance hiện có
            var list = new List<ScheduleCellAttendanceDto>();
            foreach (var e in entries)
            {
                if (e.LessonId == null) continue;

                var group = atts.Where(a => a.LessonId == e.LessonId);
                var dto = new ScheduleCellAttendanceDto
                {
                    ScheduleEntryId = e.Id,
                    LessonId = e.LessonId,
                    StartTime = e.StartTime,
                    EndTime = e.EndTime,
                    TotalStudents = group.Count(),
                    Present = group.Count(x => x.Status == AttendanceStatus.Present),
                    Late = group.Count(x => x.Status == AttendanceStatus.Late),
                    Absent = group.Count(x => x.Status == AttendanceStatus.Absent),
                    Excused = group.Count(x => x.Status == AttendanceStatus.Excused)
                };
                list.Add(dto);
            }
            return list;
        }

        public async Task<IEnumerable<ScheduleCellAttendanceDto>> GetStudentAttendanceOnCalendarAsync(
            string studentUserId, DateTime start, DateTime end)
        {
            var startUtc = start.Date.ToUniversalTime();
            var endUtc = end.Date.AddDays(1).ToUniversalTime();

            // Tìm student profile từ userId
            var stu = await _uow.StudentProfiles.GetAsync(s => s.UserId == studentUserId)
                      ?? throw new KeyNotFoundException("không tìm thấy student profile");

            // Entry thuộc lesson có attendance của HS này
            var entries = await _ctx.ScheduleEntries
                .Where(se => se.EntryType == EntryType.LESSON
                          && se.DeletedAt == null
                          && se.StartTime < endUtc
                          && se.EndTime > startUtc)
                .Include(se => se.Lesson)
                .ToListAsync();

            var lessonIds = entries.Where(e => e.LessonId != null)
                                   .Select(e => e.LessonId!)
                                   .Distinct().ToList();

            var atts = await _ctx.Attendances
                .Where(a => a.StudentId == stu.Id && lessonIds.Contains(a.LessonId))
                .ToListAsync();

            // Overlay mỗi entry của HS (1-1 sẽ có 1 record/lesson)
            var list = new List<ScheduleCellAttendanceDto>();
            foreach (var e in entries)
            {
                if (e.LessonId == null) continue;
                var a = atts.FirstOrDefault(x => x.LessonId == e.LessonId);
                if (a == null) continue; // chưa có bản ghi -> ko hiện hoặc bạn có thể show Total=0

                list.Add(new ScheduleCellAttendanceDto
                {
                    ScheduleEntryId = e.Id,
                    LessonId = e.LessonId,
                    StartTime = e.StartTime,
                    EndTime = e.EndTime,
                    TotalStudents = 1,
                    Present = a.Status == AttendanceStatus.Present ? 1 : 0,
                    Late = a.Status == AttendanceStatus.Late ? 1 : 0,
                    Absent = a.Status == AttendanceStatus.Absent ? 1 : 0,
                    Excused = a.Status == AttendanceStatus.Excused ? 1 : 0
                });
            }
            return list.OrderBy(x => x.StartTime);
        }

        public async Task<IEnumerable<ScheduleCellAttendanceDto>> GetParentChildAttendanceCalendarAsync(
            string parentUserId, string studentId, DateTime start, DateTime end)
        {
            // Xác minh parent có link với studentId
            var link = await _uow.ParentProfiles.GetLinkAsync(parentUserId, studentId);
            if (link == null) throw new UnauthorizedAccessException("không có quyền xem học sinh này");

            var stu = await _uow.StudentProfiles.GetByIdAsync(studentId)
                      ?? throw new KeyNotFoundException("không tìm thấy học sinh");

            var startUtc = start.Date.ToUniversalTime();
            var endUtc = end.Date.AddDays(1).ToUniversalTime();

            var entries = await _ctx.ScheduleEntries
                .Where(se => se.EntryType == EntryType.LESSON
                          && se.DeletedAt == null
                          && se.StartTime < endUtc
                          && se.EndTime > startUtc)
                .Include(se => se.Lesson)
                .ToListAsync();

            var lessonIds = entries.Where(e => e.LessonId != null)
                                   .Select(e => e.LessonId!)
                                   .Distinct().ToList();

            var atts = await _ctx.Attendances
                .Where(a => a.StudentId == stu.Id && lessonIds.Contains(a.LessonId))
                .ToListAsync();

            var list = new List<ScheduleCellAttendanceDto>();
            foreach (var e in entries)
            {
                if (e.LessonId == null) continue;
                var a = atts.FirstOrDefault(x => x.LessonId == e.LessonId);
                if (a == null) continue;

                list.Add(new ScheduleCellAttendanceDto
                {
                    ScheduleEntryId = e.Id,
                    LessonId = e.LessonId,
                    StartTime = e.StartTime,
                    EndTime = e.EndTime,
                    TotalStudents = 1,
                    Present = a.Status == AttendanceStatus.Present ? 1 : 0,
                    Late = a.Status == AttendanceStatus.Late ? 1 : 0,
                    Absent = a.Status == AttendanceStatus.Absent ? 1 : 0,
                    Excused = a.Status == AttendanceStatus.Excused ? 1 : 0
                });
            }
            return list.OrderBy(x => x.StartTime);
        }

        public async Task<List<LessonRosterItemDto>> GetLessonRosterForTutorAsync(string tutorUserId, string lessonId)
        {
            var lesson = await _ctx.Lessons
                .Include(l => l.Class)
                .Include(l => l.ScheduleEntries)
                .FirstOrDefaultAsync(l => l.Id == lessonId)
                ?? throw new KeyNotFoundException("không tìm thấy buổi học");

            var owningTutorId = lesson.Class?.TutorId
                                ?? lesson.ScheduleEntries.FirstOrDefault()?.TutorId;
            if (string.IsNullOrEmpty(owningTutorId) || owningTutorId != tutorUserId)
                throw new UnauthorizedAccessException("bạn không có quyền xem roster buổi này");

            if (lesson.ClassId == null)
                throw new InvalidOperationException("buổi 1-1 không có roster — điểm danh trực tiếp.");

            // DS học sinh đã đăng ký lớp
            var assigns = await _ctx.ClassAssigns
                .Where(x => x.ClassId == lesson.ClassId &&
                            x.ApprovalStatus == ApprovalStatus.Approved)
                .Include(x => x.Student)            // StudentProfile
                .ThenInclude(s => s!.User)          // User
                .ToListAsync();

            var studentIds = assigns.Where(x => x.StudentId != null)
                                    .Select(x => x.StudentId!)
                                    .ToList();

            // Attendance hiện có cho lesson này (để biết status hiện tại)
            var atts = await _ctx.Attendances
                .Where(a => a.LessonId == lessonId && studentIds.Contains(a.StudentId))
                .ToDictionaryAsync(a => a.StudentId, a => a.Status.ToString());

            var roster = assigns.Select(a => new LessonRosterItemDto
            {
                StudentId = a.StudentId!,
                StudentUserId = a.Student!.UserId!,
                StudentName = a.Student!.User?.UserName ?? "Student",
                Email = a.Student!.User?.Email ?? "",
                AvatarUrl = a.Student!.User?.AvatarUrl,
                CurrentStatus = atts.TryGetValue(a.StudentId!, out var st) ? st : null
            }).OrderBy(x => x.StudentName).ToList();

            return roster;
        }
    }
}
