using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("DnsProvider")]
[Index("Name", Name = "UQ_DnsProvider_Name", IsUnique = true)]
public partial class DnsProvider
{
    [Key]
    public int Id { get; set; }

    [StringLength(128)]
    public string Name { get; set; } = null!;

    public byte ProviderType { get; set; }

    public string Credential { get; set; } = null!;

    public bool IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [InverseProperty("DnsProvider")]
    public virtual ICollection<HostedDomain> HostedDomains { get; set; } = new List<HostedDomain>();
}
