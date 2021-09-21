using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace traktorImpo
{
    public class PostMeta
    {
        [Key]
        public int meta_id { get; set; }
        [ForeignKey("Post")]
        public int post_id { get; set; }
        public Post Post { get; set; }
        public string meta_key { get; set; }
        public string meta_value { get; set; }
    }
}