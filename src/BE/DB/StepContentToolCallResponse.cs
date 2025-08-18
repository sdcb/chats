using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("StepContentToolCallResponse")]
public partial class StepContentToolCallResponse
{
    [Key]
    public long Id { get; set; }

    [StringLength(100)]
    [Unicode(false)]
    public string? ToolCallId { get; set; }

    public string Response { get; set; } = null!;

    [ForeignKey("Id")]
    [InverseProperty("StepContentToolCallResponse")]
    public virtual StepContent IdNavigation { get; set; } = null!;
}
