using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("ChatRun")]
[Index("ChatId", "Status", Name = "IX_ChatRun_Chat_Status")]
[Index("Status", "HeartbeatAt", Name = "IX_ChatRun_Status_Heartbeat")]
public partial class ChatRun
{
    [Key]
    public long Id { get; set; }

    public int ChatId { get; set; }

    public int UserId { get; set; }

    public byte Kind { get; set; }

    public byte Status { get; set; }

    public string? Metadata { get; set; }

    public int LastEventSeq { get; set; }

    [StringLength(64)]
    [Unicode(false)]
    public string InstanceId { get; set; } = null!;

    public DateTime? StopRequestedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime HeartbeatAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    [ForeignKey("ChatId")]
    [InverseProperty("ChatRuns")]
    public virtual Chat Chat { get; set; } = null!;

    [InverseProperty("Run")]
    public virtual ICollection<ChatRunEvent> ChatRunEvents { get; set; } = new List<ChatRunEvent>();

    [InverseProperty("Run")]
    public virtual ICollection<ChatRunSteering> ChatRunSteerings { get; set; } = new List<ChatRunSteering>();

    [ForeignKey("UserId")]
    [InverseProperty("ChatRuns")]
    public virtual User User { get; set; } = null!;
}
