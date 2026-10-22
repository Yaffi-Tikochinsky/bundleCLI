using fib;
using System.CommandLine;

#region bundle
//necessary variables
Dictionary<string, string> supportedLanguages = new() {
    { "csharp", ".cs" },
    { "python", ".py" },
    { "javascript", ".js" },
    { "typescript", ".ts" },
    { "java", ".java" },
    { "cpp", ".cpp" },
    { "c", ".c" },
    { "go", ".go" },
    { "rust", ".rs" },
    { "php", ".php" },
    { "ruby", ".rb" },
    { "swift", ".swift" },
    { "kotlin", ".kt" },
    { "html", ".html" },
    { "css", ".css" },
    { "sql", ".sql" },

};


#region bundleOptions
var languageOption = new Option<string[]>("--language", "-l")
{
    Description = "codes in which languages to include",
    Required = true,
    AllowMultipleArgumentsPerToken = true,

};
languageOption.AcceptOnlyFromAmong(supportedLanguages.Keys.Append("all").ToArray());

var outputOption = new Option<FileInfo>("--output", "-o")
{
    Required = true,
    Description = "File path and name",
};
var noteOption = new Option<bool>("--note", "-n")
{
    Description = "add the original path in a note"
};
var sortOption = new Option<SortMethod>("--sort", "-s")
{
    Description = "sorting method"
};
var removeLinesOption = new Option<bool>("--remove-empty-lines", "-r")
{
    Description = "remove empty lines"
};
var authorOption = new Option<string>("--author", "-a")
{
    Description = "write the author name in a note"
};
#endregion
//------logical operation for options------------
static IEnumerable<string> Collect(string root, HashSet<string> extensions, string outputFullPath)
{
    HashSet<string> excludedDirs = new(StringComparer.OrdinalIgnoreCase)
        { "bin", "obj", "debug", "release", ".git", ".vs", "node_modules" };

    var options = new EnumerationOptions
    {
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Hidden
    };


    var stack = new Stack<string>();
    stack.Push(root);
    while (stack.Count > 0)
    {
        var dir = stack.Pop();
        foreach (var sub in Directory.EnumerateDirectories(dir, "*", options))
            if (!excludedDirs.Contains(Path.GetFileName(sub)))
                stack.Push(sub);

        foreach (var file in Directory.EnumerateFiles(dir, "*", options))
        {
            if (!extensions.Contains(Path.GetExtension(file))) continue;
            if (string.Equals(Path.GetFullPath(file), outputFullPath, StringComparison.OrdinalIgnoreCase)) continue;
            yield return file;
        }
    }
}

//-------------------------------------
var bundleCommand = new Command("bundle", "bundle code files to a single file")
{
    languageOption, outputOption, authorOption,sortOption,noteOption,removeLinesOption
};

bundleCommand.SetAction(parseResult =>
{
    //--------------option arguments-------------
    var languages = parseResult.GetValue(languageOption)!;
    var output = parseResult.GetValue(outputOption)!;
    var author = parseResult.GetValue(authorOption);
    var sort = parseResult.GetValue(sortOption);
    var note = parseResult.GetValue(noteOption);
    var removeEmpty = parseResult.GetValue(removeLinesOption);
    //------------------------------------------------

    var extensions = languages.Contains("all")
        ? supportedLanguages.Values.ToHashSet(StringComparer.OrdinalIgnoreCase)
        : languages.Select(l => supportedLanguages[l]).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var root = Directory.GetCurrentDirectory();
    if (Directory.Exists(output.FullName))
    {
        Console.Error.WriteLine($"Error: '{output.FullName}' is a folder. Please provide a file name for the bundle.");
        return 1;
    }
    if (output.Directory is { Exists: false })
    {
        Console.Error.WriteLine($"Error: the folder '{output.DirectoryName}' does not exist.");
        return 1;
    }


    try
    {
        var files = Collect(root, extensions, output.FullName).ToList();
        if (files.Count == 0)
        {
            Console.Error.WriteLine("No matching code files were found. No bundle was created.");
            return 1;
        }
        files = (sort == SortMethod.CodeType
              ? files.OrderBy(f => Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)
                     .ThenBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
              : files.OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase))
          .ThenBy(f => f, StringComparer.OrdinalIgnoreCase)
          .ToList();

        using var writer = new StreamWriter(output.FullName, append: false);

        if (!string.IsNullOrWhiteSpace(author))
            writer.WriteLine($"// Author: {author}");

        foreach (var file in files)
        {
            if (note)
                writer.WriteLine($"// Source: {Path.GetRelativePath(root, file)}");

            string[] lines;
            try
            {
                lines = File.ReadAllLines(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"Error: could not read the source file '{Path.GetRelativePath(root, file)}': {ex.Message}");
                return 1;
            }

            foreach (var line in lines)
            {
                if (removeEmpty && string.IsNullOrWhiteSpace(line)) continue;
                writer.WriteLine(line);
            }
            if (removeEmpty == false) writer.WriteLine();
        }
        Console.WriteLine($"Bundle created: {output.FullName}");
        return 0;
    }
    catch (DirectoryNotFoundException)
    {
        Console.Error.WriteLine("Error: the output folder does not exist.");
        return 1;
    }
    catch (UnauthorizedAccessException)
    {
        Console.Error.WriteLine("Error: no permission to write to this path.");
        return 1;
    }
    catch (IOException ex)
    {
        Console.Error.WriteLine($"Error: could not write the bundle file: {ex.Message}");
        return 1;
    }
});
#endregion


#region create-rsp
static string GetInput(string question, Func<string, bool> isValid, string errMessage)
{
    while (true)
    {
        Console.Write(question);
        var line = Console.ReadLine();
        if (line == null) throw new OperationCanceledException("Input closed.");
        var input = line.Trim();
        if (isValid(input)) return input;
        Console.WriteLine(errMessage);

    }

}
static bool AskYesNo(string question)
{
    var answer = GetInput($"{question}: (y/n)", s => s.ToLower() is "y" or "n", "Please type y or n.");
    return answer.ToLower() == "y";
}
static string SelectOption(string question, string[] options)
{
    Console.WriteLine(question);
    for (int i = 0; i < options.Length; i++)
        Console.WriteLine($"  {i + 1}. {options[i]}");

    var answer = GetInput("Choose a number",
        s => int.TryParse(s, out var n) && n >= 1 && n <= options.Length,
        "Invalid choice.");
    return options[int.Parse(answer) - 1];
}
var rspOutputOption = new Option<FileInfo>("--output", "-o")
{
    Description = "path of the rsp file",
    DefaultValueFactory = _ => new FileInfo("bundle.rsp")

};
var createRspCommand = new Command("create-rsp", "create rsp with the desired definitions")
{
    rspOutputOption,
};

createRspCommand.SetAction(parseResult =>
{
    FileInfo rspOutput = parseResult.GetValue(rspOutputOption)!;
    if (Directory.Exists(rspOutput.FullName))
    {
        Console.Error.WriteLine($"Error: '{rspOutput.FullName}' is a folder. Please provide a file name for the rsp file.");
        return 1;
    }
    try
    {
        if (File.Exists(rspOutput.FullName) && !AskYesNo($"file in directory {rspOutput.FullName} already exists. Overwrite?"))
        {
            Console.WriteLine("Cancelled.");
            return 0;
        }
        var language = GetInput("which languages do you want to collect? press the names with a space between each 2 of them.",
             list => list.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 0 &&
                     list.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(l => supportedLanguages.Keys.Append("all").Contains(l.ToLower())),
             $"Invalid language. Supported languages: {string.Join(", ", supportedLanguages.Keys)}, all").ToLower();
        var bundleOutput = GetInput(
        "Enter the bundle file path: ",
        p =>
        {
            try
            {
                string fullPath = Path.GetFullPath(p);

                return !string.IsNullOrWhiteSpace(p)
                       && !Directory.Exists(fullPath)
                       && Directory.Exists(Path.GetDirectoryName(fullPath));
            }
            catch
            {
                return false;
            }
        },
        "Invalid path. Please try again.");
        var note = AskYesNo("note?");
        var sort = SelectOption("select sort method:", ["Alphabetic", "CodeType"]);
        var removeEmpty = AskYesNo("remove empty lines?");
        var isAuthor = AskYesNo("write author name?");
        var author = "";
        if (isAuthor)
            author = GetInput("enter the author name", s => !string.IsNullOrWhiteSpace(s), "no name was entered.try again");

        var ops = new List<string> {$"bundle" ,
                                $"--output \"{bundleOutput}\" ",
                                $"--language {language}" ,
                                $"--sort {sort} ",
                                };

        if (note) ops.Add("--note ");
        if (removeEmpty) ops.Add("-r ");
        if (isAuthor) ops.Add($"--author \"{author}\" ");

        if (File.Exists(bundleOutput) && !AskYesNo($"file in directory {bundleOutput} already exists. Overwrite?"))
        {
            Console.WriteLine("Cancelled.");
            return 0;
        }



        rspOutput.Directory?.Create();
        File.WriteAllLines(rspOutput.FullName, ops);
        Console.WriteLine($"Saved to {rspOutput.FullName}");
        return 0;
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"Could not save file: {ex.Message}");
        return 1;
    }
    catch (OperationCanceledException)
    {
        Console.Error.WriteLine("Input closed. Cancelled.");
        return 1;
    }



});

var rootCommand = new RootCommand("root command for file bundler CLI")
{
    bundleCommand,createRspCommand
};
#endregion


return await rootCommand.Parse(args).InvokeAsync();
