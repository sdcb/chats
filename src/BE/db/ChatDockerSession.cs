using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("ChatDockerSession")]
[Index("ExpiresAt", Name = "IX_ChatDockerSession_Active_ExpiresAt")]
[Index("OwnerTurnId", Name = "IX_ChatDockerSession_OwnerTurnId")]
public partial class ChatDockerSession
{
    [Key]
    public long Id { get; set; }

    public long OwnerTurnId { get; set; }

    [StringLength(64)]
    public string Label { get; set; } = null!;

    [StringLength(128)]
    public string ContainerId { get; set; } = null!;

    [StringLength(256)]
    public string Image { get; set; } = null!;

    public long? MemoryBytes { get; set; }

    public float? CpuCores { get; set; }

    public short? MaxProcesses { get; set; }

    public byte NetworkMode { get; set; }

    public DateTime? TerminatedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastActiveAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    [ForeignKey("OwnerTurnId")]
    [InverseProperty("ChatDockerSessions")]
    public virtual ChatTurn OwnerTurn { get; set; } = null!;
}
