using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("ContainerImage")]
public partial class ContainerImage
{
    [Key]
    [StringLength(512)]
    [Unicode(false)]
    public string Image { get; set; } = null!;

    [StringLength(1000)]
    public string? Description { get; set; }

    public bool IsEnabled { get; set; }
}
