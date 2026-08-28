using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("ContainerVolume")]
[Index("OwnerUserId", "IsActive", "IsStandalone", Name = "IX_ContainerVolume_OwnerUser_Active")]
[Index("ContainerResourceId", Name = "UX_ContainerVolume_InternalContainer", IsUnique = true)]
public partial class ContainerVolume
{
    [Key]
    public long Id { get; set; }

    public int OwnerUserId { get; set; }

    public int RuntimeNodeId { get; set; }

    public long? ContainerResourceId { get; set; }

    public bool IsStandalone { get; set; }

    [StringLength(256)]
    [Unicode(false)]
    public string? BackendVolumeId { get; set; }

    [StringLength(128)]
    public string Name { get; set; } = null!;

    public long? DeclaredBytes { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    [ForeignKey("ContainerResourceId")]
    [InverseProperty("ContainerVolume")]
    public virtual ContainerResource? ContainerResource { get; set; }

    [InverseProperty("Volume")]
    public virtual ICollection<ContainerVolumeMount> ContainerVolumeMounts { get; set; } = new List<ContainerVolumeMount>();

    [ForeignKey("OwnerUserId")]
    [InverseProperty("ContainerVolumes")]
    public virtual User OwnerUser { get; set; } = null!;

    [ForeignKey("RuntimeNodeId")]
    [InverseProperty("ContainerVolumes")]
    public virtual ContainerRuntimeNode RuntimeNode { get; set; } = null!;
}
