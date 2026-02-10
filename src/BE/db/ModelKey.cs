using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Chats.DB.Enums;

namespace Chats.DB;

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

    public byte AuthTypeId { get; set; }

    [StringLength(4000)]
    public string? OAuthConfigJson { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public short Order { get; set; }

    [InverseProperty("ModelKey")]
    public virtual ICollection<Model> Models { get; set; } = new List<Model>();

    [NotMapped]
    public DBModelAuthType AuthType
    {
        get => (DBModelAuthType)AuthTypeId;
        set => AuthTypeId = (byte)value;
    }
}
