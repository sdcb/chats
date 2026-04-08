using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[PrimaryKey("UserId", "Key")]
[Table("UserConfig")]
public partial class UserConfig
{
    [Key]
    public int UserId { get; set; }

    [Key]
    [StringLength(100)]
    [Unicode(false)]
    public string Key { get; set; } = null!;

    public string Value { get; set; } = null!;

    [StringLength(50)]
    public string? Description { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("UserConfigs")]
    public virtual User User { get; set; } = null!;
}
