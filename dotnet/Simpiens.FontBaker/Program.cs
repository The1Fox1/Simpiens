using System;
using System.IO;

namespace Simpiens.FontBaker
{
    /// <summary><c>dotnet run --project dotnet/Simpiens.FontBaker [-- --out &lt;content folder&gt;]</c>: bakes the fonts into the client's content.</summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            string output = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--out" && i + 1 < args.Length) output = args[++i];
                else
                {
                    Console.Error.WriteLine("usage: Simpiens.FontBaker [--out <content folder>]  (default: dotnet/Simpiens.Client/Content)");
                    return 64;
                }
            }
            output ??= DefaultContentFolder();
            var files = FontBaker.Bake(output);
            foreach (var file in files) Console.WriteLine(Path.GetRelativePath(output, file));
            Console.WriteLine($"{files.Count} files in {output}");
            return 0;
        }

        /// <summary>The client's content folder in this repository (found from the baker's own folder).</summary>
        public static string DefaultContentFolder()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "dotnet", "Simpiens.slnx"))) dir = dir.Parent;
            if (dir == null) throw new InvalidOperationException("Can't find the repository (dotnet/Simpiens.slnx) above " + AppContext.BaseDirectory);
            return Path.Combine(dir.FullName, "dotnet", "Simpiens.Client", "Content");
        }
    }
}
