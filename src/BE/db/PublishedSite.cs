using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("PublishedSite")]
[Index("ContainerResourceId", Name = "IX_PublishedSite_Container")]
[Index("UserId", "Status", Name = "IX_PublishedSite_User_Status")]
public partial class PublishedSite
{
    [Key]
    public long Id { get; set; }

    public int UserId { get; set; }

    public int HostedDomainId { get; set; }

    [StringLength(63)]
    [Unicode(false)]
    public string Subdomain { get; set; } = null!;

    [StringLength(253)]
    [Unicode(false)]
    public string Hostname { get; set; } = null!;

    public long ContainerResourceId { get; set; }

    public long? ContainerPortMappingId { get; set; }

    public int ContainerPort { get; set; }

    [StringLength(300)]
    [Unicode(false)]
    public string? UpstreamDial { get; set; }

    public byte Status { get; set; }

    public string? DnsRecords { get; set; }

    public string? GatewayRoutes { get; set; }

    public string? LastError { get; set; }

    public int? CreatedByChatId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? UnpublishedAt { get; set; }

    [ForeignKey("ContainerPortMappingId")]
    [InverseProperty("PublishedSites")]
    public virtual ContainerPortMapping? ContainerPortMapping { get; set; }

    [ForeignKey("ContainerResourceId")]
    [InverseProperty("PublishedSites")]
    public virtual ContainerResource ContainerResource { get; set; } = null!;

    [ForeignKey("HostedDomainId")]
    [InverseProperty("PublishedSites")]
    public virtual HostedDomain HostedDomain { get; set; } = null!;

    [InverseProperty("PublishedSite")]
    public virtual ICollection<PublishedSiteEvent> PublishedSiteEvents { get; set; } = new List<PublishedSiteEvent>();

    [ForeignKey("UserId")]
    [InverseProperty("PublishedSites")]
    public virtual User User { get; set; } = null!;
}
