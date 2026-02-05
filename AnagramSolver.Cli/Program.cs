using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Configuration;
using AnagramSolver.BusinessLogic;
using AnagramSolver.Contracts;

namespace AnagramSolver.Cli
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            var config = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).AddJsonFile("appsettings.json", optional: false, reloadOnChange: true).Build();

            int minUserLen = int.Parse(config["Settings:MinUserWordLength"]);
            int maxResults = int.Parse(config["Settings:MaxResults"]);
            int maxWords = int.Parse(config["Settings:MaxWordsInAnagram"]);
            string path = config["Dictionary:WordFilePath"];

            var normalizer = new WordNormalizer();

            IWordRepository repo = new FileWordRepository(path);

            IAnagramSolver solver = new DefaultAnagramSolver(repo, maxResults, maxWords);

            var user = new UserProcessor(minUserLen);

            Console.WriteLine("Įveskite žodžius: ");
            string input = Console.ReadLine();

            if (!user.IsValid(input))
            {
                Console.WriteLine($"Įvestas per trumpas žodis. Minimalus ilgis: {minUserLen}");
                return;
            }

            var combined = normalizer.NormalizeUserWords(input);
            var sortedKey = AnagramKeyBuilder.BuildKey(combined);

            var results = solver.GetAnagrams(sortedKey);

            if(results.Count == 0)
            {
                Console.WriteLine("Anagramų nerasta.");
            }
            else
            {
                Console.WriteLine("Anagramos: ");
                foreach(var r in results)
                {
                    Console.WriteLine(r);
                }
            }
        }
    }
}