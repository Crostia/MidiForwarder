using System.Globalization;
using System.Resources;

namespace MidiForwarder
{
    public static class LocalizationManager
    {
        private static readonly ResourceManager _resourceManager = new("MidiForwarder.Resources.Strings", typeof(LocalizationManager).Assembly);
        private static CultureInfo _currentCulture = CultureInfo.CurrentUICulture;

        public static event EventHandler? LanguageChanged;

        public static CultureInfo CurrentCulture
        {
            get => _currentCulture;
            set
            {
                if (_currentCulture.Name != value.Name)
                {
                    _currentCulture = value;
                    LanguageChanged?.Invoke(null, EventArgs.Empty);
                }
            }
        }

        public static string GetString(string key)
        {
            return _resourceManager.GetString(key, _currentCulture) ?? key;
        }

        public static string GetString(string key, params object?[] args)
        {
            var format = _resourceManager.GetString(key, _currentCulture) ?? key;
            return string.Format(format, args);
        }

        public static void SetLanguage(string cultureName)
        {
            CurrentCulture = new CultureInfo(cultureName);
        }

        public static IEnumerable<CultureInfo> GetSupportedCultures()
        {
            var cultures = new List<CultureInfo>();
            _ = typeof(LocalizationManager).Assembly;

            // 默认语言 (中文)
            cultures.Add(new CultureInfo("zh-CN"));

            // 英文
            cultures.Add(new CultureInfo("en"));

            return cultures;
        }
    }
}
