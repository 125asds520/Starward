using System.Windows;

namespace Starward.Installer
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // 确保应用程序单实例运行
            if (!SingleInstance.EnsureSingleInstance("StarwardInstaller"))
            {
                Shutdown();
                return;
            }
            
            var mainWindow = new InstallerView();
            mainWindow.Show();
        }
    }

    /// <summary>
    /// 单实例运行辅助类
    /// </summary>
    public static class SingleInstance
    {
        private static Mutex _mutex;
        private static bool _isFirstInstance;
        private const string MutexName = "Global\\StarwardInstallerMutex";

        public static bool EnsureSingleInstance(string identifier)
        {
            _mutex = new Mutex(true, MutexName, out _isFirstInstance);
            return _isFirstInstance;
        }
    }
}  