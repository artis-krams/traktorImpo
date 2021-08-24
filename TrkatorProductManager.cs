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
        public List<Product> LoadedProducts { get; private set; }
        public TrkatorProductManager(string pathToCsv)
        {
            LoadedProducts = GetProductList(pathToCsv);
        }

        public IEnumerable<Post> CreateMissingProductPosts(IEnumerable<Post> existingPosts)
        {
            IEnumerable<string> existingPostNames = existingPosts.Select(x => x.post_name);
            IEnumerable<string> localProductNames = LoadedProducts.Select(x => CreatePostName(x));
            IEnumerable<string> newProductNames = localProductNames.Except(existingPostNames);
            var existingProducsToPrint = existingPostNames.Except(localProductNames);
            Console.WriteLine();
            IEnumerable<Product> productsToPost = LoadedProducts.Join(newProductNames, lp => CreatePostName(lp), npn => npn, (loadedProduct, npn) => loadedProduct);

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

        public void UpdateLegacySkus(TraktorSqlContext context)
        {
            var skusUpdated = 0;
            var skusCorrect = 0;
            var skusMissing = 0;
            var productsMissing = 0;
            var newProducts = new List<Product>();
            var productPosts = context.wp_posts.ToList();
            var skus = context.wp_postmeta.Where(pm => pm.meta_key == "_sku").ToList();

            foreach (var csvProduct in LoadedProducts)
            {
                Console.Write("\r{3}:       Legacy skus updated - {0}, correct - {1}, new - {2}",
                skusUpdated, skusCorrect, skusMissing, DateTime.Now);

                if (productsMissing > 0)
                {
                    Console.Write(", missing product posts! - {0}", productsMissing);
                }

                var productPost = productPosts.FirstOrDefault(x => x.post_name == CreatePostName(csvProduct));

                if (productPost == null)
                {
                    productsMissing++;
                    continue;
                }

                var postId = productPost.ID;

                var skuMetaCheck = skus.FirstOrDefault(pm => pm.post_id == postId);

                if (skuMetaCheck == null)
                {
                    skusMissing++;
                    context.Add(new PostMeta
                    {
                        post_id = postId,
                        meta_key = "_sku",
                        meta_value = csvProduct.Part
                    });
                    continue;
                }

                var skuMeta = context.wp_postmeta.FirstOrDefault(pm => pm.meta_key == "_sku" && pm.post_id == postId);

                if (skuMeta.meta_value == csvProduct.Part)
                {
                    skusCorrect++;
                    continue;
                }

                skuMeta.meta_value = csvProduct.Part;
                skusUpdated++;

                if (skusUpdated % 1000 == 0)
                {
                    context.SaveWithRetry();
                }
            }

            context.SaveWithRetry();
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
        public void UpdatePrices(TraktorSqlContext context, int percent)
        {
            Console.WriteLine("{1}:     Updating prices by {0} percent.", percent, DateTime.Now);

            var priceMetas = context.wp_postmeta.Where(pm => pm.meta_key == "_price" || pm.meta_key == "_regular_price");
            var metaCount = priceMetas.Count() / 2;
            var processedMetas = 0;
            foreach (var meta in priceMetas)
            {
                meta.meta_value = (float.Parse(meta.meta_value) * (percent / 100)).ToString();
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