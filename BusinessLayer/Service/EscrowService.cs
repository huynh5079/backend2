using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BusinessLayer.DTOs.Wallet;
using BusinessLayer.Options;
using BusinessLayer.Service.Interface;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using Microsoft.Extensions.Options;

namespace BusinessLayer.Service
{
    public class EscrowService : IEscrowService
    {
        private readonly IUnitOfWork _uow;
        private readonly SystemWalletOptions _systemWalletOptions;
        private readonly INotificationService _notificationService;
        private readonly ICommissionService _commissionService;

        public EscrowService(
            IUnitOfWork uow,
            IOptions<SystemWalletOptions> systemWalletOptions,
            INotificationService notificationService,
            ICommissionService commissionService)
        {
            _uow = uow;
            _systemWalletOptions = systemWalletOptions.Value;
            _notificationService = notificationService;
            _commissionService = commissionService;
        }

        /// <summary>
        /// Tính tiền cọc dựa trên học phí: % học phí (không có min/max)
        /// </summary>
        private async Task<decimal> CalculateDepositAmountAsync(decimal classPrice, CancellationToken ct = default)
        {
            var settings = await _uow.SystemSettings.GetOrCreateSettingsAsync(ct);

            // Tính % học phí (ví dụ: 10% = 0.10)
            var depositAmount = Math.Round(classPrice * settings.DepositRate, 2, MidpointRounding.AwayFromZero);

            return depositAmount;
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
            // 1. Kiểm tra ngày bắt đầu học
            if (classEntity.ClassStartDate.HasValue && classEntity.ClassStartDate.Value > DateTime.UtcNow)
            {
                return false; // Chưa đến ngày bắt đầu
            }

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

        private async Task<Wallet> GetOrCreateWalletAsync(string userId, CancellationToken ct)
        {
            var w = await _uow.Wallets.GetByUserIdAsync(userId, ct);
            if (w != null) return w;

            w = new Wallet { UserId = userId, Balance = 0m, Currency = "VND", IsFrozen = false };
            await _uow.Wallets.AddAsync(w, ct);
            await _uow.SaveChangesAsync();
            return w;
        }

        public async Task<OperationResult> PayEscrowAsync(string actorUserId, PayEscrowRequest req, CancellationToken ct = default)
        {
            using var tx = await _uow.BeginTransactionAsync();

            // 1) Lấy Class entity từ DB - KHÔNG TIN client
            var classEntity = await _uow.Classes.GetByIdAsync(req.ClassId);
            if (classEntity == null)
            {
                return new OperationResult { Status = "Fail", Message = "Không tìm thấy lớp học." };
            }

            // 2) Lấy ClassAssign - Mỗi học sinh = 1 Escrow riêng
            var payerUserId = string.IsNullOrWhiteSpace(req.PayerStudentUserId) ? actorUserId : req.PayerStudentUserId;
            string? studentProfileId = null;

            // Lấy studentProfileId từ payerUserId (có thể là student hoặc parent)
            if (!string.IsNullOrWhiteSpace(req.PayerStudentUserId))
            {
                var studentProfile = await _uow.StudentProfiles.GetByUserIdAsync(req.PayerStudentUserId);
                studentProfileId = studentProfile?.Id;
            }
            else
            {
                var studentProfile = await _uow.StudentProfiles.GetByUserIdAsync(payerUserId);
                studentProfileId = studentProfile?.Id;
            }

            if (string.IsNullOrWhiteSpace(studentProfileId))
            {
                return new OperationResult { Status = "Fail", Message = "Không tìm thấy hồ sơ học sinh." };
            }

            var classAssign = await _uow.ClassAssigns.GetByClassAndStudentAsync(req.ClassId, studentProfileId);
            if (classAssign == null)
            {
                return new OperationResult { Status = "Fail", Message = "Học sinh chưa ghi danh vào lớp học này." };
            }

            // Kiểm tra đã thanh toán chưa
            if (classAssign.PaymentStatus == PaymentStatus.Paid)
            {
                return new OperationResult { Status = "Fail", Message = "Học sinh đã thanh toán cho lớp học này." };
            }

            // Kiểm tra đã có Escrow chưa (tránh duplicate)
            var existingEscrows = await _uow.Escrows.GetAllAsync(
                filter: e => e.ClassAssignId == classAssign.Id && e.Status == EscrowStatus.Held);
            if (existingEscrows.Any())
            {
                return new OperationResult { Status = "Fail", Message = "Đã có escrow cho học sinh này." };
            }

            // 3) Tính GrossAmount = phần học phí của học sinh này
            // Với lớp group: Class.Price / số học sinh
            // Với lớp 1-1: Class.Price
            if (classEntity.Price == null || classEntity.Price <= 0)
            {
                return new OperationResult { Status = "Fail", Message = "Lớp học chưa có giá hoặc giá không hợp lệ." };
            }

            decimal grossAmount;
            bool isOneToOne = classEntity.StudentLimit == 1;

            if (isOneToOne)
            {
                // Lớp 1-1: học sinh trả toàn bộ
                grossAmount = classEntity.Price.Value;
            }
            else
            {
                // Lớp group: chia đều cho số học sinh tối đa (StudentLimit)
                // Mỗi học sinh trả: Class.Price / StudentLimit
                if (classEntity.StudentLimit <= 0)
                {
                    return new OperationResult { Status = "Fail", Message = "Lớp học chưa có số lượng học sinh tối đa." };
                }

                // Chia đều: Class.Price / StudentLimit
                grossAmount = Math.Round(classEntity.Price.Value / classEntity.StudentLimit, 2, MidpointRounding.AwayFromZero);
            }

            // 4) Tính commission rate tự động từ DB - KHÔNG TIN commissionRate từ client
            decimal commissionRate = await _commissionService.CalculateCommissionRateAsync(classEntity, null, ct);

            // 5) Lấy TutorUserId từ Class (required)
            if (string.IsNullOrWhiteSpace(classEntity.TutorId))
            {
                return new OperationResult { Status = "Fail", Message = "Lớp học chưa có gia sư." };
            }

            var tutorProfile = await _uow.TutorProfiles.GetByIdAsync(classEntity.TutorId);
            if (tutorProfile == null || string.IsNullOrWhiteSpace(tutorProfile.UserId))
            {
                return new OperationResult { Status = "Fail", Message = "Không tìm thấy thông tin gia sư." };
            }
            string tutorUserId = tutorProfile.UserId;

            // 6) Lấy ví payer và ví admin (system) – đảm bảo tồn tại
            var payerWallet = await GetOrCreateWalletAsync(payerUserId, ct);
            var adminWallet = await GetOrCreateWalletAsync(_systemWalletOptions.SystemWalletUserId, ct);

            if (payerWallet.IsFrozen) return new OperationResult { Status = "Fail", Message = "Ví bị khóa" };
            if (payerWallet.Balance < grossAmount) return new OperationResult { Status = "Fail", Message = $"Số dư không đủ. Cần {grossAmount:N0} VND, hiện có {payerWallet.Balance:N0} VND." };

            payerWallet.Balance -= grossAmount;
            adminWallet.Balance += grossAmount;

            await _uow.Wallets.Update(payerWallet);
            await _uow.Wallets.Update(adminWallet);

            await _uow.Transactions.AddAsync(new Transaction
            {
                WalletId = payerWallet.Id,
                Type = TransactionType.PayEscrow,
                Status = TransactionStatus.Succeeded,
                Amount = grossAmount * -1,
                Note = $"Pay escrow for class {req.ClassId} - student {studentProfileId}"
            }, ct);

            await _uow.Transactions.AddAsync(new Transaction
            {
                WalletId = adminWallet.Id,
                Type = TransactionType.EscrowIn,
                Status = TransactionStatus.Succeeded,
                Amount = grossAmount,
                Note = $"Receive escrow for class {req.ClassId} - student {studentProfileId}",
                CounterpartyUserId = payerUserId
            }, ct);

            // 7) Tạo Escrow per student
            var esc = new Escrow
            {
                ClassId = req.ClassId,
                ClassAssignId = classAssign.Id, // Gắn trực tiếp tới ClassAssign
                StudentUserId = payerUserId, // UserId của học sinh
                TutorUserId = tutorUserId, // Required - gia sư của lớp
                GrossAmount = grossAmount, // Phần học phí của học sinh này
                CommissionRateSnapshot = commissionRate, // Lưu snapshot commission rate
                Status = EscrowStatus.Held,
                ReleasedAmount = 0,
                RefundedAmount = 0
            };
            await _uow.Escrows.AddAsync(esc, ct);
            await _uow.SaveChangesAsync(); // Save escrow để có esc.Id

            // 8) Cập nhật ClassAssign.PaymentStatus = Paid
            classAssign.PaymentStatus = PaymentStatus.Paid;
            await _uow.ClassAssigns.UpdateAsync(classAssign);

            // Tạo notification sau khi save
            var notification = await _notificationService.CreateEscrowNotificationAsync(
                payerUserId,
                NotificationType.EscrowPaid,
                grossAmount,
                req.ClassId,
                esc.Id,
                ct);

            await _uow.SaveChangesAsync(); // Save notification và ClassAssign

            // Gửi real-time notification sau khi save
            await _notificationService.SendRealTimeNotificationAsync(payerUserId, notification, ct);

            await tx.CommitAsync();

            // Tính toán các giá trị để trả về
            decimal commissionAmount = Math.Round(grossAmount * commissionRate, 2, MidpointRounding.AwayFromZero);
            decimal netAmount = grossAmount - commissionAmount;

            return new OperationResult
            {
                Status = "Ok",
                Message = "Thanh toán escrow thành công",
                Data = new PayEscrowResponse
                {
                    EscrowId = esc.Id,
                    ClassId = req.ClassId,
                    ClassAssignId = classAssign.Id,
                    StudentUserId = payerUserId,
                    GrossAmount = grossAmount,
                    CommissionRateSnapshot = commissionRate,
                    CommissionAmount = commissionAmount,
                    NetAmount = netAmount,
                    Status = esc.Status.ToString()
                }
            };
        }

        // Helper: suy TutorUserId từ ClassId nếu escrow chưa có
        private async Task<string?> ResolveTutorUserIdFromClassIdAsync(string classId, CancellationToken ct)
        {
            var cls = await _uow.Classes.GetByIdAsync(classId);
            if (cls == null || string.IsNullOrWhiteSpace(cls.TutorId)) return null;

            var tutorProfile = await _uow.TutorProfiles.GetByIdAsync(cls.TutorId);
            return tutorProfile?.UserId;
        }

        public async Task<OperationResult> RefundAsync(string adminUserId, RefundEscrowRequest req, CancellationToken ct = default)
        {
            var esc = await _uow.Escrows.GetByIdAsync(req.EscrowId, ct);
            if (esc == null) return new OperationResult { Status = "Fail", Message = "Escrow không tồn tại" };
            if (esc.Status != EscrowStatus.Held) return new OperationResult { Status = "Fail", Message = "Chỉ refund khi Held" };

            using var tx = await _uow.BeginTransactionAsync();

            // Rút từ ví hệ thống – nơi đang giữ escrow
            var adminWallet = await GetOrCreateWalletAsync(_systemWalletOptions.SystemWalletUserId, ct);
            var payerWallet = await GetOrCreateWalletAsync(esc.StudentUserId, ct);

            if (adminWallet.Balance < esc.GrossAmount) return new OperationResult { Status = "Fail", Message = "Số dư admin không đủ" };

            adminWallet.Balance -= esc.GrossAmount;
            payerWallet.Balance += esc.GrossAmount;

            // Attach wallets để EF track changes
            await _uow.Wallets.Update(adminWallet);
            await _uow.Wallets.Update(payerWallet);

            await _uow.Transactions.AddAsync(new Transaction
            {
                WalletId = adminWallet.Id,
                Type = TransactionType.RefundOut,
                Status = TransactionStatus.Succeeded,
                Amount = -esc.GrossAmount,
                Note = $"Refund escrow {esc.Id}",
                CounterpartyUserId = esc.StudentUserId
            }, ct);

            await _uow.Transactions.AddAsync(new Transaction
            {
                WalletId = payerWallet.Id,
                Type = TransactionType.RefundIn,
                Status = TransactionStatus.Succeeded,
                Amount = esc.GrossAmount,
                Note = $"Refund received for escrow {esc.Id}",
                CounterpartyUserId = adminUserId
            }, ct);

            esc.Status = EscrowStatus.Refunded;
            esc.RefundedAt = DateTime.UtcNow;
            esc.RefundedAmount = esc.GrossAmount; // Track đã refund bao nhiêu
            await _uow.Escrows.UpdateAsync(esc);
            await _uow.SaveChangesAsync(); // Save escrow changes

            // Tạo notification sau khi save
            var notification = await _notificationService.CreateEscrowNotificationAsync(
                esc.StudentUserId,
                NotificationType.EscrowRefunded,
                esc.GrossAmount,
                esc.ClassId,
                esc.Id,
                ct);

            await _uow.SaveChangesAsync(); // Save notification

            // Gửi real-time notification sau khi save
            await _notificationService.SendRealTimeNotificationAsync(esc.StudentUserId, notification, ct);

            await tx.CommitAsync();
            return new OperationResult { Status = "Ok" };
        }

        public async Task<CommissionCalculationDto> CalculateCommissionAsync(string classId, decimal? grossAmount = null, CancellationToken ct = default)
        {
            var classEntity = await _uow.Classes.GetByIdAsync(classId);
            if (classEntity == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy lớp học với ID '{classId}'.");
            }

            // Lấy GrossAmount từ Class.Price trong DB - KHÔNG TIN client
            // Bảo mật: Tránh client hack để giảm học phí
            decimal actualGrossAmount = grossAmount ?? classEntity.Price ?? 0;
            if (actualGrossAmount <= 0)
                throw new InvalidOperationException("Lớp học chưa có giá hoặc giá không hợp lệ.");

            var commissionType = _commissionService.DetermineCommissionType(classEntity, null);
            var commissionRate = await _commissionService.GetCommissionRateAsync(commissionType, ct);
            var commissionAmount = Math.Round(actualGrossAmount * commissionRate, 2, MidpointRounding.AwayFromZero);
            var netAmount = actualGrossAmount - commissionAmount;

            return new CommissionCalculationDto
            {
                ClassId = classId,
                ClassTitle = classEntity.Title ?? string.Empty,
                CommissionType = commissionType,
                CommissionRate = commissionRate,
                GrossAmount = actualGrossAmount, // Lấy từ DB
                CommissionAmount = commissionAmount,
                NetAmount = netAmount
            };
        }

        /// <summary>
        /// Gia sư chấp nhận lớp và đặt cọc
        /// </summary>
        public async Task<OperationResult> ProcessTutorDepositAsync(string tutorUserId, ProcessTutorDepositRequest req, CancellationToken ct = default)
        {
            // 1) Kiểm tra Class và Escrow
            var classEntity = await _uow.Classes.GetByIdAsync(req.ClassId);
            if (classEntity == null)
                return new OperationResult { Status = "Fail", Message = "Không tìm thấy lớp học." };

            // Kiểm tra có escrow nào cho lớp này chưa (có thể có nhiều escrow nếu lớp group)
            var escrows = await _uow.Escrows.GetAllAsync(
                filter: e => e.ClassId == req.ClassId && e.Status == EscrowStatus.Held);
            if (!escrows.Any())
                return new OperationResult { Status = "Fail", Message = "Không tìm thấy escrow cho lớp học này." };

            // Lấy escrow đầu tiên để tính deposit (deposit tính theo tổng học phí của lớp)
            var firstEscrow = escrows.First();

            // Kiểm tra tutor có phải là tutor của lớp không
            var tutorProfile = await _uow.TutorProfiles.GetByIdAsync(classEntity.TutorId ?? "");
            if (tutorProfile == null || tutorProfile.UserId != tutorUserId)
                return new OperationResult { Status = "Fail", Message = "Bạn không phải gia sư của lớp học này." };

            // Kiểm tra đã có deposit chưa
            var existingDeposit = await _uow.TutorDepositEscrows.GetByClassIdAsync(req.ClassId, ct);
            if (existingDeposit != null && existingDeposit.Status == TutorDepositStatus.Held)
                return new OperationResult { Status = "Fail", Message = "Đã đặt cọc cho lớp học này rồi." };

            using var tx = await _uow.BeginTransactionAsync();

            // 2) Tính số tiền cọc = % tổng học phí của lớp
            // Với lớp group: tính deposit dựa trên tổng học phí (Class.Price), không phải từng escrow
            if (classEntity.Price == null || classEntity.Price <= 0)
                return new OperationResult { Status = "Fail", Message = "Lớp học chưa có giá." };

            var settings = await _uow.SystemSettings.GetOrCreateSettingsAsync(ct);
            decimal depositRate = settings.DepositRate; // Lưu snapshot này
            // Deposit tính theo tổng học phí của lớp (Class.Price), không phải từng escrow
            decimal depositAmount = await CalculateDepositAmountAsync(classEntity.Price.Value, ct);

            // 3) Kiểm tra số dư ví tutor
            var tutorWallet = await GetOrCreateWalletAsync(tutorUserId, ct);
            if (tutorWallet.IsFrozen)
                return new OperationResult { Status = "Fail", Message = "Ví bị khóa" };
            if (tutorWallet.Balance < depositAmount)
                return new OperationResult { Status = "Fail", Message = $"Số dư không đủ để đặt cọc. Cần {depositAmount:N0} VND, hiện có {tutorWallet.Balance:N0} VND." };

            var adminWallet = await GetOrCreateWalletAsync(_systemWalletOptions.SystemWalletUserId, ct);

            // 4) Trừ tiền từ ví tutor, cộng vào ví admin
            tutorWallet.Balance -= depositAmount;
            adminWallet.Balance += depositAmount;

            await _uow.Wallets.Update(tutorWallet);
            await _uow.Wallets.Update(adminWallet);

            // 5) Ghi transaction
            await _uow.Transactions.AddAsync(new Transaction
            {
                WalletId = tutorWallet.Id,
                Type = TransactionType.DepositOut,
                Status = TransactionStatus.Succeeded,
                Amount = depositAmount * -1,
                Note = $"Đặt cọc cho lớp {req.ClassId}"
            }, ct);

            await _uow.Transactions.AddAsync(new Transaction
            {
                WalletId = adminWallet.Id,
                Type = TransactionType.DepositIn,
                Status = TransactionStatus.Succeeded,
                Amount = depositAmount,
                Note = $"Nhận tiền cọc từ tutor cho lớp {req.ClassId}",
                CounterpartyUserId = tutorUserId
            }, ct);

            // 6) Tạo TutorDepositEscrow - Lưu DepositRateSnapshot để không bị ảnh hưởng khi admin đổi rate sau này
            // Deposit gắn với Class, không phải từng Escrow (với lớp group có nhiều escrow)
            var tutorDeposit = new TutorDepositEscrow
            {
                ClassId = req.ClassId,
                EscrowId = null, // Nullable - Deposit gắn với Class, không phải từng Escrow
                TutorUserId = tutorUserId,
                DepositAmount = depositAmount,
                DepositRateSnapshot = depositRate, // Lưu snapshot tỷ lệ tại thời điểm tạo
                Status = TutorDepositStatus.Held
            };
            await _uow.TutorDepositEscrows.AddAsync(tutorDeposit, ct);

            // 7) TutorUserId đã có sẵn trong Escrow khi tạo, không cần update

            // 8) Kiểm tra và cập nhật trạng thái lớp sang Ongoing (nếu đủ điều kiện)
            // Điều kiện để chuyển sang Ongoing:
            // 1. Đến ngày bắt đầu học (ClassStartDate <= DateTime.Now)
            // 2. Có đủ số học sinh đã đóng tiền (ít nhất 1, hoặc theo MinStudent nếu có)
            // 3. Gia sư đã đặt cọc (đã tạo TutorDepositEscrow với Status = Held)
            if (await CanClassStartAsync(classEntity, ct))
            {
                classEntity.Status = ClassStatus.Ongoing;
                await _uow.Classes.UpdateAsync(classEntity);
            }

            await _uow.SaveChangesAsync();
            await tx.CommitAsync();

            // 9) Gửi notification cho tất cả học sinh đã thanh toán trong lớp
            var allPaidEscrows = await _uow.Escrows.GetAllAsync(
                filter: e => e.ClassId == req.ClassId && e.Status == EscrowStatus.Held);

            foreach (var paidEscrow in allPaidEscrows)
            {
                var studentNotification = await _notificationService.CreateEscrowNotificationAsync(
                    paidEscrow.StudentUserId,
                    NotificationType.EscrowPaid, // Có thể tạo notification type mới: TutorAccepted
                    depositAmount,
                    req.ClassId,
                    paidEscrow.Id,
                    ct);
                await _uow.SaveChangesAsync();
                await _notificationService.SendRealTimeNotificationAsync(paidEscrow.StudentUserId, studentNotification, ct);
            }

            return new OperationResult { Status = "Ok", Message = $"Đã đặt cọc {depositAmount:N0} VND thành công." };
        }

        /// <summary>
        /// Hoàn thành khóa học: Hoàn cọc + Giải ngân học phí + Commission
        /// </summary>
        public async Task<OperationResult> ReleaseAsync(string adminUserId, ReleaseEscrowRequest req, CancellationToken ct = default)
        {
            var esc = await _uow.Escrows.GetByIdAsync(req.EscrowId, ct);
            if (esc == null) return new OperationResult { Status = "Fail", Message = "Escrow không tồn tại" };
            if (esc.Status != EscrowStatus.Held) return new OperationResult { Status = "Fail", Message = "Escrow không ở trạng thái Held" };

            using var tx = await _uow.BeginTransactionAsync();

            // TutorUserId đã required trong Escrow, không cần check null
            if (string.IsNullOrWhiteSpace(esc.TutorUserId))
                return new OperationResult { Status = "Fail", Message = "Escrow chưa gắn tutor" };

            // Tìm TutorDepositEscrow theo ClassId (vì deposit gắn với Class, không phải từng Escrow)
            var tutorDeposit = await _uow.TutorDepositEscrows.GetByClassIdAsync(esc.ClassId, ct);
            if (tutorDeposit == null || tutorDeposit.Status != TutorDepositStatus.Held)
                return new OperationResult { Status = "Fail", Message = "Không tìm thấy tiền cọc hoặc đã được xử lý." };

            var adminWallet = await GetOrCreateWalletAsync(_systemWalletOptions.SystemWalletUserId, ct);
            var tutorWallet = await GetOrCreateWalletAsync(esc.TutorUserId, ct);

            // Bước 1: Tính commission và net amount (dùng CommissionRateSnapshot)
            var commission = Math.Round(esc.GrossAmount * esc.CommissionRateSnapshot, 2, MidpointRounding.AwayFromZero);
            var net = esc.GrossAmount - commission;

            // Bước 2: Giải ngân học phí (net) cho tutor
            if (adminWallet.Balance < net)
                return new OperationResult { Status = "Fail", Message = "Số dư admin không đủ để giải ngân" };

            adminWallet.Balance -= net;
            tutorWallet.Balance += net;

            await _uow.Transactions.AddAsync(new Transaction
            {
                WalletId = adminWallet.Id,
                Type = TransactionType.PayoutOut,
                Status = TransactionStatus.Succeeded,
                Amount = -net,
                Note = $"Release payout for escrow {esc.Id}",
                CounterpartyUserId = esc.TutorUserId
            }, ct);

            await _uow.Transactions.AddAsync(new Transaction
            {
                WalletId = tutorWallet.Id,
                Type = TransactionType.PayoutIn,
                Status = TransactionStatus.Succeeded,
                Amount = net,
                Note = $"Payout received for escrow {esc.Id}",
                CounterpartyUserId = adminUserId
            }, ct);

            // Bước 3: Hoàn cọc cho tutor (chỉ 1 lần cho cả lớp, không phải từng escrow)
            // Kiểm tra xem đã hoàn cọc chưa bằng cách check status của tutorDeposit
            bool shouldRefundDeposit = tutorDeposit.Status == TutorDepositStatus.Held;
            var depositAmount = tutorDeposit.DepositAmount;

            if (shouldRefundDeposit)
            {
                if (adminWallet.Balance < depositAmount)
                    return new OperationResult { Status = "Fail", Message = "Số dư admin không đủ để hoàn cọc" };

                adminWallet.Balance -= depositAmount;
                tutorWallet.Balance += depositAmount;

                await _uow.Transactions.AddAsync(new Transaction
                {
                    WalletId = adminWallet.Id,
                    Type = TransactionType.DepositRefundOut,
                    Status = TransactionStatus.Succeeded,
                    Amount = -depositAmount,
                    Note = $"Hoàn cọc cho tutor từ lớp {esc.ClassId}",
                    CounterpartyUserId = esc.TutorUserId
                }, ct);

                await _uow.Transactions.AddAsync(new Transaction
                {
                    WalletId = tutorWallet.Id,
                    Type = TransactionType.DepositRefundIn,
                    Status = TransactionStatus.Succeeded,
                    Amount = depositAmount,
                    Note = $"Nhận hoàn cọc từ lớp {esc.ClassId}",
                    CounterpartyUserId = adminUserId
                }, ct);

                // Cập nhật trạng thái deposit (chỉ 1 lần)
                tutorDeposit.Status = TutorDepositStatus.Refunded;
                tutorDeposit.RefundedAt = DateTime.UtcNow;
                await _uow.TutorDepositEscrows.UpdateAsync(tutorDeposit);
            }

            // Bước 4: Ghi sổ commission (không đổi số dư)
            await _uow.Transactions.AddAsync(new Transaction
            {
                WalletId = adminWallet.Id,
                Type = TransactionType.Commission,
                Status = TransactionStatus.Succeeded,
                Amount = commission,
                Note = $"Commission for escrow {esc.Id}",
                CounterpartyUserId = esc.StudentUserId
            }, ct);

            // Cập nhật trạng thái escrow
            esc.Status = EscrowStatus.Released;
            esc.ReleasedAt = DateTime.UtcNow;
            esc.ReleasedAmount = net; // Track đã release bao nhiêu

            await _uow.Wallets.Update(adminWallet);
            await _uow.Wallets.Update(tutorWallet);
            await _uow.Escrows.UpdateAsync(esc);
            await _uow.SaveChangesAsync();

            // Gửi notification cho tutor (chỉ khi hoàn cọc lần đầu)
            if (shouldRefundDeposit)
            {
                var totalReceived = net + depositAmount;
                var notification = await _notificationService.CreateEscrowNotificationAsync(
                    esc.TutorUserId,
                    NotificationType.PayoutReceived,
                    totalReceived,
                    esc.ClassId,
                    esc.Id,
                    ct);
                await _uow.SaveChangesAsync();
                await _notificationService.SendRealTimeNotificationAsync(esc.TutorUserId, notification, ct);
            }
            else
            {
                // Chỉ gửi notification cho payout (không có deposit)
                var notification = await _notificationService.CreateEscrowNotificationAsync(
                    esc.TutorUserId,
                    NotificationType.PayoutReceived,
                    net,
                    esc.ClassId,
                    esc.Id,
                    ct);
                await _uow.SaveChangesAsync();
                await _notificationService.SendRealTimeNotificationAsync(esc.TutorUserId, notification, ct);
            }

            await tx.CommitAsync();

            var message = shouldRefundDeposit
                ? $"Đã giải ngân {net:N0} VND học phí + hoàn {depositAmount:N0} VND cọc. Commission: {commission:N0} VND."
                : $"Đã giải ngân {net:N0} VND học phí. Commission: {commission:N0} VND.";
            return new OperationResult { Status = "Ok", Message = message };
        }

        /// <summary>
        /// Xử lý vi phạm: Tịch thu tiền cọc khi tutor bỏ dở/vi phạm
        /// </summary>
        public async Task<OperationResult> ForfeitDepositAsync(string adminUserId, ForfeitDepositRequest req, CancellationToken ct = default)
        {
            var tutorDeposit = await _uow.TutorDepositEscrows.GetByIdAsync(req.TutorDepositEscrowId, ct);
            if (tutorDeposit == null)
                return new OperationResult { Status = "Fail", Message = "Không tìm thấy tiền cọc." };
            if (tutorDeposit.Status != TutorDepositStatus.Held)
                return new OperationResult { Status = "Fail", Message = "Tiền cọc không ở trạng thái Held." };

            // Lấy Class để lấy thông tin
            var classEntity = await _uow.Classes.GetByIdAsync(tutorDeposit.ClassId);
            if (classEntity == null)
                return new OperationResult { Status = "Fail", Message = "Không tìm thấy lớp học liên quan." };

            using var tx = await _uow.BeginTransactionAsync();

            var adminWallet = await GetOrCreateWalletAsync(_systemWalletOptions.SystemWalletUserId, ct);
            var depositAmount = tutorDeposit.DepositAmount;

            if (req.RefundToStudent)
            {
                // Trả về học sinh - với lớp group, chia đều cho tất cả học sinh đã thanh toán
                var paidEscrows = await _uow.Escrows.GetAllAsync(
                    filter: e => e.ClassId == tutorDeposit.ClassId && e.Status == EscrowStatus.Held);

                if (!paidEscrows.Any())
                {
                    return new OperationResult { Status = "Fail", Message = "Không tìm thấy học sinh đã thanh toán để trả tiền." };
                }

                // Chia đều depositAmount cho tất cả học sinh đã thanh toán
                decimal refundPerStudent = Math.Round(depositAmount / paidEscrows.Count(), 2, MidpointRounding.AwayFromZero);

                if (adminWallet.Balance < depositAmount)
                    return new OperationResult { Status = "Fail", Message = "Số dư admin không đủ" };

                foreach (var esc in paidEscrows)
                {
                    var studentWallet = await GetOrCreateWalletAsync(esc.StudentUserId, ct);

                    adminWallet.Balance -= refundPerStudent;
                    studentWallet.Balance += refundPerStudent;

                    await _uow.Wallets.Update(studentWallet);

                    await _uow.Transactions.AddAsync(new Transaction
                    {
                        WalletId = adminWallet.Id,
                        Type = TransactionType.DepositForfeitOut,
                        Status = TransactionStatus.Succeeded,
                        Amount = -refundPerStudent,
                        Note = $"Tịch thu cọc và trả cho học sinh: {req.Reason}",
                        CounterpartyUserId = esc.StudentUserId
                    }, ct);

                    await _uow.Transactions.AddAsync(new Transaction
                    {
                        WalletId = studentWallet.Id,
                        Type = TransactionType.DepositForfeitIn,
                        Status = TransactionStatus.Succeeded,
                        Amount = refundPerStudent,
                        Note = $"Nhận tiền bồi thường từ cọc gia sư: {req.Reason}",
                        CounterpartyUserId = adminUserId
                    }, ct);
                }

                await _uow.Wallets.Update(adminWallet);
            }
            else
            {
                // Giữ lại cho hệ thống (phí vi phạm)
                // Không cần chuyển tiền, chỉ ghi transaction
                await _uow.Transactions.AddAsync(new Transaction
                {
                    WalletId = adminWallet.Id,
                    Type = TransactionType.Commission, // Hoặc tạo type mới: ViolationFee
                    Status = TransactionStatus.Succeeded,
                    Amount = depositAmount,
                    Note = $"Tịch thu cọc do vi phạm: {req.Reason}",
                    CounterpartyUserId = tutorDeposit.TutorUserId
                }, ct);
            }

            // Cập nhật trạng thái
            tutorDeposit.Status = TutorDepositStatus.Forfeited;
            tutorDeposit.ForfeitedAt = DateTime.UtcNow;
            tutorDeposit.ForfeitReason = req.Reason;

            await _uow.TutorDepositEscrows.UpdateAsync(tutorDeposit);
            await _uow.SaveChangesAsync();
            await tx.CommitAsync();

            // Gửi notification cho tutor khi deposit bị forfeited
            if (!string.IsNullOrEmpty(tutorDeposit.TutorUserId))
            {
                try
                {
                    var notification = await _notificationService.CreateWalletNotificationAsync(
                        tutorDeposit.TutorUserId,
                        NotificationType.TutorDepositForfeited,
                        depositAmount,
                        $"Tiền cọc đã bị tịch thu: {req.Reason}",
                        tutorDeposit.Id,
                        ct);
                    await _uow.SaveChangesAsync();
                    await _notificationService.SendRealTimeNotificationAsync(tutorDeposit.TutorUserId, notification, ct);
                }
                catch (Exception notifEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to send notification: {notifEx.Message}");
                }
            }

            return new OperationResult { Status = "Ok", Message = $"Đã tịch thu tiền cọc {depositAmount:N0} VND." };
        }

        /// <summary>
        /// Partial release escrow - Release một phần cho tutor (khi đã dạy một phần)
        /// </summary>
        public async Task<OperationResult> PartialReleaseAsync(string adminUserId, PartialReleaseEscrowRequest req, CancellationToken ct = default)
        {
            var esc = await _uow.Escrows.GetByIdAsync(req.EscrowId, ct);
            if (esc == null) return new OperationResult { Status = "Fail", Message = "Escrow không tồn tại" };
            if (esc.Status != EscrowStatus.Held && esc.Status != EscrowStatus.PartiallyReleased)
                return new OperationResult { Status = "Fail", Message = "Escrow không ở trạng thái hợp lệ để partial release" };

            if (req.ReleasePercentage <= 0 || req.ReleasePercentage > 1)
                return new OperationResult { Status = "Fail", Message = "ReleasePercentage phải trong khoảng (0, 1]" };

            using var tx = await _uow.BeginTransactionAsync();

            var adminWallet = await GetOrCreateWalletAsync(_systemWalletOptions.SystemWalletUserId, ct);
            var tutorWallet = await GetOrCreateWalletAsync(esc.TutorUserId, ct);

            // Tính số tiền còn lại có thể release
            decimal remainingAmount = esc.GrossAmount - esc.ReleasedAmount;
            decimal releaseAmount = Math.Round(remainingAmount * req.ReleasePercentage, 2, MidpointRounding.AwayFromZero);

            if (releaseAmount <= 0)
                return new OperationResult { Status = "Fail", Message = "Không còn tiền để release" };

            // Tính commission và net
            var commission = Math.Round(releaseAmount * esc.CommissionRateSnapshot, 2, MidpointRounding.AwayFromZero);
            var net = releaseAmount - commission;

            if (adminWallet.Balance < net)
                return new OperationResult { Status = "Fail", Message = "Số dư admin không đủ để giải ngân" };

            adminWallet.Balance -= net;
            tutorWallet.Balance += net;

            await _uow.Transactions.AddAsync(new Transaction
            {
                WalletId = adminWallet.Id,
                Type = TransactionType.PayoutOut,
                Status = TransactionStatus.Succeeded,
                Amount = -net,
                Note = $"Partial release {req.ReleasePercentage:P0} for escrow {esc.Id}",
                CounterpartyUserId = esc.TutorUserId
            }, ct);

            await _uow.Transactions.AddAsync(new Transaction
            {
                WalletId = tutorWallet.Id,
                Type = TransactionType.PayoutIn,
                Status = TransactionStatus.Succeeded,
                Amount = net,
                Note = $"Partial payout received for escrow {esc.Id}",
                CounterpartyUserId = adminUserId
            }, ct);

            // Cập nhật escrow
            esc.ReleasedAmount += releaseAmount;
            if (esc.ReleasedAmount >= esc.GrossAmount)
            {
                esc.Status = EscrowStatus.Released;
            }
            else
            {
                esc.Status = EscrowStatus.PartiallyReleased;
            }
            await _uow.Escrows.UpdateAsync(esc);
            await _uow.SaveChangesAsync();

            // Ghi commission
            await _uow.Transactions.AddAsync(new Transaction
            {
                WalletId = adminWallet.Id,
                Type = TransactionType.Commission,
                Status = TransactionStatus.Succeeded,
                Amount = commission,
                Note = $"Commission from partial release escrow {esc.Id}",
                CounterpartyUserId = esc.TutorUserId
            }, ct);

            await tx.CommitAsync();

            // Notification cho tutor
            var notification = await _notificationService.CreateEscrowNotificationAsync(
                esc.TutorUserId,
                NotificationType.EscrowReleased,
                net,
                esc.ClassId,
                esc.Id,
                ct);
            await _notificationService.SendRealTimeNotificationAsync(esc.TutorUserId, notification, ct);

            return new OperationResult
            {
                Status = "Ok",
                Message = $"Đã giải ngân {releaseAmount:N0} VND ({req.ReleasePercentage:P0}) cho tutor."
            };
        }

        /// <summary>
        /// Partial refund escrow - Refund một phần cho student (phần chưa học)
        /// </summary>
        public async Task<OperationResult> PartialRefundAsync(string adminUserId, PartialRefundEscrowRequest req, CancellationToken ct = default)
        {
            var esc = await _uow.Escrows.GetByIdAsync(req.EscrowId, ct);
            if (esc == null) return new OperationResult { Status = "Fail", Message = "Escrow không tồn tại" };
            if (esc.Status != EscrowStatus.Held && esc.Status != EscrowStatus.PartiallyReleased)
                return new OperationResult { Status = "Fail", Message = "Escrow không ở trạng thái hợp lệ để partial refund" };

            if (req.RefundPercentage <= 0 || req.RefundPercentage > 1)
                return new OperationResult { Status = "Fail", Message = "RefundPercentage phải trong khoảng (0, 1]" };

            using var tx = await _uow.BeginTransactionAsync();

            var adminWallet = await GetOrCreateWalletAsync(_systemWalletOptions.SystemWalletUserId, ct);
            var studentWallet = await GetOrCreateWalletAsync(esc.StudentUserId, ct);

            // Tính số tiền còn lại có thể refund (chưa release)
            decimal remainingAmount = esc.GrossAmount - esc.ReleasedAmount - esc.RefundedAmount;
            decimal refundAmount = Math.Round(remainingAmount * req.RefundPercentage, 2, MidpointRounding.AwayFromZero);

            if (refundAmount <= 0)
                return new OperationResult { Status = "Fail", Message = "Không còn tiền để refund" };

            if (adminWallet.Balance < refundAmount)
                return new OperationResult { Status = "Fail", Message = "Số dư admin không đủ" };

            adminWallet.Balance -= refundAmount;
            studentWallet.Balance += refundAmount;

            await _uow.Transactions.AddAsync(new Transaction
            {
                WalletId = adminWallet.Id,
                Type = TransactionType.RefundOut,
                Status = TransactionStatus.Succeeded,
                Amount = -refundAmount,
                Note = $"Partial refund {req.RefundPercentage:P0} for escrow {esc.Id}",
                CounterpartyUserId = esc.StudentUserId
            }, ct);

            await _uow.Transactions.AddAsync(new Transaction
            {
                WalletId = studentWallet.Id,
                Type = TransactionType.RefundIn,
                Status = TransactionStatus.Succeeded,
                Amount = refundAmount,
                Note = $"Partial refund received for escrow {esc.Id}",
                CounterpartyUserId = adminUserId
            }, ct);

            // Cập nhật escrow
            esc.RefundedAmount += refundAmount;
            if (esc.RefundedAmount + esc.ReleasedAmount >= esc.GrossAmount)
            {
                esc.Status = EscrowStatus.Refunded;
                esc.RefundedAt = DateTime.UtcNow;
            }
            await _uow.Escrows.UpdateAsync(esc);
            await _uow.SaveChangesAsync();

            await tx.CommitAsync();

            // Notification cho student
            var notification = await _notificationService.CreateEscrowNotificationAsync(
                esc.StudentUserId,
                NotificationType.EscrowRefunded,
                refundAmount,
                esc.ClassId,
                esc.Id,
                ct);
            await _notificationService.SendRealTimeNotificationAsync(esc.StudentUserId, notification, ct);

            return new OperationResult
            {
                Status = "Ok",
                Message = $"Đã hoàn {refundAmount:N0} VND ({req.RefundPercentage:P0}) cho học sinh."
            };
        }
    }
}


