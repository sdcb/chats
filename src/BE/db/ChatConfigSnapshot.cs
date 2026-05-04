using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("ChatConfigSnapshot")]
[Index("HashCode", Name = "IX_ChatConfigSnapshot_HashCode")]
[Index("ModelSnapshotId", Name = "IX_ChatConfigSnapshot_ModelSnapshotId")]
[Index("ModelSnapshotId", "HashCode", Name = "UX_ChatConfigSnapshot_ModelSnapshotId_HashCode", IsUnique = true)]
public partial class ChatConfigSnapshot
{
    [Key]
    public int Id { get; set; }

    public int ModelSnapshotId { get; set; }

    public string? SystemPrompt { get; set; }

    public float? Temperature { get; set; }

    public bool WebSearchEnabled { get; set; }

    public int? MaxOutputTokens { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string? Effort { get; set; }

    public bool CodeExecutionEnabled { get; set; }

    [StringLength(40)]
    public string? ImageSize { get; set; }

    public int? ThinkingBudget { get; set; }

    public string? EnabledMcpNames { get; set; }

    public long? HashCode { get; set; }

    public DateTime CreatedAt { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string? Format { get; set; }

    public byte? Compression { get; set; }

    [InverseProperty("ChatConfigSnapshot")]
    public virtual ICollection<ChatTurn> ChatTurns { get; set; } = new List<ChatTurn>();

    [ForeignKey("ModelSnapshotId")]
    [InverseProperty("ChatConfigSnapshots")]
    public virtual ModelSnapshot ModelSnapshot { get; set; } = null!;
}
