using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("HostedDomain")]
[Index("Domain", Name = "UQ_HostedDomain_Domain", IsUnique = true)]
public partial class HostedDomain
{
    [Key]
    public int Id { get; set; }

    [StringLength(253)]
    [Unicode(false)]
    public string Domain { get; set; } = null!;

    [StringLength(253)]
    [Unicode(false)]
    public string DnsZone { get; set; } = null!;

    public int DnsProviderId { get; set; }

    public int WebGatewayId { get; set; }

    public byte RecordMode { get; set; }

    public int RecordTtl { get; set; }

    public bool IsProxied { get; set; }

    public bool AllowAllUsers { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [ForeignKey("DnsProviderId")]
    [InverseProperty("HostedDomains")]
    public virtual DnsProvider DnsProvider { get; set; } = null!;

    [InverseProperty("HostedDomain")]
    public virtual ICollection<PublishedSite> PublishedSites { get; set; } = new List<PublishedSite>();

    [ForeignKey("WebGatewayId")]
    [InverseProperty("HostedDomains")]
    public virtual WebGateway WebGateway { get; set; } = null!;
}
