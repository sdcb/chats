using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("MessageContentToolCallResponse")]
public partial class MessageContentToolCallResponse
{
    [Key]
    public long Id { get; set; }

    [StringLength(100)]
    [Unicode(false)]
    public string? ToolCallId { get; set; }

    public string Response { get; set; } = null!;

    [ForeignKey("Id")]
    [InverseProperty("MessageContentToolCallResponse")]
    public virtual MessageContent IdNavigation { get; set; } = null!;
}
