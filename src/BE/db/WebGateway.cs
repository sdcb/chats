using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("WebGateway")]
[Index("Name", Name = "UQ_WebGateway_Name", IsUnique = true)]
public partial class WebGateway
{
    [Key]
    public int Id { get; set; }

    [StringLength(128)]
    public string Name { get; set; } = null!;

    public byte GatewayType { get; set; }

    [StringLength(2048)]
    [Unicode(false)]
    public string AdminEndpoint { get; set; } = null!;

    public string? Credential { get; set; }

    [StringLength(512)]
    [Unicode(false)]
    public string ServerNames { get; set; } = null!;

    [Column("PublicIPv4")]
    [StringLength(45)]
    [Unicode(false)]
    public string? PublicIpv4 { get; set; }

    [Column("PublicIPv6")]
    [StringLength(45)]
    [Unicode(false)]
    public string? PublicIpv6 { get; set; }

    [StringLength(253)]
    [Unicode(false)]
    public string? CnameTarget { get; set; }

    [StringLength(8)]
    [Unicode(false)]
    public string PublicScheme { get; set; } = null!;

    public int PublicPort { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [InverseProperty("WebGateway")]
    public virtual ICollection<HostedDomain> HostedDomains { get; set; } = new List<HostedDomain>();
}
