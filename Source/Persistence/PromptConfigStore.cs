using System;
using System.IO;
using Ustas.RimAI.Core.Storage;

namespace Ustas.RimAI.Communication.Relations.Persistence
{
    /// <summary>/// Dependencies: System.IO file APIs.
 /// Responsibility: centralize prompt-config file existence/read/write operations.
 ///</summary>
    internal sealed class PromptConfigStore
    {
        private readonly Func<string> configPathResolver;
        private readonly Action ensureDirectoryExists;

        public PromptConfigStore(Func<string> configPathResolver, Action ensureDirectoryExists)
        {
            this.configPathResolver = configPathResolver ?? throw new ArgumentNullException(nameof(configPathResolver));
            this.ensureDirectoryExists = ensureDirectoryExists;
        }

        public bool Exists()
        {
            string path = ResolvePath();
            return LocalStorage.Current.FileExists(path);
        }

        public string ReadAllText()
        {
            string path = ResolvePath();
            return LocalStorage.Current.ReadAllText(path);
        }

        public void WriteAllText(string content)
        {
            ensureDirectoryExists?.Invoke();
            WriteAllText(ResolvePath(), content);
        }

        public static bool FileExists(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && LocalStorage.Current.FileExists(path);
        }

        public static string ReadAllText(string path)
        {
            if (!FileExists(path))
            {
                return string.Empty;
            }

            return LocalStorage.Current.ReadAllText(path);
        }

        public static void WriteAllText(string path, string content)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && !LocalStorage.Current.DirectoryExists(directory))
            {
                LocalStorage.Current.CreateDirectory(directory);
            }

            LocalStorage.Current.WriteAllText(path, content ?? string.Empty);
        }

        private string ResolvePath()
        {
            string path = configPathResolver();
            return path ?? string.Empty;
        }
    }
}
