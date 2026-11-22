using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("PublishedSiteEvent")]
[Index("PublishedSiteId", "Id", Name = "IX_PublishedSiteEvent_Site")]
public partial class PublishedSiteEvent
{
    [Key]
    public long Id { get; set; }

    public long PublishedSiteId { get; set; }

    [StringLength(64)]
    [Unicode(false)]
    public string Action { get; set; } = null!;

    public bool IsSuccess { get; set; }

    public string? Message { get; set; }

    public int? OperatorUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    [ForeignKey("PublishedSiteId")]
    [InverseProperty("PublishedSiteEvents")]
    public virtual PublishedSite PublishedSite { get; set; } = null!;
}
