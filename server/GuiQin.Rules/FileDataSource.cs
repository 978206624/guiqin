using System;
using System.IO;

namespace GuiQin
{
    // 服务端和回放工具从同一个数据目录读配置。
    public sealed class FileDataSource : IDataSource
    {
        readonly string directory;

        public FileDataSource(string directory)
        {
            this.directory = Path.GetFullPath(directory);
        }

        public string ReadText(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || Path.GetFileName(name) != name)
                throw new ArgumentException("需要不带路径的逻辑文件名", nameof(name));
            var file = Path.Combine(directory, name + ".json");
            try { return File.ReadAllText(file); }
            catch (FileNotFoundException) { return null; }
        }
    }
}
