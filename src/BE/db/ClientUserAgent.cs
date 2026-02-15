using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("ClientUserAgent")]
[Index("UserAgent", Name = "IX_ClientUserAgent", IsUnique = true)]
public partial class ClientUserAgent
{
    [Key]
    public int Id { get; set; }

    [StringLength(500)]
    [Unicode(false)]
    public string UserAgent { get; set; } = null!;

    [InverseProperty("ClientUserAgent")]
    public virtual ICollection<ClientInfo> ClientInfos { get; set; } = new List<ClientInfo>();
}
