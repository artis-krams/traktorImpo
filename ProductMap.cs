using CsvHelper.Configuration;

namespace traktorImpo
{
    public sealed class ProductMap: ClassMap < Product > {  
        public ProductMap() {  
            Map(x => x.Part).Name("Part");  
            Map(x => x.Description).Name("Description");  
            Map(x => x.Price).Name("Price");  
        }  
    }  
}
