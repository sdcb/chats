using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("ChatRunSteering")]
[Index("RunId", "Status", Name = "IX_ChatRunSteering_Run_Status")]
public partial class ChatRunSteering
{
    [Key]
    public long Id { get; set; }

    public long RunId { get; set; }

    public string Content { get; set; } = null!;

    public byte Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? HandledAt { get; set; }

    public long? InjectedStepId { get; set; }

    [ForeignKey("RunId")]
    [InverseProperty("ChatRunSteerings")]
    public virtual ChatRun Run { get; set; } = null!;
}
