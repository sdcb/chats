using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("Config")]
[Index("Key", Name = "Configs_key_key", IsUnique = true)]
public partial class Config
{
    [Key]
    [StringLength(100)]
    [Unicode(false)]
    public string Key { get; set; } = null!;

    public string Value { get; set; } = null!;

    [StringLength(50)]
    public string? Description { get; set; }
}
