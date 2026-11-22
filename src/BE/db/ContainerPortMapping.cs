using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("ContainerPortMapping")]
public partial class ContainerPortMapping
{
    [Key]
    public long Id { get; set; }

    public long ContainerResourceId { get; set; }

    public int RuntimeNodeId { get; set; }

    public int ContainerPort { get; set; }

    public int HostPort { get; set; }

    [StringLength(8)]
    [Unicode(false)]
    public string Protocol { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? RemovedAt { get; set; }

    [ForeignKey("ContainerResourceId")]
    [InverseProperty("ContainerPortMappings")]
    public virtual ContainerResource ContainerResource { get; set; } = null!;

    [InverseProperty("ContainerPortMapping")]
    public virtual ICollection<PublishedSite> PublishedSites { get; set; } = new List<PublishedSite>();

    [ForeignKey("RuntimeNodeId")]
    [InverseProperty("ContainerPortMappings")]
    public virtual ContainerRuntimeNode RuntimeNode { get; set; } = null!;
}
