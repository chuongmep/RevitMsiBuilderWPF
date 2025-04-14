using RevitMsiBuilder.Models;

namespace RevitMsiBuilder.Services;

public interface IMsiBuilder
{
    string GenerateVersionString();
    string BuildMsi(AddinFile addinFile, IEnumerable<string> revitVersions, BuildConfiguration config);
    void CompressFile(string filePath, string outputFilePath, int compressionLevel = 9);
}