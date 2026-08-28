using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("ContainerImage")]
[Index("Image", Name = "UQ_ContainerImage_Image", IsUnique = true)]
public partial class ContainerImage
{
    [Key]
    public int Id { get; set; }

    [StringLength(512)]
    [Unicode(false)]
    public string Image { get; set; } = null!;

    [StringLength(1000)]
    public string? Description { get; set; }

    public bool IsEnabled { get; set; }
}
