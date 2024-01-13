﻿using System.Text;
using Ws2Explorer.Compiler;
using Ws2Explorer.Compiler.Opcodes;

namespace Ws2Explorer;

public class Ws2File : BinaryFile, IFolder {
    private const string DECOMPILATION_FILENAME = "opcodes.json";
    private const string TEXT_FILENAME = "text.txt";
    private const string NAMES_FILENAME = "names.txt";
    private const string CHOICES_FILENAME = "choices.txt";

    private List<IOpcode> opcodes;

    public Ws2Version Ws2Version { get; }
    public bool Encrypted { get; }
    public bool CanRenameChildren => false;

    public Ws2File(IFolder? parent, string name, BinaryStream stream) : base(parent, name, stream) {
        stream.Reset();
        opcodes = Ws2Compiler.Decompile(Stream.MemoryStream, out var version, out var encrypted);
        Ws2Version = version;
        Encrypted = encrypted;
    }

    public List<FileMetadata> ListChildren() {
        return new List<FileMetadata>() {
            new(DECOMPILATION_FILENAME, null),
            new(TEXT_FILENAME, null),
            new(NAMES_FILENAME, null),
            new(CHOICES_FILENAME, null),
        };
    }

    public Task<IFile> GetChild(string name, CancellationToken ct, ITaskProgress? progress) {
        Task<IFile> FilterText<Op>(Func<Op, IEnumerable<string>> selector, bool nonEmptyAndUnique = false) {
            var lines = opcodes.OfType<Op>().SelectMany(selector);
            if (nonEmptyAndUnique) {
                lines = lines.Where(line => line.Length > 0).Distinct();
            }
            var textBytes = Encoding.UTF8.GetBytes(string.Join('\n', lines));
            var textStream = new BinaryStream(textBytes, 0, textBytes.Length);
            return Task.FromResult<IFile>(new BinaryFile(this, name, textStream, PrettyNameEncoding.UTF8));
        }

        switch (name) {
            case DECOMPILATION_FILENAME:
                var json = Ws2Compiler.SerializeToJson(opcodes);
                var jsonBytes = Encoding.UTF8.GetBytes(json);
                var decompilation = new BinaryStream(jsonBytes, 0, jsonBytes.Length);
                return Task.FromResult<IFile>(new BinaryFile(this, name, decompilation, PrettyNameEncoding.UTF8));
            case TEXT_FILENAME:
                return FilterText<Op14_DisplayMessage>(op => new[] { op.Message });
            case NAMES_FILENAME:
                return FilterText<Op15_SetDisplayName>(op => new[] { op.CharacterName }, nonEmptyAndUnique: true);
            case CHOICES_FILENAME:
                return FilterText<Op0F_ShowChoice>(op => op.Choices.Select(c => c.Text));
            default:
                throw new FileNotFoundException(name);
        }
    }

    public Task NotifyChildChanged(string child, BinaryStream newData, CancellationToken ct, ITaskProgress? progress) {
        throw new InvalidOperationException("Should never be called");
    }

    public Task RenameChild(string from, string to, CancellationToken ct, ITaskProgress? progress) {
        throw new NotSupportedException("Rename is not supported for ws2 files");
    }

    public async Task CopyFiles(string[] fromFullPath, string[] to, Func<string, bool> prompt, CancellationToken ct, ITaskProgress? progress) {
        if (fromFullPath.Length != to.Length) {
            throw new ArgumentException("Argument length mismatch");
        }
        if (to.Length != 1) {
            throw new InvalidOperationException("Copying multiple files into ws2 file at once is not supported");
        }

        if (to[0] != DECOMPILATION_FILENAME &&
            to[0] != TEXT_FILENAME)
        {
            throw new InvalidOperationException("Copy destination is invalid");
        }

        var srcFile = await Folder.GetFile(fromFullPath[0], ct, progress);
        if (srcFile is not BinaryFile binaryFile) {
            throw new InvalidOperationException($"Copying folder {((IFolder)srcFile).FullPath} into ws2 file is not supported");
        }

        switch (to[0]) {
            case DECOMPILATION_FILENAME:
                UpdateDecompilation(binaryFile.Stream);
                break;
            case TEXT_FILENAME:
                UpdateText(binaryFile.Stream);
                break;
            default:
                throw new InvalidOperationException();
        }

        if (Parent != null) {
            await Parent.NotifyChildChanged(Name, Stream, ct, progress);
        }
    }

    private void UpdateDecompilation(BinaryStream newData) {
        var (data, start, len) = newData.RawBuffer;
        var json = Encoding.UTF8.GetString(data, start, len);

        var opcodes = Ws2Compiler.DeserializeFromJson(json, Ws2Version);
        RebuildStream(opcodes);
    }

    private void UpdateText(BinaryStream newData) {
        var textFile = AsText(null, "", newData);
        var newLines = textFile.GetText(textFile.SuggestedEncoding!)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        var oldLines = opcodes.OfType<Op14_DisplayMessage>().ToArray();

        if (newLines.Length != oldLines.Length) {
            throw new InvalidDataException($"Number of lines in text file does not match number of messages. Expected {oldLines.Length}, got {newLines.Length}");
        }

        foreach (var (oldLine, newLine) in oldLines.Zip(newLines)) {
            oldLine.Message = newLine;
        }
        RebuildStream(opcodes);
    }

    private void RebuildStream(List<IOpcode> opcodes) {
        Stream compiled;
        try {
            compiled = Ws2Compiler.Compile(opcodes, Ws2Version, Encrypted);
        } catch (Exception ex) {
            throw new InvalidDataException($"Compilation failed: ({ex.GetType().Name}) {ex.Message}");
        }

        var buffer = new byte[compiled.Length];
        Stream = new BinaryStream(buffer, 0, buffer.Length);
        compiled.Position = 0;
        compiled.CopyTo(Stream.MemoryStream);

        this.opcodes = opcodes;
    }

    public Task SwapChildren(string a, string b, CancellationToken ct, ITaskProgress? progress) {
        throw new NotSupportedException("Swap is not supported for ws2 files");
    }

    public Task DeleteChildren(string[] names, CancellationToken ct, ITaskProgress? progress) {
        throw new NotSupportedException("Delete is not supported for ws2 files");
    }
}