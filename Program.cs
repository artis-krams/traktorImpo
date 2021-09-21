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
        private static SqlHelper sqlHelper;
        static string fileName;
        static float percent = 0;
        private static string dbUser;
        private static string dbPass;
        private static string dbIp;
        private static string dbName;

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                fileName = "./cenas.csv";
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
                            percent = float.Parse(commandValue);
                            break;
                        case "-u":
                            dbUser = commandValue;
                            break;
                        case "-pw":
                            dbPass = commandValue;
                            break;
                        case "-s":
                            dbIp = commandValue;
                            break;
                        case "-dn":
                            dbName = commandValue;
                            break;
                        default:
                            Console.WriteLine("Invalid command: {0}", command);
                            break;
                    }
                }
            }

            manager = new TrkatorProductManager(fileName);
            sqlHelper = new SqlHelper();

            var csvCount = manager.LoadedLocalProducts.Count;
            Console.WriteLine("{1}:     {0} products in csv, loading products from server. ", csvCount, DateTime.Now);

            var duplicates = manager.LoadedLocalProducts.GroupBy(x => x.Part)
              .Where(g => g.Count() > 1).Select(y => y.Key);

            if (duplicates.Count() > 0)
            {
                Console.WriteLine("{0} duplicates detected:", duplicates.Count());
                foreach (var dup in duplicates)
                {
                    Console.WriteLine("dup id: " + dup);
                }
                throw new Exception("Remove duplicates in the input csv and retry.");
            }

            List<PostMeta> allPriceMetas;
            List<PostMeta> allSkuMetas;

            using (var context = new TraktorSqlContext(dbUser, dbPass, dbIp, dbName))
            {
                IEnumerable<Post> postsToSkip = context.wp_postmeta.Where(x => x.meta_key == "skip_sync" && x.meta_value == "yes").Select(x => x.Post);
                Console.WriteLine("skip_sync enabled for {0} products", postsToSkip.Count());
                manager.RemoveIgnoredProductsFromLocal(postsToSkip);

                IEnumerable<Post> allProductPosts = context.wp_posts.Where(p => p.post_type == "product" && p.metas.Any(x => x.meta_key == "_price")).Except(postsToSkip).ToList();

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

                var productMetas = allProductPosts.Join(context.wp_postmeta, post => post.ID, meta => meta.post_id, (p, m) => m);

                allPriceMetas = productMetas.Where(pm => pm.meta_key == "_price").ToList();
                allSkuMetas = productMetas.Where(pm => pm.meta_key == "_sku").ToList();

                string[] skusWithCorrectPrices = manager.LoadedLocalProducts.Join(
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
                    Console.WriteLine("{0}:     {1} prices correct | {2} product posts",
                     DateTime.Now, skusWithCorrectPrices.Count(), allProductPosts.Count());

                    var correctSkuStr = sqlHelper.BuildIdList(skusWithCorrectPrices);
                    var sql = string.Format(
                        "SELECT * FROM wp_postmeta WHERE meta_key = '_sku' AND meta_value NOT IN ({0})",
                        correctSkuStr);

                    if (postsToSkip.Count() > 0)
                    {

                        var ignorePostIds = sqlHelper.BuildIdList(postsToSkip.Select(x => x.ID.ToString()).ToArray());
                        sql += string.Format(" AND post_id NOT IN ({0})", ignorePostIds);
                    }

                    updateNeededSkuMetas = context.wp_postmeta.FromSqlRaw(sql).ToList();
                }
                else
                {
                    updateNeededSkuMetas = allSkuMetas;
                }

                Console.WriteLine("{1}:     checking {0} products, if updates needed", updateNeededSkuMetas.Count(), DateTime.Now);

                int priceChangeCount = 0;
                int newMetaCount = 0;
                var processedPostCount = 0;
                var newProducts = new List<Product>();

                foreach (var csvProduct in manager.LoadedLocalProducts.Join(updateNeededSkuMetas, lp => lp.Part, usk => usk.meta_value, (l, u) => l))
                {

                    var existingPost = allProductPosts.FirstOrDefault(p => p.post_name == manager.CreatePostName(csvProduct));

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
                        var regularPriceMeta = context.wp_postmeta.FirstOrDefault(x => x.post_id == existingPost.ID && x.meta_key == "_regular_price");
                        if (regularPriceMeta != null)
                            regularPriceMeta.meta_value = csvProduct.Price;
                        priceChangeCount++;
                    }

                    if (processedPostCount % 1000 == 0)
                    {
                        context.SaveWithRetry();
                    }
                    Console.Write("\r {0} new metas | {1} prices changed | {2} total updates",
                        newMetaCount, priceChangeCount, processedPostCount++);
                }

                Console.WriteLine("processing done, uploading to server");
                context.SaveWithRetry();


                if (percent != 0)
                    manager.UpdatePrices(context, percent);
            }
        }
    }
}
