using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("ContainerResource")]
[Index("CleanupAt", Name = "IX_ContainerResource_CleanupAt")]
[Index("OwnerChatId", "OwnerTurnId", "DeletedAt", "StoppedAt", Name = "IX_ContainerResource_OwnerChat_Turn_Deleted_Stopped")]
[Index("OwnerTurnId", "Name", Name = "IX_ContainerResource_OwnerTurn_Name")]
[Index("OwnerUserId", "DeletedAt", "StoppedAt", "IsPermanent", Name = "IX_ContainerResource_OwnerUser_Deleted_Stopped")]
[Index("RuntimeNodeId", "DeletedAt", "StoppedAt", Name = "IX_ContainerResource_RuntimeNode_Deleted_Stopped")]
[Index("RuntimeNodeId", "BackendResourceId", Name = "UX_ContainerResource_RuntimeNode_BackendResource", IsUnique = true)]
public partial class ContainerResource
{
    [Key]
    public long Id { get; set; }

    public int OwnerUserId { get; set; }

    public int? OwnerChatId { get; set; }

    public long? OwnerTurnId { get; set; }

    public int RuntimeNodeId { get; set; }

    public bool IsPermanent { get; set; }

    [StringLength(256)]
    [Unicode(false)]
    public string BackendResourceId { get; set; } = null!;

    [StringLength(45)]
    [Unicode(false)]
    public string? Ip { get; set; }

    [StringLength(128)]
    public string Name { get; set; } = null!;

    [StringLength(512)]
    [Unicode(false)]
    public string Image { get; set; } = null!;

    [StringLength(128)]
    [Unicode(false)]
    public string? ShellPrefix { get; set; }

    public float? CpuCores { get; set; }

    public long? MemoryBytes { get; set; }

    public int? MaxProcesses { get; set; }

    [StringLength(128)]
    [Unicode(false)]
    public string? BackendNetworkName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? LastActiveAt { get; set; }

    public DateTime? StoppedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime? CleanupAt { get; set; }

    [InverseProperty("ContainerResource")]
    public virtual ICollection<ChatContainerResourceAccess> ChatContainerResourceAccesses { get; set; } = new List<ChatContainerResourceAccess>();

    [InverseProperty("ContainerResource")]
    public virtual ContainerVolume? ContainerVolume { get; set; }

    [InverseProperty("ContainerResource")]
    public virtual ICollection<ContainerVolumeMount> ContainerVolumeMounts { get; set; } = new List<ContainerVolumeMount>();

    [ForeignKey("OwnerChatId")]
    [InverseProperty("ContainerResources")]
    public virtual Chat? OwnerChat { get; set; }

    [ForeignKey("OwnerTurnId")]
    [InverseProperty("ContainerResources")]
    public virtual ChatTurn? OwnerTurn { get; set; }

    [ForeignKey("OwnerUserId")]
    [InverseProperty("ContainerResources")]
    public virtual User OwnerUser { get; set; } = null!;

    [ForeignKey("RuntimeNodeId")]
    [InverseProperty("ContainerResources")]
    public virtual ContainerRuntimeNode RuntimeNode { get; set; } = null!;
}
