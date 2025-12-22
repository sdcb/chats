using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.Web.DB;

[Table("Step")]
[Index("TurnId", "Id", Name = "IX_Step_TurnId")]
public partial class Step
{
    [Key]
    public long Id { get; set; }

    public long TurnId { get; set; }

    public byte ChatRoleId { get; set; }

    public bool Edited { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? UsageId { get; set; }

    [InverseProperty("Step")]
    public virtual ICollection<StepContent> StepContents { get; set; } = new List<StepContent>();

    [ForeignKey("TurnId")]
    [InverseProperty("Steps")]
    public virtual ChatTurn Turn { get; set; } = null!;

    [ForeignKey("UsageId")]
    [InverseProperty("Steps")]
    public virtual UserModelUsage? Usage { get; set; }
}
