using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Enum
{
    public enum Gender
    {
        Male,
        Female
    }

    public enum UploadContext
    {
        Avatar,
        Certificate,        // Chứng chỉ gia sư
        IdentityDocument,   // CCCD/CMND
        Material,           // Tài liệu học tập
        LessonVideo         // Video bài học
    }

    public enum AccountStatus
    {
        Active,             // Đang hoạt động
        PendingApproval,    // Chờ duyệt (Tutor sau đăng ký)
        Rejected,           // Bị từ chối (Tutor)
        Banned              // Bị khóa (ban)
    }

    public enum ReviewStatus
    {
        Pending,            // mặc định sau khi đăng ký (đang chờ duyệt)
        NeedsProvideInfor,  // yêu cầu bổ sung hồ sơ
        Approved,           // đã duyệt
        Rejected            // đã từ chối
    }

    public enum OtpPurpose
    {
        Register,
        ResetPassword
    }

    public enum PaymentStatus
    {
        Pending,   // Đơn thanh toán vừa tạo, chờ IPN từ MoMo
        Paid,      // MoMo xác nhận đã thanh toán thành công
        Failed,    // MoMo báo lỗi hoặc tạo đơn thất bại
        Expired,   // Người dùng không thanh toán kịp, MoMo hết hạn
        Refunded   // Giao dịch đã được hoàn tiền thành công
    }

    public enum ApprovalStatus
    {
        Pending,
        Approved
    }

    public enum AttendanceStatus
    {
        Present,       // Có mặt
        Late,          // Đi trễ
        Absent,        // Vắng
        Excused        // Vắng có phép
    }

    public enum TransactionStatus
    {
        Pending,    // đang chờ xử lý (ít dùng trong flow hiện tại)
        Succeeded,  // giao dịch thành công và đã ghi sổ
        Failed      // giao dịch thất bại (không ghi sổ số dư)
    }

    public enum TransactionType
    {
        Credit,       // nạp tiền vào ví (tăng số dư)
        Debit,        // rút tiền khỏi ví (giảm số dư)
        TransferIn,   // nhận chuyển tiền từ ví khác
        TransferOut,  // chuyển tiền sang ví khác
        // Escrow flow
        PayEscrow,    // payer trả vào escrow (ghi âm ở ví payer)
        EscrowIn,     // ví admin nhận tiền escrow (ghi dương)
        PayoutOut,    // ví admin chi trả cho tutor phần Net (ghi âm)
        PayoutIn,     // ví tutor nhận thanh toán Net (ghi dương)
        RefundOut,    // ví admin hoàn tiền cho payer (ghi âm)
        RefundIn,     // ví payer nhận tiền hoàn (ghi dương)
        Commission    // ghi nhận hoa hồng (doanh thu) không thay đổi số dư
    }

    public enum EscrowStatus
    {
        Held,      // đã thu Gross, đang giữ (chưa chi trả tutor)
        Released,  // đã giải ngân cho tutor (đã ghi nhận hoa hồng)
        Refunded   // đã hoàn trả lại payer (chỉ khi còn Held)
    }

    public enum PaymentProvider
    {
        MoMo // Cổng thanh toán MoMo (sandbox/production)
    }

    public enum PaymentContextType
    {
        Escrow,         // Thanh toán cho đơn ký quỹ lớp học
        WalletDeposit   // Nạp tiền trực tiếp vào ví người dùng
    }

    // Dùng cho cả Class và ClassRequest
    public enum ClassMode
    {
        Online,
        Offline
    }

    // Trạng thái cho ClassRequest
    public enum ClassRequestStatus
    {
        Pending,    // Mới tạo, học sinh có thể sửa/hủy
        Active,     // Đã duyệt, hiển thị cho gia sư
        Cancelled,  // Học sinh tự hủy
        Expired,    // Hết hạn (do background job)
        Matched,    // Đã khớp gia sư (nhưng chờ thanh toán)
        Ongoing,    // ĐÃ THANH TOÁN, ĐANG HỌC
        Completed,  // Đã hoàn thành
        Rejected    // Bị từ chối (ví dụ: gia sư từ chối direct request)
    }

    // Trạng thái cho TutorApplication
    public enum ApplicationStatus
    {
        Pending,    // Gia sư mới ứng tuyển
        Accepted,   // Học sinh đã chấp nhận (-> trigger thanh toán)
        Rejected    // Học sinh đã từ chối
    }

    // Dùng cho Bảng Lesson
    public enum LessonStatus
    {
        SCHEDULED,  // Đã lên lịch (mặc định)
        COMPLETED,  // Đã hoàn thành
        CANCELLED,  // Đã bị hủy
        STUDENT_ABSENT, // Học sinh vắng
        TUTOR_ABSENT    // Gia sư vắng
    }

    // Trạng thái cho Class (Lớp học)
    public enum ClassStatus
    {
        Pending,    // Chờ học sinh ghi danh (do Gia sư tạo)
        Active,     // Lớp đang hoạt động (nhưng chưa đủ học sinh)
        Ongoing,    // Lớp đang học (được tạo từ Request, hoặc đã đủ học sinh)
        Completed,  // Lớp đã kết thúc
        Cancelled   // Lớp đã bị hủy
    }
    //Schedule Entry Type  
    public enum EntryType
    {
        LESSON,
        BLOCK
    }

    // Notification Type
    public enum NotificationType
    {
        // Auth / User lifecycle
        AccountVerified,    // Tài khoản đã được xác minh
        AccountBlocked,     // Tài khoản bị khóa tạm thời
        TutorApproved,      // Hồ sơ gia sư được duyệt
        TutorRejected,      // Hồ sơ gia sư bị từ chối
        SystemAnnouncement, // Thông báo chung từ hệ thống

        // Wallet & Escrow flow
        WalletDeposit,            // Nạp tiền thành công
        WalletWithdraw,           // Rút tiền thành công
        WalletTransferIn,         // Nhận chuyển tiền
        WalletTransferOut,        // Chuyển tiền đi
        EscrowPaid,               // Đã thanh toán escrow
        EscrowReleased,           // Escrow đã được giải ngân (cho tutor)
        EscrowRefunded,           // Escrow đã được hoàn tiền (cho payer)
        PayoutReceived,           // Nhận thanh toán từ escrow (cho tutor)

        // Reschedule Lessons
        LessonRescheduleRequest,  // Gửi cho Student/Parent khi có yêu cầu mới
        LessonRescheduleAccepted, // Gửi cho Tutor khi được chấp nhận
        LessonRescheduleRejected  // Gửi cho Tutor khi bị từ chối
    }

    // Notification Status
    public enum NotificationStatus
    {
        Unread,     // Chưa đọc
        Read        // Đã đọc
    }

    public enum ReportStatus
    {
        Pending,    // Vừa nhận, chờ xử lý
        InReview,   // Đang xem xét (Tutor/Admin)
        Resolved,   // Đã xử lý (ẩn/xoá tài liệu, nhắc nhở…)
        Rejected,   // Từ chối report (không hợp lệ)
        Escalated   // Chuyển cấp cao hơn/Admin
    }

    public enum RescheduleStatus
    {
        Pending,
        Accepted,
        Rejected,
        Cancelled // (Tùy chọn: nếu Tutor muốn hủy yêu cầu)
    }
}
