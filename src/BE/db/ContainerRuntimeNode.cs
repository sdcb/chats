using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("ContainerRuntimeNode")]
[Index("AiName", Name = "UQ_ContainerRuntimeNode_AiName", IsUnique = true)]
[Index("Name", Name = "UQ_ContainerRuntimeNode_Name", IsUnique = true)]
public partial class ContainerRuntimeNode
{
    [Key]
    public int Id { get; set; }

    [StringLength(128)]
    public string Name { get; set; } = null!;

    [StringLength(128)]
    [Unicode(false)]
    public string AiName { get; set; } = null!;

    [StringLength(1000)]
    public string? Description { get; set; }

    public byte BackendType { get; set; }

    [StringLength(2048)]
    [Unicode(false)]
    public string? Endpoint { get; set; }

    [StringLength(4000)]
    [Unicode(false)]
    public string? Credential { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [InverseProperty("RuntimeNode")]
    public virtual ICollection<ContainerResourceTemplate> ContainerResourceTemplates { get; set; } = new List<ContainerResourceTemplate>();

    [InverseProperty("RuntimeNode")]
    public virtual ICollection<ContainerResource> ContainerResources { get; set; } = new List<ContainerResource>();

    [InverseProperty("RuntimeNode")]
    public virtual ICollection<ContainerVolume> ContainerVolumes { get; set; } = new List<ContainerVolume>();
}
