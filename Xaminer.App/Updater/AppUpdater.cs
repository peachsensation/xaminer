using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Xaminer.App.APIS;

namespace Xaminer.App.Updater
{
    public sealed class AppUpdater
    {
        private static readonly string[] s_separator = ["\r\n", "\n"];

        private (bool? IsOkResponse, GithubRelease LatestRelease) _latestRelease;

        private readonly Github _github;
        private readonly string _os = Environment.OSVersion.Platform switch
        {
            PlatformID.Win32S or PlatformID.Win32NT => "windows",
            _ => ""
        };
        private readonly string _arch = Enum.GetName(RuntimeInformation.OSArchitecture) ?? "";

        private string _platformArch => $"{_os}-{_arch}";

        public AppUpdater()
        {
            _github = new Github();
        }

        public async Task<FileInfo> PrepareUpdate(IProgress<int> progress, CancellationToken token)
        {
            progress.Report(1);

            var updateFile = await DownloadUpdate(progress, token) ?? throw new Exception(Strings.DownloadFailed);

            if (!await CompareHashWithRemote(updateFile, token))
                throw new Exception(Strings.HashCompareFailed);

            progress.Report(100);

            return updateFile;
        }

        public void ExecuteUpdate(FileInfo updateFile)
        {
            var processPath = Environment.ProcessPath;
            if (processPath is null)
                return;

            try
            {
                var script = s_script.Replace("\"", "\\\"");
                var pid = Environment.ProcessId;
                var orgPath = new FileInfo(processPath);
                var orgNewName = $"{orgPath.Name}.dead";
                var updatePath = updateFile;
                var updateNewName = orgPath.Name;

                script = string.Format(script, pid, orgPath.FullName, orgNewName, updatePath, updateNewName);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -Command \"{script}\"",
                    CreateNoWindow = true
                };

                using var process = new Process
                {
                    StartInfo = startInfo
                };
                process.Start();
            }
            catch
            {
                return;
            }
        }

        private async Task<FileInfo?> DownloadUpdate(IProgress<int> progress, CancellationToken token)
        {
            var latestRelease = await GetLatestRelease(token);
            if (latestRelease is null)
                return null;

            if (latestRelease.Assets.FirstOrDefault(x => x.Name.Contains(_platformArch, StringComparison.OrdinalIgnoreCase)) is not { } updateAsset)
                return null;

            try
            {
                using var updateStream = await _github.DownloadAssetAsStream(updateAsset);

                var updateFileName = new FileInfo(Path.Combine(AppContext.BaseDirectory, $"{updateAsset.Name}.update"));
                if (updateFileName.Exists)
                    updateFileName.Delete();


                await SaveToFileWithProgress(updateStream, updateFileName, updateAsset.Size, progress, token);

                return updateFileName;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task SaveToFileWithProgress(Stream stream, FileInfo filePath, long totalBytes, IProgress<int> progress, CancellationToken token)
        {
            var downloadedBytes = 0L;
            using var progressCts = new CancellationTokenSource();

            var progressTask = Task.Run(async () =>
            {
                while (!progressCts.Token.IsCancellationRequested)
                {
                    progress.Report((int)Math.Round((double)(100 * downloadedBytes) / totalBytes));
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }
            });

            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Options = FileOptions.WriteThrough | FileOptions.Asynchronous,
                BufferSize = 0,
                PreallocationSize = totalBytes
            };

            await using var updateFile = new FileStream(filePath.FullName, options);

            var buffer = ArrayPool<byte>.Shared.Rent(4096);
            try
            {
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(new Memory<byte>(buffer), token)) != 0)
                {
                    await updateFile.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), token);
                    downloadedBytes += bytesRead;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            progressCts.Cancel();
            await progressTask;
        }

        public async Task<GithubRelease?> GetLatestRelease(CancellationToken token)
        {
            if (_latestRelease.IsOkResponse == true)
                return _latestRelease.LatestRelease;

            if (_latestRelease.IsOkResponse == false)
                return null;

            try
            {
                _latestRelease = (true, await _github.GetLatestRelease(token));
                return _latestRelease.LatestRelease;
            }
            catch
            {
                _latestRelease.IsOkResponse = false;
                return null;
            }
        }

        private async Task<bool> CompareHashWithRemote(FileInfo file, CancellationToken token)
        {
            var localHash = await GetLocalHash(file, token);
            var remoteHash = await GetRemoteHash(token);

            if (localHash is null || remoteHash is null)
                return false;

            return string.Equals(localHash, remoteHash, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string?> GetLocalHash(FileInfo file, CancellationToken token)
        {
            await using var fileStream = file.OpenRead();

            if (fileStream.Length == 0)
                return null;

            var buffer = ArrayPool<byte>.Shared.Rent(4096);

            int bytesRead;
            using var hashAlgorithm = SHA256.Create();
            while ((bytesRead = await fileStream.ReadAsync(buffer, token)) != 0)
            {
                hashAlgorithm.TransformBlock(buffer, 0, bytesRead, buffer, 0);
            }
            hashAlgorithm.TransformFinalBlock(buffer, 0, bytesRead);

            ArrayPool<byte>.Shared.Return(buffer);

            if (hashAlgorithm.Hash is not { } fileHash)
                return null;

            return BitConverter.ToString(fileHash).Replace("-", string.Empty);
        }

        private async Task<string?> GetRemoteHash(CancellationToken token)
        {
            var latestRelease = await GetLatestRelease(token);
            if (latestRelease is null)
                return null;

            var hashesAsset = latestRelease.Assets.FirstOrDefault(x => x.Name == "hashes.sha256");
            if (hashesAsset is null)
                return null;

            using var hashesStream = new StreamReader(await _github.DownloadAssetAsStream(hashesAsset));
            var hashesText = await hashesStream.ReadToEndAsync(token);

            var hashes = hashesText.Split(s_separator, StringSplitOptions.RemoveEmptyEntries).ToList();

            var index = hashes.FindIndex(x => x.StartsWith(_platformArch, StringComparison.OrdinalIgnoreCase));

            return index != -1 ? hashes.ElementAt(index + 1) : null;
        }

        private const string s_script =
"""
Wait-Process -Id {0}

# Start-Sleep -Seconds 10

$orgPath = "{1}"
$orgNewName = "{2}"
$orgFileInfo = Rename-Item $orgPath -NewName $orgNewName -PassThru

$updatePath = "{3}"
$updateNewName = "{4}"
Rename-Item $updatePath -NewName $updateNewName

Remove-Item $orgFileInfo.FullName

Start-Process $orgPath
""";
    }
}
