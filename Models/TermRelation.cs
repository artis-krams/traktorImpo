using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace traktorImpo
{
    public class TermRelation
    {
        public int object_id
        {
            get;
            set;
        }
        public int term_taxonomy_id
        {
            get;
            set;
        }
        public int term_order
        {
            get;
            set;
        }
    }
}
