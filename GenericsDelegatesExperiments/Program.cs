using GenericsDelegatesExperiments;
using System;
using System.Linq;

internal class Program
{
    static void Main()
    {
        var gm = new GenericMethods();

        int x = 10;
        int y = 20;

        Console.WriteLine($"{x}, {y}");

        gm.Swap(ref x, ref y);

        Console.WriteLine($"{x}, {y}");

        

        string zodis1 = "labas";
        string zodis2 = "rytas";

        Console.WriteLine($"{zodis1}, {zodis2}");

        gm.Swap(ref zodis1, ref zodis2);

        Console.WriteLine($"{zodis1}, {zodis2}");



        var cl1 = new CustomClass("Jonas");
        var cl2 = new CustomClass("Petras");

        Console.WriteLine($"{cl1.Name}, {cl2.Name}");

        gm.Swap(ref cl1, ref cl2);

        Console.WriteLine($"{cl1.Name}, {cl2.Name}");

        //---------------------------------\\

        var source = new List<int> {1, 2, 3, 4, 5, 6, 7, 8, 9, 10};
        var even = gm.Where(source, x => x % 2 == 0); 
        var more = gm.Where(source, x => x > 4);
        var both = gm.Where(source, x => x % 2 == 0 && x > 4);

        Console.WriteLine("Even: ");
        foreach (var w in even) Console.WriteLine($"{w} ");
        Console.WriteLine("More than 4: ");
        foreach (var w in more) Console.WriteLine($"{w} ");
        Console.WriteLine("Even and more than 4: ");
        foreach (var w in both) Console.WriteLine($"{w} ");

        //---------------------------------\\

        var operations = new Dictionary<string, Func<string, string>>
        {
            ["lower"] = s => s.ToLower(),
            ["upper"] = s => s.ToUpper(),
            ["reverse"] = s => new string(s.Reverse().ToArray()),
        };

        Console.WriteLine(operations["lower"]("PRaKtikA"));
        Console.WriteLine(operations["upper"]("praktika"));
        Console.WriteLine(operations["reverse"]("praktika"));

        var lowerWord = gm.DelegateOperations("lower", "TESTAS");
        var upperWord = gm.DelegateOperations("upper", "testas");
        var reverseWord = gm.DelegateOperations("reverse", "testas");
        Console.WriteLine($"Lower: {lowerWord}, upper: {upperWord}, reverse: {reverseWord}");

        //---------------------------------\\

        var key1 = "aabls";
        var key2 = "abcd";
        var key3 = "aakrs";
        var value1 = new List<string> { "labas", "balas" };
        var value2 = new List<string> { "cdba", "dbac", "cabd" };
        var value3 = new List<string> { "karas" };

        var cache = new MemoryCache<List<string>>();

        cache.Set(key1, value1);
        cache.Set(key2, value2);
        cache.Set(key3, value3);

        var keys = cache.Keys;
        
        foreach(var k in keys)
        {
            Console.WriteLine($"{k}: ");

            if(cache.TryGet(k, out var list))
            {
                Console.Write(string.Join(", ", list));
            }
            Console.WriteLine("\n");
        }




        return;
    }
}