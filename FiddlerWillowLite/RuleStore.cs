using System;
using System.IO;
using System.Runtime.Serialization.Json;

namespace FiddlerWillowLite
{
    /// <summary>把项目与规则保存到 %APPDATA%\FiddlerWillowLite\config.json。</summary>
    internal static class RuleStore
    {
        private static readonly string DirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FiddlerWillowLite");

        internal static string ConfigPath
        {
            get { return Path.Combine(DirectoryPath, "config.json"); }
        }

        internal static WillowConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    return new WillowConfig();
                }

                var serializer = new DataContractJsonSerializer(typeof(WillowConfig));
                using (var stream = new FileStream(ConfigPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    return serializer.ReadObject(stream) as WillowConfig ?? new WillowConfig();
                }
            }
            catch
            {
                // 配置损坏时不要阻止 Fiddler 启动。
                return new WillowConfig();
            }
        }

        internal static void Save(WillowConfig config)
        {
            if (config == null)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(DirectoryPath);
                var serializer = new DataContractJsonSerializer(typeof(WillowConfig));
                var tempPath = ConfigPath + ".tmp";

                using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    serializer.WriteObject(stream, config);
                }

                File.Copy(tempPath, ConfigPath, true);
                File.Delete(tempPath);
            }
            catch
            {
                // 保存失败时保留内存中的配置，避免打断抓包。
            }
        }
    }
}
