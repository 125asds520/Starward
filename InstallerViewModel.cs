using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Starward.Installer
{
    /// <summary>
    /// 安装器视图模型
    /// </summary>
    public partial class InstallerViewModel : ObservableObject
    {
        private readonly InstallerService _installerService = new InstallerService();

        [ObservableProperty]
        private string _statusMessage = "就绪";

        [ObservableProperty]
        private double _progressValue;

        [ObservableProperty]
        private bool _isInstallButtonEnabled = true;

        [ObservableProperty]
        private bool _isUninstallButtonEnabled;

        [ObservableProperty]
        private string _installedVersion = "未安装";

        [ObservableProperty]
        private ObservableCollection<string> _logMessages = new ObservableCollection<string>();

        public InstallerViewModel()
        {
            CheckInstallationStatus();
        }

        /// <summary>
        /// 检查安装状态
        /// </summary>
        private void CheckInstallationStatus()
        {
            if (_installerService.IsAppInstalled())
            {
                InstalledVersion = _installerService.GetInstalledVersion();
                IsInstallButtonEnabled = false;
                IsUninstallButtonEnabled = true;
                LogMessage($"检测到已安装版本: {InstalledVersion}");
            }
            else
            {
                InstalledVersion = "未安装";
                IsInstallButtonEnabled = true;
                IsUninstallButtonEnabled = false;
                LogMessage("未检测到安装的应用");
            }
        }

        /// <summary>
        /// 记录日志消息
        /// </summary>
        private void LogMessage(string message)
        {
            StatusMessage = message;
            LogMessages.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        /// <summary>
        /// 安装命令
        /// </summary>
        [RelayCommand]
        private async Task InstallAsync()
        {
            try
            {
                IsInstallButtonEnabled = false;
                LogMessage("开始安装...");

                // 获取最新版本的下载URL
                var downloadUrl = await GetLatestReleaseDownloadUrl();
                LogMessage($"获取到下载地址: {downloadUrl}");

                // 下载安装包
                var progress = new Progress<double>(p => ProgressValue = p);
                LogMessage("正在下载安装包...");
                var installerPath = await _installerService.DownloadInstaller(downloadUrl, progress);
                LogMessage($"安装包下载完成: {installerPath}");

                // 安装应用
                var installProgress = new Progress<string>(msg => LogMessage(msg));
                await _installerService.InstallApp(installerPath, installProgress);

                // 更新安装状态
                CheckInstallationStatus();
                LogMessage("安装成功！");
            }
            catch (Exception ex)
            {
                LogMessage($"安装过程中发生错误: {ex.Message}");
            }
            finally
            {
                IsInstallButtonEnabled = !_installerService.IsAppInstalled();
                IsUninstallButtonEnabled = _installerService.IsAppInstalled();
            }
        }

        /// <summary>
        /// 卸载命令
        /// </summary>
        [RelayCommand]
        private async Task UninstallAsync()
        {
            try
            {
                IsUninstallButtonEnabled = false;
                LogMessage("开始卸载...");

                var uninstallProgress = new Progress<string>(msg => LogMessage(msg));
                await _installerService.UninstallApp(uninstallProgress);

                // 更新安装状态
                CheckInstallationStatus();
                LogMessage("卸载成功！");
            }
            catch (Exception ex)
            {
                LogMessage($"卸载过程中发生错误: {ex.Message}");
            }
            finally
            {
                IsInstallButtonEnabled = !_installerService.IsAppInstalled();
                IsUninstallButtonEnabled = _installerService.IsAppInstalled();
            }
        }

        /// <summary>
        /// 获取最新版本的下载URL
        /// </summary>
        private async Task<string> GetLatestReleaseDownloadUrl()
        {
            // 实际应用中应该从GitHub API获取最新版本信息
            // 这里简化处理，返回一个示例URL
            await Task.Delay(1000); // 模拟网络延迟
            return "https://github.com/Scighost/Starward/releases/download/v1.0.0/Starward_x64.zip";
        }
    }
}  