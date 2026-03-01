using System.Collections.Concurrent;
using System.Xml.Linq;

namespace Tria.Services
{
    public interface IUiLocalizer
    {
        string this[string key] { get; }
    }

    public class XmlUiLocalizer : IUiLocalizer
    {
        // Cache: lang -> (key -> value)
        private static readonly ConcurrentDictionary<string, Dictionary<string, string>> _cache = new();

        private readonly IWebHostEnvironment _env;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public XmlUiLocalizer(IWebHostEnvironment env, IHttpContextAccessor httpContextAccessor)
        {
            _env = env;
            _httpContextAccessor = httpContextAccessor;
        }

        public string this[string key]
        {
            get
            {
                var lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                var strings = GetStrings(lang);

                if (strings.TryGetValue(key, out var value))
                    return value;

                // Fallback to English
                if (lang != "en")
                {
                    var fallback = GetStrings("en");
                    if (fallback.TryGetValue(key, out var fallbackValue))
                        return fallbackValue;
                }

                return key; // Last resort: return the key itself
            }
        }

        private Dictionary<string, string> GetStrings(string lang)
        {
            return _cache.GetOrAdd(lang, l =>
            {
                var path = Path.Combine(_env.ContentRootPath, "Resources/Content", $"ui.{l}.xml");

                if (!File.Exists(path))
                    return new Dictionary<string, string>();

                var doc = XDocument.Load(path);
                return doc.Root!
                    .Elements()
                    .ToDictionary(e => e.Name.LocalName, e => e.Value);
            });
        }
    }
}
