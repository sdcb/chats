using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("ModelKey")]
[Index("CurrentSnapshotId", Name = "UX_ModelKey_CurrentSnapshotId", IsUnique = true)]
public partial class ModelKey
{
    [Key]
    public short Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public short Order { get; set; }

    public int CurrentSnapshotId { get; set; }

    [ForeignKey("CurrentSnapshotId")]
    [InverseProperty("ModelKey")]
    public virtual ModelKeySnapshot CurrentSnapshot { get; set; } = null!;
}
