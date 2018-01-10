using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Pocket;
using static Pocket.Logger<WorkspaceServer.Servers.OmniSharp.OmniSharp>;

namespace WorkspaceServer.Servers.OmniSharp
{
    internal static class OmniSharp
    {
        private static readonly DirectoryInfo _omniSharpInstallFolder;
        private static readonly FileInfo _omniSharpExe;
        private static readonly FileInfo _omniSharpRunScript;

        static OmniSharp()
        {
            var environmentVariable = Environment.GetEnvironmentVariable("TRYDOTNET_OMNISHARP_PATH");

            var omniSharpInstallPath =
                environmentVariable ??
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".trydotnet",
                    "omnisharp");

            _omniSharpInstallFolder = new DirectoryInfo(omniSharpInstallPath);

            _omniSharpRunScript = new FileInfo(
                Path.Combine(
                    _omniSharpInstallFolder.FullName,
                    "run"));

            _omniSharpExe = new FileInfo(
                Path.Combine(
                    _omniSharpInstallFolder.FullName,
                    "OmniSharp.exe"));
        }

        public static FileInfo GetPath()
        {
            var fileInfo = EnsureInstalledOrAcquire() ??
                           throw new OmniSharpNotFoundException("Failed to locate or acquire OmniSharp.");

            Log.Info("Using OmniSharp at {path}", fileInfo);

            return fileInfo;
        }

        private static FileInfo EnsureInstalledOrAcquire()
        {
            using (Log.OnEnterAndExit())
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return AcquireAndExtractWithZip("omnisharp-win-x64.zip");
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return AcquireAndExtractWithTar("omnisharp-linux-x64.tar.gz");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return AcquireAndExtractWithTar("omnisharp-osx.tar.gz");
                }

                throw new InvalidOperationException("BeOS? You go dawg.");
            }
        }

        private static FileInfo AcquireAndExtractWithTar(string file)
        {
            using (var operation = Log.OnEnterAndConfirmOnExit())
            {
                if (!_omniSharpRunScript.Exists)
                {
                    var downloadUri = new Uri($@"https://github.com/OmniSharp/omnisharp-roslyn/releases/download/v1.27.2/{file}");

                    operation.Info("OmniSharp not found at {path}. Downloading from {uri}.", _omniSharpRunScript, downloadUri);

                    var targzFile = Download(downloadUri);
                    _omniSharpInstallFolder.Create();

                    CommandLine.Execute(
                        "tar",
                        $"xvf {targzFile.FullName} -C {_omniSharpInstallFolder}");

                    _omniSharpRunScript.Refresh();

                    if (!_omniSharpRunScript.Exists)
                    {
                        return null;
                    }
                }

                operation.Succeed();

                return _omniSharpRunScript;
            }
        }

        private static FileInfo AcquireAndExtractWithZip(string file)
        {
            using (var operation = Log.OnEnterAndConfirmOnExit())
            {
                if (!_omniSharpExe.Exists)
                {
                    var zipFile = Download(new Uri($@"https://github.com/OmniSharp/omnisharp-roslyn/releases/download/v1.27.2/{file}"));

                    using (var stream = zipFile.OpenRead())
                    using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                    {
                        archive.ExtractToDirectory(_omniSharpInstallFolder.FullName);
                    }

                    _omniSharpExe.Refresh();

                    if (!_omniSharpExe.Exists)
                    {
                        return null;
                    }
                }

                operation.Succeed();

                return _omniSharpExe;
            }
        }

        private static FileInfo Download(Uri uri)
        {
            return Task.Run(async () =>
            {
                var fileToWriteTo = Path.GetTempFileName();

                using (var httpClient = new HttpClient())
                using (var response = await httpClient.GetStreamAsync(uri))
                using (Stream streamToWriteTo = File.Open(fileToWriteTo, FileMode.Create))
                {
                    await response.CopyToAsync(streamToWriteTo);
                }

                return new FileInfo(fileToWriteTo);
            }).Result;
        }
    }
}
