using System.Text;
using Ws2Explorer.Compiler;

namespace Ws2Explorer;

public class Program {
    private static async Task Main(string[] args) {
        var program = new Program();
        program.OnOutput += (_, text) => Console.WriteLine(text);
        await program.Run(args, CancellationToken.None);
    }

    public static bool CommandWillWrite(string command) {
        return new[] { "cp", "rm", "swap", "rename", "compile", "insert_text" }.Contains(command);
    }

    private static (string path, string name) ParseFolderAndFilename(string path) {
        path = path.Replace('\\', '/');
        var index = path.LastIndexOf('/');
        if (index == -1) {
            return (".", path);
        } else {
            return (path[..index], path[(index + 1)..]);
        }
    }

    public event EventHandler<string> OnOutput = delegate { };
    private readonly Progress<(int id, string task, float t)>? progress;

    public Program(bool useProgress = false) {
        if (useProgress) {
            progress = new Progress<(int id, string task, float t)>(p => {
                var (id, task, t) = p;
                WriteOutput("\r" + new string(' ', 50));
                WriteOutput($"\r{task}: {t * 100}%");
            });
        }
    }

    private void WriteOutput(string s) {
        OnOutput(this, s);
    }

    public async Task Run(string[] args, CancellationToken ct) {
        if (args.Length == 0) {
            PrintHelp();
            return;
        }

        try {
            var op = args[0];
            switch (op) {
                case "help": PrintHelp();  break;
                case "pwd": PrintWorkingDirectory(); break;
                case "cd": ChangeWorkingDirectory(args); break;
                case "ls": await ListFiles(args, ct); break;
                case "cp": await CopyFiles(args, ct); break;
                case "rm": await DeleteFile(args, ct); break;
                case "swap": await SwapFiles(args, ct); break;
                case "rename": await RenameFile(args, ct); break;
                case "version": await PrintWs2Version(args, ct); break;
                case "extract_text": await ExtractText(args, ct); break;
                case "insert_text": await InsertText(args, ct); break;
                case "extract_text_folder": await ExtractTextFolder(args, ct); break;
                case "insert_text_folder": await InsertTextFolder(args, ct); break;
                case "decompile": await DecompileFile(args, ct); break;
                case "compile": await CompileFile(args, ct); break;
                case "decompile_folder": await DecompileFolder(args, ct); break;
                case "compile_folder": await CompileFolder(args, ct); break;
                default: throw new ArgumentException($"Unknown command {op}.");
            }
        } catch (Exception ex) {
            WriteOutput(ex.ToString());
        }
    }

    private void PrintHelp() {
        WriteOutput("Usage:");
        WriteOutput("  help                                  Show this help text.");
        WriteOutput("  pwd                                   Print current working directory.");
        WriteOutput("  cd [path]                             Change current working directory (GUI only).");
        WriteOutput("  ls [path]                             List files in directory.");
        WriteOutput("");
        WriteOutput("  cp <src> <dst>                        Copy files.");
        WriteOutput("  rm <path>                             Delete files.");
        WriteOutput("  swap <folder> <file1> <file2>         Swap the contents of two files in the same folder.");
        WriteOutput("  rename <path> <new_filename>          Rename files.");
        WriteOutput("");
        WriteOutput("  version <ws2_file>                    Print the version of a ws2 file.");
        WriteOutput("  extract_text <ws2> <output_path>      Extract text from ws2 files.");
        WriteOutput("  insert_text <text> <output_path>      Insert extracted text back into existing ws2 files.");
        WriteOutput("  extract_text_folder <ws2_folder> <txt_folder>      Extract text from ws2 files.");
        WriteOutput("  insert_text_folder <txt_folder> <ws2_folder>      Insert extracted text back into existing ws2 files.");
        WriteOutput("  decompile <ws2_file> <output_path>    Decompile a ws2 file.");
        WriteOutput("  compile <code_file> <output_path> <v1|v2|v3|v4> [-e]");
        WriteOutput("                                        Compile a ws2 file using the given ws2 version (optionally with encryption).");
    }

    private void PrintWorkingDirectory() {
        WriteOutput(Environment.CurrentDirectory);
    }

    private void ChangeWorkingDirectory(string[] args) {
        if (args.Length < 2) {
            Environment.CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        } else {
            Environment.CurrentDirectory = args[1];
        }
        WriteOutput(Environment.CurrentDirectory);
    }

    private async Task ListFiles(string[] args, CancellationToken ct) {
        var path = args.Length < 2 ? "." : args[1];
        var folder = await Folder.GetFolder(path, ct, progress);

        var children = folder.ListChildren();
        var padLen = children.Max(c => c.Name.Length) + 4;
        var indexPadLen = children.Count.ToString().Length;
        WriteOutput($"{new string(' ', indexPadLen)}  Name{new string(' ', padLen - 4)}Size");
        for (int i = 0; i < children.Count; i++) {
            var child = children[i];
            WriteOutput($"{i.ToString().PadLeft(indexPadLen, '0')}: {child.Name.PadRight(padLen)}{child.Length}");
        }
    }

    private async Task CopyFiles(string[] args, CancellationToken ct) {
        var srcPath = args[1];
        var dstPath = args[2];
        var (dstFolder, dstName) = ParseFolderAndFilename(dstPath);
        var dst = await Folder.GetFolder(dstFolder, ct, progress);
        await dst.CopyFiles(new[] { srcPath }, new[] { dstName }, _ => true, ct, progress);
    }

    private async Task DeleteFile(string[] args, CancellationToken ct) {
        var path = args[1];
        var (folderName, name) = ParseFolderAndFilename(path);
        var folder = await Folder.GetFolder(folderName, ct, progress);
        await folder.DeleteChildren(new[] { name }, ct, progress);
    }

    private async Task SwapFiles(string[] args, CancellationToken ct) {
        var folderName = args[1];
        var file1 = args[2];
        var file2 = args[3];
        var folder = await Folder.GetFolder(folderName, ct, progress);
        await folder.SwapChildren(file1, file2, ct, progress);
    }

    private async Task RenameFile(string[] args, CancellationToken ct) {
        var src = args[1];
        var newName = args[2];
        var (folderName, name) = ParseFolderAndFilename(src);
        var folder = await Folder.GetFolder(folderName, ct, progress);
        await folder.RenameChild(name, newName, ct, progress);
    }

    private async Task DecompileFile(string[] args, CancellationToken ct) {
        var src = args[1];
        var dst = args[2];
        var (dstFolderName, dstName) = ParseFolderAndFilename(dst);
        var dstFolder = await Folder.GetFolder(dstFolderName, ct, progress);
        await dstFolder.CopyFiles(new[] { Path.Combine(src, "opcodes.json") }, new[] { dstName }, _ => true, ct, progress);
    }

    private async Task DecompileFolder(string[] args, CancellationToken ct)
    {
        var src = args[1];
        var dst = args[2];
        var srcFolder = await Folder.GetFolder(src, ct, progress);
        var children = srcFolder.ListChildren();
        foreach (var child in children)
        {
            var tmpArgs = GetArgs(args, src, child.Name, dst);
            if (tmpArgs == null) continue;
            await DecompileFile(tmpArgs, ct);
            WriteOutput("Decompile: " + child.Name);
        }
        WriteOutput("Decompile end!");
    }

    private async Task PrintWs2Version(string[] args, CancellationToken ct) {
        var src = args[1];
        var file = await Folder.GetFile(src, ct, progress);
        if (file is not Ws2File ws2) {
            throw new InvalidDataException($"File {src} is not a ws2 file.");
        }

        WriteOutput(ws2.Ws2Version.ToString());
    }

    private async Task CompileFile(string[] args, CancellationToken ct) {
        var src = args[1];
        var dst = args[2];
        var version = args[3] switch {
            "v1" => Ws2Version.V1,
            "v2" => Ws2Version.V2,
            "v3" => Ws2Version.V3,
            "v4" => Ws2Version.V4,
            _ => throw new ArgumentException($"Unknown version {args[1]}.")
        };
        var encrypt = args.Length > 4 && args[4] == "-e";

        var file = await Folder.GetFile(src, ct, progress);
        if (file is not BinaryFile srcFile) {
            throw new InvalidDataException($"File {src} is not a regular file.");
        }

        var json = srcFile.GetText(Encoding.UTF8);
        var opcodes = Ws2Compiler.DeserializeFromJson(json, version);
        var compiled = Ws2Compiler.Compile(opcodes, version, encrypt);

        var tempOutputPath = Path.GetTempFileName();
        await using (var tempOutput = File.Create(tempOutputPath)) {
            await compiled.CopyToAsync(tempOutput, ct);
        }

        await CopyFiles(new[] { "cp", tempOutputPath, dst }, ct);
    }

    private async Task CompileFolder(string[] args, CancellationToken ct)
    {
        var src = args[1];
        var dst = args[2];
        var srcFolder = await Folder.GetFolder(src, ct, progress);
        var children = srcFolder.ListChildren();
        foreach (var child in children)
        {
            var tmpArgs = GetArgs(args, src, child.Name, dst);
            if (tmpArgs == null) continue;
            await CompileFile(tmpArgs, ct);
            WriteOutput("Compile: " + child.Name);
        }
        WriteOutput("Compile end!");
    }

    private async Task ExtractText(string[] args, CancellationToken ct) {
        var src = args[1];
        var dst = args[2];
        var (dstFolderName, dstName) = ParseFolderAndFilename(dst);
        var dstFolder = await Folder.GetFolder(dstFolderName, ct, progress);
        await dstFolder.CopyFiles(new[] { Path.Combine(src, "text.txt") }, new[] { dstName }, _ => true, ct, progress);
    }

    private async Task InsertText(string[] args, CancellationToken ct) {
        var src = args[1];
        var dst = args[2];
        var dstFolder = await Folder.GetFolder(dst, ct, progress);
        await dstFolder.CopyFiles(new[] { src }, new[] { "text.txt" }, _ => true, ct, progress);
    }

    private async Task ExtractTextFolder(string[] args, CancellationToken ct)
    {
        var src = args[1];
        var dst = args[2];
        var srcFolder = await Folder.GetFolder(src, ct, progress);
        var children = srcFolder.ListChildren();
        foreach (var child in children)
        {
            var tmpArgs = GetArgs(args, src, child.Name, dst);
            if (tmpArgs == null) continue;
            await ExtractText(tmpArgs, ct);
            WriteOutput("Extract: " + child.Name);
        }
        WriteOutput("Extract end!");
    }

    private async Task InsertTextFolder(string[] args, CancellationToken ct)
    {
        var src = args[1];
        var dst = args[2];
        var srcFolder = await Folder.GetFolder(src, ct, progress);
        var children = srcFolder.ListChildren();
        foreach (var child in children)
        {
            var tmpArgs = GetArgs(args, src, child.Name, dst);
            if (tmpArgs == null) continue;
            await InsertText(tmpArgs, ct);
            WriteOutput("Insert: " + child.Name);
        }
        WriteOutput("Insert end!");
    }

    private readonly Dictionary<string, string[]> FilenameConfig = new Dictionary<string, string[]> // opcode, [srcPostfix, dstPostfix]
    {
        {"extract_text_folder", new string[] { ".ws2", ".txt" } },
        {"insert_text_folder", new string[] { ".txt", ".ws2" }},
        {"decompile_folder", new string[] { ".ws2", ".json" }},
        {"compile_folder", new string[] { ".json", ".ws2" }},
    };
    private string[]? GetArgs(string[] args, string srcFolder, string srcName, string dstFolder)
    {
        var postfixList = FilenameConfig[args[0]];
        if (!srcName.EndsWith(postfixList[0])) return null;
        var dstName = srcName.Substring(0, srcName.LastIndexOf(".")) + postfixList[1];
        var tmpArgs = new List<string> { args[0], Path.Combine(srcFolder, srcName), Path.Combine(dstFolder, dstName) };
        if (args.Length > 3) tmpArgs.Add(args[3]);
        if (args.Length > 4) tmpArgs.Add(args[4]);
        return tmpArgs.ToArray();
    }
}
