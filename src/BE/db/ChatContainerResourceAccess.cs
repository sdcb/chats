using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("ChatContainerResourceAccess")]
[Index("ContainerResourceId", Name = "IX_ChatContainerAccess_Container")]
[Index("ChatId", "ContainerResourceId", Name = "UQ_ChatContainerAccess_ChatContainer", IsUnique = true)]
public partial class ChatContainerResourceAccess
{
    [Key]
    public long Id { get; set; }

    public int ChatId { get; set; }

    public long ContainerResourceId { get; set; }

    public DateTime GrantedAt { get; set; }

    [ForeignKey("ChatId")]
    [InverseProperty("ChatContainerResourceAccesses")]
    public virtual Chat Chat { get; set; } = null!;

    [ForeignKey("ContainerResourceId")]
    [InverseProperty("ChatContainerResourceAccesses")]
    public virtual ContainerResource ContainerResource { get; set; } = null!;
}
