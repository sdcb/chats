using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.Web.DB;

[Table("UserApiKey")]
[Index("Key", Name = "IX_UserApiKey_Key", IsUnique = true)]
[Index("UserId", Name = "IX_UserApiKey_UserId")]
public partial class UserApiKey
{
    [Key]
    public int Id { get; set; }

    [StringLength(200)]
    [Unicode(false)]
    public string Key { get; set; } = null!;

    public bool IsRevoked { get; set; }

    [StringLength(50)]
    public string? Comment { get; set; }

    public bool AllowEnumerate { get; set; }

    public bool AllowAllModels { get; set; }

    public DateTime Expires { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UserId { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("UserApiKeys")]
    public virtual User User { get; set; } = null!;

    [InverseProperty("UserApiKey")]
    public virtual ICollection<UserApiCache> UserApiCaches { get; set; } = new List<UserApiCache>();

    [InverseProperty("ApiKey")]
    public virtual ICollection<UserApiUsage> UserApiUsages { get; set; } = new List<UserApiUsage>();

    [ForeignKey("ApiKeyId")]
    [InverseProperty("ApiKeys")]
    public virtual ICollection<Model> Models { get; set; } = new List<Model>();
}
