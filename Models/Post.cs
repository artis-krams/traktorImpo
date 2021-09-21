using System;
using System.Collections.Generic;

namespace traktorImpo
{
    public class Post
    {
        public int ID { get; set; }
        public string post_title { get; set; }
        public string post_name { get; set; }
        public string post_content { get; set; }
        public string post_type { get; set; }
        public DateTime post_date { get; set; }
        public DateTime post_date_gmt { get; set; }
        public DateTime post_modified { get; set; }
        public DateTime post_modified_gmt { get; set; }
        public string post_excerpt { get; set; }    // needs default
        public string to_ping { get; set; } // needs default
        public string pinged { get; set; }  // needs default
        public string post_content_filtered { get; set; }  // needs default

        public virtual ICollection<PostMeta> metas { get; set; }
        // public virtual ICollection<TermRelation> terms { get; set; }
    }
}