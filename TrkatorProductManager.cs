using System;
using CsvHelper;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace traktorImpo
{
    public class TrkatorProductManager
    {
        public List<Product> LoadedLocalProducts { get; private set; }
        public TrkatorProductManager(string pathToCsv)
        {
            LoadedLocalProducts = GetProductList(pathToCsv);
        }

        public IEnumerable<Post> CreateMissingProductPosts(IEnumerable<Post> existingPosts)
        {
            IEnumerable<string> existingPostNames = existingPosts.Select(x => x.post_name);
            IEnumerable<string> localProductNames = LoadedLocalProducts.Select(x => CreatePostName(x));
            IEnumerable<string> newProductNames = localProductNames.Except(existingPostNames);
            var existingProducsToPrint = existingPostNames.Except(localProductNames);
            Console.WriteLine();
            IEnumerable<Product> productsToPost = LoadedLocalProducts.Join(newProductNames, lp => CreatePostName(lp), npn => npn, (loadedProduct, npn) => loadedProduct);

            if (productsToPost.Count() > 0)
            {
                Console.WriteLine("{0}:     {1} new products found {2} / {3} / {4}",
                DateTime.Now,
                productsToPost.Count(),
                existingPostNames.Count(),
                localProductNames.Count(),
                newProductNames.Count());
            }

            return CreatePostsFromProducts(productsToPost);
        }
        private IEnumerable<Post> CreatePostsFromProducts(IEnumerable<Product> products)
        {
            var posts = products.Select(product =>
                new Post
                {
                    post_title = product.Description + " " + product.Part,
                    post_name = CreatePostName(product),
                    post_content = product.Description,
                    post_type = "product",
                    post_date = DateTime.Now,
                    post_excerpt = string.Empty,
                    to_ping = string.Empty,
                    pinged = string.Empty,
                    post_content_filtered = string.Empty,
                    metas = new List<PostMeta>
                    {
                        new PostMeta
                        {
                            meta_key = "_price",
                            meta_value = product.Price
                        },
                        new PostMeta
                        {
                            meta_key = "_regular_price",
                            meta_value = product.Price
                        },
                        new PostMeta
                        {
                            meta_key = "_sku",
                            meta_value = product.Part
                        }
                    }
                }).ToArray();


            return posts;
        }

        public List<TermRelation> CreateTermRelations(int postId)
        {
            return new List<TermRelation>
                {
                    new TermRelation
                    {
                        object_id = postId,
                        term_taxonomy_id = 2,   // type
                        term_order = 0
                    },
                    new TermRelation
                    {
                        object_id = postId,
                        term_taxonomy_id = 7,   // visibility
                        term_order = 0
                    },
                    new TermRelation
                    {
                        object_id = postId,
                        term_taxonomy_id = 153, // category
                        term_order = 1
                    }
                };
        }

        internal void RemoveIgnoredProductsFromLocal(IEnumerable<Post> postsToSkip)
        {
            IEnumerable<string> postNames = postsToSkip.Select(x => x.post_name);
            foreach (var name in postNames)
            {
                var localProduct = LoadedLocalProducts.FirstOrDefault(x => CreatePostName(x) == name);

                if (localProduct != null)
                {
                    LoadedLocalProducts.Remove(localProduct);
                }
            }
        }

        public List<Product> GetProductList(string filePath)
        {
            using (var fileReader = new StreamReader(filePath, Encoding.Default))
            using (var csv = new CsvReader(fileReader, CultureInfo.InvariantCulture))
            {
                return csv.GetRecords<Product>().ToList();
            }
        }

        public string CreatePostName(Product product)
        {
            return (product.Description + "-" + product.Part).Replace(' ', '-').ToLowerInvariant();
        }
        public void UpdatePrices(TraktorSqlContext context, float percent)
        {
            Console.WriteLine("{1}:     Updating prices by {0} percent.", percent, DateTime.Now);

            var priceMetas = context.wp_postmeta.Where(pm => pm.meta_key == "_price" || pm.meta_key == "_regular_price");
            var metaCount = priceMetas.Count() / 2;
            var processedMetas = 0;

            foreach (var meta in priceMetas)
            {
                var price = float.Parse(meta.meta_value);
                meta.meta_value = price + (price * (percent / 100)).ToString();
                processedMetas++;
                Console.Write("\r {0} / {1} prices updated with {2} %.", processedMetas / 2, metaCount, percent);
            }

            Console.WriteLine();
            Console.WriteLine("{0}:     Uploading prices", DateTime.Now);
            context.SaveWithRetry();
            Console.WriteLine("{0}:     Done", DateTime.Now);
        }
    }
}