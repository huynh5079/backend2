namespace BusinessLayer.Service.Interface
{
    /// <summary>
    /// Service để kiểm tra và cập nhật trạng thái lớp học tự động
    /// </summary>
    public interface IClassStatusCheckService
    {
        /// <summary>
        /// Kiểm tra các lớp có StartDate <= now và đủ điều kiện để chuyển sang Ongoing
        /// </summary>
        Task<int> CheckAndUpdateClassStatusAsync(CancellationToken ct = default);
    }
}

