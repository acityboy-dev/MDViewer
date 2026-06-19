using System.Drawing.Imaging;
using Svg;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: MarkFlow.IconBuilder <source.svg> <output-directory>");
    return 1;
}

var sourcePath = Path.GetFullPath(args[0]);
var outputDirectory = Path.GetFullPath(args[1]);
if (!File.Exists(sourcePath))
{
    Console.Error.WriteLine($"SVG source was not found: {sourcePath}");
    return 2;
}

Directory.CreateDirectory(outputDirectory);
var document = SvgDocument.Open<SvgDocument>(sourcePath);
var iconSizes = new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
var iconFrames = new List<(int Size, byte[] Data)>();

foreach (var size in iconSizes)
{
    var outputPath = Path.Combine(outputDirectory, $"MarkFlow-{size}.png");
    RenderPng(document, size, outputPath);
    iconFrames.Add((size, File.ReadAllBytes(outputPath)));
}

RenderPng(document, 1024, Path.Combine(outputDirectory, "MarkFlow.png"));
WriteIco(Path.Combine(outputDirectory, "MarkFlow.ico"), iconFrames);
return 0;

static void RenderPng(SvgDocument document, int size, string outputPath)
{
    using var bitmap = document.Draw(size, size);
    bitmap.SetResolution(96, 96);
    bitmap.Save(outputPath, ImageFormat.Png);
}

static void WriteIco(string outputPath, IReadOnlyList<(int Size, byte[] Data)> frames)
{
    using var stream = File.Create(outputPath);
    using var writer = new BinaryWriter(stream);

    writer.Write((ushort)0);
    writer.Write((ushort)1);
    writer.Write((ushort)frames.Count);

    var dataOffset = 6 + (16 * frames.Count);
    foreach (var frame in frames)
    {
        writer.Write((byte)(frame.Size >= 256 ? 0 : frame.Size));
        writer.Write((byte)(frame.Size >= 256 ? 0 : frame.Size));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(frame.Data.Length);
        writer.Write(dataOffset);
        dataOffset += frame.Data.Length;
    }

    foreach (var frame in frames)
    {
        writer.Write(frame.Data);
    }
}
