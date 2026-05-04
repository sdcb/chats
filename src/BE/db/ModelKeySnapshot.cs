using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("ModelKeySnapshot")]
[Index("ModelKeyId", Name = "IX_ModelKeySnapshot_ModelKeyId")]
public partial class ModelKeySnapshot
{
    [Key]
    public int Id { get; set; }

    public short ModelKeyId { get; set; }

    public short ModelProviderId { get; set; }

    [StringLength(100)]
    public string Name { get; set; } = null!;

    [StringLength(500)]
    [Unicode(false)]
    public string? Host { get; set; }

    [StringLength(1000)]
    [Unicode(false)]
    public string? Secret { get; set; }

    [Unicode(false)]
    public string? CustomHeaders { get; set; }

    [Unicode(false)]
    public string? CustomBody { get; set; }

    public DateTime CreatedAt { get; set; }

    [InverseProperty("CurrentSnapshot")]
    public virtual ModelKey? ModelKey { get; set; }

    [InverseProperty("ModelKeySnapshot")]
    public virtual ICollection<ModelSnapshot> ModelSnapshots { get; set; } = new List<ModelSnapshot>();
}
