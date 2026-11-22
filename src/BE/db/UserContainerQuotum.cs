using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

public partial class UserContainerQuotum
{
    [Key]
    public int Id { get; set; }

    public int? UserId { get; set; }

    public bool AllowCustomImage { get; set; }

    [StringLength(1024)]
    [Unicode(false)]
    public string AllowedNetworkModes { get; set; } = null!;

    public int? MaxContainerCount { get; set; }

    public float? MaxCpuCores { get; set; }

    public long? MaxMemoryBytes { get; set; }

    public int? MaxContainerProcesses { get; set; }

    public long? MaxVolumeBytes { get; set; }

    public float? MaxContainerCpuCores { get; set; }

    public long? MaxContainerMemoryBytes { get; set; }

    public long? MaxVolumeBytesPerVolume { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int? MaxPortMappings { get; set; }

    public int? MaxPublishedSites { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("UserContainerQuotum")]
    public virtual User? User { get; set; }
}
