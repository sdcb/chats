using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Chats.DB;

[Table("UserApiCacheBody")]
public partial class UserApiCacheBody
{
    [Key]
    public int UserApiCacheId { get; set; }

    public string Request { get; set; } = null!;

    public string Response { get; set; } = null!;

    [ForeignKey("UserApiCacheId")]
    [InverseProperty("UserApiCacheBody")]
    public virtual UserApiCache UserApiCache { get; set; } = null!;
}
