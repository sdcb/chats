using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("KnownImageSize")]
public partial class KnownImageSize
{
    [Key]
    public short Id { get; set; }

    public short Width { get; set; }

    public short Height { get; set; }

    [InverseProperty("ImageSize")]
    public virtual ICollection<ChatConfig> ChatConfigs { get; set; } = new List<ChatConfig>();
}
