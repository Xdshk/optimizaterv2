using System;
using System.IO;
using System.IO.Compression;

string publishDir = @"C:\Users\Burov\Downloads\majestic\GTA5Optimizer\publish";
string outputDir = @"C:\Users\Burov\Downloads\majestic\Output";
string zipName = "GTA5Optimizer-v2.0.0-portable.zip";
string zipPath = Path.Combine(outputDir, zipName);

if (!Directory.Exists(outputDir))
    Directory.CreateDirectory(outputDir);

var files = Directory.GetFiles(publishDir, "*", SearchOption.AllDirectories);
Console.WriteLine($"Files to archive: {files.Length}");

long totalSize = 0;
foreach (var f in files)
    totalSize += new FileInfo(f).Length;
Console.WriteLine($"Uncompressed size: {totalSize / 1024.0 / 1024.0:F2} MB");

if (File.Exists(zipPath))
    File.Delete(zipPath);

ZipFile.CreateFromDirectory(publishDir, zipPath, CompressionLevel.Optimal, false);

var zipInfo = new FileInfo(zipPath);
Console.WriteLine($"\nSUCCESS: {zipPath}");
Console.WriteLine($"Archive size: {zipInfo.Length / 1024.0 / 1024.0:F2} MB");
