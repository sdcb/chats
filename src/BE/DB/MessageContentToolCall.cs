using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("MessageContentToolCall")]
public partial class MessageContentToolCall
{
    [Key]
    public long Id { get; set; }

    [StringLength(100)]
    [Unicode(false)]
    public string? ToolCallId { get; set; }

    [StringLength(200)]
    public string Name { get; set; } = null!;

    public string Parameters { get; set; } = null!;

    [ForeignKey("Id")]
    [InverseProperty("MessageContentToolCall")]
    public virtual MessageContent IdNavigation { get; set; } = null!;
}
