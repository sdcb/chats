using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("Model")]
[Index("CurrentSnapshotId", Name = "UX_Model_CurrentSnapshotId", IsUnique = true)]
public partial class Model
{
    [Key]
    public short Id { get; set; }

    public short Order { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int CurrentSnapshotId { get; set; }

    public bool Enabled { get; set; }

    [InverseProperty("Model")]
    public virtual ICollection<ChatConfig> ChatConfigs { get; set; } = new List<ChatConfig>();

    [ForeignKey("CurrentSnapshotId")]
    [InverseProperty("Model")]
    public virtual ModelSnapshot CurrentSnapshot { get; set; } = null!;

    [InverseProperty("Model")]
    public virtual ICollection<UserApiCache> UserApiCaches { get; set; } = new List<UserApiCache>();

    [InverseProperty("Model")]
    public virtual ICollection<UserModel> UserModels { get; set; } = new List<UserModel>();

    [ForeignKey("ModelId")]
    [InverseProperty("Models")]
    public virtual ICollection<UserApiKey> ApiKeys { get; set; } = new List<UserApiKey>();
}
