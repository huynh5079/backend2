using DataLayer.Enum;
using System;
using System.Collections.Generic;

namespace DataLayer.Entities;

public partial class Message : BaseEntity
{
    public string? SenderId { get; set; }

    // Cho chat 1-1: ReceiverId
    // Cho chat theo lớp: null (dùng ConversationId)
    public string? ReceiverId { get; set; }

    // ConversationId: null cho chat 1-1 cũ, có giá trị cho conversation mới
    public string? ConversationId { get; set; }

    public string? Content { get; set; }

    public MessageType MessageType { get; set; } = MessageType.Text;

    // Cho file/hình ảnh
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public string? MediaType { get; set; }
    public long? FileSize { get; set; }

    // Note: CreatedAt, UpdatedAt, DeletedAt đã có trong BaseEntity
    // Các properties này chỉ override nếu cần nullable

    public string? Status { get; set; } // Sent, Read, Deleted

    // Cho edit message
    public bool IsEdited { get; set; } = false;

    // Navigation properties
    public virtual User? Receiver { get; set; }
    public virtual User? Sender { get; set; }
    public virtual Conversation? Conversation { get; set; }
}
