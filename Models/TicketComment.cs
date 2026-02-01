using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using System.ComponentModel; // nếu cần

namespace DoAnTotNghiep.Models
{
    public class TicketComment
    {
        public int Id { get; set; }

        [Required]
        public string? Content { get; set; }

        public DateTime CreatedAt { get; set; }
        public bool IsInternalNote { get; set; }

        public string? AuthorId { get; set; }
        [ForeignKey("AuthorId")]
        public virtual IdentityUser? Author { get; set; }

        public int TicketId { get; set; }
        [ForeignKey("TicketId")]
        public virtual Ticket? Ticket { get; set; }

        public virtual ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();

       
        public int? ReplyToCommentId { get; set; }

       
        [ForeignKey("ReplyToCommentId")]
        [InverseProperty("Replies")]
        public virtual TicketComment? ReplyToComment { get; set; }

       
        [InverseProperty("ReplyToComment")]
        public virtual ICollection<TicketComment> Replies { get; set; } = new List<TicketComment>();
    }
}
