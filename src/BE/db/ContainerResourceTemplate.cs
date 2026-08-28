using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("ContainerResourceTemplate")]
[Index("Name", Name = "UQ_ContainerResourceTemplate_Name", IsUnique = true)]
public partial class ContainerResourceTemplate
{
    [Key]
    public int Id { get; set; }

    [StringLength(128)]
    public string Name { get; set; } = null!;

    public int RuntimeNodeId { get; set; }

    [StringLength(512)]
    [Unicode(false)]
    public string Image { get; set; } = null!;

    public float CpuCores { get; set; }

    public long MemoryBytes { get; set; }

    public int MaxProcesses { get; set; }

    [StringLength(128)]
    [Unicode(false)]
    public string? BackendNetworkName { get; set; }

    public long? DefaultVolumeBytes { get; set; }

    public byte Visibility { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [ForeignKey("RuntimeNodeId")]
    [InverseProperty("ContainerResourceTemplates")]
    public virtual ContainerRuntimeNode RuntimeNode { get; set; } = null!;
}
