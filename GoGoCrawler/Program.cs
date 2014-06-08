using CsQuery;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GoGoCrawler
{
    class Program
    {
        static MongoClient _mongoClient = new MongoClient("mongodb://@ds029797.mongolab.com:29797/nortromdevdb");

        static void Main(string[] args)
        {
            Console.WriteLine("running job ...");

            var categories = Shuffle(GetCategoriesAsync().Result.ToList());

            Console.WriteLine("found {0} categories ...", categories.Count());

            foreach (var cat in categories)
            {
                try
                {
                    var episodes = GetEpisodesAsync(cat).Result;

                    Console.WriteLine("found {0} episodes for category {1}", episodes.Count(), cat);
                    foreach (var episode in episodes)
                    {
                        SaveMoviesAsync(cat, episode).Wait();
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            Console.WriteLine("job completed");
        }


        static async Task<IEnumerable<Uri>> GetCategoriesAsync()
        {
            HttpClient client = new HttpClient();
            var html = await client.GetStringAsync("http://www.gogoanime.com/watch-anime-list");

            var cq = CQ.Create(html);
            var categoryAnchors = cq[".cat-item a"];
            var allLinks = categoryAnchors.Select(i => i.GetAttribute("href")).Select(i => new Uri(i));
            return allLinks;
        }

        static async Task<IEnumerable<Uri>> GetEpisodesAsync(Uri category)
        {
            var html = await new HttpClient().GetStringAsync(category);
            var cq = CQ.Create(html);
            var episodes = cq["div.postlist a"].Select(i => i.GetAttribute("href")).Select(i => new Uri(i));
            return episodes;
        }

        static async Task SaveMoviesAsync(Uri category, Uri episode)
        {
            var html = await new HttpClient().GetStringAsync(episode);
            var cq = CQ.Create(html);
            var sources = cq["iframe"].Select(i => i.GetAttribute("src"))
                .Where(i => Uri.IsWellFormedUriString(i, UriKind.Absolute))
                .Select(i => new Uri(i)).ToArray();

            // save the movies to mongodb
            var movieEntity = new Movie(category, episode, sources);
            var server = _mongoClient.GetServer();
            var database = server.GetDatabase("nortromdevdb");
            var collection = database.GetCollection<Movie>("movies");

            collection.Update(
            Query<Movie>.EQ(m => m.Episode, movieEntity.Episode),
            Update.Replace(movieEntity),
            UpdateFlags.Upsert);
        }

        public static IList<T> Shuffle<T>(IList<T> list)
        {
            Random rng = new Random();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
            return list;
        }
    }

    public class Movie
    {
        public string Category { get; set; }

        public string Episode { get; set; }

        public string[] Sources { get; set; }

        public string CreatedDateTime { get; set; }

        public Movie() { }

        public Movie(Uri category, Uri episode, Uri[] sources)
        {
            this.CreatedDateTime = DateTimeOffset.UtcNow.ToString();
            this.Sources = sources.Select(i => i.ToString()).ToArray();
            this.Episode = episode.ToString().Replace(@"http://www.gogoanime.com/", "");
            this.Category = category.ToString().Replace(@"http://www.gogoanime.com/category/", "");
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
