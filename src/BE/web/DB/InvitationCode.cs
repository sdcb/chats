using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.Web.DB;

[Table("InvitationCode")]
[Index("Value", Name = "InvitationCode2_value_key", IsUnique = true)]
public partial class InvitationCode
{
    [Key]
    public int Id { get; set; }

    [StringLength(100)]
    public string Value { get; set; } = null!;

    public short Count { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public int CreateUserId { get; set; }

    [InverseProperty("InvitationCode")]
    public virtual ICollection<UserInitialConfig> UserInitialConfigs { get; set; } = new List<UserInitialConfig>();

    [ForeignKey("InvitationCodeId")]
    [InverseProperty("InvitationCodes")]
    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
