using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("StepContent")]
[Index("StepId", Name = "IX_StepContent_StepId")]
public partial class StepContent
{
    [Key]
    public long Id { get; set; }

    public byte ContentTypeId { get; set; }

    public long StepId { get; set; }

    [ForeignKey("StepId")]
    [InverseProperty("StepContents")]
    public virtual Step Step { get; set; } = null!;

    [InverseProperty("IdNavigation")]
    public virtual StepContentBlob? StepContentBlob { get; set; }

    [InverseProperty("IdNavigation")]
    public virtual StepContentFile? StepContentFile { get; set; }

    [InverseProperty("IdNavigation")]
    public virtual StepContentText? StepContentText { get; set; }

    [InverseProperty("IdNavigation")]
    public virtual StepContentThink? StepContentThink { get; set; }

    [InverseProperty("IdNavigation")]
    public virtual StepContentToolCall? StepContentToolCall { get; set; }

    [InverseProperty("IdNavigation")]
    public virtual StepContentToolCallResponse? StepContentToolCallResponse { get; set; }
}
