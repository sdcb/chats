using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("ContainerVolumeMount")]
[Index("ContainerResourceId", "IsActive", Name = "IX_ContainerVolumeMount_Container_Active")]
[Index("VolumeId", "ContainerResourceId", "ContainerPath", Name = "UX_ContainerVolumeMount_ActivePath", IsUnique = true)]
public partial class ContainerVolumeMount
{
    [Key]
    public long Id { get; set; }

    public long VolumeId { get; set; }

    public long ContainerResourceId { get; set; }

    [StringLength(512)]
    public string ContainerPath { get; set; } = null!;

    public bool IsReadOnly { get; set; }

    public bool IsActive { get; set; }

    public DateTime MountedAt { get; set; }

    public DateTime? UnmountedAt { get; set; }

    [ForeignKey("ContainerResourceId")]
    [InverseProperty("ContainerVolumeMounts")]
    public virtual ContainerResource ContainerResource { get; set; } = null!;

    [ForeignKey("VolumeId")]
    [InverseProperty("ContainerVolumeMounts")]
    public virtual ContainerVolume Volume { get; set; } = null!;
}
