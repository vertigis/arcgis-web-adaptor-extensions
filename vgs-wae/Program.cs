using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;

using VertiGIS.WebAdaptorExtensions;

var enc = Encoding.UTF8;
var readDepsAsync = async () =>
{
    var dll = typeof(ConfigFile).Assembly;
    var name = dll.GetName().Name;
    var ver = dll.GetName().Version!;
    ver = new Version(ver.Major, ver.Minor, ver.Build);
    var prefix = $"{name}/";
    var nameVer = $"{name}/{ver}";

    var isPresent = false;
    var loc = Path.Combine(dll.Location, @"..\ESRI.ArcGIS.WebAdaptor.deps.json");
    var json = JsonNode.Parse(await File.ReadAllTextAsync(loc, enc)) as JsonObject;
    var removals = new List<Action>();
    var framework = json?["targets"]?.AsObject()?.FirstOrDefault().Value as JsonObject;
    var libraries = json?["libraries"]?.AsObject();
    if (framework != null && libraries != null)
    {
        foreach (var key in framework.Select(x => x.Key))
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                removals.Add(() => framework.Remove(key));
            }
        }

        foreach (var key in libraries.Select(x => x.Key))
        {
            if (key == nameVer)
            {
                isPresent = true;
            }

            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                removals.Add(() => libraries.Remove(key));
            }
        }

        removals.ForEach(x => x());
    }

    var fn = Path.GetFileName(dll.Location);
    return (loc, fn, json, framework, libraries, nameVer, isPresent);
};

var readWebConfig = async () =>
{
    var dll = typeof(ConfigFile).Assembly;
    var name = dll.GetName().Name;
    var loc = Path.Combine(dll.Location, @"..\web.config");
    var xml = XDocument.Parse(await File.ReadAllTextAsync(loc, enc));
    var aspNetCore = xml.Descendants("aspNetCore").First();
    var fixStartup = (bool add) =>
    {
        var envs = aspNetCore.Element("environmentVariables");
        if (envs == null)
        {
            envs = new XElement("environmentVariables");
            aspNetCore.Add(envs);
        }

        var envName = "ASPNETCORE_HOSTINGSTARTUPASSEMBLIES";
        var env = envs.Elements("environmentVariable").FirstOrDefault(x => x.Attribute("name")?.Value == envName);
        if (env == null)
        {
            env = new XElement("environmentVariable");
            env.SetAttributeValue("name", envName);
            envs.Add(env);
        }

        var value = env.Attribute("value");
        if (value == null)
        {
            value = new XAttribute("value", "");
            env.Add(value);
        }

        var list = value.Value.Split(",").Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        if (add)
        {
            list.Insert(0, name!);
        }
        else
        {
            list.RemoveAll(x => x == name);
        }

        list = list.Distinct(StringComparer.Ordinal).ToList();
        value.Value = string.Join(",", list);

        if (value.Value.Length < 1)
        {
            env.Remove();
        }

        if (!envs.Elements().Any())
        {
            envs.Remove();
        }

        return xml;
    };

    return (loc, fixStartup);
};

var getAppPools = async () =>
{
    var tasklist = Process.Start(new ProcessStartInfo
    {
        FileName = "tasklist",
        Arguments = "/fi \"ImageName eq w3wp.exe\" /v /fo list",
        RedirectStandardOutput = true,
    })!;

    string? line;
    var pid = string.Empty;
    var user = string.Empty;
    var stdout = tasklist.StandardOutput!;
    var result = new Dictionary<string, string>(StringComparer.Ordinal);
    while ((line = await stdout.ReadLineAsync()) != null)
    {
        var values = line.Split(":");
        if (values.Length > 1)
        {
            var prefix = @"IIS APPPOOL\";
            var name = values[0].Trim();
            var value = values[1].Trim();
            if (name == "PID")
            {
                pid = value;
            }

            if (name == "User Name" && value.StartsWith(prefix, StringComparison.Ordinal))
            {
                user = value[prefix.Length..];
            }
        }
        else
        {
            if (pid.Length > 0 && user.Length > 0)
            {
                result[pid] = user;
            }

            pid = string.Empty;
            user = string.Empty;
        }
    }

    return result;
};

if (args.Length < 1)
{
    Console.WriteLine("Help:");
    Console.WriteLine("vgs-wae add");
    Console.WriteLine("vgs-wae remove");
    Console.WriteLine("vgs-wae trust [-r | --remove] [group/user]");
    Console.WriteLine();

    var file = await ConfigFile.LoadAsync();
    Console.WriteLine("Config:");
    Console.WriteLine(file);
    Console.WriteLine();

    await file.ResolveAsync();

    Console.WriteLine("Resolutions:");
    foreach (var (key, value) in file.Resolutions)
    {
        Console.WriteLine($"{key} = {value}");
    }

    Console.WriteLine();

    try
    {
        var (_, _, _, _, _, _, isPresent) = await readDepsAsync();
        Console.WriteLine("Status: " + (isPresent ? "Installed" : "None"));
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Status: Error {ex.Message}");
        Console.WriteLine();
    }

    Console.WriteLine("AppPools:");
    foreach (var (pid, user) in await getAppPools())
    {
        var padded = pid.PadRight(8);
        Console.WriteLine($"{padded}{user}");
    }

    Console.WriteLine();

    return;
}

if (args[0] == "add")
{
    var (depsPath, fn, json, framework, libraries, nameVer, _) = await readDepsAsync();
    framework[nameVer] = new JsonObject
    {
        ["runtime"] = new JsonObject
        {
            [fn] = new JsonObject()
        }
    };

    libraries[nameVer] = new JsonObject
    {
        ["type"] = "project",
        ["serviceable"] = false,
        ["sha512"] = string.Empty,
    };

    await File.WriteAllTextAsync(depsPath, json.ToString(), enc);
    
    var (webPath, register) = await readWebConfig();
    await File.WriteAllTextAsync(webPath, register(true).ToString(), enc);
}

if (args[0] == "remove")
{
    var (depsPath, _, json, _, _, _, _) = await readDepsAsync();
    await File.WriteAllTextAsync(depsPath, json.ToString(), enc);

    var (webPath, register) = await readWebConfig();
    await File.WriteAllTextAsync(webPath, register(false).ToString(), enc);
}

if (args[0] == "trust")
{
    var remove = false;
    var config = await ConfigFile.LoadAsync();
    foreach (var value in args[1..])
    {
        if (value == "-r" || value == "--remove")
        {
            remove = true;
        }
        else if (remove)
        {
            config.TrustedServiceAccounts.Remove(value);
            remove = false;
        }
        else
        {
            config.TrustedServiceAccounts.Add(value);
        }
    }

    await config.SaveAsync();
}

if (args[0] == "kill")
{
    var pids = new StringBuilder();
    var hints = new HashSet<string>(args[1..], StringComparer.OrdinalIgnoreCase);
    if (hints.Count < 1)
    {
        hints.Add("ArcGISWebAdaptorPortal");
    }

    foreach (var (pid, user) in await getAppPools())
    {
        if (hints.Contains(user))
        {
            pids.Append($"/PID {pid} ");
        }
    }

    if (pids.Length > 0)
    {
        var p = Process.Start(new ProcessStartInfo
        {
            FileName = "taskkill",
            Arguments = $"{pids} /f"
        })!;

        p.WaitForExit();
    }
}
