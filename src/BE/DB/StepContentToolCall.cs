using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("StepContentToolCall")]
public partial class StepContentToolCall
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
    [InverseProperty("StepContentToolCall")]
    public virtual StepContent IdNavigation { get; set; } = null!;
}
