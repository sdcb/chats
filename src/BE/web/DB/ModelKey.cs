using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.Web.DB;

[Table("ModelKey")]
[Index("ModelProviderId", Name = "IX_ModelKey2_ModelProviderId")]
public partial class ModelKey
{
    [Key]
    public short Id { get; set; }

    public short ModelProviderId { get; set; }

    [StringLength(50)]
    public string Name { get; set; } = null!;

    [StringLength(500)]
    [Unicode(false)]
    public string? Host { get; set; }

    [StringLength(1000)]
    [Unicode(false)]
    public string? Secret { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public short Order { get; set; }

    [InverseProperty("ModelKey")]
    public virtual ICollection<Model> Models { get; set; } = new List<Model>();
}
