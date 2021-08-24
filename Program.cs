using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace traktorImpo
{
    class TempProd
    {
        public int post_id { get; set; }
        public string sku { get; set; }
        public string price { get; set; }

    }
    class Program
    {
        static TrkatorProductManager manager;
        static string fileName;
        static int percent = 0;
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                fileName = "./Pricelist.csv";
            }
            else
            {
                for (int i = 0; i < args.Length; i += 2)
                {
                    var command = args[i];
                    var commandValue = args[i + 1];
                    switch (command)
                    {
                        case "-f":
                            fileName = commandValue;
                            break;
                        case "-p":
                            percent = int.Parse(commandValue);
                            break;
                        default:
                            Console.WriteLine("Invalid command: {0}", command);
                            break;
                    }
                }
            }

            manager = new TrkatorProductManager(fileName);
            var csvCount = manager.LoadedProducts.Count;
            Console.WriteLine("{1}:     {0} products in csv ", csvCount, DateTime.Now);

            var duplicates = manager.LoadedProducts.GroupBy(x => x.Part)
              .Where(g => g.Count() > 1).Select(y => y.Key);

            if (duplicates.Count() > 0)
            {
                Console.WriteLine("{0} duplicates detected:", duplicates.Count());
                foreach (var dup in duplicates)
                {
                    Console.WriteLine(dup);
                }
                throw new Exception("Remove duplicates in the input csv and retry.");
            }

            List<PostMeta> allPriceMetas;
            List<PostMeta> allSkuMetas;

            using (var context = new TraktorSqlContext())
            {
                IEnumerable<Post> allProductPosts = context.wp_posts.Where(p => p.post_type == "product").ToList();
                IEnumerable<Post> newPosts = manager.CreateMissingProductPosts(allProductPosts);

                if (newPosts.Count() > 0)
                {
                    context.AddRange(newPosts);
                    context.SaveWithRetry();

                    var newPostCount = newPosts.Count();
                    Console.WriteLine("{1}:     {0} new products created", newPostCount, DateTime.Now);

                    Console.WriteLine("{0}: Upload finished, creating relations (categories) ", DateTime.Now);
                    var processedRelations = 0;

                    foreach (var newPost in newPosts)
                    {
                        context.wp_term_relationships.AddRange(manager.CreateTermRelations(newPost.ID));
                        processedRelations++;
                        Console.Write("\r {0} / {1} prices updated with percentile.", processedRelations, newPostCount);
                    }

                    context.SaveWithRetry();
                    Console.WriteLine(Environment.NewLine + "{0}: Term relations updated ", DateTime.Now);
                }

                allPriceMetas = context.wp_postmeta.Where(pm => pm.meta_key == "_price").ToList();
                allSkuMetas = context.wp_postmeta.Where(pm => pm.meta_key == "_sku").ToList();

                string[] skusWithCorrectPrices = manager.LoadedProducts.Join(
                    allSkuMetas,
                    csv => csv.Part,
                    meta => meta.meta_value,
                    (c, m) => new { c.Price, m.post_id, m.meta_value }).Join(
                        allPriceMetas,
                        skuMeta => new { post_id = skuMeta.post_id, meta_value = skuMeta.Price },
                        priceMeta => new { post_id = priceMeta.post_id, meta_value = priceMeta.meta_value },
                        (s, p) => s.meta_value
                    ).ToArray();

                List<PostMeta> updateNeededSkuMetas;

                if (skusWithCorrectPrices.Length > 0)
                {
                    Console.WriteLine("{2}:     {1} prices correct | {3} product posts | {4} sku metas | {1} price metas",
                    allPriceMetas.Count(), skusWithCorrectPrices.Count(), DateTime.Now, context.wp_posts.Count(), allSkuMetas.Count());

                    var correctSkuStr = new StringBuilder();
                    correctSkuStr.AppendFormat("'{0}'", skusWithCorrectPrices[0]);
                    for (int i = 1; i < skusWithCorrectPrices.Count(); i++)
                        correctSkuStr.AppendFormat(", '{0}'", skusWithCorrectPrices[i]);

                    var sql = string.Format(
                        "SELECT * FROM wp_postmeta WHERE meta_key = '_sku' AND meta_value NOT IN ({0})",
                        correctSkuStr);

                    updateNeededSkuMetas = context.wp_postmeta.FromSqlRaw(sql).ToList();
                }
                else
                {
                    updateNeededSkuMetas = allSkuMetas;
                }

                Console.WriteLine("{1}:     {0} updates needed", updateNeededSkuMetas.Count(), DateTime.Now);

                int priceChangeCount = 1;
                int newMetaCount = 1;
                var processedPostCount = 1;
                var newProducts = new List<Product>();

                foreach (var csvProduct in manager.LoadedProducts.Join(updateNeededSkuMetas, lp => lp.Part, usk => usk.meta_value, (l, u) => l))
                {
                    Console.Write("\r {0} new metas | {1} prices changed | {2} total updates",
                        newMetaCount, priceChangeCount, processedPostCount++);

                    var existingPost = context.wp_posts.FirstOrDefault(p => p.post_name == manager.CreatePostName(csvProduct));

                    var skuMeta = allSkuMetas.Where(m => m.meta_value.Equals(csvProduct.Part)).FirstOrDefault();

                    if (skuMeta is null)
                    {
                        Console.WriteLine("\r new sku for existing post id:{0}", existingPost.ID);
                        context.wp_postmeta.Add(
                            new PostMeta
                            {
                                post_id = existingPost.ID,
                                meta_key = "_sku",
                                meta_value = csvProduct.Part
                            }
                        );
                        newMetaCount++;
                    }

                    var priceMeta = allPriceMetas.Where(x => x.post_id == existingPost.ID).FirstOrDefault();

                    if (priceMeta is null)
                    {
                        Console.WriteLine("\r new price meta for existing post id:{0}", existingPost.ID);
                        context.wp_postmeta.Add(
                            new PostMeta
                            {
                                post_id = existingPost.ID,
                                meta_key = "_price",
                                meta_value = csvProduct.Price
                            }
                        );
                        context.wp_postmeta.Add(
                            new PostMeta
                            {
                                post_id = existingPost.ID,
                                meta_key = "_regular_price",
                                meta_value = csvProduct.Price
                            }
                        );
                        newMetaCount++;
                    }
                    else
                    {
                        if (updateNeededSkuMetas.Count() < 10)
                            Console.WriteLine("\r new price for post id: {0}   | new/old {0}/{1}", existingPost.ID, priceMeta.meta_value, csvProduct.Price);
                        priceMeta.meta_value = csvProduct.Price;
                        priceChangeCount++;
                    }
                }

                Console.WriteLine("processing done, uploading to server");
                context.SaveWithRetry();


                if (percent != 0)
                    manager.UpdatePrices(context, percent);
            }
        }
    }
}
