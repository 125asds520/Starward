using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Starward.Installer
{
    /// <summary>
    /// 安装服务，处理应用的安装、更新和卸载
    /// </summary>
    public class InstallerService
    {
        private const string AppName = "Starward";
        private const string RegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{APP_GUID}";
        private readonly HttpClient _httpClient;
        private readonly string _installPath;

        public InstallerService()
        {
            _httpClient = new HttpClient();
            _installPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), 
                AppName);
        }

        /// <summary>
        /// 检查应用是否已安装
        /// </summary>
        public bool IsAppInstalled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKey);
                return key != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取已安装应用的版本
        /// </summary>
        public string GetInstalledVersion()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKey);
                return key?.GetValue("DisplayVersion")?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// 下载安装包
        /// </summary>
        public async Task<string> DownloadInstaller(string downloadUrl, IProgress<double> progress = null)
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"{AppName}_Installer_{Guid.NewGuid()}.zip");
            
            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var contentLength = response.Content.Headers.ContentLength;
            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write);

            var buffer = new byte[8192];
            var totalBytesRead = 0L;
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalBytesRead += bytesRead;

                if (contentLength.HasValue)
                {
                    progress?.Report((double)totalBytesRead / contentLength.Value * 100);
                }
            }

            return tempFile;
        }

        /// <summary>
        /// 安装应用
        /// </summary>
        public async Task InstallApp(string installerPath, IProgress<string> progress = null)
        {
            try
            {
                progress?.Report("准备安装...");
                
                // 创建安装目录
                if (!Directory.Exists(_installPath))
                {
                    Directory.CreateDirectory(_installPath);
                }

                progress?.Report("正在解压文件...");
                
                // 解压安装包
                await ExtractZipFile(installerPath, _installPath);

                progress?.Report("正在注册应用...");
                
                // 注册到系统
                RegisterAppInRegistry();

                progress?.Report("安装完成！");
            }
            catch (Exception ex)
            {
                progress?.Report($"安装失败: {ex.Message}");
                throw;
            }
            finally
            {
                // 清理临时文件
                if (File.Exists(installerPath))
                {
                    File.Delete(installerPath);
                }
            }
        }

        /// <summary>
        /// 卸载应用
        /// </summary>
        public async Task UninstallApp(IProgress<string> progress = null)
        {
            try
            {
                progress?.Report("准备卸载...");
                
                // 停止运行中的应用
                KillRunningProcesses();

                progress?.Report("正在移除注册表项...");
                
                // 移除注册表项
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
                key?.DeleteSubKeyTree();

                progress?.Report("正在删除文件...");
                
                // 删除安装目录
                if (Directory.Exists(_installPath))
                {
                    Directory.Delete(_installPath, true);
                }

                progress?.Report("卸载完成！");
            }
            catch (Exception ex)
            {
                progress?.Report($"卸载失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 提取ZIP文件
        /// </summary>
        private async Task ExtractZipFile(string zipPath, string extractPath)
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(zipPath);
            
            var entries = archive.Entries;
            var totalEntries = entries.Count;
            var processedEntries = 0;

            foreach (var entry in entries)
            {
                var destinationPath = Path.Combine(extractPath, entry.FullName);
                
                if (entry.Name == "")
                {
                    // 是目录
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                }
                else
                {
                    // 是文件
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                    entry.ExtractToFile(destinationPath, true);
                }

                processedEntries++;
                // 可以在这里报告进度
            }
        }

        /// <summary>
        /// 在注册表中注册应用
        /// </summary>
        private void RegisterAppInRegistry()
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryKey);
            if (key != null)
            {
                key.SetValue("DisplayName", AppName);
                key.SetValue("DisplayVersion", "1.0.0"); // 应从应用获取实际版本
                key.SetValue("Publisher", "Scighost");
                key.SetValue("InstallLocation", _installPath);
                key.SetValue("UninstallString", Path.Combine(_installPath, "uninstall.exe"));
                key.SetValue("DisplayIcon", Path.Combine(_installPath, "Starward.exe"));
                key.SetValue("NoModify", 1);
                key.SetValue("NoRepair", 1);
            }
        }

        /// <summary>
        /// 终止运行中的应用进程
        /// </summary>
        private void KillRunningProcesses()
        {
            foreach (var process in Process.GetProcessesByName("Starward"))
            {
                try
                {
                    process.Kill();
                    process.WaitForExit();
                }
                catch { }
            }
        }
    }
}  