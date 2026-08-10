using System.IO.Compression;
using System.Reflection.Metadata;

var pdbPath = @"D:\Desktop\Kwy\KwyTemplate.App\bin\Debug\net8.0-windows\KwyTemplate.App.pdb";
var outPath = @"D:\Desktop\Kwy\KwyTemplate.App\ViewModels\HomeViewModel.cs";
var embeddedSourceGuid = new Guid("0E8A571B-6926-466E-B4AD-8AB04611F5FE");
using var stream = File.OpenRead(pdbPath);
using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
var reader = provider.GetMetadataReader();
foreach (var documentHandle in reader.Documents)
{
    var document = reader.GetDocument(documentHandle);
    var name = reader.GetString(document.Name);
    if (!name.EndsWith("HomeViewModel.cs", StringComparison.OrdinalIgnoreCase))
    {
        continue;
    }

    foreach (var cdiHandle in reader.GetCustomDebugInformation(documentHandle))
    {
        var cdi = reader.GetCustomDebugInformation(cdiHandle);
        if (reader.GetGuid(cdi.Kind) != embeddedSourceGuid)
        {
            continue;
        }

        var bytes = reader.GetBlobBytes(cdi.Value);
        var uncompressedSize = BitConverter.ToInt32(bytes, 0);
        byte[] sourceBytes;
        if (uncompressedSize == 0)
        {
            sourceBytes = bytes.Skip(4).ToArray();
        }
        else
        {
            using var compressed = new MemoryStream(bytes, 4, bytes.Length - 4);
            using var deflate = new DeflateStream(compressed, CompressionMode.Decompress);
            using var output = new MemoryStream(uncompressedSize);
            deflate.CopyTo(output);
            sourceBytes = output.ToArray();
        }

        File.WriteAllBytes(outPath, sourceBytes);
        Console.WriteLine($"Restored {sourceBytes.Length} bytes from embedded source: {name}");
        return;
    }

    Console.WriteLine($"Document found but no embedded source: {name}");
    return;
}
Console.WriteLine("HomeViewModel embedded source not found.");
