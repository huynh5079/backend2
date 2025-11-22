using System;
using System.Collections.Generic;

namespace DataLayer.Entities;

public partial class Message : BaseEntity
{
    // public string MessageId { get; set; }

    public string? SenderId { get; set; }

    public string? ReceiverId { get; set; }

    public string? Content { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? Status { get; set; }

    public virtual User? Receiver { get; set; }

    public virtual User? Sender { get; set; }
}
