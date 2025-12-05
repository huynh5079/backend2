using BusinessLayer.Service.Interface;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using Microsoft.Extensions.Logging;

namespace BusinessLayer.Service
{
    /// <summary>
    /// Service để kiểm tra và cập nhật trạng thái lớp học tự động
    /// </summary>
    public class ClassStatusCheckService : IClassStatusCheckService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<ClassStatusCheckService> _logger;

        public ClassStatusCheckService(
            IUnitOfWork uow,
            ILogger<ClassStatusCheckService> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        /// <summary>
        /// Kiểm tra các lớp có StartDate <= now và đủ điều kiện để chuyển sang Ongoing
        /// </summary>
        public async Task<int> CheckAndUpdateClassStatusAsync(CancellationToken ct = default)
        {
            try
            {
                var now = DateTime.UtcNow;
                
                // Lấy tất cả các lớp có:
                // 1. ClassStartDate <= now (hoặc null)
                // 2. Status = Pending hoặc Active (chưa Ongoing)
                var classesToCheck = await _uow.Classes.GetAllAsync(
                    filter: c => c.DeletedAt == null &&
                                 (c.Status == ClassStatus.Pending || c.Status == ClassStatus.Active) &&
                                 (c.ClassStartDate == null || c.ClassStartDate <= now));

                if (!classesToCheck.Any())
                {
                    _logger.LogInformation("Không có lớp nào cần kiểm tra trạng thái");
                    return 0;
                }

                int updatedCount = 0;

                foreach (var classEntity in classesToCheck)
                {
                    try
                    {
                        // Sử dụng reflection để gọi private method CanClassStartAsync
                        // Hoặc tạo public method trong EscrowService
                        var canStart = await CanClassStartAsync(classEntity, ct);
                        
                        if (canStart && classEntity.Status != ClassStatus.Ongoing)
                        {
                            classEntity.Status = ClassStatus.Ongoing;
                            await _uow.Classes.UpdateAsync(classEntity);
                            updatedCount++;
                            
                            _logger.LogInformation(
                                "Đã chuyển lớp {ClassId} ({Title}) sang trạng thái Ongoing",
                                classEntity.Id, classEntity.Title);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Lỗi khi kiểm tra lớp {ClassId}: {Message}",
                            classEntity.Id, ex.Message);
                    }
                }

                if (updatedCount > 0)
                {
                    await _uow.SaveChangesAsync();
                    _logger.LogInformation("Đã cập nhật {Count} lớp sang trạng thái Ongoing", updatedCount);
                }

                return updatedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi chạy background job kiểm tra trạng thái lớp học");
                throw;
            }
        }

        /// <summary>
        /// Kiểm tra xem lớp có đủ điều kiện để chuyển sang trạng thái "Ongoing" không
        /// Điều kiện:
        /// 1. Đến ngày bắt đầu học (ClassStartDate <= DateTime.Now hoặc null)
        /// 2. Có đủ số học sinh đã đóng tiền (ít nhất 1 học sinh có PaymentStatus = Paid)
        /// 3. Gia sư đã đặt cọc (có TutorDepositEscrow với Status = Held)
        /// </summary>
        private async Task<bool> CanClassStartAsync(Class classEntity, CancellationToken ct = default)
        {
            // 1. Kiểm tra ngày bắt đầu học (đã được filter ở trên)
            // Không cần check lại vì đã filter trong query

            // 2. Kiểm tra có học sinh đã đóng tiền chưa
            var paidStudents = await _uow.ClassAssigns.GetAllAsync(
                filter: ca => ca.ClassId == classEntity.Id && ca.PaymentStatus == PaymentStatus.Paid);
            
            if (!paidStudents.Any())
            {
                return false; // Chưa có học sinh nào đóng tiền
            }

            // 3. Kiểm tra gia sư đã đặt cọc chưa
            var deposit = await _uow.TutorDepositEscrows.GetByClassIdAsync(classEntity.Id, ct);
            if (deposit == null || deposit.Status != TutorDepositStatus.Held)
            {
                return false; // Gia sư chưa đặt cọc
            }

            return true; // Đủ tất cả điều kiện
        }
    }
}

