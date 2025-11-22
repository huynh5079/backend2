using BusinessLayer.DTOs.Feedback;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface
{
    public interface IFeedbackService
    {
        Task<FeedbackDto> CreateAsync(string actorUserId, CreateFeedbackRequest req);
        Task<FeedbackDto> CreateForTutorProfileAsync(string actorUserId, string tutorUserId, CreateTutorProfileFeedbackRequest req);
        Task<FeedbackDto> UpdateAsync(string actorUserId, string feedbackId, UpdateFeedbackRequest req);
        Task<bool> DeleteAsync(string actorUserId, string feedbackId);

        Task<IEnumerable<FeedbackDto>> GetClassFeedbacksAsync(string classId);
        Task<(IEnumerable<FeedbackDto> items, int total)> GetTutorFeedbacksAsync(string tutorUserId, int page, int pageSize);
        Task<TutorRatingSummaryDto> GetTutorRatingAsync(string tutorUserId);
    }
}
