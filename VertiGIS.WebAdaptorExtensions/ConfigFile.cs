using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VertiGIS.WebAdaptorExtensions
{
    /// <summary>
    /// Configuration file for this module.
    /// </summary>
    public sealed class ConfigFile
    {
        /// <summary>
        /// Gets the trusted service accounts.
        /// </summary>
        [JsonPropertyName("trustedServiceAccounts")]
        public IList<string> TrustedServiceAccounts { get; set; }

        /// <summary>
        /// Gets the trusted service account resolutions.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<string> TrustedServiceAccountsResolutions { get; private set; }

        /// <summary>
        /// Gets the resolutions.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyDictionary<string, string> Resolutions { get; private set; }

        /// <summary>
        /// Converts a sequence of names into SIDs.
        /// </summary>
        /// <param name="names">The names to convert.</param>
        public IEnumerable<string> MapToSIDs(IEnumerable<string> names)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in TrustedServiceAccounts)
            {
                if (Resolutions.TryGetValue(key, out var sid))
                {
                    result.Add(sid);
                }
            }

            return result;
        }

        /// <summary>
        /// Resolves all properties.
        /// </summary>
        public async Task ResolveAsync()
        {
            var accounts = new List<string>();
            accounts.AddRange(TrustedServiceAccounts);

            var inputs = accounts.Distinct(StringComparer.OrdinalIgnoreCase);
            var tasks = inputs.Select(name => Task.Run(() =>
            {
                KeyValuePair<string, string>? result = null;
                if (!OperatingSystem.IsWindows())
                {
                    return result;
                }

                try
                {
                    var key = name;
                    if (name.StartsWith(@".\", StringComparison.Ordinal))
                    {
                        name = name[1..];
                        name = Environment.MachineName + name;
                    }

                    var account = new NTAccount(name);
                    var sid = account.Translate(typeof(SecurityIdentifier));
                    result = KeyValuePair.Create(key, sid.ToString());
                }
                catch
                {
                    // don't care
                }

                return result;
            }));

            var results = await Task.WhenAll(tasks);
            var outputs = results.OfType<KeyValuePair<string, string>>();
            Resolutions = new Dictionary<string, string>(outputs, StringComparer.OrdinalIgnoreCase);
            TrustedServiceAccountsResolutions = MapToSIDs(TrustedServiceAccounts);
        }

        /// <summary>
        /// Constructs a new instance of a ConfigFile.
        /// </summary>
        public ConfigFile()
        {
            TrustedServiceAccounts = new List<string>();
            TrustedServiceAccountsResolutions = Enumerable.Empty<string>();
            Resolutions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the default path for the config file.
        /// </summary>
        /// <returns>
        /// Returns the default path.
        /// </returns>
        public static string GetDefaultPath()
        {
            return typeof(ConfigFile).Assembly.Location + ".json";
        }

        /// <summary>
        /// Loads a <see cref="ConfigFile"/>
        /// </summary>
        /// <returns>
        /// Returns a <see cref="ConfigFile"/>
        /// </returns>
        public static Task<ConfigFile> LoadAsync()
        {
            return LoadAsync(null);
        }

        /// <summary>
        /// Loads a <see cref="ConfigFile"/>
        /// </summary>
        /// <param name="path">The path to load from.</param>
        /// <returns>
        /// Returns a <see cref="ConfigFile"/>
        /// </returns>
        public static async Task<ConfigFile> LoadAsync(string? path)
        {
            path ??= GetDefaultPath();

            if (File.Exists(path))
            {
                var json = await File.ReadAllTextAsync(path, Encoding.UTF8);
                return JsonSerializer.Deserialize<ConfigFile>(json)!;
            }

            var def = new ConfigFile();
            def.TrustedServiceAccounts.Add(@".\ArcGIS Web Adaptor Trusted Service Accounts");

            return def;
        }

        /// <summary>
        /// Saves the current <see cref="ConfigFile"/>
        /// </summary>
        public Task SaveAsync()
        {
            return SaveAsync(null);
        }

        /// <summary>
        /// Saves the current <see cref="ConfigFile"/>
        /// </summary>
        /// <param name="path">The path to save to.</param>
        public async Task SaveAsync(string? path)
        {
            path ??= GetDefaultPath();
            await File.WriteAllTextAsync(path, ToString());
        }

        internal static async Task<ConfigFile> InitializeAsync()
        {
            var file = await LoadAsync();
            await file.ResolveAsync();

            return file;
        }

        public override string ToString()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            return JsonSerializer.Serialize(this, options);
        }
    }
}
